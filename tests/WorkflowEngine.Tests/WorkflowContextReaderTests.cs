using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.WorkflowContext;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowContext")]
public sealed class WorkflowContextReaderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-07T12:00:00Z");

    [Fact]
    public void CheckerSeesSqlBackedActionsAndRequestDefaults()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-CHECKER", "PT-CTX-CHECKER", "CheckerReview", "Active", 3);
        AddTask(dbContext, "WF-PT-CTX-CHECKER", "CheckerReviewTask", "Checker", Now.AddMinutes(10));
        dbContext.SaveChanges();

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest("WF-PT-CTX-CHECKER", "checker1", ["Checker"]));
        var context = response.WorkflowContext;

        Assert.Equal("CheckerReview", context.Workflow.CurrentState);
        Assert.Equal("Checker Review", context.Workflow.CurrentStateLabel);
        Assert.Equal("CheckerReviewTask", context.CurrentTask?.TaskKey);
        Assert.Equal("OnTrack", context.Sla.Status);
        Assert.True(context.ActorAccess.CanAct);
        Assert.Equal(["CheckerReturnToMaker", "CheckerSendToTradingDesk"], context.AvailableActions.Select(row => row.ActionKey).ToArray());
        Assert.All(context.AvailableActions, action =>
        {
            Assert.Equal("PortfolioTrade", action.RequestDefaults.WorkflowKey);
            Assert.Equal("WF-PT-CTX-CHECKER", action.RequestDefaults.WorkflowInstanceId);
            Assert.Equal("CheckerReview", action.RequestDefaults.ExpectedCurrentState);
            Assert.Equal("CheckerReviewTask", action.RequestDefaults.ExpectedCurrentTaskKey);
            Assert.Equal(3, action.RequestDefaults.ExpectedInstanceVersion);
        });
    }

    [Fact]
    public void WrongRoleSeesContextButNoActionsAndReaderDoesNotWriteRows()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-WRONG-ROLE", "PT-CTX-WRONG-ROLE", "CheckerReview", "Active", 1);
        AddTask(dbContext, "WF-PT-CTX-WRONG-ROLE", "CheckerReviewTask", "Checker", Now.AddMinutes(10));
        dbContext.SaveChanges();
        var countsBefore = CountRuntimeRows(dbContext);

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest("WF-PT-CTX-WRONG-ROLE", "maker1", ["Maker"]));
        var countsAfter = CountRuntimeRows(dbContext);

        Assert.False(response.WorkflowContext.ActorAccess.CanAct);
        Assert.Empty(response.WorkflowContext.AvailableActions);
        Assert.Contains("Checker", response.WorkflowContext.ActorAccess.ReadOnlyReason, StringComparison.Ordinal);
        Assert.Equal("CheckerReviewTask", response.WorkflowContext.CurrentTask?.TaskKey);
        Assert.Equal(countsBefore, countsAfter);
    }

    [Fact]
    public void MakerReworkWithoutDueDateReturnsNoSla()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-NO-SLA", "PT-CTX-NO-SLA", "ReturnedToMaker", "Active", 2);
        AddTask(dbContext, "WF-PT-CTX-NO-SLA", "MakerReworkTask", "Maker", null);
        dbContext.SaveChanges();

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest("WF-PT-CTX-NO-SLA", "maker1", ["Maker"]));

        Assert.Equal("NoSla", response.WorkflowContext.Sla.Status);
        Assert.False(response.WorkflowContext.Sla.Overdue);
        Assert.True(response.WorkflowContext.ActorAccess.CanAct);
        Assert.Contains(response.WorkflowContext.AvailableActions, row => row.ActionKey == "ResubmitForChecker");
    }

    [Fact]
    public void DueDateInPastReturnsOverdueSla()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-OVERDUE", "PT-CTX-OVERDUE", "CheckerReview", "Active", 1);
        AddTask(dbContext, "WF-PT-CTX-OVERDUE", "CheckerReviewTask", "Checker", Now.AddMinutes(-11));
        dbContext.SaveChanges();

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest("WF-PT-CTX-OVERDUE", "checker1", ["Checker"]));

        Assert.Equal("Overdue", response.WorkflowContext.Sla.Status);
        Assert.True(response.WorkflowContext.Sla.Overdue);
        Assert.Equal(11, response.WorkflowContext.Sla.OverdueByMinutes);
    }

    [Fact]
    public void ApplicationTargetDateScheduleIsProjectedWithOperationalEvidence()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-APP-SLA", "PT-CTX-APP-SLA", "TradingDeskReview", "Active", 3);
        AddTask(
            dbContext,
            "WF-PT-CTX-APP-SLA",
            "TradingDeskReviewTask",
            "TradingDeskTaxable",
            DateTimeOffset.Parse("2030-01-21T14:00:00Z"));
        dbContext.SaveChanges();
        var task = dbContext.WorkflowTasks.Single();
        dbContext.WorkflowTaskSlaSchedules.Add(new WorkflowTaskSlaSchedule
        {
            WorkflowTaskId = task.Id,
            ScheduleVersion = 2,
            SlaPolicyKey = "TradingDeskReviewSla",
            TargetDate = new DateOnly(2030, 1, 20),
            DueReminderAtUtc = DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
            OverdueAtUtc = DateTimeOffset.Parse("2030-01-21T14:00:00Z"),
            NextReminderAtUtc = DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
            CalculationMode = "ApplicationTargetDate",
            TimeZoneId = "America/New_York",
            ReminderLocalTime = new TimeOnly(9, 0),
            OverdueStartOffsetDays = 1,
            RecurrenceIntervalDays = 1,
            DueNotificationRouteKey = "RouteSlaTaskDueToday",
            OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
            Status = "Active",
            CreatedAtUtc = Now,
            CreatedByUserId = "maker1",
            CorrelationId = "corr-context",
            IdempotencyKey = "idem-context"
        });
        dbContext.SaveChanges();

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest(
            "WF-PT-CTX-APP-SLA",
            "trader1",
            ["TradingDesk"]));

        var sla = response.WorkflowContext.Sla;
        Assert.Equal("OnTrack", sla.Status);
        Assert.Equal("ApplicationTargetDate", sla.CalculationMode);
        Assert.Equal(new DateOnly(2030, 1, 20), sla.TargetDate);
        Assert.Equal(2, sla.ScheduleVersion);
        Assert.Equal(DateTimeOffset.Parse("2030-01-20T14:00:00Z"), sla.NextReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-01-21T14:00:00Z"), sla.DueAtUtc);
        Assert.False(sla.Overdue);
    }

    [Fact]
    public void CompletedWorkflowReturnsNoActionsAndNoSla()
    {
        using var dbContext = CreateSeededContext();
        AddInstance(dbContext, "WF-PT-CTX-COMPLETE", "PT-CTX-COMPLETE", "Completed", "Completed", 5, completedAtUtc: Now);
        dbContext.SaveChanges();

        var response = CreateReader(dbContext).Read(new WorkflowContextReadRequest("WF-PT-CTX-COMPLETE", "checker1", ["Checker"]));

        Assert.False(response.WorkflowContext.ActorAccess.CanAct);
        Assert.Empty(response.WorkflowContext.AvailableActions);
        Assert.Null(response.WorkflowContext.CurrentTask);
        Assert.Equal("NoSla", response.WorkflowContext.Sla.Status);
        Assert.Equal("Workflow is completed.", response.WorkflowContext.ActorAccess.ReadOnlyReason);
    }

    internal static WorkflowDbContext CreateSeededContext()
    {
        var dbContext = CreateDbContext();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(Now)).SeedPortfolioTrade();
        return dbContext;
    }

    internal static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_phase3_context_tests"))
            .Options;

        return new WorkflowDbContext(options);
    }

    internal static WorkflowContextReader CreateReader(WorkflowDbContext dbContext)
    {
        return new WorkflowContextReader(dbContext, new FixedTimeProvider(Now));
    }

    internal static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.VersionNumber == 1).Id;
    }

    internal static void AddInstance(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string businessObjectId,
        string currentState,
        string status,
        int version,
        DateTimeOffset? completedAtUtc = null)
    {
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = PortfolioTradeVersionId(dbContext),
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = businessObjectId,
            CurrentStateKey = currentState,
            Status = status,
            StartedAtUtc = Now.AddMinutes(-30),
            StartedByUserId = "maker1",
            CompletedAtUtc = completedAtUtc,
            Version = version
        });
    }

    internal static void AddTask(
        WorkflowDbContext dbContext,
        string workflowInstanceId,
        string taskKey,
        string assignedRole,
        DateTimeOffset? dueAtUtc)
    {
        dbContext.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = taskKey,
            AssignedRoleKey = assignedRole,
            Status = "Pending",
            CreatedAtUtc = Now.AddMinutes(-20),
            DueAtUtc = dueAtUtc
        });
    }

    private static RuntimeCounts CountRuntimeRows(WorkflowDbContext dbContext)
    {
        return new RuntimeCounts(
            dbContext.WorkflowInstances.Count(),
            dbContext.WorkflowTasks.Count(),
            dbContext.WorkflowAudits.Count(),
            dbContext.MockSectionData.Count(),
            dbContext.NotificationOutbox.Count());
    }

    private sealed record RuntimeCounts(
        int WorkflowInstances,
        int WorkflowTasks,
        int WorkflowAudits,
        int MockSectionData,
        int NotificationOutbox);
}

[CollectionDefinition("SqlServerWorkflowContext")]
public sealed class SqlServerWorkflowContextCollection
{
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
