using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

public sealed class SlaScheduleCalculatorTests
{
    private readonly SlaScheduleCalculator calculator = new();

    [Fact]
    public void ApplicationDateUsesNineAmEasternAndNextCalendarDayOverdue()
    {
        var result = calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 20),
            new TimeOnly(9, 0),
            "America/New_York",
            overdueStartOffsetDays: 1,
            recurrenceIntervalDays: 1,
            now: DateTimeOffset.Parse("2030-01-19T12:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2030-01-20T14:00:00Z"), result.DueReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-01-21T14:00:00Z"), result.OverdueAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-01-20T14:00:00Z"), result.NextReminderAtUtc);
        Assert.Equal("DueToday", result.NextReminderKind);
    }

    [Fact]
    public void ApplicationDateConvertsSpringDaylightSavingDatesIndependently()
    {
        var result = calculator.CalculateApplicationDate(
            new DateOnly(2030, 3, 9),
            new TimeOnly(9, 0),
            "America/New_York",
            overdueStartOffsetDays: 1,
            recurrenceIntervalDays: 1,
            now: DateTimeOffset.Parse("2030-03-09T12:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2030-03-09T14:00:00Z"), result.DueReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-03-10T13:00:00Z"), result.OverdueAtUtc);
    }

    [Fact]
    public void ApplicationDateConvertsFallDaylightSavingDatesIndependently()
    {
        var result = calculator.CalculateApplicationDate(
            new DateOnly(2030, 11, 2),
            new TimeOnly(9, 0),
            "America/New_York",
            overdueStartOffsetDays: 1,
            recurrenceIntervalDays: 1,
            now: DateTimeOffset.Parse("2030-11-02T12:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2030-11-02T13:00:00Z"), result.DueReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-11-03T14:00:00Z"), result.OverdueAtUtc);
    }

    [Fact]
    public void DurationUsesConfiguredMinutesFromTaskCreation()
    {
        var result = calculator.CalculateDuration(
            DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
            durationMinutes: 120,
            now: DateTimeOffset.Parse("2030-01-20T14:30:00Z"));

        Assert.Null(result.TargetDate);
        Assert.Null(result.DueReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-01-20T16:00:00Z"), result.OverdueAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-01-20T16:00:00Z"), result.NextReminderAtUtc);
        Assert.Equal("Overdue", result.NextReminderKind);
    }

    [Fact]
    public void DueTodayCatchUpQueuesOnlyTheCurrentInstant()
    {
        var result = calculator.CalculateApplicationDate(
            new DateOnly(2030, 3, 10),
            new TimeOnly(9, 0),
            "America/New_York",
            overdueStartOffsetDays: 1,
            recurrenceIntervalDays: 1,
            now: DateTimeOffset.Parse("2030-03-10T15:00:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2030-03-10T13:00:00Z"), result.DueReminderAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-03-11T13:00:00Z"), result.OverdueAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2030-03-10T15:00:00Z"), result.NextReminderAtUtc);
        Assert.Equal("DueToday", result.NextReminderKind);
    }

    [Fact]
    public void OverdueCatchUpQueuesOnlyTheCurrentInstant()
    {
        var now = DateTimeOffset.Parse("2030-01-25T18:30:00Z");

        var result = calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 20),
            new TimeOnly(9, 0),
            "America/New_York",
            overdueStartOffsetDays: 1,
            recurrenceIntervalDays: 1,
            now);

        Assert.Equal(now, result.NextReminderAtUtc);
        Assert.Equal("Overdue", result.NextReminderKind);
    }

    [Fact]
    public void NextRecurringOccurrenceUsesTheNextLocalCalendarDate()
    {
        var next = calculator.CalculateNextRecurringOccurrence(
            new DateOnly(2030, 11, 2),
            new TimeOnly(9, 0),
            "America/New_York",
            recurrenceIntervalDays: 1);

        Assert.Equal(DateTimeOffset.Parse("2030-11-03T14:00:00Z"), next);
    }
}
