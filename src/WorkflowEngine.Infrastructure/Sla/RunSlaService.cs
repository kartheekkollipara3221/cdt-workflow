using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Sla;

public sealed class RunSlaService(
    WorkflowDbContext dbContext,
    SlaScheduleCalculator scheduleCalculator) : IRunSlaService
{
    private static readonly TimeSpan FixedUtcMinusFiveOffset = TimeSpan.FromHours(-5);

    public async Task<RunSlaResult> RunAsync(RunSlaRequest request, CancellationToken cancellationToken)
    {
        var now = request.Now.ToUniversalTime();
        var activeQuery =
            from schedule in dbContext.WorkflowTaskSlaSchedules.AsNoTracking()
            join task in dbContext.WorkflowTasks.AsNoTracking()
                on schedule.WorkflowTaskId equals task.Id
            join instance in dbContext.WorkflowInstances.AsNoTracking()
                on task.WorkflowInstanceId equals instance.WorkflowInstanceId
            where schedule.Status == "Active"
                && schedule.NextReminderAtUtc != null
                && task.Status == "Pending"
                && instance.Status == "Active"
            select new
            {
                Schedule = schedule,
                Task = task,
                Instance = instance
            };

        var pendingTasksWithSla = await activeQuery.CountAsync(cancellationToken);
        var scanRows = await activeQuery
            .Where(row => row.Schedule.NextReminderAtUtc <= now)
            .OrderBy(row => row.Schedule.NextReminderAtUtc)
            .ThenBy(row => row.Schedule.Id)
            .Select(row => new SlaScanRow(
                row.Schedule.Id,
                row.Task.Id,
                row.Instance.WorkflowInstanceId))
            .ToListAsync(cancellationToken);

        var alreadyQueued = 0;
        var queued = new List<RunSlaQueuedNotification>();
        var warnings = new List<string>();

        foreach (var row in scanRows)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var schedule = await dbContext.WorkflowTaskSlaSchedules.SingleAsync(
                    item => item.Id == row.ScheduleId,
                    cancellationToken);
                var task = await dbContext.WorkflowTasks.SingleAsync(
                    item => item.Id == row.TaskId,
                    cancellationToken);
                var instance = await dbContext.WorkflowInstances.SingleAsync(
                    item => item.WorkflowInstanceId == row.WorkflowInstanceId,
                    cancellationToken);

                if (schedule.Status != "Active"
                    || schedule.NextReminderAtUtc is null
                    || schedule.NextReminderAtUtc > now
                    || task.Status != "Pending"
                    || instance.Status != "Active")
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                var occurrence = ResolveOccurrence(schedule, now);
                if (occurrence.EligibleAtUtc > now)
                {
                    schedule.NextReminderAtUtc = occurrence.EligibleAtUtc;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                var exists = await dbContext.NotificationOutbox.AnyAsync(outbox =>
                    outbox.WorkflowTaskSlaScheduleId == schedule.Id
                    && outbox.SlaOccurrenceAtUtc == occurrence.EligibleAtUtc
                    && outbox.SlaReminderKind == occurrence.Kind,
                    cancellationToken);
                if (exists)
                {
                    alreadyQueued++;
                    AdvanceSchedule(schedule, occurrence);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                if (!TryValidateNotificationRecipientsSnapshot(task, out var recipientsWarning))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add(recipientsWarning);
                    continue;
                }

                var routeKey = occurrence.Kind == "DueToday"
                    ? schedule.DueNotificationRouteKey
                    : schedule.OverdueNotificationRouteKey;
                if (string.IsNullOrWhiteSpace(routeKey))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add($"schedule {schedule.Id} {occurrence.Kind} notification route missing");
                    continue;
                }

                var route = await dbContext.NotificationRoutes.AsNoTracking().SingleAsync(item =>
                    item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                    && item.RouteKey == routeKey
                    && item.IsActive,
                    cancellationToken);

                if (!TryBuildNotificationContext(
                    task,
                    instance,
                    schedule,
                    occurrence,
                    now,
                    out var context,
                    out var contextWarning))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add(contextWarning);
                    continue;
                }

                var primaryEmails = await dbContext.NotificationOutbox
                    .AsNoTracking()
                    .Where(outbox =>
                        outbox.WorkflowTaskId == task.Id
                        && outbox.SlaReminderKind == null)
                    .Take(2)
                    .ToListAsync(cancellationToken);
                if (primaryEmails.Count != 1)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add($"task {task.Id} SLA primary task email {(primaryEmails.Count == 0 ? "missing" : "ambiguous")}");
                    continue;
                }

                var primaryEmail = primaryEmails[0];
                var subject = route.OverdueSubjectPrefix + primaryEmail.Subject;
                if (subject.Length > 300)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add($"task {task.Id} SLA notification subject exceeds 300 characters");
                    continue;
                }

                var outbox = new NotificationOutbox
                {
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowTaskId = task.Id,
                    WorkflowTaskSlaScheduleId = schedule.Id,
                    SlaOccurrenceAtUtc = occurrence.EligibleAtUtc,
                    SlaOccurrenceDate = occurrence.Date,
                    SlaReminderKind = occurrence.Kind,
                    BusinessObjectType = instance.BusinessObjectType,
                    BusinessObjectId = instance.BusinessObjectId,
                    NotificationType = route.NotificationType,
                    NotificationRecipientsJson = task.NotificationRecipientsJson,
                    Subject = subject,
                    Body = primaryEmail.Body,
                    HtmlBody = primaryEmail.HtmlBody,
                    PayloadJson = JsonSerializer.Serialize(context),
                    Status = "Pending",
                    CorrelationId = $"sla-{schedule.Id}-{occurrence.EligibleAtUtc.UtcTicks}-{occurrence.Kind}",
                    CreatedAtUtc = now
                };
                dbContext.NotificationOutbox.Add(outbox);
                AdvanceSchedule(schedule, occurrence);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                queued.Add(new RunSlaQueuedNotification(
                    task.Id,
                    instance.WorkflowInstanceId,
                    task.TaskKey,
                    task.AssignedRoleKey,
                    schedule.OverdueAtUtc,
                    outbox.Id,
                    schedule.Id,
                    schedule.ScheduleVersion,
                    occurrence.EligibleAtUtc,
                    occurrence.Date,
                    occurrence.Kind));
            }
            catch (DbUpdateException exception) when (IsSlaOutboxUniqueViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                alreadyQueued++;
                warnings.Add($"schedule {row.ScheduleId} occurrence was already queued");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                warnings.Add($"schedule {row.ScheduleId} changed during the SLA scan; a later scan will re-evaluate it");
            }
        }

        return new RunSlaResult(now, pendingTasksWithSla, alreadyQueued, queued, warnings);
    }

    private SlaOccurrence ResolveOccurrence(WorkflowTaskSlaSchedule schedule, DateTimeOffset now)
    {
        if (schedule.CalculationMode == "DurationFromTaskCreation")
        {
            if (schedule.RecurrenceIntervalMinutes is > 0)
            {
                var next = schedule.NextReminderAtUtc ?? schedule.OverdueAtUtc;
                var occurrenceAtUtc = ResolveLatestMinuteOccurrence(
                    next,
                    now,
                    schedule.RecurrenceIntervalMinutes.Value);
                return new SlaOccurrence(
                    DateOnly.FromDateTime(occurrenceAtUtc.UtcDateTime),
                    "Overdue",
                    occurrenceAtUtc);
            }

            if (schedule.RecurrenceIntervalDays is null)
            {
                return new SlaOccurrence(
                    DateOnly.FromDateTime(schedule.OverdueAtUtc.UtcDateTime),
                    "Overdue",
                    schedule.OverdueAtUtc);
            }

            if (schedule.ReminderLocalTime is null
                || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
            {
                throw new InvalidOperationException($"SLA schedule {schedule.Id} metadata is invalid.");
            }

            var durationTimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
            var overdueLocalDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(schedule.OverdueAtUtc, durationTimeZone).DateTime);
            var durationCurrentLocalDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now, durationTimeZone).DateTime);
            if (durationCurrentLocalDate <= overdueLocalDate)
            {
                return new SlaOccurrence(overdueLocalDate, "Overdue", schedule.OverdueAtUtc);
            }

            var durationEligibleAtUtc = scheduleCalculator.CalculateCalendarOccurrence(
                durationCurrentLocalDate,
                schedule.ReminderLocalTime.Value,
                schedule.TimeZoneId);
            return new SlaOccurrence(durationCurrentLocalDate, "Overdue", durationEligibleAtUtc);
        }

        if (schedule.CalculationMode != "ApplicationTargetDate"
            || schedule.TargetDate is null
            || schedule.ReminderLocalTime is null
            || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
        {
            throw new InvalidOperationException($"SLA schedule {schedule.Id} metadata is invalid.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var currentLocalDate = DateOnly.FromDateTime(localNow.DateTime);
        var overdueStartDate = schedule.TargetDate.Value.AddDays(schedule.OverdueStartOffsetDays);
        var kind = currentLocalDate >= overdueStartDate ? "Overdue" : "DueToday";
        var eligibleAtUtc = scheduleCalculator.CalculateCalendarOccurrence(
            currentLocalDate,
            schedule.ReminderLocalTime.Value,
            schedule.TimeZoneId);
        return new SlaOccurrence(currentLocalDate, kind, eligibleAtUtc);
    }

    private void AdvanceSchedule(WorkflowTaskSlaSchedule schedule, SlaOccurrence occurrence)
    {
        if (schedule.CalculationMode == "DurationFromTaskCreation")
        {
            if (schedule.RecurrenceIntervalMinutes is > 0)
            {
                schedule.NextReminderAtUtc = occurrence.EligibleAtUtc.AddMinutes(
                    schedule.RecurrenceIntervalMinutes.Value);
                return;
            }

            if (schedule.RecurrenceIntervalDays is null)
            {
                schedule.NextReminderAtUtc = null;
                return;
            }

            if (schedule.ReminderLocalTime is null
                || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
            {
                throw new InvalidOperationException($"SLA schedule {schedule.Id} metadata is invalid.");
            }

            schedule.NextReminderAtUtc = scheduleCalculator.CalculateNextRecurringOccurrence(
                occurrence.Date,
                schedule.ReminderLocalTime.Value,
                schedule.TimeZoneId,
                schedule.RecurrenceIntervalDays.Value);
            return;
        }

        if (occurrence.Kind == "DueToday")
        {
            schedule.NextReminderAtUtc = schedule.OverdueAtUtc;
            return;
        }

        if (schedule.RecurrenceIntervalDays is null
            || schedule.ReminderLocalTime is null
            || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
        {
            schedule.NextReminderAtUtc = null;
            return;
        }

        schedule.NextReminderAtUtc = scheduleCalculator.CalculateNextRecurringOccurrence(
            occurrence.Date,
            schedule.ReminderLocalTime.Value,
            schedule.TimeZoneId,
            schedule.RecurrenceIntervalDays.Value);
    }

    private static bool TryValidateNotificationRecipientsSnapshot(
        WorkflowTask task,
        out string warning)
    {
        if (string.IsNullOrWhiteSpace(task.NotificationRecipientsJson))
        {
            warning = $"task {task.Id} overdue notification recipients snapshot missing";
            return false;
        }

        try
        {
            var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
                task.NotificationRecipientsJson,
                WorkflowJson.Options);
            if (recipients is null
                || recipients.ToEmailAddresses is null
                || recipients.CcEmailAddresses is null)
            {
                warning = $"task {task.Id} overdue notification recipients snapshot invalid: typed recipient arrays are required";
                return false;
            }
        }
        catch (JsonException exception)
        {
            warning = $"task {task.Id} overdue notification recipients snapshot invalid: {exception.Message}";
            return false;
        }

        warning = string.Empty;
        return true;
    }

    private static bool TryBuildNotificationContext(
        WorkflowTask task,
        WorkflowInstance instance,
        WorkflowTaskSlaSchedule schedule,
        SlaOccurrence occurrence,
        DateTimeOffset processedAtUtc,
        out Dictionary<string, string?> context,
        out string warning)
    {
        warning = string.Empty;
        if (string.IsNullOrWhiteSpace(task.NotificationContextJson))
        {
            context = new Dictionary<string, string?>(StringComparer.Ordinal);
        }
        else
        {
            try
            {
                context = JsonSerializer.Deserialize<Dictionary<string, string?>>(task.NotificationContextJson)
                    ?? new Dictionary<string, string?>(StringComparer.Ordinal);
            }
            catch (JsonException exception)
            {
                context = new Dictionary<string, string?>(StringComparer.Ordinal);
                warning = $"task {task.Id} overdue notification context invalid: {exception.Message}";
                return false;
            }
        }

        context["workflowInstanceId"] = instance.WorkflowInstanceId;
        context["taskKey"] = task.TaskKey;
        context["assignedRoleKey"] = task.AssignedRoleKey;
        context["targetDate"] = schedule.TargetDate?.ToString("yyyy-MM-dd");
        context["dueAtUtc"] = schedule.OverdueAtUtc.ToString("O");
        context["dueDateDisplay"] = FormatFixedUtcMinusFiveDate(schedule.OverdueAtUtc);
        context["dueReminderAtUtc"] = schedule.DueReminderAtUtc?.ToString("O");
        context["overdueAtUtc"] = schedule.OverdueAtUtc.ToString("O");
        context["overdueDateDisplay"] = FormatFixedUtcMinusFiveDate(schedule.OverdueAtUtc);
        context["reminderQueuedAtUtc"] = processedAtUtc.ToString("O");
        context["slaOccurrenceAtUtc"] = occurrence.EligibleAtUtc.ToString("O");
        context["slaOccurrenceDate"] = occurrence.Date.ToString("yyyy-MM-dd");
        context["slaReminderKind"] = occurrence.Kind;
        return true;
    }

    private static string FormatFixedUtcMinusFiveDate(DateTimeOffset value)
    {
        return value
            .ToOffset(FixedUtcMinusFiveOffset)
            .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ResolveLatestMinuteOccurrence(
        DateTimeOffset next,
        DateTimeOffset now,
        int intervalMinutes)
    {
        var intervalTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        var elapsedTicks = (now - next).Ticks;
        var completedIntervals = elapsedTicks / intervalTicks;
        return next.AddTicks(completedIntervals * intervalTicks);
    }

    private static bool IsSlaOutboxUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);
    }

    private sealed record SlaScanRow(long ScheduleId, long TaskId, string WorkflowInstanceId);

    private sealed record SlaOccurrence(DateOnly Date, string Kind, DateTimeOffset EligibleAtUtc);
}
