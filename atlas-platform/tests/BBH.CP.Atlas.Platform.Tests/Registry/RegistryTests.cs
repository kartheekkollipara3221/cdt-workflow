using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
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
    public void A_form_in_a_sibling_folder_with_the_same_name_prefix_is_outside_the_config_folder()
    {
        // Review HO-09: "..\<folder>Old\DEMO.json" starts with the folder text but is not in the folder.
        string sibling = _folder + "Old";
        Directory.CreateDirectory(sibling);
        try
        {
            File.Copy(Path.Combine(_folder, "DEMO.json"), Path.Combine(sibling, "DEMO.json"));
            string form = Path.Combine("..", Path.GetFileName(sibling), "DEMO.json").Replace("\\", "\\\\", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(_folder, "SIB.app.json"), $$"""{ "Code": "SIB", "Form": "{{form}}" }""");

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Store().GetForm("SIB"));

            Assert.Contains("outside the config folder", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(sibling, recursive: true);
        }
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
    public void ToLegacy_gives_404_for_a_platform_app_that_is_not_available_and_the_enum_for_a_current_app()
    {
        // Review 3 RV-12: at the target a disabled platform app falls through to the current if chain.
        AtlasConfigStore store = Store(new() { ["AppSettings:Modules:OFF:Enabled"] = "false" });

        Assert.Equal(BBH.CP.Atlas.Data.Enumerations.CPApplicationCode.TTD, AtlasAppCode.ToLegacy("ttd", store));
        Assert.Equal(BBH.CP.Atlas.Data.Enumerations.CPApplicationCode.TTD, AtlasAppCode.ToLegacy("13", store));
        Assert.Equal(404, Assert.Throws<AtlasProblemException>(() => AtlasAppCode.ToLegacy("off", store)).StatusCode);
        Assert.Equal(400, Assert.Throws<AtlasProblemException>(() => AtlasAppCode.ToLegacy(" ", store)).StatusCode);
        // An unknown code gives the error of Enum.Parse, as before (no change for the current callers).
        Assert.Throws<ArgumentException>(() => AtlasAppCode.ToLegacy("XYZ", store));
        Assert.Throws<ArgumentException>(() => AtlasAppCode.ToLegacy("OFF", null));
    }

    [Fact]
    public void A_second_manifest_with_the_same_Code_is_a_config_error()
    {
        // Review 3 RV-L09: a copied file left with the same Code was ignored with no message.
        File.WriteAllText(Path.Combine(_folder, "DEMO2.app.json"), """{ "Code": "DEMO", "Form": "DEMO.json" }""");

        AtlasConfigStore store = Store();

        Assert.Equal("DEMO.app.json", Path.GetFileName(store.FindManifest("DEMO")!.SourceFile));
        Assert.Contains(store.ConfigErrors, x => x.Contains("DEMO2.app.json has the Code DEMO", StringComparison.Ordinal));
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

    [Fact]
    public void The_TimeProvider_of_the_host_is_kept()
    {
        TimeProvider fixedTime = new FixedTime();
        ServiceCollection services = new();
        services.AddSingleton(fixedTime);
        services.AddAtlasPlatform(new ConfigurationBuilder().Build());

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(fixedTime, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void A_second_registration_line_for_the_same_code_is_refused_with_a_clear_message()
    {
        ServiceCollection services = new();
        services.AddAtlasModule<DemoModule>("DEMO");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => services.AddAtlasModule<DemoModule>("demo"));

        Assert.Contains("'DEMO' is registered twice", error.Message, StringComparison.Ordinal);
    }

    private sealed class FixedTime : TimeProvider
    {
    }

    [Fact]
    public void The_manifests_are_parsed_once_while_the_folder_does_not_change()
    {
        AtlasConfigStore store = Store();

        AtlasAppManifest first = store.FindManifest("DEMO")!;

        Assert.Same(first, store.FindManifest("DEMO"));
        Assert.Same(first, store.GetManifests().Single(x => x.Code == "DEMO"));
    }

    [Fact]
    public void A_changed_manifest_is_seen_after_the_refresh_time()
    {
        ManualTime time = new(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        AtlasConfigStore store = Store(new() { ["AppSettings:ConfigRefreshSeconds"] = "60" }, time);
        Assert.Equal(AtlasAccessMode.Enforce, store.FindManifest("DEMO")!.AccessMode);

        string path = Path.Combine(_folder, "DEMO.app.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Enforce\"", "\"LogOnly\"", StringComparison.Ordinal));
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        time.Now = time.Now.AddSeconds(30);
        Assert.Equal(AtlasAccessMode.Enforce, store.FindManifest("DEMO")!.AccessMode);   // not checked yet
        time.Now = time.Now.AddSeconds(31);
        Assert.Equal(AtlasAccessMode.LogOnly, store.FindManifest("DEMO")!.AccessMode);
    }

    [Fact]
    public void A_manifest_file_that_cannot_be_read_does_not_stop_the_other_apps()
    {
        File.WriteAllText(Path.Combine(_folder, "BROKEN.app.json"), """{ "Code": "BROKEN", "Form": """);
        AtlasConfigStore store = Store();

        Assert.NotNull(store.FindManifest("DEMO"));
        Assert.Null(store.FindManifest("BROKEN"));
        Assert.Contains(store.ConfigErrors, x => x.StartsWith("BROKEN.app.json cannot be read", StringComparison.Ordinal)
            && x.EndsWith("The app of this file is not available.", StringComparison.Ordinal));
    }

    [Fact]
    public void A_manifest_file_that_becomes_unreadable_keeps_its_last_good_version()
    {
        AtlasConfigStore store = Store();
        Assert.Equal(AtlasAccessMode.Enforce, store.FindManifest("DEMO")!.AccessMode);

        string path = Path.Combine(_folder, "DEMO.app.json");
        File.WriteAllText(path, """{ "Code": "demo", "AccessMode": "LogOnly", """); // a copy that is not complete
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(AtlasAccessMode.Enforce, store.FindManifest("DEMO")!.AccessMode);
        Assert.Contains(store.ConfigErrors, x => x.Contains("The last good version (DEMO) stays in use.", StringComparison.Ordinal));
        Assert.NotNull(store.FindManifest("OFF"));
    }

    [Fact]
    public void An_app_whose_table_was_not_loaded_at_start_up_is_not_available()
    {
        File.WriteAllText(Path.Combine(_folder, "LATE.app.json"), """{ "Code": "LATE", "Form": "DEMO.json", "Table": "dbo.Late", "AuditMode": "Client" }""");
        AtlasTableRegistry tables = new();
        ServiceCollection services = new();
        services.AddSingleton<IAtlasConfigStore>(Store());
        services.AddSingleton(tables);
        services.AddScoped<IAtlasModuleResolver, AtlasModuleResolver>();
        services.AddAtlasModule<DemoModule>("LATE");
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAtlasModuleResolver resolver = scope.ServiceProvider.GetRequiredService<IAtlasModuleResolver>();

        Assert.False(resolver.IsPlatformApp("LATE"));
        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() => resolver.Get<DemoModule>("LATE", "form"));
        Assert.Equal(404, error.StatusCode);
        Assert.Contains("the table 'dbo.Late' was not loaded at start-up", error.Message, StringComparison.Ordinal);

        tables.Add(new AtlasTable("dbo", "Late", [new AtlasColumn("ID", "int", false, null, 10, 0, true, false)]));
        Assert.True(resolver.IsPlatformApp("LATE"));
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private AtlasConfigStore Store(Dictionary<string, string?>? settings = null, TimeProvider? time = null)
    {
        Dictionary<string, string?> values = new(settings ?? []) { ["AppSettings:ConfigFolder"] = _folder };
        values.TryAdd("AppSettings:ConfigRefreshSeconds", "0");
        return new AtlasConfigStore(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new TestHost(_folder), time);
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
