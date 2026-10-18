using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    private readonly SlaProcessingOptions processingOptions = new();

    public RunSlaService(WorkflowDbContext dbContext, SlaScheduleCalculator scheduleCalculator,
        SlaProcessingOptions processingOptions) : this(dbContext, scheduleCalculator)
    {
        this.processingOptions = processingOptions ?? throw new ArgumentNullException(nameof(processingOptions));
    }

    public async Task<RunSlaResult> RunAsync(RunSlaRequest request, CancellationToken cancellationToken)
    {
        var now = request.Now.ToUniversalTime();
        var contextOptions = new DbContextOptionsBuilder<WorkflowDbContext>(
                (DbContextOptions<WorkflowDbContext>)dbContext.GetService<IDbContextOptions>())
            .UseSqlServer(dbContext.Database.GetDbConnection(), contextOwnsConnection: false)
            .Options;
        var pendingTasksWithSla = 0;
        var alreadyQueued = 0;
        var queued = new List<RunSlaQueuedNotification>();
        var warnings = new List<string>();
        DateTimeOffset? cursorTime = null;
        long cursorId = 0;
        long upperId = 0;
        var firstPage = true;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Reuse provider configuration and interceptors, never the caller's tracked entities.
            await using var batchContext = new WorkflowDbContext(contextOptions);
            batchContext.Database.SetCommandTimeout(dbContext.Database.GetCommandTimeout());
            var activeQuery =
                from schedule in batchContext.WorkflowTaskSlaSchedules.AsNoTracking()
                join task in batchContext.WorkflowTasks.AsNoTracking()
                    on schedule.WorkflowTaskId equals task.Id
                join instance in batchContext.WorkflowInstances.AsNoTracking()
                    on task.WorkflowInstanceId equals instance.WorkflowInstanceId
                where schedule.Status == "Active"
                    && schedule.NextReminderAtUtc != null
                    && task.Status == "Pending"
                    && instance.Status == "Active"
                select new { Schedule = schedule, Task = task, Instance = instance };

            if (firstPage)
            {
                pendingTasksWithSla = await activeQuery.CountAsync(cancellationToken);
                // New schedules created during this run are left for the next invocation.
                upperId = await activeQuery.MaxAsync(row => (long?)row.Schedule.Id, cancellationToken) ?? 0;
                firstPage = false;
            }

            var scanRows = await activeQuery
                .Where(row => row.Schedule.Id <= upperId && row.Schedule.NextReminderAtUtc <= now
                    && (cursorTime == null || row.Schedule.NextReminderAtUtc > cursorTime
                        || (row.Schedule.NextReminderAtUtc == cursorTime && row.Schedule.Id > cursorId)))
                .OrderBy(row => row.Schedule.NextReminderAtUtc)
                .ThenBy(row => row.Schedule.Id)
                .Select(row => new SlaScanRow(row.Schedule.Id, row.Task.Id,
                    row.Instance.WorkflowInstanceId, row.Schedule.NextReminderAtUtc!.Value))
                .Take(processingOptions.BatchSize)
                .ToListAsync(cancellationToken);
            if (scanRows.Count == 0)
                break;

            // Advance from selection, including failed/skipped rows, rather than OFFSET over changing data.
            cursorTime = scanRows[^1].NextReminderAtUtc;
            cursorId = scanRows[^1].ScheduleId;
            var batch = await new RunSlaService(batchContext, scheduleCalculator)
                .ProcessBatchAsync(scanRows, now, cancellationToken);
            // Existing integrations may inspect or complete a schedule through the caller's context
            // after scanning. Refresh only its unchanged, already-tracked schedules (including rowversion).
            // Never attach new entries or overwrite caller edits.
            var batchIds = scanRows.Select(row => row.ScheduleId).ToHashSet();
            if (!dbContext.ChangeTracker.AutoDetectChangesEnabled)
                dbContext.ChangeTracker.DetectChanges();
            var callerSchedules = dbContext.ChangeTracker.Entries<WorkflowTaskSlaSchedule>()
                .Where(entry => entry.State == EntityState.Unchanged && batchIds.Contains(entry.Entity.Id))
                .ToDictionary(entry => entry.Entity.Id);
            if (callerSchedules.Count > 0)
            {
                var ids = callerSchedules.Keys.ToArray();
                var refreshed = await batchContext.WorkflowTaskSlaSchedules.AsNoTracking()
                    .Where(schedule => ids.Contains(schedule.Id)).ToListAsync(cancellationToken);
                foreach (var schedule in refreshed)
                {
                    var entry = callerSchedules[schedule.Id];
                    entry.CurrentValues.SetValues(schedule);
                    entry.OriginalValues.SetValues(schedule);
                    entry.State = EntityState.Unchanged;
                }
            }
            alreadyQueued += batch.AlreadyQueued;
            queued.AddRange(batch.QueuedNotifications);
            warnings.AddRange(batch.Warnings);
        }

        return new RunSlaResult(now, pendingTasksWithSla, alreadyQueued, queued, warnings);
    }

    private async Task<RunSlaResult> ProcessBatchAsync(IReadOnlyList<SlaScanRow> scanRows,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
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

                var occurrence = CalculateScheduleValue(() => ResolveOccurrence(schedule, now));
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
                    schedule.NextReminderAtUtc = CalculateScheduleValue(() => CalculateNextReminder(schedule, occurrence));
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                // A reminder route with its own rules saved its audience on the schedule
                // when the task was created. Other routes reuse the task snapshot.
                var recipientsJson = (occurrence.Kind == "DueToday"
                        ? schedule.DueRecipientsJson
                        : schedule.OverdueRecipientsJson)
                    ?? task.NotificationRecipientsJson;
                if (!TryValidateNotificationRecipientsSnapshot(task, recipientsJson, out var recipientsWarning))
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

                var route = await dbContext.NotificationRoutes.AsNoTracking().SingleOrDefaultAsync(item =>
                    item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                    && item.RouteKey == routeKey
                    && item.IsActive,
                    cancellationToken);
                if (route is null)
                {
                    throw new SlaScheduleDataException($"{occurrence.Kind} notification route '{routeKey}' is missing or inactive");
                }

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
                    NotificationRecipientsJson = recipientsJson,
                    Subject = subject,
                    Body = primaryEmail.Body,
                    HtmlBody = primaryEmail.HtmlBody,
                    PayloadJson = JsonSerializer.Serialize(context),
                    Status = "Pending",
                    CorrelationId = $"sla-{schedule.Id}-{occurrence.EligibleAtUtc.UtcTicks}-{occurrence.Kind}",
                    CreatedAtUtc = now
                };
                dbContext.NotificationOutbox.Add(outbox);
                schedule.NextReminderAtUtc = CalculateScheduleValue(() => CalculateNextReminder(schedule, occurrence));
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
            catch (SlaScheduleDataException exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                // Rollback does not remove a staged outbox insert from EF tracking.
                // It must not be saved by the next healthy schedule's transaction.
                dbContext.ChangeTracker.Clear();
                warnings.Add($"schedule {row.ScheduleId} task {row.TaskId} workflow {row.WorkflowInstanceId} skipped: {exception.Message}");
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

        return new RunSlaResult(now, 0, alreadyQueued, queued, warnings);
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
                throw new SlaScheduleDataException("duration recurrence requires reminder time and time zone");
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
            throw new SlaScheduleDataException("calculation mode or application target date, reminder time and time zone metadata is invalid");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var currentLocalDate = DateOnly.FromDateTime(localNow.DateTime);
        var overdueStartDate = schedule.TargetDate.Value.AddDays(schedule.OverdueStartOffsetDays);
        var kind = currentLocalDate >= overdueStartDate ? "Overdue" : "DueToday";
        var occurrenceLocalTime = kind == "DueToday"
            ? schedule.DueReminderLocalTime ?? schedule.ReminderLocalTime.Value
            : schedule.ReminderLocalTime.Value;
        var eligibleAtUtc = scheduleCalculator.CalculateCalendarOccurrence(
            currentLocalDate,
            occurrenceLocalTime,
            schedule.TimeZoneId);
        return new SlaOccurrence(currentLocalDate, kind, eligibleAtUtc);
    }

    private DateTimeOffset? CalculateNextReminder(WorkflowTaskSlaSchedule schedule, SlaOccurrence occurrence)
    {
        if (schedule.CalculationMode == "DurationFromTaskCreation")
        {
            if (schedule.RecurrenceIntervalMinutes is > 0)
            {
                return occurrence.EligibleAtUtc.AddMinutes(
                    schedule.RecurrenceIntervalMinutes.Value);
            }

            if (schedule.RecurrenceIntervalDays is null)
            {
                return null;
            }

            if (schedule.ReminderLocalTime is null
                || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
            {
                throw new SlaScheduleDataException("duration recurrence requires reminder time and time zone");
            }

            return scheduleCalculator.CalculateNextRecurringOccurrence(
                occurrence.Date,
                schedule.ReminderLocalTime.Value,
                schedule.TimeZoneId,
                schedule.RecurrenceIntervalDays.Value);
        }

        if (occurrence.Kind == "DueToday")
        {
            return schedule.OverdueAtUtc;
        }

        if (schedule.RecurrenceIntervalDays is null
            || schedule.ReminderLocalTime is null
            || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
        {
            return null;
        }

        return scheduleCalculator.CalculateNextRecurringOccurrence(
            occurrence.Date,
            schedule.ReminderLocalTime.Value,
            schedule.TimeZoneId,
            schedule.RecurrenceIntervalDays.Value);
    }

    private static bool TryValidateNotificationRecipientsSnapshot(
        WorkflowTask task,
        string recipientsJson,
        out string warning)
    {
        if (string.IsNullOrWhiteSpace(recipientsJson))
        {
            warning = $"task {task.Id} overdue notification recipients snapshot missing";
            return false;
        }

        try
        {
            var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
                recipientsJson,
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
        return CalculateScheduleValue(() => value
            .ToOffset(FixedUtcMinusFiveOffset)
            .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
    }

    private static T CalculateScheduleValue<T>(Func<T> calculate)
    {
        try
        {
            return calculate();
        }
        catch (Exception exception) when (exception is ArgumentException
            or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Only calendar calculations use this boundary. Database, cancellation
            // and unexpected runtime failures must still escape the scan.
            throw new SlaScheduleDataException($"calendar calculation failed: {exception.Message}", exception);
        }
    }

    private sealed class SlaScheduleDataException(string message, Exception? innerException = null)
        : Exception(message, innerException);

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

    private sealed record SlaScanRow(long ScheduleId, long TaskId, string WorkflowInstanceId, DateTimeOffset NextReminderAtUtc);

    private sealed record SlaOccurrence(DateOnly Date, string Kind, DateTimeOffset EligibleAtUtc);
}
