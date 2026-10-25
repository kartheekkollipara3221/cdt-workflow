using BBH.CP.Atlas.App.Platform;
using BBH.CP.Atlas.App.StandIn;
using BBH.CP.Atlas.Platform.Tests.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

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
