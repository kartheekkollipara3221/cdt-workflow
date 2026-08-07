using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowSlaSynchronizationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public void ApplicationDateScheduleCreatesNoOpsAndReschedulesWithHistory()
    {
        using var dbContext = CreateSeededContext();
        var task = AddTask(dbContext, "TradingDeskReviewTask", Now);
        var service = CreateService(dbContext);

        var first = service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 20), "idem-1"));
        var same = service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 20), "idem-2"));
        var revised = service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 25), "idem-3"));

        Assert.True(first.Changed);
        Assert.Equal(1, first.ScheduleVersion);
        Assert.Equal(new DateOnly(2030, 1, 20), first.TargetDate);
        Assert.Equal(DateTimeOffset.Parse("2030-01-21T14:00:00Z"), first.DueAtUtc);
        Assert.False(same.Changed);
        Assert.Equal(1, same.ScheduleVersion);
        Assert.True(revised.Changed);
        Assert.Equal(2, revised.ScheduleVersion);
        Assert.Equal(DateTimeOffset.Parse("2030-01-26T14:00:00Z"), revised.DueAtUtc);

        var schedules = dbContext.WorkflowTaskSlaSchedules
            .OrderBy(row => row.ScheduleVersion)
            .ToList();
        Assert.Equal(2, schedules.Count);
        Assert.Equal("Superseded", schedules[0].Status);
        Assert.NotNull(schedules[0].SupersededAtUtc);
        Assert.Equal("Active", schedules[1].Status);
        Assert.Equal(DateTimeOffset.Parse("2030-01-26T14:00:00Z"), task.DueAtUtc);
        Assert.Equal(3, dbContext.WorkflowInstances.Single().Version);
        Assert.Empty(dbContext.WorkflowAudits);
    }

    [Fact]
    public void RescheduleCancelsUndeliveredOldScheduleOccurrencesButPreservesSentHistory()
    {
        using var dbContext = CreateSeededContext();
        var task = AddTask(dbContext, "TradingDeskReviewTask", Now);
        var service = CreateService(dbContext);
        service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 20), "idem-1"));
        var oldSchedule = dbContext.WorkflowTaskSlaSchedules.Single();
        AddOccurrence(dbContext, task, oldSchedule, "Pending", new DateOnly(2030, 1, 20));
        AddOccurrence(dbContext, task, oldSchedule, "Sent", new DateOnly(2030, 1, 21));
        dbContext.SaveChanges();

        service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 25), "idem-2"));

        var pending = dbContext.NotificationOutbox.Single(row => row.SlaOccurrenceDate == new DateOnly(2030, 1, 20));
        var sent = dbContext.NotificationOutbox.Single(row => row.SlaOccurrenceDate == new DateOnly(2030, 1, 21));
        Assert.Equal("Cancelled", pending.Status);
        Assert.NotNull(pending.CancelledAtUtc);
        Assert.Equal("SlaScheduleSuperseded", pending.CancellationReason);
        Assert.Equal("Sent", sent.Status);
        Assert.Null(sent.CancelledAtUtc);
    }

    [Fact]
    public void DurationScheduleIgnoresTargetDateAuthority()
    {
        using var dbContext = CreateSeededContext();
        var task = AddTask(dbContext, "CheckerReviewTask", Now);
        var service = CreateService(dbContext);

        var first = service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 20), "idem-1"));
        var second = service.Synchronize(Request(task.Id, new DateOnly(2040, 1, 20), "idem-2"));

        Assert.True(first.Changed);
        Assert.False(second.Changed);
        Assert.Equal(DateTimeOffset.Parse("2030-01-15T15:32:00Z"), task.DueAtUtc);
        var schedule = Assert.Single(dbContext.WorkflowTaskSlaSchedules);
        Assert.Equal("DurationFromTaskCreation", schedule.CalculationMode);
        Assert.Null(schedule.TargetDate);
        Assert.Equal(1, schedule.ScheduleVersion);
    }

    [Fact]
    public void ApplicationDateScheduleRequiresTargetBeforePersistence()
    {
        using var dbContext = CreateSeededContext();
        var task = AddTask(dbContext, "TradingDeskReviewTask", Now);
        var service = CreateService(dbContext);

        var exception = Assert.Throws<WorkflowSlaValidationException>(() =>
            service.Synchronize(Request(task.Id, targetDate: null, "idem-missing")));

        Assert.Equal("slaInput.targetDate", exception.Field);
        Assert.Empty(dbContext.WorkflowTaskSlaSchedules);
        Assert.Null(task.DueAtUtc);
    }

    [Fact]
    public void CompletingTaskCompletesScheduleAndCancelsUndeliveredOccurrences()
    {
        using var dbContext = CreateSeededContext();
        var task = AddTask(dbContext, "TradingDeskReviewTask", Now);
        var service = CreateService(dbContext);
        service.Synchronize(Request(task.Id, new DateOnly(2030, 1, 20), "idem-1"));
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single();
        AddOccurrence(dbContext, task, schedule, "Pending", new DateOnly(2030, 1, 20));
        dbContext.SaveChanges();

        var result = service.Complete(new WorkflowSlaCompletionRequest(
            "WF-SLA-1",
            task.Id,
            "trader1",
            "corr-complete"));

        Assert.True(result.Changed);
        Assert.Equal("Completed", schedule.Status);
        Assert.NotNull(schedule.CompletedAtUtc);
        var occurrence = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal("Cancelled", occurrence.Status);
        Assert.Equal("WorkflowTaskCompleted", occurrence.CancellationReason);
    }

    private static WorkflowDbContext CreateSeededContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_sla_sync_tests"))
            .Options;
        var dbContext = new WorkflowDbContext(options);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(Now)).SeedPortfolioTrade();
        AddInstance(dbContext);
        return dbContext;
    }

    private static void AddInstance(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        var version = dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.IsActive);
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-SLA-1",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = version.Id,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-SLA-1",
            CurrentStateKey = "TradingDeskReview",
            Status = "Active",
            StartedAtUtc = Now,
            StartedByUserId = "maker1",
            Version = 3
        });
        dbContext.SaveChanges();
    }

    private static WorkflowTask AddTask(WorkflowDbContext dbContext, string taskKey, DateTimeOffset createdAtUtc)
    {
        var task = new WorkflowTask
        {
            WorkflowInstanceId = "WF-SLA-1",
            TaskKey = taskKey,
            AssignedRoleKey = taskKey == "CheckerReviewTask" ? "Checker" : "TradingDeskTaxable",
            Status = "Pending",
            CreatedAtUtc = createdAtUtc,
            NotificationRecipientsJson = """{"toEmailAddresses":["desk@example.test"],"ccEmailAddresses":[]}"""
        };
        dbContext.WorkflowTasks.Add(task);
        dbContext.SaveChanges();
        return task;
    }

    private static WorkflowSlaSynchronizationService CreateService(WorkflowDbContext dbContext)
    {
        return new WorkflowSlaSynchronizationService(
            dbContext,
            new FixedTimeProvider(Now),
            new SlaScheduleCalculator());
    }

    private static WorkflowSlaSynchronizationRequest Request(
        long taskId,
        DateOnly? targetDate,
        string idempotencyKey)
    {
        return new WorkflowSlaSynchronizationRequest(
            "WF-SLA-1",
            taskId,
            targetDate,
            "maker1",
            "corr-sla",
            idempotencyKey);
    }

    private static void AddOccurrence(
        WorkflowDbContext dbContext,
        WorkflowTask task,
        WorkflowTaskSlaSchedule schedule,
        string status,
        DateOnly occurrenceDate)
    {
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = task.WorkflowInstanceId,
            WorkflowTaskId = task.Id,
            WorkflowTaskSlaScheduleId = schedule.Id,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-SLA-1",
            NotificationType = "SlaTaskOverdue",
            NotificationRecipientsJson = task.NotificationRecipientsJson,
            Subject = "SLA reminder",
            Body = "body",
            HtmlBody = "<p>body</p>",
            PayloadJson = "{}",
            Status = status,
            CorrelationId = $"sla-{occurrenceDate:yyyyMMdd}",
            CreatedAtUtc = Now,
            SentAtUtc = status == "Sent" ? Now : null,
            SlaOccurrenceDate = occurrenceDate,
            SlaReminderKind = occurrenceDate == schedule.TargetDate ? "DueToday" : "Overdue"
        });
    }
}
