using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class NotificationOnlyQueueTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");

    [Fact]
    public async Task QueueWithoutInstanceResolvesStaticAndRequestRecipientsAndRendersTemplates()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(dbContext);
        var input = Input(
            new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["team"] = [new("team1", "team@example.test", "Team One")]
            },
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["name"] = "A < B",
                ["link"] = "https://example.test/open?a=1&b=2"
            });

        var result = await CreateService(dbContext).QueueAsync(Request("occurrence-success", input), CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Replay);
        Assert.NotNull(result.NotificationOutboxId);
        var row = Assert.Single(dbContext.NotificationOutbox);
        Assert.Null(row.WorkflowInstanceId);
        Assert.Equal("occurrence-success", row.NotificationOccurrenceKey);
        Assert.Equal("Hello A < B", row.Subject);
        Assert.Equal("Open https://example.test/open?a=1&b=2", row.Body);
        Assert.Equal("<strong>A &lt; B</strong> <a href=\"https://example.test/open?a=1&amp;b=2\">Open</a>", row.HtmlBody);
        Assert.Equal("Pending", row.Status);
        Assert.Contains("ops@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        Assert.Contains("team@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        var payload = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.PayloadJson, WorkflowJson.Options);
        Assert.NotNull(payload);
        Assert.Equal("A < B", payload["name"]);
    }

    [Fact]
    public async Task MissingTemplateTokenDoesNotInsert()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(dbContext);
        var input = Input(
            RecipientInput("team", "team@example.test"),
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["name"] = "Missing link" });

        var result = await CreateService(dbContext).QueueAsync(Request("occurrence-token", input), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ResultCode);
        Assert.Contains(result.Errors, error => error.Contains("notificationInput.templateData.link", StringComparison.Ordinal));
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public async Task MissingRequiredRecipientDoesNotInsert()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(dbContext);

        var result = await CreateService(dbContext).QueueAsync(
            Request("occurrence-recipient", Input(EmptyRecipientInput(), TemplateData())),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Required recipient source 'team' is missing", StringComparison.Ordinal));
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public async Task UnexpectedRecipientInputDoesNotInsert()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(dbContext);
        var recipients = RecipientInput("unexpected", "unexpected@example.test");

        var result = await CreateService(dbContext).QueueAsync(
            Request("occurrence-unexpected", Input(recipients, TemplateData())),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Recipient input 'unexpected' is not declared", StringComparison.Ordinal));
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public async Task RequiredWorkflowParticipantWithoutInstanceUsesExistingRequiredSourceError()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var route = SeedRoute(dbContext, includeDefaultRules: false);
        dbContext.NotificationRecipientRules.Add(new NotificationRecipientRule
        {
            NotificationRouteId = route.Id,
            RecipientKind = RecipientKinds.To,
            SourceType = RecipientSourceTypes.WorkflowParticipant,
            SourceKey = "maker",
            SortOrder = 10,
            IsRequired = true,
            IsActive = true
        });
        dbContext.SaveChanges();

        var result = await CreateService(dbContext).QueueAsync(
            Request("occurrence-participant", Input(EmptyRecipientInput(), TemplateData())),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Required recipient source 'maker' is missing", StringComparison.Ordinal));
        Assert.Empty(dbContext.NotificationOutbox);
    }

    [Fact]
    public async Task ExactOccurrenceReplayReturnsExistingRowWithoutDuplicate()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(dbContext);
        var request = Request("occurrence-replay", Input(RecipientInput("team", "team@example.test"), TemplateData()));
        var service = CreateService(dbContext);

        var first = await service.QueueAsync(request, CancellationToken.None);
        var second = await service.QueueAsync(request, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(second.Replay);
        Assert.Equal(first.NotificationOutboxId, second.NotificationOutboxId);
        Assert.Single(dbContext.NotificationOutbox);
    }

    [Fact]
    public async Task ConcurrentDuplicateInsertReturnsOneSuccessAndOneReplay()
    {
        using var setupContext = WorkflowExecutionValidationTests.CreateSeededContext();
        SeedRoute(setupContext);
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var request = Request("occurrence-concurrent", Input(RecipientInput("team", "team@example.test"), TemplateData()));

        var results = await Task.WhenAll(
            CreateService(firstContext).QueueAsync(request, CancellationToken.None),
            CreateService(secondContext).QueueAsync(request, CancellationToken.None));

        Assert.All(results, result => Assert.True(result.Success));
        Assert.Single(results, result => result.Replay);
        setupContext.ChangeTracker.Clear();
        Assert.Single(setupContext.NotificationOutbox.Where(row => row.NotificationOccurrenceKey == "occurrence-concurrent"));
    }

    private static NotificationRoute SeedRoute(WorkflowDbContext dbContext, bool includeDefaultRules = true)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        var version = dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive);
        var route = new NotificationRoute
        {
            WorkflowDefinitionVersionId = version.Id,
            RouteKey = "RouteNotificationOnlyTest",
            NotificationType = "NotificationOnlyTest",
            SubjectTemplate = "Hello {{name}}",
            BodyTemplate = "Open {{link}}",
            HtmlBodyTemplate = "<strong>{{name}}</strong> <a href=\"{{link}}\">Open</a>",
            IsActive = true
        };
        dbContext.NotificationRoutes.Add(route);
        dbContext.SaveChanges();
        if (!includeDefaultRules)
        {
            return route;
        }

        dbContext.NotificationRecipientRules.AddRange(
            new NotificationRecipientRule
            {
                NotificationRouteId = route.Id,
                RecipientKind = RecipientKinds.To,
                SourceType = RecipientSourceTypes.StaticAddress,
                StaticAddress = "ops@example.test",
                SortOrder = 10,
                IsRequired = true,
                IsActive = true
            },
            new NotificationRecipientRule
            {
                NotificationRouteId = route.Id,
                RecipientKind = RecipientKinds.Cc,
                SourceType = RecipientSourceTypes.RequestInput,
                SourceKey = "team",
                SortOrder = 20,
                IsRequired = true,
                IsActive = true
            });
        dbContext.SaveChanges();
        return route;
    }

    private static QueueWorkflowNotificationService CreateService(WorkflowDbContext dbContext)
    {
        return new QueueWorkflowNotificationService(
            dbContext,
            new NotificationRecipientResolver(dbContext),
            new NotificationTemplateRenderer(),
            new FixedTimeProvider(Now));
    }

    private static QueueWorkflowNotificationRequest Request(string occurrenceKey, WorkflowNotificationInput input)
    {
        return new QueueWorkflowNotificationRequest(
            "PortfolioTrade",
            1,
            "RouteNotificationOnlyTest",
            occurrenceKey,
            "NotificationOnlyObject",
            "42",
            input,
            $"corr-{occurrenceKey}");
    }

    private static WorkflowNotificationInput Input(
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> recipients,
        IReadOnlyDictionary<string, string?> templateData)
    {
        return new WorkflowNotificationInput
        {
            RecipientInputs = recipients,
            TemplateData = templateData
        };
    }

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInput(string key, string address)
    {
        return new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
        {
            [key] = [new(key, address, key)]
        };
    }

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> EmptyRecipientInput()
    {
        return new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);
    }

    private static Dictionary<string, string?> TemplateData()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = "Test",
            ["link"] = "https://example.test/open"
        };
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_execution_tests"))
            .Options;
        return new WorkflowDbContext(options);
    }
}
