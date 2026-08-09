using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Sla;

public sealed class WorkflowSlaSynchronizationService(
    WorkflowDbContext dbContext,
    TimeProvider timeProvider,
    SlaScheduleCalculator calculator) : IWorkflowSlaSynchronizationService
{
    public WorkflowSlaSynchronizationResult Synchronize(WorkflowSlaSynchronizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = ResolvePendingTask(request.WorkflowInstanceId, request.WorkflowTaskId);
        var instance = dbContext.WorkflowInstances.AsNoTracking().Single(row =>
            row.WorkflowInstanceId == request.WorkflowInstanceId);
        var taskDefinition = dbContext.WorkflowTaskDefinitions.AsNoTracking().Single(row =>
            row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
            && row.TaskKey == task.TaskKey
            && row.IsActive);

        if (string.IsNullOrWhiteSpace(taskDefinition.SlaPolicyKey))
        {
            return new WorkflowSlaSynchronizationResult(false, 0, null, null, null);
        }

        var policy = dbContext.SlaPolicies.AsNoTracking().Single(row =>
            row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
            && row.SlaPolicyKey == taskDefinition.SlaPolicyKey
            && row.IsActive);
        ValidatePolicy(policy);

        if (policy.CalculationMode == "ApplicationTargetDate" && request.TargetDate is null)
        {
            throw new WorkflowSlaValidationException(
                "slaInput.targetDate",
                $"slaInput.targetDate is required for task '{task.TaskKey}'.");
        }

        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;

        var activeSchedule = dbContext.WorkflowTaskSlaSchedules.SingleOrDefault(row =>
            row.WorkflowTaskId == task.Id && row.Status == "Active");
        if (activeSchedule is not null
            && (policy.CalculationMode == "DurationFromTaskCreation"
                || activeSchedule.TargetDate == request.TargetDate))
        {
            transaction?.Commit();
            return ToResult(activeSchedule, changed: false);
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var nextVersion = activeSchedule is null
            ? dbContext.WorkflowTaskSlaSchedules
                .Where(row => row.WorkflowTaskId == task.Id)
                .Select(row => (int?)row.ScheduleVersion)
                .Max() is { } maximum ? maximum + 1 : 1
            : activeSchedule.ScheduleVersion + 1;

        if (activeSchedule is not null)
        {
            activeSchedule.Status = "Superseded";
            activeSchedule.SupersededAtUtc = now;
            activeSchedule.SupersededByUserId = request.ActorUserId;
            CancelUndeliveredOccurrences(activeSchedule.Id, now, "SlaScheduleSuperseded");
        }

        var calculated = Calculate(policy, task.CreatedAtUtc, request.TargetDate, now);
        var schedule = new WorkflowTaskSlaSchedule
        {
            WorkflowTaskId = task.Id,
            ScheduleVersion = nextVersion,
            SlaPolicyKey = policy.SlaPolicyKey,
            TargetDate = calculated.TargetDate,
            DueReminderAtUtc = calculated.DueReminderAtUtc,
            OverdueAtUtc = calculated.OverdueAtUtc,
            NextReminderAtUtc = calculated.NextReminderAtUtc,
            CalculationMode = policy.CalculationMode,
            TimeZoneId = policy.TimeZoneId,
            ReminderLocalTime = policy.ReminderLocalTime,
            OverdueStartOffsetDays = policy.OverdueStartOffsetDays,
            RecurrenceIntervalMinutes = policy.RecurrenceIntervalMinutes,
            RecurrenceIntervalDays = policy.RecurrenceIntervalDays,
            DueNotificationRouteKey = policy.DueNotificationRouteKey,
            OverdueNotificationRouteKey = policy.OverdueNotificationRouteKey,
            Status = "Active",
            CreatedAtUtc = now,
            CreatedByUserId = request.ActorUserId,
            CorrelationId = request.CorrelationId,
            IdempotencyKey = request.IdempotencyKey
        };
        task.DueAtUtc = calculated.OverdueAtUtc;
        dbContext.WorkflowTaskSlaSchedules.Add(schedule);
        dbContext.SaveChanges();
        transaction?.Commit();

        return ToResult(schedule, changed: true);
    }

    public WorkflowSlaSynchronizationResult Complete(WorkflowSlaCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var task = dbContext.WorkflowTasks.Single(row =>
            row.Id == request.WorkflowTaskId
            && row.WorkflowInstanceId == request.WorkflowInstanceId);
        var schedule = dbContext.WorkflowTaskSlaSchedules.SingleOrDefault(row =>
            row.WorkflowTaskId == task.Id && row.Status == "Active");
        if (schedule is null)
        {
            return new WorkflowSlaSynchronizationResult(false, 0, null, task.DueAtUtc, null);
        }

        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        schedule.Status = "Completed";
        schedule.CompletedAtUtc = now;
        schedule.CompletedByUserId = request.ActorUserId;
        schedule.NextReminderAtUtc = null;
        CancelUndeliveredOccurrences(schedule.Id, now, "WorkflowTaskCompleted");
        dbContext.SaveChanges();
        transaction?.Commit();

        return ToResult(schedule, changed: true);
    }

    private WorkflowTask ResolvePendingTask(string workflowInstanceId, long? workflowTaskId)
    {
        var query = dbContext.WorkflowTasks.Where(row =>
            row.WorkflowInstanceId == workflowInstanceId && row.Status == "Pending");
        return workflowTaskId is null
            ? query.Single()
            : query.Single(row => row.Id == workflowTaskId.Value);
    }

    private CalculatedSlaSchedule Calculate(
        SlaPolicy policy,
        DateTimeOffset taskCreatedAtUtc,
        DateOnly? targetDate,
        DateTimeOffset now)
    {
        if (policy.CalculationMode == "DurationFromTaskCreation")
        {
            return calculator.CalculateDuration(taskCreatedAtUtc, policy.DurationMinutes!.Value, now);
        }

        var calculated = calculator.CalculateApplicationDate(
            targetDate!.Value,
            policy.ReminderLocalTime!.Value,
            policy.TimeZoneId!,
            policy.OverdueStartOffsetDays,
            policy.RecurrenceIntervalDays!.Value,
            now);
        if (policy.SendDueDateReminder)
        {
            return calculated;
        }

        return calculated with
        {
            DueReminderAtUtc = null,
            NextReminderAtUtc = now >= calculated.OverdueAtUtc ? now : calculated.OverdueAtUtc,
            NextReminderKind = "Overdue"
        };
    }

    private void CancelUndeliveredOccurrences(long scheduleId, DateTimeOffset now, string reason)
    {
        foreach (var row in dbContext.NotificationOutbox.Where(row =>
            row.WorkflowTaskSlaScheduleId == scheduleId
            && (row.Status == "Pending" || row.Status == "Processing")))
        {
            row.Status = "Cancelled";
            row.CancelledAtUtc = now;
            row.CancellationReason = reason;
            row.ProcessingToken = null;
            row.ProcessingStartedAtUtc = null;
        }
    }

    private static WorkflowSlaSynchronizationResult ToResult(
        WorkflowTaskSlaSchedule schedule,
        bool changed)
    {
        return new WorkflowSlaSynchronizationResult(
            changed,
            schedule.ScheduleVersion,
            schedule.TargetDate,
            schedule.OverdueAtUtc,
            schedule.NextReminderAtUtc);
    }

    private static void ValidatePolicy(SlaPolicy policy)
    {
        if (policy.CalculationMode == "DurationFromTaskCreation")
        {
            var hasMinuteRecurrence = policy.RecurrenceIntervalMinutes is > 0;
            var hasDayRecurrence = policy.RecurrenceIntervalDays is > 0;
            if (policy.DurationMinutes is null or <= 0
                || policy.SendDueDateReminder
                || policy.OverdueStartOffsetDays != 0
                || policy.RecurrenceIntervalMinutes is <= 0
                || policy.RecurrenceIntervalDays is <= 0
                || (hasMinuteRecurrence && hasDayRecurrence)
                || (hasDayRecurrence
                    && (policy.ReminderLocalTime is null
                        || string.IsNullOrWhiteSpace(policy.TimeZoneId)))
                || (!hasDayRecurrence
                    && (policy.ReminderLocalTime is not null
                        || policy.TimeZoneId is not null))
                || policy.DueNotificationRouteKey is not null
                || string.IsNullOrWhiteSpace(policy.OverdueNotificationRouteKey))
            {
                throw new InvalidOperationException(
                    $"SLA policy '{policy.SlaPolicyKey}' has invalid duration-mode metadata.");
            }

            return;
        }

        if (policy.CalculationMode != "ApplicationTargetDate"
            || policy.DurationMinutes is not null
            || policy.RecurrenceIntervalMinutes is not null
            || policy.ReminderLocalTime is null
            || string.IsNullOrWhiteSpace(policy.TimeZoneId)
            || policy.OverdueStartOffsetDays < 0
            || policy.RecurrenceIntervalDays is null or <= 0
            || string.IsNullOrWhiteSpace(policy.OverdueNotificationRouteKey)
            || (policy.SendDueDateReminder && string.IsNullOrWhiteSpace(policy.DueNotificationRouteKey)))
        {
            throw new InvalidOperationException(
                $"SLA policy '{policy.SlaPolicyKey}' has invalid calculation metadata.");
        }
    }
}
