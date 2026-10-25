using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace BBH.CP.Atlas.Platform.Tests.Registry;

public sealed class RegistryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "atlas-config-" + Guid.NewGuid().ToString("N")[..8]);

    public RegistryTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "DEMO.app.json"), """
            {
              // comments and trailing commas are accepted (spec 11, 4.2)
              "Code": "demo",
              "Form": "DEMO.json",
              "AccessMode": "Enforce",
            }
            """);
        File.WriteAllText(Path.Combine(_folder, "DEMO.json"), """
            { "Name": "Demo", "Section": { "Name": "Demo", "Controls": [ { "Name": "A", "Label": "A", "Type": "text" } ] } }
            """);
        File.WriteAllText(Path.Combine(_folder, "OFF.app.json"), """{ "Code": "OFF", "Form": "DEMO.json" }""");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Manifests_load_from_the_config_folder_with_defaults()
    {
        AtlasConfigStore store = Store();

        AtlasAppManifest demo = Assert.Single(store.GetManifests(), x => x.Code == "DEMO");

        Assert.Equal(AtlasAccessMode.Enforce, demo.AccessMode);
        Assert.Equal(AtlasRuleMode.MostPermissive, demo.RuleMode);
        Assert.Equal(AtlasRuleDefault.ReadOnly, demo.RuleDefault);
        Assert.Equal("DEMO", store.FindManifest("demo")!.Code);
    }

    [Fact]
    public void Environment_settings_win_over_the_manifest()
    {
        AtlasConfigStore store = Store(new()
        {
            ["AppSettings:Modules:DEMO:AccessMode"] = "LogOnly",
            ["AppSettings:Modules:OFF:Enabled"] = "false"
        });

        Assert.Equal(AtlasAccessMode.LogOnly, store.FindManifest("DEMO")!.AccessMode);
        Assert.False(store.FindManifest("OFF")!.Enabled);
    }

    [Theory]
    [InlineData("flase")]
    [InlineData("0")]
    [InlineData("no")]
    public void An_Enabled_setting_that_is_not_valid_disables_the_app_and_is_reported(string value)
    {
        AtlasConfigStore store = Store(new() { ["AppSettings:Modules:DEMO:Enabled"] = value });

        AtlasAppManifest demo = store.FindManifest("DEMO")!;

        Assert.False(demo.Enabled);
        Assert.Contains(demo.SettingErrors, x => x.Contains("AppSettings:Modules:DEMO:Enabled", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Enfroce")]
    [InlineData("2")]
    [InlineData("1")]
    public void An_AccessMode_setting_that_is_not_valid_gives_Enforce_and_is_reported(string value)
    {
        AtlasConfigStore store = Store(new() { ["AppSettings:Modules:DEMO:AccessMode"] = value });

        AtlasAppManifest demo = store.FindManifest("DEMO")!;

        Assert.Equal(AtlasAccessMode.Enforce, demo.AccessMode);
        Assert.Contains(demo.SettingErrors, x => x.Contains("AccessMode", StringComparison.Ordinal));
    }

    [Fact]
    public void A_mode_setting_that_is_not_valid_keeps_the_manifest_value_and_is_reported()
    {
        AtlasConfigStore store = Store(new() { ["AppSettings:Modules:DEMO:RuleDefault"] = "Hiden", ["AppSettings:Modules:DEMO:Enabled"] = " True " });

        AtlasAppManifest demo = store.FindManifest("DEMO")!;

        Assert.Equal(AtlasRuleDefault.ReadOnly, demo.RuleDefault);
        Assert.True(demo.Enabled);
        Assert.Single(demo.SettingErrors);
    }

    [Fact]
    public void Each_form_request_gets_its_own_copy()
    {
        AtlasConfigStore store = Store();

        CPAtlasApplicationConfig first = store.GetForm("DEMO");
        first.Section!.Controls![0].Label = "changed";

        Assert.Equal("A", store.GetForm("DEMO").Section!.Controls![0].Label);
    }

    [Fact]
    public void A_changed_file_is_read_again()
    {
        AtlasConfigStore store = Store();
        Assert.Equal("Demo", store.GetForm("DEMO").Name);

        string path = Path.Combine(_folder, "DEMO.json");
        File.WriteAllText(path, """{ "Name": "Demo 2", "Section": { "Name": "Demo" } }""");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal("Demo 2", store.GetForm("DEMO").Name);
    }

    [Fact]
    public void A_form_file_outside_the_folder_is_refused()
    {
        File.WriteAllText(Path.Combine(_folder, "BAD.app.json"), """{ "Code": "BAD", "Form": "..\\..\\secret.json" }""");

        Assert.Throws<InvalidOperationException>(() => Store().GetForm("BAD"));
    }

    [Fact]
    public void Resolver_returns_404_for_unknown_and_disabled_apps_and_400_for_a_missing_feature()
    {
        ServiceCollection services = new();
        services.AddSingleton<IAtlasConfigStore>(Store(new() { ["AppSettings:Modules:OFF:Enabled"] = "false" }));
        services.AddScoped<IAtlasModuleResolver, AtlasModuleResolver>();
        services.AddAtlasModule<DemoModule>("DEMO");
        services.AddAtlasModule<DemoModule>("OFF");
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAtlasModuleResolver resolver = scope.ServiceProvider.GetRequiredService<IAtlasModuleResolver>();

        Assert.Equal("DEMO", resolver.Get<DemoModule>("demo", "form").AppCode);
        Assert.True(resolver.IsPlatformApp("DEMO"));
        Assert.False(resolver.IsPlatformApp("OFF"));
        Assert.False(resolver.IsPlatformApp("TTD"));
        Assert.Equal(404, Assert.Throws<AtlasProblemException>(() => resolver.Get<DemoModule>("XYZ", "form")).StatusCode);
        Assert.Equal(404, Assert.Throws<AtlasProblemException>(() => resolver.Get<DemoModule>("OFF", "form")).StatusCode);
        Assert.Equal(400, Assert.Throws<AtlasProblemException>(() => resolver.Get<IDisposable>("DEMO", "step form")).StatusCode);
        Assert.Equal(400, Assert.Throws<AtlasProblemException>(() => resolver.Get<DemoModule>("1BAD", "form")).StatusCode);
    }

    [Fact]
    public void AddAtlasModule_refuses_a_bad_code()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddAtlasModule<DemoModule>("2TTD"));
    }

    [Theory]
    [InlineData("TTD.json.txt")]
    [InlineData("SQMSO.json.txt")]
    [InlineData("SQMSB.json.txt")]
    [InlineData("AIN.json.txt")]
    [InlineData("CDT.json.txt")]
    public void All_current_form_files_load_with_the_platform_loader(string fileName)
    {
        string json = File.ReadAllText(TestPaths.TargetConfigFile(fileName));

        CPAtlasApplicationConfig config = AtlasJson.Deserialize<CPAtlasApplicationConfig>(json, fileName);

        Assert.False(string.IsNullOrWhiteSpace(config.Name));
        Assert.True(config.Section != null || config.Steps is { Count: > 0 });
    }

    [Fact]
    public void The_current_dashboard_file_loads_with_the_platform_loader()
    {
        string json = File.ReadAllText(TestPaths.TargetConfigFile("CPAtlasDashboardConfig.json.txt"));

        CPAtlasDashboardConfig config = AtlasJson.Deserialize<CPAtlasDashboardConfig>(json, "CPAtlasDashboardConfig.json");

        Assert.Contains(config.Dashboards!, x => x.Code == BBH.CP.Atlas.Data.Enumerations.CPApplicationCode.TTD);
    }

    [Fact]
    public void The_TTDV2_manifest_in_the_App_project_is_valid()
    {
        string folder = Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");
        AtlasConfigStore store = new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AppSettings:ConfigFolder"] = folder }).Build(), new TestHost(folder));

        AtlasAppManifest manifest = store.FindManifest("TTDV2")!;

        Assert.Equal("TTDV2.General", manifest.Table);
        Assert.Equal("FORM", manifest.FormType);
        Assert.Equal(AtlasAuditMode.Server, manifest.AuditMode);
    }

    [Fact]
    public void The_platform_settings_come_from_the_configuration_given_to_AddAtlasPlatform()
    {
        // The target replaces the container IConfiguration with its own root (review HO-01).
        IConfiguration container = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:ConfigFolder"] = "WrongFolder",
            ["AppSettings:Modules:DEMO:AccessMode"] = "LogOnly"
        }).Build();
        IConfiguration given = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AppSettings:ConfigFolder"] = _folder
        }).Build();
        ServiceCollection services = new();
        services.AddSingleton(container);
        services.AddSingleton<IHostEnvironment>(new TestHost(_folder));
        services.AddAtlasPlatform(given);

        using ServiceProvider provider = services.BuildServiceProvider();
        IAtlasConfigStore store = provider.GetRequiredService<IAtlasConfigStore>();

        Assert.Equal(Path.GetFullPath(_folder), store.ConfigFolder);
        Assert.Equal(AtlasAccessMode.Enforce, store.FindManifest("DEMO")!.AccessMode); // the container override is not used
        Assert.Same(given, provider.GetRequiredService<AtlasPlatformConfiguration>().Configuration);
    }

    private AtlasConfigStore Store(Dictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> values = new(settings ?? []) { ["AppSettings:ConfigFolder"] = _folder };
        return new AtlasConfigStore(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new TestHost(_folder));
    }

    public sealed class DemoModule : IAtlasModule
    {
        public string AppCode { get; set; } = "";
    }

    private sealed class TestHost(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
