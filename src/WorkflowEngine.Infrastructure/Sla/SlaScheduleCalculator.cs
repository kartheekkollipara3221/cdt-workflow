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
