using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

// E2 (separate due time, skip of a missed due reminder) and E3 (first overdue on
// the next local day) calculations, plus compatibility with the original overload.
public sealed class CdtV2SlaCalculatorTests
{
    private const string NewYork = "America/New_York";
    private static readonly SlaScheduleCalculator Calculator = new();
    private static readonly TimeSpan Est = TimeSpan.FromHours(-5);

    [Fact]
    public void SameDayBeforeTwoPmUsesTheDueTimeAndTheOverdueTimeSeparately()
    {
        var result = Calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 15), new TimeOnly(8, 0), new TimeOnly(14, 0), NewYork, 1, 1,
            new DateTimeOffset(2030, 1, 15, 10, 0, 0, Est), skipMissedDueReminder: true);

        Assert.Equal(new DateTimeOffset(2030, 1, 15, 14, 0, 0, Est), result.DueReminderAtUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 16, 8, 0, 0, Est), result.OverdueAtUtc);
        Assert.Equal(result.DueReminderAtUtc, result.NextReminderAtUtc);
        Assert.Equal("DueToday", result.NextReminderKind);
    }

    [Fact]
    public void SameDayAfterTwoPmWithSkipGoesStraightToOverdue()
    {
        var result = Calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 15), new TimeOnly(8, 0), new TimeOnly(14, 0), NewYork, 1, 1,
            new DateTimeOffset(2030, 1, 15, 15, 0, 0, Est), skipMissedDueReminder: true);

        Assert.Null(result.DueReminderAtUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 16, 8, 0, 0, Est), result.NextReminderAtUtc);
        Assert.Equal("Overdue", result.NextReminderKind);
    }

    [Fact]
    public void SameDayAfterTwoPmWithoutSkipKeepsTheImmediateDueReminder()
    {
        var now = new DateTimeOffset(2030, 1, 15, 15, 0, 0, Est);
        var result = Calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 15), new TimeOnly(8, 0), new TimeOnly(14, 0), NewYork, 1, 1, now, skipMissedDueReminder: false);

        Assert.Equal(now, result.NextReminderAtUtc);
        Assert.Equal("DueToday", result.NextReminderKind);
    }

    [Theory]
    [InlineData(2030, 1, 15, 10)]
    [InlineData(2030, 1, 15, 15)]
    [InlineData(2030, 1, 17, 9)]
    [InlineData(2030, 3, 10, 7)]
    public void NewOverloadEqualsTheOriginalWhenBothTimesAreTheSame(int year, int month, int day, int hour)
    {
        var now = new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero);
        var original = Calculator.CalculateApplicationDate(new DateOnly(2030, 1, 15), new TimeOnly(9, 0), NewYork, 1, 1, now);
        var extended = Calculator.CalculateApplicationDate(
            new DateOnly(2030, 1, 15), new TimeOnly(9, 0), new TimeOnly(9, 0), NewYork, 1, 1, now, skipMissedDueReminder: false);

        Assert.Equal(original, extended);
    }

    [Fact]
    public void NextLocalDayStartsAtTheReminderTimeAfterTheCreationDate()
    {
        var created = new DateTimeOffset(2030, 1, 14, 10, 30, 0, Est);
        var result = Calculator.CalculateNextLocalDay(created, new TimeOnly(9, 0), NewYork, created);

        Assert.Null(result.DueReminderAtUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 9, 0, 0, Est), result.OverdueAtUtc);
        Assert.Equal(result.OverdueAtUtc, result.NextReminderAtUtc);
        Assert.Equal("Overdue", result.NextReminderKind);
    }

    [Fact]
    public void NextLocalDayUsesTheLocalCreationDateNotTheUtcDate()
    {
        // 23:30 New York time is already the next day in UTC.
        var created = new DateTimeOffset(2030, 1, 14, 23, 30, 0, Est);
        var result = Calculator.CalculateNextLocalDay(created, new TimeOnly(9, 0), NewYork, created);

        Assert.Equal(new DateTimeOffset(2030, 1, 15, 9, 0, 0, Est), result.OverdueAtUtc);
    }

    [Fact]
    public void NextLocalDayAfterTheOverdueTimeIsEligibleImmediately()
    {
        var created = new DateTimeOffset(2030, 1, 14, 10, 30, 0, Est);
        var now = new DateTimeOffset(2030, 1, 15, 11, 0, 0, Est);
        var result = Calculator.CalculateNextLocalDay(created, new TimeOnly(9, 0), NewYork, now);

        Assert.Equal(now, result.NextReminderAtUtc);
    }
}
