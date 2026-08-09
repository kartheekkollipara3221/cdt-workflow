using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class RecurringSlaMonitorTests
{
    private static readonly DateTimeOffset CreatedAtUtc = DateTimeOffset.Parse("2030-01-15T15:30:00Z");

    [Fact]
    public async Task ApplicationDateScheduleQueuesDueTodayThenCalendarDayOverdueOccurrencesIncludingWeekend()
    {
        using var dbContext = CreateScenario(new DateOnly(2030, 1, 18));
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single();
        var service = CreateRunSlaService(dbContext);

        var due = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-18T14:00:00Z")),
            CancellationToken.None);
        var firstOverdue = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-19T14:00:00Z")),
            CancellationToken.None);
        var repeatedSaturdayScan = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-19T20:00:00Z")),
            CancellationToken.None);
        var sundayOverdue = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-20T14:00:00Z")),
            CancellationToken.None);

        Assert.Equal("DueToday", Assert.Single(due.QueuedNotifications).ReminderKind);
        Assert.Equal("Overdue", Assert.Single(firstOverdue.QueuedNotifications).ReminderKind);
        Assert.Empty(repeatedSaturdayScan.QueuedNotifications);
        Assert.Equal("Overdue", Assert.Single(sundayOverdue.QueuedNotifications).ReminderKind);
        var dueOccurrence = dbContext.NotificationOutbox.Single(row =>
            row.WorkflowTaskSlaScheduleId == schedule.Id
            && row.SlaReminderKind == "DueToday");
        Assert.Equal("SlaTaskDueToday", dueOccurrence.NotificationType);
        Assert.Equal("Workflow task due today", dueOccurrence.Subject);
        using (var payload = JsonDocument.Parse(dueOccurrence.PayloadJson))
        {
            Assert.Equal("2030-01-18", payload.RootElement.GetProperty("targetDate").GetString());
            Assert.Equal("2030-01-18", payload.RootElement.GetProperty("slaOccurrenceDate").GetString());
            Assert.Equal("DueToday", payload.RootElement.GetProperty("slaReminderKind").GetString());
            Assert.Equal("2030-01-18T14:00:00.0000000+00:00", payload.RootElement.GetProperty("dueReminderAtUtc").GetString());
            Assert.Equal("2030-01-19T14:00:00.0000000+00:00", payload.RootElement.GetProperty("overdueAtUtc").GetString());
        }
        Assert.Equal(
            [
                (new DateOnly(2030, 1, 18), "DueToday"),
                (new DateOnly(2030, 1, 19), "Overdue"),
                (new DateOnly(2030, 1, 20), "Overdue")
            ],
            dbContext.NotificationOutbox
                .Where(row => row.WorkflowTaskSlaScheduleId == schedule.Id)
                .OrderBy(row => row.SlaOccurrenceDate)
                .Select(row => new ValueTuple<DateOnly, string>(row.SlaOccurrenceDate!.Value, row.SlaReminderKind!))
                .ToArray());
        Assert.Equal(DateTimeOffset.Parse("2030-01-21T14:00:00Z"), schedule.NextReminderAtUtc);
    }

    [Fact]
    public async Task DelayedScanQueuesOnlyCurrentLocalDateAndAdvancesFromThatDate()
    {
        using var dbContext = CreateScenario(new DateOnly(2030, 1, 18));
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single();

        var result = await CreateRunSlaService(dbContext).RunAsync(
            new(DateTimeOffset.Parse("2030-01-23T16:00:00Z")),
            CancellationToken.None);

        var queued = Assert.Single(result.QueuedNotifications);
        Assert.Equal(schedule.Id, queued.WorkflowTaskSlaScheduleId);
        Assert.Equal(1, queued.ScheduleVersion);
        Assert.Equal(new DateOnly(2030, 1, 23), queued.OccurrenceDate);
        Assert.Equal("Overdue", queued.ReminderKind);
        var occurrence = Assert.Single(dbContext.NotificationOutbox);
        Assert.Equal(DateTimeOffset.Parse("2030-01-23T14:00:00Z"), occurrence.SlaOccurrenceAtUtc);
        Assert.Equal(new DateOnly(2030, 1, 23), occurrence.SlaOccurrenceDate);
        Assert.Equal("Overdue", occurrence.SlaReminderKind);
        Assert.Equal(DateTimeOffset.Parse("2030-01-24T14:00:00Z"), schedule.NextReminderAtUtc);
    }

    [Fact]
    public async Task DelayedScanBeforeCurrentLocalReminderTimeWaitsForCurrentBoundary()
    {
        using var dbContext = CreateScenario(new DateOnly(2030, 1, 18));
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single();
        var service = CreateRunSlaService(dbContext);

        var early = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-23T13:59:00Z")),
            CancellationToken.None);
        var atBoundary = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-23T14:00:00Z")),
            CancellationToken.None);

        Assert.Empty(early.QueuedNotifications);
        var queued = Assert.Single(atBoundary.QueuedNotifications);
        Assert.Equal(new DateOnly(2030, 1, 23), queued.OccurrenceDate);
        Assert.Equal("Overdue", queued.ReminderKind);
        Assert.Equal(DateTimeOffset.Parse("2030-01-24T14:00:00Z"), schedule.NextReminderAtUtc);
    }

    [Fact]
    public async Task CompletedTaskDoesNotQueueALaterRecurringOccurrence()
    {
        using var dbContext = CreateScenario(new DateOnly(2030, 1, 18));
        var schedule = dbContext.WorkflowTaskSlaSchedules.Single();
        var task = dbContext.WorkflowTasks.Single();
        var service = CreateRunSlaService(dbContext);
        await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-18T14:00:00Z")),
            CancellationToken.None);
        new WorkflowSlaSynchronizationService(
            dbContext,
            new FixedTimeProvider(DateTimeOffset.Parse("2030-01-18T15:00:00Z")),
            new SlaScheduleCalculator()).Complete(new(
                task.WorkflowInstanceId,
                task.Id,
                "trader1",
                "corr-complete"));
        task.Status = "Completed";
        dbContext.SaveChanges();

        var result = await service.RunAsync(
            new(DateTimeOffset.Parse("2030-01-19T14:00:00Z")),
            CancellationToken.None);

        Assert.Empty(result.QueuedNotifications);
        Assert.Equal("Completed", schedule.Status);
        Assert.Null(schedule.NextReminderAtUtc);
        Assert.Single(dbContext.NotificationOutbox);
    }

    private static RunSlaService CreateRunSlaService(WorkflowDbContext dbContext)
    {
        return new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator());
    }

    private static WorkflowDbContext CreateScenario(DateOnly targetDate)
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_recurring_sla_monitor_tests"))
            .Options;
        var dbContext = new WorkflowDbContext(options);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.Migrate();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(CreatedAtUtc)).SeedPortfolioTrade();

        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        var version = dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.IsActive);
        dbContext.WorkflowInstances.Add(new WorkflowInstance
        {
            WorkflowInstanceId = "WF-RECURRING-SLA-1",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = version.Id,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-RECURRING-SLA-1",
            CurrentStateKey = "TradingDeskReview",
            Status = "Active",
            StartedAtUtc = CreatedAtUtc,
            StartedByUserId = "maker1",
            Version = 3
        });
        var task = new WorkflowTask
        {
            WorkflowInstanceId = "WF-RECURRING-SLA-1",
            TaskKey = "TradingDeskReviewTask",
            AssignedRoleKey = "TradingDeskTaxable",
            Status = "Pending",
            CreatedAtUtc = CreatedAtUtc,
            NotificationRecipientsJson = """{"toEmailAddresses":["desk@example.test"],"ccEmailAddresses":[]}""",
            NotificationContextJson = JsonSerializer.Serialize(NotificationContext())
        };
        dbContext.WorkflowTasks.Add(task);
        dbContext.SaveChanges();

        new WorkflowSlaSynchronizationService(
            dbContext,
            new FixedTimeProvider(CreatedAtUtc),
            new SlaScheduleCalculator()).Synchronize(new(
                task.WorkflowInstanceId,
                task.Id,
                targetDate,
                "maker1",
                "corr-create",
                "idem-create"));

        return dbContext;
    }

    private static Dictionary<string, string?> NotificationContext()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["referenceNumber"] = "1050",
            ["submitterDisplayName"] = "Kollipara, Kartheek",
            ["returnedByDisplayName"] = "Kollipara, Kartheek",
            ["recipientDisplayName"] = "Kollipara, Kartheek",
            ["traderDisplayName"] = "Kollipara, Kartheek",
            ["confirmerDisplayName"] = "Kollipara, Kartheek",
            ["deskName"] = "Taxable",
            ["securityType"] = "Other",
            ["accountShortName"] = "EFG",
            ["accountDisplayName"] = "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
            ["submittedDate"] = "4/24/2026",
            ["tradeLink"] = "https://sharepoint.example.test/trades/1050"
        };
    }
}
