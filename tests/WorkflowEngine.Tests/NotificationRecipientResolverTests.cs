using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class NotificationRecipientResolverTests
{
    [Fact]
    public void ResolveUsesRuleOrderDeduplicatesCaseInsensitivelyAndGivesToPrecedence()
    {
        using var dbContext = CreateContextWithMaker("workflow-operations@example.test");
        var routeId = RouteId(dbContext, "RouteReturnedToMakerFromChecker");
        var input = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["selectedCcUsers"] =
                [
                    new("duplicate-to", "WORKFLOW-OPERATIONS@example.test", null),
                    new("manager-1", "manager@example.test", null),
                    new("manager-2", "MANAGER@example.test", null),
                    new("blank", string.Empty, null),
                    new("malformed", "not-an-address", null)
                ]
            }
        };

        var result = new NotificationRecipientResolver(dbContext).Resolve(
            routeId,
            "WF-PT-RESOLVE",
            input);

        Assert.Empty(result.Errors);
        Assert.Equal(["workflow-operations@example.test"], result.Recipients.ToEmailAddresses);
        Assert.Equal(
            ["manager@example.test", string.Empty, "not-an-address"],
            result.Recipients.CcEmailAddresses);
    }

    [Fact]
    public void ResolveRejectsUnknownRequestInputKey()
    {
        using var dbContext = CreateContextWithMaker("maker@example.test");
        var input = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["originalMaker"] = [new("replacement", "replacement", null)]
            }
        };

        var result = new NotificationRecipientResolver(dbContext).Resolve(
            RouteId(dbContext, "RouteReturnedToMakerFromChecker"),
            "WF-PT-RESOLVE",
            input);

        Assert.Contains(result.Errors, error => error.Contains("originalMaker", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveReportsMissingRequiredParticipantAndZeroTo()
    {
        using var dbContext = CreateContextWithMaker(null);

        var result = new NotificationRecipientResolver(dbContext).Resolve(
            RouteId(dbContext, "RouteReturnedToMakerFromChecker"),
            "WF-PT-RESOLVE",
            null);

        Assert.Contains(result.Errors, error => error.Contains("originalMaker", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("To recipient", StringComparison.Ordinal));
        Assert.Empty(result.Recipients.ToEmailAddresses);
    }

    [Fact]
    public void ResolveAssignmentRouteIncludesMakerAndPreservesOptionalInputOrder()
    {
        using var dbContext = CreateContextWithMaker("maker@example.test");
        var input = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["selectedCcUsers"] =
                [
                    new("one", "first", null),
                    new("two", "second", null)
                ]
            }
        };

        var result = new NotificationRecipientResolver(dbContext).Resolve(
            RouteId(dbContext, "RouteCheckerAssignedOnSubmit"),
            "WF-PT-RESOLVE",
            input);

        Assert.Empty(result.Errors);
        Assert.Equal(["checker@example.test"], result.Recipients.ToEmailAddresses);
        Assert.Equal(["maker@example.test", "first", "second"], result.Recipients.CcEmailAddresses);
    }

    private static WorkflowDbContext CreateContextWithMaker(string? makerAddress)
    {
        var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-PT-RESOLVE",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = versionId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-RESOLVE",
            CurrentStateKey = "CheckerReview",
            Status = "Active",
            StartedAtUtc = DateTimeOffset.Parse("2030-01-15T15:30:00Z"),
            StartedByUserId = "maker-1",
            Version = 2
        });
        if (makerAddress is not null)
        {
            dbContext.WorkflowParticipants.Add(new WorkflowParticipant
            {
                WorkflowInstanceId = "WF-PT-RESOLVE",
                ParticipantKey = "originalMaker",
                UserId = "maker-1",
                EmailAddress = makerAddress,
                DisplayName = "Maker One",
                SortOrder = 1,
                CreatedAtUtc = DateTimeOffset.Parse("2030-01-15T15:30:00Z")
            });
        }

        dbContext.SaveChanges();
        return dbContext;
    }

    private static long RouteId(WorkflowDbContext dbContext, string routeKey)
    {
        return dbContext.NotificationRoutes.Single(row => row.RouteKey == routeKey).Id;
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definitionId = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade").Id;
        return dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definitionId && row.IsActive).Id;
    }
}
