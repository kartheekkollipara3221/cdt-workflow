using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BBH.CP.Atlas.Platform.Tests.Api;

/// <summary>Spec 12, section 8: --atlas-check returns 0 for TTDV2 and 1 for a manifest with no registration line.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class AtlasCheckTests(SqlServerDatabaseFixture database) : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "atlas-check-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly string RealConfig = Path.Combine(TestPaths.AtlasPlatformRoot(), "src", "BBH.CP.Atlas.App", "Config");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task The_TTDV2_files_pass_with_exit_code_0()
    {
        (int code, string report) = await CheckAsync(RealConfig);

        Assert.True(code == 0, report);
        Assert.Contains("0 error(s)", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_with_no_registration_line_gives_exit_code_1()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "PBSP.app.json"), """{ "Code": "PBSP", "Form": "TTDV2.json", "Table": "TTDV2.General" }""");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR PBSP: has a manifest but no registration line", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_form_field_or_tab_column_that_is_not_in_the_table_is_an_error()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Field\": \"Category\"", "\"Field\": \"Kategory\"", StringComparison.Ordinal));
        string dashboard = Path.Combine(_folder, "TTDV2.Dashboard.json");
        File.WriteAllText(dashboard, File.ReadAllText(dashboard).Replace("\"Name\": \"AccountName\"", "\"Name\": \"ClientName\"", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("names the column 'Kategory'", report, StringComparison.Ordinal);
        Assert.Contains("names the column 'ClientName'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_rule_role_is_a_warning_only()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Delete\": [ \"Owner\" ]", "\"Delete\": [ \"Owner\", \"Auditor\" ]", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: the role 'Auditor'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_form_without_a_Permissions_block_is_an_error()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Permissions\":", "\"PermissionsOff\":", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: the form has no Permissions block", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_setting_that_is_not_valid_is_an_error()
    {
        (int code, string report) = await CheckAsync(RealConfig, new() { ["AppSettings:Modules:TTDV2:AccessMode"] = "Enfroce" });

        Assert.Equal(1, code);
        Assert.Contains("ERROR TTDV2: the setting AppSettings:Modules:TTDV2:AccessMode = 'Enfroce' is not valid", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_table_with_an_unsupported_column_or_key_type_is_an_error()
    {
        string name = "BadTypes" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"CREATE TABLE TTDV2.{name} (ID bigint IDENTITY PRIMARY KEY, Notes xml NULL, Shift time NULL);");
        try
        {
            CopyRealConfig();
            string manifest = Path.Combine(_folder, "TTDV2.app.json");
            File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("\"TTDV2.AuditLog\"", $"\"TTDV2.{name}\"", StringComparison.Ordinal));

            (int code, string report) = await CheckAsync(_folder);

            Assert.Equal(1, code);
            Assert.Contains($"the ID column must be an int IDENTITY column (it is bigint)", report, StringComparison.Ordinal);
            Assert.Contains("the column 'Notes' has the type 'xml'", report, StringComparison.Ordinal);
            Assert.Contains("the column 'Shift' has the type 'time'", report, StringComparison.Ordinal);
        }
        finally
        {
            await database.ScalarAsync($"DROP TABLE TTDV2.{name};");
        }
    }

    [Fact]
    public async Task A_manifest_file_that_cannot_be_read_is_an_error_and_the_other_apps_are_still_checked()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "BROKEN.app.json"), """{ "Code": """);

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("ERROR *: BROKEN.app.json cannot be read", report, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR TTDV2", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_control_type_with_no_client_renderer_is_a_warning()
    {
        CopyRealConfig();
        string form = Path.Combine(_folder, "TTDV2.json");
        File.WriteAllText(form, File.ReadAllText(form).Replace("\"Type\": \"textarea\"", "\"Type\": \"filepicker\"", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: the control 'WorkDescription' has the type 'filepicker'", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_misspelled_property_is_a_warning_with_its_path()
    {
        CopyRealConfig();
        ReplaceInForm("\"Validator\": { \"Required\": true }", "\"Validator\": { \"Requried\": true }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.True(code == 0, report);
        Assert.Contains("WARNING TTDV2: TTDV2.json: 'Section.Controls[", report, StringComparison.Ordinal);
        Assert.Contains(".Validator.Requried' is not a property of CPValidator", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_effect_target_that_is_not_a_control_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"Clear\": [ \"AccountNumber\", \"AccountName\" ]", "\"Clear\": [ \"AccountNumber\", \"AcountName\" ]");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("names 'AcountName', which is not a control or field of the form", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_default_token_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"TaxPreparer\": \"{User.LanID}\"", "\"TaxPreparer\": \"{Todya}\"");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the default token '{Todya}' of 'TaxPreparer' is not known", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pattern_that_does_not_compile_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"Validator\": { \"Required\": true }", "\"Validator\": { \"Required\": true, \"Pattern\": \"[0-9\" }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("is not a valid regular expression", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_newer_SchemaVersion_is_an_error()
    {
        CopyRealConfig();
        string manifest = Path.Combine(_folder, "TTDV2.app.json");
        File.WriteAllText(manifest, File.ReadAllText(manifest).Replace("{", "{ \"SchemaVersion\": 2,", StringComparison.Ordinal));

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("the manifest SchemaVersion 2 is newer than this platform", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_SoftDelete_field_other_than_IsDeleted_is_an_error()
    {
        CopyRealConfig();
        ReplaceInForm("\"SoftDelete\": { \"Field\": \"IsDeleted\" }", "\"SoftDelete\": { \"Field\": \"Category\" }");

        (int code, string report) = await CheckAsync(_folder);

        Assert.Equal(1, code);
        Assert.Contains("SoftDelete names 'Category'; the platform reads only IsDeleted", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_not_null_bit_column_with_a_default_that_nothing_sets_is_a_warning()
    {
        string column = "Flag" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"ALTER TABLE TTDV2.General ADD {column} bit NOT NULL CONSTRAINT DF_{column} DEFAULT 1;");
        try
        {
            (int code, string report) = await CheckAsync(RealConfig);

            Assert.True(code == 0, report);
            Assert.Contains($"WARNING TTDV2: the column '{column}' of TTDV2.General is NOT NULL bit with a default", report, StringComparison.Ordinal);
        }
        finally
        {
            await database.ScalarAsync($"ALTER TABLE TTDV2.General DROP CONSTRAINT DF_{column}; ALTER TABLE TTDV2.General DROP COLUMN {column};");
        }
    }

    [Fact]
    public async Task A_shared_AuditTable_without_AuditRefTypes_is_a_warning()
    {
        CopyRealConfig();
        File.WriteAllText(Path.Combine(_folder, "PBSP.app.json"),
            """{ "Code": "PBSP", "Form": "TTDV2.json", "Table": "TTDV2.General", "AuditTable": "TTDV2.AuditLog", "AuditRefTypes": [ "PBSP" ] }""");

        (_, string report) = await CheckAsync(_folder);

        Assert.Contains("WARNING TTDV2: the AuditTable TTDV2.AuditLog is shared with PBSP", report, StringComparison.Ordinal);
        Assert.DoesNotContain("WARNING PBSP: the AuditTable", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_host_loads_the_platform_when_program_cs_does_not_call_StartAtlasPlatformAsync()
    {
        // Review HO-06: the hosted service of AddAtlasPlatform runs the start-up; a second call does nothing.
        await using WebApplication app = StandInHost.Create(["--environment=Development", "--urls=http://127.0.0.1:0"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = database.ConnectionString,
                ["AppSettings:ConfigFolder"] = RealConfig
            }));
        AtlasTableRegistry registry = app.Services.GetRequiredService<AtlasTableRegistry>();
        Assert.False(registry.IsLoaded);

        await app.StartAsync();
        try
        {
            Assert.True(registry.IsLoaded);
            Assert.True(registry.TryGet("TTDV2.General", out _));
            await app.Services.StartAtlasPlatformAsync(); // the program.cs line of INTEGRATION.md stays valid
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private void ReplaceInForm(string oldText, string newText)
    {
        string form = Path.Combine(_folder, "TTDV2.json");
        string text = File.ReadAllText(form);
        Assert.Contains(oldText, text, StringComparison.Ordinal);
        File.WriteAllText(form, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    private void CopyRealConfig()
    {
        Directory.CreateDirectory(_folder);
        foreach (string file in Directory.GetFiles(RealConfig, "*.json"))
            File.Copy(file, Path.Combine(_folder, Path.GetFileName(file)));
    }

    private async Task<(int Code, string Report)> CheckAsync(string configFolder, Dictionary<string, string?>? settings = null)
    {
        Dictionary<string, string?> values = new(settings ?? [])
        {
            ["ConnectionStrings:DBConnection"] = database.ConnectionString,
            ["AppSettings:ConfigFolder"] = configFolder
        };
        await using WebApplication app = StandInHost.Create(["--environment=Development"], builder =>
            builder.Configuration.AddInMemoryCollection(values));
        using StringWriter output = new();
        int code = await app.Services.RunAtlasCheckAsync(output);
        return (code, output.ToString());
    }
}
