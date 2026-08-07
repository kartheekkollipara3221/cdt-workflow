using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Phase5SlaTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");
    private const string RecipientSnapshot = """{"toEmailAddresses":["primary@example.test","secondary@example.test"],"ccEmailAddresses":["cc.one@example.test","cc.two@example.test"]}""";

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
        AddTask(dbContext, "WF-PT-3", "CheckerReviewTask", "Checker", Now.AddMinutes(1));
        AddTask(dbContext, "WF-PT-4", "MakerReworkTask", "Maker", null);
        AddTask(dbContext, "WF-PT-5", "CheckerReviewTask", "Checker", Now.AddMinutes(-3), status: "Completed");
        AddTask(dbContext, "WF-PT-6", "CheckerReviewTask", "Checker", Now.AddMinutes(-2));
        dbContext.SaveChanges();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = "WF-PT-2",
            WorkflowTaskId = alreadyQueued.Id,
            WorkflowTaskSlaScheduleId = dbContext.WorkflowTaskSlaSchedules.Single(row => row.WorkflowTaskId == alreadyQueued.Id).Id,
            SlaOccurrenceDate = DateOnly.FromDateTime(alreadyQueued.DueAtUtc!.Value.UtcDateTime),
            SlaReminderKind = "Overdue",
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-2",
            NotificationType = "SlaTaskOverdue",
            NotificationRecipientsJson = RecipientSnapshot,
            Subject = "Task overdue",
            PayloadJson = "{}",
            Status = "Sent",
            CorrelationId = "existing",
            CreatedAtUtc = Now.AddMinutes(-1),
            SentAtUtc = Now.AddMinutes(-1)
        });
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Equal(4, result.PendingTasksWithSla);
        Assert.Equal(1, result.AlreadyQueued);
        Assert.Equal([returned.Id, overdue.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.All(result.QueuedNotifications, row => Assert.Equal("Active", dbContext.WorkflowInstances.Single(instance => instance.WorkflowInstanceId == row.WorkflowInstanceId).Status));
        Assert.Equal(3, dbContext.NotificationOutbox.Count(row => row.NotificationType == "SlaTaskOverdue"));
        var overdueNotification = dbContext.NotificationOutbox.Single(row =>
            row.WorkflowTaskId == overdue.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Equal("Overdue: Workflow task overdue", overdueNotification.Subject);
        Assert.Contains("WF-PT-1", overdueNotification.Body, StringComparison.Ordinal);
        Assert.Contains("CheckerReviewTask", overdueNotification.Body, StringComparison.Ordinal);
        Assert.Contains("<strong>WF-PT-1</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<strong>CheckerReviewTask</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(RecipientSnapshot, overdueNotification.NotificationRecipientsJson);
        using var payload = JsonDocument.Parse(overdueNotification.PayloadJson);
        Assert.False(payload.RootElement.TryGetProperty("ccEmailAddresses", out _));

        var second = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Empty(second.QueuedNotifications);
    }

    [Fact]
    public async Task RunSlaWarnsAndSkipsTaskWithMissingRecipientSnapshotWithoutRoleFallback()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-LEGACY-SLA", "PT-LEGACY-SLA", "Active", versionId);
        var legacyTask = AddTask(
            dbContext,
            "WF-PT-LEGACY-SLA",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5),
            storeNotificationSnapshot: false,
            notificationRecipientsJson: null);
        var route = dbContext.NotificationRoutes.Single(row => row.RouteKey == "RouteSlaTaskOverdue");
        route.RecipientRoleKey = "Maker";
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning == $"task {legacyTask.Id} overdue notification recipients snapshot missing");
        Assert.Empty(result.QueuedNotifications);
        Assert.DoesNotContain(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == legacyTask.Id && row.NotificationType == "SlaTaskOverdue");
    }

    [Fact]
    public async Task RunSlaWarnsAndContinuesWhenRecipientSnapshotIsMalformed()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-BAD-RECIPIENTS", "PT-BAD-RECIPIENTS", "Active", versionId);
        AddInstance(dbContext, "WF-PT-GOOD-RECIPIENTS", "PT-GOOD-RECIPIENTS", "Active", versionId);
        var malformed = AddTask(
            dbContext,
            "WF-PT-BAD-RECIPIENTS",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-10),
            notificationRecipientsJson: "{");
        var valid = AddTask(
            dbContext,
            "WF-PT-GOOD-RECIPIENTS",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith(
                $"task {malformed.Id} overdue notification recipients snapshot invalid:",
                StringComparison.Ordinal));
        Assert.Equal([valid.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        var notification = Assert.Single(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == valid.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Equal(RecipientSnapshot, notification.NotificationRecipientsJson);
    }

    [Fact]
    public async Task RunSlaWarnsAndContinuesWhenStoredNotificationContextIsInvalid()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var versionId = PortfolioTradeVersionId(dbContext);
        AddInstance(dbContext, "WF-PT-BAD-CONTEXT", "PT-BAD-CONTEXT", "Active", versionId);
        AddInstance(dbContext, "WF-PT-GOOD-CONTEXT", "PT-GOOD-CONTEXT", "Active", versionId);
        var malformedContextTask = AddTask(
            dbContext,
            "WF-PT-BAD-CONTEXT",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-10),
            storeNotificationSnapshot: false);
        malformedContextTask.NotificationRouteKey = "RouteCheckerAssignedOnSubmit";
        malformedContextTask.NotificationContextJson = "{";
        var validTask = AddTask(
            dbContext,
            "WF-PT-GOOD-CONTEXT",
            "CheckerReviewTask",
            "Checker",
            Now.AddMinutes(-5));
        dbContext.SaveChanges();

        var result = await new RunSlaService(
            dbContext,
            new NotificationTemplateRenderer(),
            new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

        Assert.Contains(
            result.Warnings,
            warning => warning.StartsWith(
                $"task {malformedContextTask.Id} overdue notification context invalid:",
                StringComparison.Ordinal));
        Assert.Equal([validTask.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
        Assert.DoesNotContain(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == malformedContextTask.Id && row.NotificationType == "SlaTaskOverdue");
        Assert.Single(
            dbContext.NotificationOutbox,
            row => row.WorkflowTaskId == validTask.Id && row.NotificationType == "SlaTaskOverdue");
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
        string status = "Pending",
        bool storeNotificationSnapshot = true,
        string? notificationRecipientsJson = RecipientSnapshot)
    {
        var task = new WorkflowTask
        {
            WorkflowInstanceId = workflowInstanceId,
            TaskKey = taskKey,
            AssignedRoleKey = roleKey,
            Status = status,
            CreatedAtUtc = Now.AddHours(-1),
            DueAtUtc = dueAtUtc,
            NotificationRouteKey = storeNotificationSnapshot
                ? taskKey switch
                {
                    "CheckerReviewTask" => "RouteCheckerAssignedOnSubmit",
                    "TradingDeskReviewTask" => "RouteTradingDeskAssigned",
                    _ => null
                }
                : null,
            NotificationContextJson = storeNotificationSnapshot && dueAtUtc is not null
                ? JsonSerializer.Serialize(NotificationContext())
                : null,
            NotificationRecipientsJson = notificationRecipientsJson ?? string.Empty
        };
        dbContext.WorkflowTasks.Add(task);
        dbContext.SaveChanges();
        if (dueAtUtc is not null)
        {
            dbContext.WorkflowTaskSlaSchedules.Add(new WorkflowTaskSlaSchedule
            {
                WorkflowTaskId = task.Id,
                ScheduleVersion = 1,
                SlaPolicyKey = $"{taskKey}TestSla",
                TargetDate = null,
                DueReminderAtUtc = null,
                OverdueAtUtc = dueAtUtc.Value,
                NextReminderAtUtc = dueAtUtc.Value,
                CalculationMode = "DurationFromTaskCreation",
                TimeZoneId = null,
                ReminderLocalTime = null,
                OverdueStartOffsetDays = 0,
                RecurrenceIntervalDays = null,
                DueNotificationRouteKey = null,
                OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
                Status = "Active",
                CreatedAtUtc = task.CreatedAtUtc,
                CreatedByUserId = "system",
                CorrelationId = $"corr-{workflowInstanceId}",
                IdempotencyKey = $"idem-{workflowInstanceId}"
            });
        }

        return task;
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

    private static long PortfolioTradeVersionId(WorkflowDbContext dbContext)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        return dbContext.WorkflowDefinitionVersions.Single(row => row.WorkflowDefinitionId == definition.Id && row.IsActive).Id;
    }
}
