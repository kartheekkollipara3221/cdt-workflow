using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Options;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>The platform date providers Years and WeekEndings (review FABLE F03). No database.</summary>
public sealed class DateOptionProviderTests
{
    // Wednesday 21-Oct-2026.
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

    private static AtlasOptionRequest Request(string provider, Dictionary<string, string>? parameters) =>
        new("TTDV2", new CPApplicationControl { Name = "X", Label = "X", Type = "select" },
            new CPOptionsSource { Provider = provider, Parameters = parameters });

    [Fact]
    public void The_providers_read_their_Parameters()
    {
        IReadOnlyList<CPKeyValue> years = new YearsOptionProvider(Time).GetOptions(Request("Years", new() { ["Before"] = "1", ["After"] = "0" }));
        IReadOnlyList<CPKeyValue> weeks = new WeekEndingsOptionProvider(Time).GetOptions(Request("WeekEndings", new() { ["Count"] = "2", ["DayOfWeek"] = "friday" }));

        Assert.Equal(["2026", "2025"], years.Select(x => x.Value));
        Assert.Equal(["2026-10-16", "2026-10-23"], weeks.Select(x => x.Value));
    }

    [Fact]
    public void With_no_Parameters_the_lists_are_the_TTD_lists()
    {
        IReadOnlyList<CPKeyValue> years = new YearsOptionProvider(Time).GetOptions(Request("Years", null));
        IReadOnlyList<CPKeyValue> weeks = new WeekEndingsOptionProvider(Time).GetOptions(Request("WeekEndings", new() { ["Count"] = "x" }));

        Assert.Equal(["2027", "2026", "2025", "2024", "2023"], years.Select(x => x.Value));
        Assert.Equal(["2026-10-18", "2026-10-25", "2026-11-01", "2026-11-08"], weeks.Select(x => x.Value));
    }
}
