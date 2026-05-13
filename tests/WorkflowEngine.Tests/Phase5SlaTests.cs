using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5SlaTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");

    [Fact]
    public async Task RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-1", "PT-1", "Active", versionId);
        AddInstance(dbContext, "WF-PT-2", "PT-2", "Active", versionId);
        AddInstance(dbContext, "WF-PT-3", "PT-3", "Active", versionId);
        AddInstance(dbContext, "WF-PT-4", "PT-4", "Active", versionId);
        AddInstance(dbContext, "WF-PT-5", "PT-5", "Active", versionId);
        AddInstance(dbContext, "WF-PT-6", "PT-6", "Completed", versionId);
        AddInstance(dbContext, "WF-PT-7", "PT-7", "Active", versionId);
        dbContext.SaveChanges();

        var returned = AddTask(dbContext, "WF-PT-7", "TradingDeskReviewTask", "TradingDesk", Now.AddMinutes(-6));
        var overdue = AddTask(dbContext, "WF-PT-1", "CheckerReviewTask", "Checker", Now.AddMinutes(-5));
        var alreadyQueued = AddTask(dbContext, "WF-PT-2", "CheckerReviewTask", "Checker", Now.AddMinutes(-4));
        AddTask(dbContext, "WF-PT-3", "CheckerReviewTask", "Checker", Now);
        AddTask(dbContext, "WF-PT-4", "MakerReworkTask", "Maker", null);
        AddTask(dbContext, "WF-PT-5", "CheckerReviewTask", "Checker", Now.AddMinutes(-3), status: "Completed");
        AddTask(dbContext, "WF-PT-6", "CheckerReviewTask", "Checker", Now.AddMinutes(-2));
        dbContext.SaveChanges();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = "WF-PT-2",
            WorkflowTaskId = alreadyQueued.Id,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-2",
            NotificationType = "SlaTaskOverdue",
            RecipientRoleKey = "Checker",
            Subject = "Task overdue",
            PayloadJson = "{}",
            Status = "Sent",
            CorrelationId = "existing",
            CreatedAtUtc = Now.AddMinutes(-1),
            SentAtUtc = Now.AddMinutes(-1)
        });
        dbContext.SaveChanges();

        dbContext.ChangeTracker.Clear();
        var result = await new RunSlaService(TestSqlConnectionFactory.StoreFor(dbContext)).RunAsync(new(Now), CancellationToken.None);

        Assert.Equal(4, result.PendingTasksWithSla);
        Assert.Equal(1, result.AlreadyQueued);
        Assert.Equal([returned.Id, overdue.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.All(result.QueuedNotifications, row => Assert.Equal("Active", dbContext.WorkflowInstances.Single(instance => instance.WorkflowInstanceId == row.WorkflowInstanceId).Status));
        Assert.Equal(3, dbContext.NotificationOutbox.Count(row => row.NotificationType == "SlaTaskOverdue"));

        var second = await new RunSlaService(TestSqlConnectionFactory.StoreFor(dbContext)).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(second.QueuedNotifications);
    }

    private static void AddInstance(WorkflowDbContext dbContext, string workflowInstanceId, string businessObjectId, string status, long versionId)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = versionId,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = status == "Completed" ? "Completed" : "CheckerReview",
            Status = status,
            StartedAtUtc = Now.AddHours(-1),
            StartedByUserId = "maker1",
            Version = 1
        });
    }

    private static WorkflowTask AddTask(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string taskKey,
        string roleKey,
        DateTimeOffset? dueAtUtc,
        string status = "Pending")
    {
        var task = new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = taskKey,
            AssignedRoleKey = roleKey,
            Status = status,
            CreatedAtUtc = Now.AddHours(-1),
            DueAtUtc = dueAtUtc
        };
        dbContext.WorkflowTasks.Add(task);
        return task;
    }

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }
}
