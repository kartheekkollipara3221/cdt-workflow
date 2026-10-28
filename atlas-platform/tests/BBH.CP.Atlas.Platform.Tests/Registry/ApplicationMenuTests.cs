using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Registry;

namespace BBH.CP.Atlas.Platform.Tests.Registry;

/// <summary>Review AR-02: the v1/Application menu is shared with the current apps.</summary>
public sealed class ApplicationMenuTests
{
    [Fact]
    public void An_error_in_the_menu_step_gives_the_rows_unchanged_and_is_logged()
    {
        List<Dictionary<string, object>> rows =
        [
            new() { ["ID"] = 1, ["Code"] = "TTD", ["Name"] = "Tax Time" },
            new() { ["ID"] = 2, ["Code"] = "TTDV2", ["Name"] = "Tax Time Database" }
        ];
        ListLogger<object> logger = new();

        List<Dictionary<string, object>> result = AtlasApplicationMenu.Enrich(rows, new ThrowingResolver(), new EmptyStore(), logger);

        Assert.Same(rows, result);
        Assert.Equal(2, result.Count);
        Assert.All(result, row => Assert.False(row.ContainsKey("FormType")));
        Assert.Contains(logger.Entries, x => x.EventId.Id == 41024 && x.Message.Contains("v1/Application", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_logger_an_error_still_gives_the_rows_unchanged()
    {
        List<Dictionary<string, object>> rows = [new() { ["ID"] = 1, ["Code"] = "CDT" }, new() { ["ID"] = 2, ["Code"] = "TTDV2" }];

        Assert.Same(rows, AtlasApplicationMenu.Enrich(rows, new ThrowingResolver(), new EmptyStore()));
    }

    private sealed class ThrowingResolver : IAtlasModuleResolver
    {
        public IReadOnlyList<AtlasModuleRegistration> Registrations => [];

        public bool IsPlatformApp(string? appCode) =>
            appCode == "TTDV2" ? throw new InvalidOperationException("Simulated platform defect.") : false;

        public TService Get<TService>(string appCode, string feature) where TService : class => throw new NotSupportedException();
    }

    private sealed class EmptyStore : IAtlasConfigStore
    {
        public string ConfigFolder => "";

        public IReadOnlyList<AtlasAppManifest> GetManifests() => [];

        public AtlasAppManifest? FindManifest(string appCode) => null;

        public CPAtlasApplicationConfig GetForm(string appCode) => throw new NotSupportedException();

        public CPAtlasDashboard? GetDashboard(string appCode) => null;
    }
}
