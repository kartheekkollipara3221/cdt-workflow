namespace WorkflowEngine.Infrastructure.Sla;

public sealed class SlaScheduleCalculator
{
    public CalculatedSlaSchedule CalculateDuration(
        DateTimeOffset taskCreatedAtUtc,
        int durationMinutes,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);

        var overdueAtUtc = taskCreatedAtUtc.ToUniversalTime().AddMinutes(durationMinutes);
        var normalizedNow = now.ToUniversalTime();

        return new CalculatedSlaSchedule(
            TargetDate: null,
            DueReminderAtUtc: null,
            OverdueAtUtc: overdueAtUtc,
            NextReminderAtUtc: normalizedNow >= overdueAtUtc ? normalizedNow : overdueAtUtc,
            NextReminderKind: "Overdue");
    }

    public CalculatedSlaSchedule CalculateApplicationDate(
        DateOnly targetDate,
        TimeOnly reminderLocalTime,
        string timeZoneId,
        int overdueStartOffsetDays,
        int recurrenceIntervalDays,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentOutOfRangeException.ThrowIfNegative(overdueStartOffsetDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recurrenceIntervalDays);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var dueReminderAtUtc = ConvertLocalDateToUtc(targetDate, reminderLocalTime, timeZone);
        var overdueAtUtc = ConvertLocalDateToUtc(
            targetDate.AddDays(overdueStartOffsetDays),
            reminderLocalTime,
            timeZone);
        var normalizedNow = now.ToUniversalTime();
        var isOverdue = normalizedNow >= overdueAtUtc;

        return new CalculatedSlaSchedule(
            targetDate,
            dueReminderAtUtc,
            overdueAtUtc,
            normalizedNow >= dueReminderAtUtc ? normalizedNow : dueReminderAtUtc,
            isOverdue ? "Overdue" : "DueToday");
    }

    // Separate due-reminder time (E2). With skipMissedDueReminder, a schedule that is
    // calculated after the due-reminder time goes straight to the overdue start.
    public CalculatedSlaSchedule CalculateApplicationDate(
        DateOnly targetDate,
        TimeOnly reminderLocalTime,
        TimeOnly dueReminderLocalTime,
        string timeZoneId,
        int overdueStartOffsetDays,
        int recurrenceIntervalDays,
        DateTimeOffset now,
        bool skipMissedDueReminder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentOutOfRangeException.ThrowIfNegative(overdueStartOffsetDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recurrenceIntervalDays);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var dueReminderAtUtc = ConvertLocalDateToUtc(targetDate, dueReminderLocalTime, timeZone);
        var overdueAtUtc = ConvertLocalDateToUtc(
            targetDate.AddDays(overdueStartOffsetDays),
            reminderLocalTime,
            timeZone);
        var normalizedNow = now.ToUniversalTime();
        if (normalizedNow >= overdueAtUtc || (skipMissedDueReminder && normalizedNow > dueReminderAtUtc))
        {
            return new CalculatedSlaSchedule(
                targetDate,
                skipMissedDueReminder && normalizedNow > dueReminderAtUtc ? null : dueReminderAtUtc,
                overdueAtUtc,
                normalizedNow >= overdueAtUtc ? normalizedNow : overdueAtUtc,
                "Overdue");
        }

        return new CalculatedSlaSchedule(
            targetDate,
            dueReminderAtUtc,
            overdueAtUtc,
            normalizedNow >= dueReminderAtUtc ? normalizedNow : dueReminderAtUtc,
            "DueToday");
    }

    // First overdue on the local day after task creation, at the reminder time (E3).
    public CalculatedSlaSchedule CalculateNextLocalDay(
        DateTimeOffset taskCreatedAtUtc,
        TimeOnly reminderLocalTime,
        string timeZoneId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var createdLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(taskCreatedAtUtc, timeZone).DateTime);
        var overdueAtUtc = ConvertLocalDateToUtc(createdLocalDate.AddDays(1), reminderLocalTime, timeZone);
        var normalizedNow = now.ToUniversalTime();

        return new CalculatedSlaSchedule(
            TargetDate: null,
            DueReminderAtUtc: null,
            OverdueAtUtc: overdueAtUtc,
            NextReminderAtUtc: normalizedNow >= overdueAtUtc ? normalizedNow : overdueAtUtc,
            NextReminderKind: "Overdue");
    }

    public DateTimeOffset CalculateNextRecurringOccurrence(
        DateOnly occurrenceDate,
        TimeOnly reminderLocalTime,
        string timeZoneId,
        int recurrenceIntervalDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recurrenceIntervalDays);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return ConvertLocalDateToUtc(
            occurrenceDate.AddDays(recurrenceIntervalDays),
            reminderLocalTime,
            timeZone);
    }

    public DateTimeOffset CalculateCalendarOccurrence(
        DateOnly occurrenceDate,
        TimeOnly reminderLocalTime,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return ConvertLocalDateToUtc(occurrenceDate, reminderLocalTime, timeZone);
    }

    private static DateTimeOffset ConvertLocalDateToUtc(
        DateOnly date,
        TimeOnly localTime,
        TimeZoneInfo timeZone)
    {
        var localDateTime = date.ToDateTime(localTime, DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
        return new DateTimeOffset(utcDateTime);
    }
}

public sealed record CalculatedSlaSchedule(
    DateOnly? TargetDate,
    DateTimeOffset? DueReminderAtUtc,
    DateTimeOffset OverdueAtUtc,
    DateTimeOffset? NextReminderAtUtc,
    string? NextReminderKind);
