using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5OutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");
    private const string RecipientSnapshot = """{"toEmailAddresses":["first@example.test","second@example.test"],"ccEmailAddresses":["manager@example.test","operations@example.test"]}""";

    [Fact]
    public async Task ProcessNotificationOutboxSendsPendingRowsInCreatedThenIdOrder()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-2", "Second", Now.AddMinutes(-1));
        AddNotification(dbContext, "WF-PT-1", "First", Now.AddMinutes(-2));
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Equal(2, result.PendingCount);
        Assert.Equal(["First", "Second"], sender.SentRows.Select(row => row.Subject));
        Assert.All(sender.SentRows, row =>
        {
            Assert.Equal(["first@example.test", "second@example.test"], row.ToEmailAddresses);
            Assert.Equal(["manager@example.test", "operations@example.test"], row.CcEmailAddresses);
        });
        dbContext.ChangeTracker.Clear();
        Assert.All(dbContext.NotificationOutbox, row =>
        {
            Assert.Equal("Sent", row.Status);
            Assert.Equal(Now, row.SentAtUtc);
        });
    }

    [Fact]
    public async Task ProcessNotificationOutboxRollsBackCurrentRowAndAbortsWhenSenderThrows()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-1", "First", Now.AddMinutes(-3));
        AddNotification(dbContext, "WF-PT-2", "Second", Now.AddMinutes(-2));
        AddNotification(dbContext, "WF-PT-3", "Third", Now.AddMinutes(-1));
        dbContext.SaveChanges();
        var secondId = dbContext.NotificationOutbox.Single(row => row.Subject == "Second").Id;
        var sender = new RecordingNotificationSender(secondId);

        var result = await new ProcessNotificationOutboxService(dbContext, sender, new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Equal(secondId, result.Failure.Id);
        Assert.Equal(["first@example.test", "second@example.test"], result.Failure.ToEmailAddresses);
        Assert.Equal(["manager@example.test", "operations@example.test"], result.Failure.CcEmailAddresses);
        Assert.Single(result.Processed);
        Assert.Equal(1, result.NotAttemptedCount);
        dbContext.ChangeTracker.Clear();
        Assert.Equal("Sent", dbContext.NotificationOutbox.Single(row => row.Subject == "First").Status);
        Assert.Equal("Pending", dbContext.NotificationOutbox.Single(row => row.Subject == "Second").Status);
        Assert.Equal(1, dbContext.NotificationOutbox.Single(row => row.Subject == "Second").RetryCount);
        Assert.Contains("send failed", dbContext.NotificationOutbox.Single(row => row.Subject == "Second").LastError, StringComparison.Ordinal);
        Assert.Equal("Pending", dbContext.NotificationOutbox.Single(row => row.Subject == "Third").Status);
    }

    [Fact]
    public async Task MalformedRecipientSnapshotReleasesClaimAndAbortsBeforeSending()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(dbContext, "WF-PT-1", "Malformed", Now);
        dbContext.NotificationOutbox.Single().NotificationRecipientsJson = "{";
        dbContext.SaveChanges();
        var sender = new RecordingNotificationSender();

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(Now)).ProcessAsync(CancellationToken.None);

        Assert.NotNull(result.Failure);
        Assert.Empty(result.Failure.ToEmailAddresses);
        Assert.Empty(result.Failure.CcEmailAddresses);
        Assert.Empty(sender.SentRows);
        dbContext.ChangeTracker.Clear();
        var row = dbContext.NotificationOutbox.Single();
        Assert.Equal("Pending", row.Status);
        Assert.Equal(1, row.RetryCount);
        Assert.Contains("recipient snapshot is invalid", row.LastError, StringComparison.Ordinal);
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);
    }

    [Fact]
    public async Task ConcurrentProcessorsClaimBeforeSendingSoOnlyOneSenderRuns()
    {
        using var setupContext = WorkflowExecutionValidationTests.CreateSeededContext();
        AddNotification(setupContext, "WF-PT-1", "Only", Now);
        setupContext.SaveChanges();
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var firstSender = new BlockingNotificationSender();
        var secondSender = new RecordingNotificationSender();
        var firstProcessor = new ProcessNotificationOutboxService(firstContext, firstSender, new FixedTimeProvider(Now));
        var secondProcessor = new ProcessNotificationOutboxService(secondContext, secondSender, new FixedTimeProvider(Now));

        var firstTask = firstProcessor.ProcessAsync(CancellationToken.None);
        await firstSender.WaitForSendAsync();
        var secondResult = await secondProcessor.ProcessAsync(CancellationToken.None);
        firstSender.Release();
        var firstResult = await firstTask;

        Assert.Null(firstResult.Failure);
        Assert.Null(secondResult.Failure);
        Assert.Single(firstSender.SentRows);
        Assert.Empty(secondSender.SentRows);
        Assert.Contains(secondResult.Warnings, warning => warning.Contains("already claimed", StringComparison.Ordinal));
        setupContext.ChangeTracker.Clear();
        var row = setupContext.NotificationOutbox.Single();
        Assert.Equal("Sent", row.Status);
        Assert.Null(row.ProcessingToken);
        Assert.Null(row.ProcessingStartedAtUtc);
    }

    private static void AddNotification(WorkflowDbContext dbContext, string workflowInstanceId, string subject, DateTimeOffset createdAtUtc)
    {
        if (!dbContext.WorkflowInstances.Any(row => row.WorkflowInstanceId == workflowInstanceId))
        {
            dbContext.WorkflowInstances.Add(new WorkflowInstance
            {
                WorkflowInstanceId = workflowInstanceId,
                WorkflowKey = "PortfolioTrade",
                WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
                BusinessObjectType = "PortfolioTrade",
                BusinessObjectId = workflowInstanceId,
                CurrentStateKey = "Draft",
                Status = "Active",
                StartedAtUtc = createdAtUtc,
                StartedByUserId = "maker1",
                Version = 1
            });
        }

        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = workflowInstanceId,
            NotificationType = "WorkflowTaskAssigned",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = subject,
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = subject,
            CreatedAtUtc = createdAtUtc
        });
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase4_execution_tests"))
            .Options;

        return new WorkflowDbContext(options);
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }

    private sealed class BlockingNotificationSender : INotificationSender
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string SenderName => "Blocking";

        public List<NotificationDelivery> SentRows { get; } = [];

        public async Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
        {
            SentRows.Add(notification);
            entered.SetResult();
            await release.Task.WaitAsync(cancellationToken);
        }

        public Task WaitForSendAsync()
        {
            return entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Release()
        {
            release.SetResult();
        }
    }
}
