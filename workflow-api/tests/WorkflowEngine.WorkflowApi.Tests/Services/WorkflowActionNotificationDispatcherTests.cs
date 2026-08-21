using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Tests.Services;

[Collection("SqlServerWorkflowActionNotificationDispatcher")]
public sealed class WorkflowActionNotificationDispatcherTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-08-05T15:30:00Z");

    [SqlServerFact]
    public void DispatchCreatedNotificationsPreservesOrderedTypedRecipients()
    {
        using var dbContext = CreateSeededContext();
        var row = AddNotification(
            dbContext,
            """{"toEmailAddresses":[" first@example.test ","duplicate@example.test","duplicate@example.test"],"ccEmailAddresses":[" copied@example.test ","operations@example.test"]}""");
        var sender = new RecordingNotificationSender();

        var result = new WorkflowActionNotificationDispatcher(dbContext, sender, new FixedTimeProvider(Now))
            .DispatchCreatedNotifications(row.WorkflowInstanceId!, row.CorrelationId);

        Assert.True(result.Success);
        var delivery = Assert.Single(sender.SentRows);
        Assert.Equal(
            [" first@example.test ", "duplicate@example.test", "duplicate@example.test"],
            delivery.ToEmailAddresses);
        Assert.Equal(
            [" copied@example.test ", "operations@example.test"],
            delivery.CcEmailAddresses);

        dbContext.ChangeTracker.Clear();
        var persisted = dbContext.NotificationOutbox.Single(item => item.Id == row.Id);
        Assert.Equal("Sent", persisted.Status);
        Assert.Equal(0, persisted.RetryCount);
        Assert.Null(persisted.LastError);
        Assert.Equal(Now, persisted.SentAtUtc);
    }

    [SqlServerTheory]
    [InlineData("", "recipient snapshot is missing")]
    [InlineData("{", "recipient snapshot is invalid")]
    [InlineData("{\"toEmailAddresses\":null,\"ccEmailAddresses\":[]}", "recipient snapshot is invalid")]
    [InlineData("{\"toEmailAddresses\":[],\"ccEmailAddresses\":null}", "recipient snapshot is invalid")]
    public void InvalidRecipientSnapshotReleasesRowWithoutCallingSender(
        string notificationRecipientsJson,
        string expectedError)
    {
        using var dbContext = CreateSeededContext();
        var row = AddNotification(dbContext, notificationRecipientsJson);
        var sender = new RecordingNotificationSender();

        var result = new WorkflowActionNotificationDispatcher(dbContext, sender, new FixedTimeProvider(Now))
            .DispatchCreatedNotifications(row.WorkflowInstanceId!, row.CorrelationId);

        Assert.False(result.Success);
        Assert.Empty(sender.SentRows);
        Assert.Contains(expectedError, result.FailureMessage, StringComparison.Ordinal);

        dbContext.ChangeTracker.Clear();
        var persisted = dbContext.NotificationOutbox.Single(item => item.Id == row.Id);
        Assert.Equal("Pending", persisted.Status);
        Assert.Equal(1, persisted.RetryCount);
        Assert.Contains(expectedError, persisted.LastError, StringComparison.Ordinal);
        Assert.Null(persisted.ProcessingToken);
        Assert.Null(persisted.ProcessingStartedAtUtc);
        Assert.Null(persisted.SentAtUtc);
    }

    private static WorkflowDbContext CreateSeededContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestConnectionString("workflow_api_dispatcher_tests"))
            .Options;
        var dbContext = new WorkflowDbContext(options);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(Now)).SeedPortfolioTrade();
        return dbContext;
    }

    private static NotificationOutbox AddNotification(
        WorkflowDbContext dbContext,
        string notificationRecipientsJson)
    {
        const string workflowInstanceId = "WF-API-DISPATCHER-1";
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        var definitionVersionId = dbContext.WorkflowDefinitionVersions
            .Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive)
            .Id;
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = definitionVersionId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-API-DISPATCHER-1",
            CurrentStateKey = "Draft",
            Status = "Active",
            StartedAtUtc = Now,
            StartedByUserId = "maker1",
            Version = 1
        });
        var row = new NotificationOutbox
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-API-DISPATCHER-1",
            NotificationType = "WorkflowTaskAssigned",
            NotificationRecipientsJson = notificationRecipientsJson,
            Subject = "Dispatcher recipient test",
            Body = "Plain body",
            HtmlBody = "<p>HTML body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "api-dispatcher-correlation",
            CreatedAtUtc = Now.AddMinutes(-1)
        };
        dbContext.NotificationOutbox.Add(row);
        dbContext.SaveChanges();
        return row;
    }

    private static string TestConnectionString(string databaseName)
    {
        var source = Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException(
                "SQL-backed API tests require WORKFLOW_ENGINE_TEST_CONNECTION_STRING or ConnectionStrings__DefaultConnection.");
        }

        var builder = new SqlConnectionStringBuilder(source)
        {
            InitialCatalog = databaseName
        };
        return builder.ConnectionString;
    }

    private sealed class RecordingNotificationSender : INotificationSender
    {
        public string SenderName => "Recording";

        public List<NotificationDelivery> SentRows { get; } = [];

        public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
        {
            SentRows.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}

[CollectionDefinition("SqlServerWorkflowActionNotificationDispatcher")]
public sealed class SqlServerWorkflowActionNotificationDispatcherCollection;

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        SkipWhenSqlConnectionIsMissing();
    }

    private void SkipWhenSqlConnectionIsMissing()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_TEST_CONNECTION_STRING"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
        {
            Skip = "Set WORKFLOW_ENGINE_TEST_CONNECTION_STRING or ConnectionStrings__DefaultConnection to run SQL-backed API dispatcher tests.";
        }
    }
}

internal sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_TEST_CONNECTION_STRING"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
        {
            Skip = "Set WORKFLOW_ENGINE_TEST_CONNECTION_STRING or ConnectionStrings__DefaultConnection to run SQL-backed API dispatcher tests.";
        }
    }
}
