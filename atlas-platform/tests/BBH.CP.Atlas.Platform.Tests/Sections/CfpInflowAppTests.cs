using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Options;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>
/// The ClientInflows app (CFPINFLOW, module 25) with the real app files of the Config folder on the release P2 tables:
/// the save, the dashboard menus and the Add button (build step 1); the AIN account list, the data-only target RMCC,
/// the effects rule and the saved option rule (build step 2). Own database (the P2 scripts change tables).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CfpInflowAppTests : IAsyncLifetime, IDisposable
{
    private const string App = "CFPINFLOW";
    private const string Writer = "c.write";
    private const string Reader = "c.read";
    private static readonly IFormFileCollection NoFiles = new FormFileCollection();

    private readonly SqlServerDatabaseFixture _db = new();
    private readonly AtlasTableRegistry _registry = new();
    private SectionHarness _harness = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        await _db.SeedUserAsync(Writer, "Wendy Writer", App, "Write");
        await _db.SeedUserAsync(Reader, "Rob Reader", App, "Read");
        await _db.ScalarAsync("UPDATE AIN.AccountInventory SET RMCC = 'Casey Copy;Riley Manager' WHERE AccountNumber = '700100';");
        await using (SqlConnection connection = new(_db.ConnectionString))
            await new AtlasColumnMetadataReader().LoadAsync(connection, _registry, ["CFP.Inflow", "CFP.AuditLog", "AIN.AccountInventory"]);
        _harness = new SectionHarness(_db);
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    public void Dispose() => _harness?.Dispose();

    // ---------- Build step 1 ----------

    [Fact]
    public async Task A_Write_user_saves_a_new_record_with_the_default_and_the_account_values()
    {
        CPAtlasPage result = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);

        AssertSaved(result);
        Assert.Equal("700100|Equity|1250000.5000|2026-10-20|c.write|1|c.write|0", await _db.ScalarAsync($"""
            SELECT CONCAT_WS('|', AccountNumber, InvestmentType, Amount, CONVERT(varchar(10), InflowDate, 23),
                             FlowSubmittedBy, CAST(FlowConfirm AS int), CreatedBy, CAST(ISNULL(IsDeleted, 0) AS int))
            FROM CFP.Inflow WHERE ID = {result.ID}
            """));
        Assert.Equal(1, await _db.ScalarAsync($"SELECT COUNT(*) FROM CFP.AuditLog WHERE ReferenceID = {result.ID}"));
    }

    [Fact]
    public void A_Read_user_cannot_make_a_record()
    {
        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() => Svc(Reader, CfpRoles.Read).SavePage(NewInput(), NoFiles));

        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void A_Read_user_gets_Edit_and_History_in_the_row_menu_but_no_Delete_and_no_Add_New()
    {
        CPAtlasDashboard read = Svc(Reader, CfpRoles.Read).LoadDashboard();

        foreach (CPAtlasTab tab in read.Tabs!)
        {
            Assert.True(Menu(tab, "Edit").Visible);
            Assert.True(Menu(tab, "History").Visible);
            Assert.False(Menu(tab, "Delete").Visible);
        }
        Assert.False(read.Buttons!.Single(x => x.Name == "Add").Visible);

        CPAtlasDashboard write = Svc(Writer, CfpRoles.Write).LoadDashboard();
        Assert.All(write.Tabs!, tab => Assert.True(Menu(tab, "Delete").Visible));
        Assert.True(write.Buttons!.Single(x => x.Name == "Add").Visible);
    }

    [Fact]
    public async Task The_pipeline_tab_lists_only_the_records_in_state_Submitted()
    {
        object? submitted = await _db.ScalarAsync("""
            INSERT INTO CFP.Inflow (AccountNumber, Amount, WorkflowState, Status, CreatedBy, CreatedDate, IsDeleted)
            OUTPUT INSERTED.ID VALUES ('700100', 1250000.5, 'Submitted', 'Submitted', 'c.write', '2026-10-01', 0);
            """);
        object? received = await _db.ScalarAsync("""
            INSERT INTO CFP.Inflow (AccountNumber, Amount, WorkflowState, Status, CreatedBy, CreatedDate, IsDeleted)
            OUTPUT INSERTED.ID VALUES ('700200', 10, 'FundsReceived', 'Funds Received', 'c.write', '2026-10-02', NULL);
            """);

        CPAtlasDashboard dashboard = Svc(Reader, CfpRoles.Read).LoadDashboard();

        List<int> pipeline = Ids(dashboard, "InflowPipeline");
        Assert.Contains(Convert.ToInt32(submitted), pipeline);
        Assert.DoesNotContain(Convert.ToInt32(received), pipeline);
        Assert.Contains(Convert.ToInt32(received), Ids(dashboard, "Inflows")); // IsDeleted NULL means not deleted

        Dictionary<string, object> row = dashboard.Tabs!.Single(x => x.ID == "InflowPipeline").KeyValues!
            .Single(x => Convert.ToInt32(x["ID"]) == Convert.ToInt32(submitted));
        Assert.Equal(1250000.5m, Convert.ToDecimal(row["Amount"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.StartsWith("2033-10-01", Convert.ToString(row["DestructionDate"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_Read_user_opens_a_record_read_only()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);

        CPApplicationSection section = Svc(Reader, CfpRoles.Read).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });

        IEnumerable<CPApplicationControl> controls = section.Controls!.SelectMany(x => x.ChildControls ?? []);
        Assert.All(controls, x => Assert.True(x.Disabled, x.Name));
        Assert.Equal("Equity", controls.Single(x => x.Name == "InvestmentType").Value);
        Assert.Equal(1, await _db.ScalarAsync($"SELECT COUNT(*) FROM CFP.Inflow WHERE ID = {saved.ID}"));
    }

    // ---------- Build step 2 ----------

    [Fact]
    public void The_account_list_has_the_not_deleted_AIN_rows_with_the_label_and_the_Data_columns()
    {
        CPApplicationSection section = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = 0 });

        List<CPKeyValue> options = Control(section, "AccountNumber").Options!;
        Assert.Equal(["700100", "700200", "700300", "700400"], options.Select(x => x.Value)); // 700500 is deleted
        CPKeyValue harbor = options[0];
        Assert.Equal("700100 - Harbor Family Trust", harbor.Label);
        Assert.Equal("700400 -", options[3].Label); // an empty name gives no trailing space
        Assert.Equal(
            ["AccountName=Harbor Family Trust", "Custodian=Custodian One", "RMCC=Casey Copy;Riley Manager",
             "RelationshipManager=Riley Manager", "StarAccountNumber=S-700100"],
            harbor.Data!.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"));
        Assert.DoesNotContain(harbor.Data!.Keys, x => x is "ClientLegalName" or "KYCID"); // no other column leaves the server
    }

    [Fact]
    public async Task The_account_fills_4_fields_and_RMCC_and_a_client_value_for_them_is_not_used()
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.Add(new CPPageOutput { Name = "AccountName", Value = "HACKED" }); // a server-set field: not used

        CPAtlasPage result = Svc(Writer, CfpRoles.Write).SavePage(input, NoFiles);

        AssertSaved(result);
        Assert.Equal("Harbor Family Trust|S-700100|Riley Manager|Custodian One|Casey Copy;Riley Manager", await _db.ScalarAsync(
            $"SELECT CONCAT_WS('|', AccountName, StarNumber, RelationshipManager, Custodian, RMCC) FROM CFP.Inflow WHERE ID = {result.ID}"));
        // RMCC has no control, so it is not in the History entry.
        Assert.Equal(0, await _db.ScalarAsync($"SELECT COUNT(*) FROM CFP.AuditLog WHERE ReferenceID = {result.ID} AND LogMessage LIKE '%RMCC%'"));

        // RMCC is not a field of the form, so a client value for it refuses the save.
        CPAtlasPage probe = NewInput();
        probe.Outputs!.Add(new CPPageOutput { Name = "RMCC", Value = "Mallory" });
        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(probe, NoFiles);
        Assert.Equal(0, refused.ID);
        Assert.Contains(refused.Errors!, x => x.Field == "RMCC");
    }

    [Fact]
    public async Task A_changed_AIN_row_changes_the_record_only_when_the_user_can_change_the_account()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);
        await _db.ScalarAsync("UPDATE AIN.AccountInventory SET RelationshipManager = 'Bob Ray', RMCC = 'Bob Ray' WHERE AccountNumber = '700100';");

        // The account control is read-only: the second save keeps the values of the first save.
        using (ConfigCopy config = new ConfigCopy().Replace("CFPINFLOW.json",
            "\"Label\": \"Account\",\n            \"Type\": \"ngselect\",", "\"Label\": \"Account\",\n            \"Disabled\": true,\n            \"Type\": \"ngselect\","))
        using (SectionHarness readOnlyAccount = new(_db, AtlasAccessMode.Enforce, config.Folder))
        {
            CPApplicationSection loaded = readOnlyAccount.ServiceFor(App, _registry, Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });
            AssertSaved(readOnlyAccount.ServiceFor(App, _registry, Writer, CfpRoles.Write)
                .SavePage(EditInput(saved.ID, loaded.Version, ("Description", "Second save")), NoFiles));
        }
        Assert.Equal("Riley Manager|Casey Copy;Riley Manager|Second save", await _db.ScalarAsync(
            $"SELECT CONCAT_WS('|', RelationshipManager, RMCC, Description) FROM CFP.Inflow WHERE ID = {saved.ID}"));

        // The account control is enabled: the save reads the AIN row again, as before.
        CPApplicationSection again = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });
        AssertSaved(Svc(Writer, CfpRoles.Write).SavePage(EditInput(saved.ID, again.Version, ("Description", "Third save")), NoFiles));
        Assert.Equal("Bob Ray|Bob Ray", await _db.ScalarAsync(
            $"SELECT CONCAT_WS('|', RelationshipManager, RMCC) FROM CFP.Inflow WHERE ID = {saved.ID}"));
    }

    [Fact]
    public void A_user_who_cannot_change_the_account_gets_only_the_saved_option_with_no_Data()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);

        CPApplicationSection read = Svc(Reader, CfpRoles.Read).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });
        CPKeyValue only = Assert.Single(Control(read, "AccountNumber").Options!);
        Assert.Equal("700100", only.Value);
        Assert.Equal("700100 - Harbor Family Trust", only.Label);
        Assert.Null(only.Data);

        CPApplicationSection write = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });
        Assert.Equal(4, Control(write, "AccountNumber").Options!.Count);
    }

    [Theory]
    [InlineData("Table", "AIN.Missing", "is not in the LookupTables")]
    [InlineData("Value", "NoSuchColumn", "'NoSuchColumn', which is not a column of AIN.AccountInventory")]
    [InlineData("Label", "{AccountNumber}] - {Nope}", "'Nope', which is not a column")]
    [InlineData("Data", "AccountName,Bad]Name", "'Bad]Name', which is not a column")]
    [InlineData("OrderBy", "AccountNumber; DROP TABLE x", "OrderBy must be")]
    public void The_Table_provider_refuses_a_name_that_is_not_in_the_metadata(string parameter, string value, string message)
    {
        Dictionary<string, string> parameters = new()
        {
            ["Table"] = "AIN.AccountInventory", ["Value"] = "AccountNumber", ["Label"] = "{AccountNumber}", ["Data"] = "AccountName"
        };
        parameters[parameter] = value;
        CPApplicationControl control = new()
        {
            Name = "AccountNumber", Label = "Account", Type = "ngselect",
            OptionsSource = new CPOptionsSource { Provider = "Table", Parameters = parameters }
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["AppSettings:ConfigFolder"] = SectionHarness.RealConfigFolder }).Build();
        AtlasTableOptionProvider provider = new(null!, _registry,
            new AtlasConfigStore(configuration, new TestHost(SectionHarness.RealConfigFolder)), new MemoryCache(new MemoryCacheOptions()));

        List<string> errors = [.. provider.Check(new AtlasOptionRequest(App, control, control.OptionsSource))];

        Assert.Contains(errors, x => x.Contains(message, StringComparison.Ordinal));
        Assert.Throws<AtlasProblemException>(() => provider.GetOptions(new AtlasOptionRequest(App, control, control.OptionsSource)));
    }

    private ConfigSectionService Svc(string lanId, params AtlasRole[] roles) => _harness.ServiceFor(App, _registry, lanId, roles);

    private static void AssertSaved(CPAtlasPage result) =>
        Assert.True(result.ID > 0 && result.Errors is null or { Count: 0 }, string.Join("; ", (result.Errors ?? []).Select(x => $"{x.Field}: {x.Message}")));

    private static CPApplicationControl Control(CPApplicationSection section, string name) =>
        section.Controls!.SelectMany(x => new[] { x }.Concat(x.ChildControls ?? [])).Single(x => x.Name == name);

    private static CPMenu Menu(CPAtlasTab tab, string name) => tab.Menus!.Single(x => x.Name == name);

    private static List<int> Ids(CPAtlasDashboard dashboard, string tab) =>
        [.. dashboard.Tabs!.Single(x => x.ID == tab).KeyValues!.Select(x => Convert.ToInt32(x["ID"]))];

    private static CPAtlasPage EditInput(int id, string? version, params (string Name, string? Value)[] values) =>
        new() { AppCode = App, ID = id, Version = version, Outputs = [.. values.Select(x => new CPPageOutput { Name = x.Name, Value = x.Value })] };

    private static CPAtlasPage NewInput() => new()
    {
        AppCode = App,
        ID = 0,
        Outputs =
        [
            new() { Name = "AccountNumber", Value = "700100" },
            new() { Name = "InvestmentType", Value = "Equity" },
            new() { Name = "Amount", Value = "1250000.50" },
            new() { Name = "InflowDate", Value = "2026-10-20" },
            new() { Name = "Description", Value = "Wire from the client" },
            new() { Name = "FlowConfirm", Value = "true" }
        ]
    };
}
