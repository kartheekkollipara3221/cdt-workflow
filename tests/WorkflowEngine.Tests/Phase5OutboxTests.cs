using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5OutboxTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");

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
        Assert.Single(result.Processed);
        Assert.Equal(1, result.NotAttemptedCount);
        dbContext.ChangeTracker.Clear();
        Assert.Equal("Sent", dbContext.NotificationOutbox.Single(row => row.Subject == "First").Status);
        Assert.Equal("Pending", dbContext.NotificationOutbox.Single(row => row.Subject == "Second").Status);
        Assert.Equal("Pending", dbContext.NotificationOutbox.Single(row => row.Subject == "Third").Status);
    }

    private static void AddNotification(WorkflowDbContext dbContext, string workflowInstanceId, string subject, DateTimeOffset createdAtUtc)
    {
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = workflowInstanceId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = workflowInstanceId,
            NotificationType = "WorkflowTaskAssigned",
            RecipientRoleKey = "Checker",
            Subject = subject,
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = subject,
            CreatedAtUtc = createdAtUtc
        });
    }
}
