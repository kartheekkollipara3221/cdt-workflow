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
/// the effects rule and the saved option rule (build step 2); the state in the rules, MustBeTrue and the one submit
/// button with the steps by state (build step 3). Own database (the P2 scripts change tables).
/// </summary>
/// <remarks>
/// The engine writes WorkflowState from build step 6. Until then the tests set the state by SQL.
/// </remarks>
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
        await SetStateAsync(saved.ID, "Draft");
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
    public async Task A_user_who_cannot_change_the_account_gets_only_the_saved_option_with_no_Data()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);
        await SetStateAsync(saved.ID, "Draft");

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

    // ---------- Build step 3 ----------

    [Fact]
    public void A_new_record_is_in_state_Draft_part_2_is_hidden_and_part_1_and_Submit_are_enabled()
    {
        CPApplicationSection section = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = 0 });

        Assert.False(Control(section, "CashReceipt").Visible);
        Assert.False(Control(section, "InflowAccount").Disabled);
        Assert.False(Control(section, "InflowDetails").Disabled);
        CPButton submit = section.Buttons!.Single(x => x.Name == "Submit");
        Assert.True(submit.Visible);
        Assert.Equal("Submit", submit.Label);
    }

    [Fact]
    public async Task The_first_submit_needs_FlowConfirm_and_not_the_part_2_fields()
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.RemoveAll(x => x.Name == "FlowConfirm");
        input.Outputs.Add(new CPPageOutput { Name = "FlowConfirm", Value = "false" });

        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(input, NoFiles);

        Assert.Equal(0, refused.ID);
        CPPageError error = Assert.Single(refused.Errors!);
        Assert.Equal(("FlowConfirm", "MustBeTrue"), (error.Field, error.Code));
        Assert.Equal("Select I confirm that the information above is correct. before you submit.", error.Message);

        // FundsReceived and CashConfirm are hidden in Draft: their MustBeTrue does not stop the first submit.
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);
        Assert.Equal("||", await _db.ScalarAsync(
            $"SELECT CONCAT_WS('|', ISNULL(CAST(FundsReceived AS varchar(1)), ''), ISNULL(CashSubmittedBy, ''), ISNULL(CONVERT(varchar(19), CashSubmittedDate, 126), '')) FROM CFP.Inflow WHERE ID = {saved.ID}"));
    }

    [Fact]
    public void A_part_2_value_at_the_first_submit_is_refused()
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.Add(new CPPageOutput { Name = "Comment", Value = "Too early" });

        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(input, NoFiles);

        Assert.Equal(0, refused.ID);
        Assert.Contains(refused.Errors!, x => x is { Field: "Comment", Code: "NotAllowed" });
    }

    [Fact]
    public async Task In_state_Submitted_part_1_is_read_only_and_part_2_is_enabled()
    {
        int id = await SubmittedRecordAsync();

        CPApplicationSection write = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = id });
        Assert.True(Control(write, "InflowAccount").Disabled);
        Assert.True(Control(write, "Amount").Disabled);
        Assert.True(Control(write, "CashReceipt").Visible);
        Assert.False(Control(write, "FundsReceived").Disabled);
        Assert.True(write.Buttons!.Single(x => x.Name == "Submit").Visible);

        CPApplicationSection read = Svc(Reader, CfpRoles.Read).LoadPage(new CPAtlasPage { AppCode = App, ID = id });
        Assert.True(Control(read, "CashReceipt").Visible);
        Assert.True(Control(read, "FundsReceived").Disabled);
        Assert.False(read.Buttons!.Single(x => x.Name == "Submit").Visible);
    }

    [Fact]
    public async Task The_second_submit_cannot_change_part_1()
    {
        int id = await SubmittedRecordAsync();
        CPApplicationSection loaded = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = id });

        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(
            EditInput(id, loaded.Version, ("Amount", "5"), ("FundsReceived", "true"), ("CashConfirm", "true")), NoFiles);

        Assert.Equal(0, refused.ID);
        Assert.Contains(refused.Errors!, x => x is { Field: "Amount", Code: "NotAllowed" });
        Assert.Equal(1250000.5m, Convert.ToDecimal(await _db.ScalarAsync($"SELECT Amount FROM CFP.Inflow WHERE ID = {id}"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_second_submit_needs_the_two_part_2_checkboxes()
    {
        int id = await SubmittedRecordAsync();
        CPApplicationSection loaded = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = id });

        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(EditInput(id, loaded.Version, ("Comment", "Received")), NoFiles);

        Assert.Equal(0, refused.ID);
        Assert.Equal(["CashConfirm:MustBeTrue", "FundsReceived:MustBeTrue"],
            refused.Errors!.Select(x => $"{x.Field}:{x.Code}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_second_submit_sets_CashSubmittedBy_and_CashSubmittedDate_on_the_server()
    {
        int id = await SubmittedRecordAsync();
        CPApplicationSection loaded = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = id });

        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(EditInput(id, loaded.Version,
            ("FundsReceived", "true"), ("Comment", "Received"), ("CashConfirm", "true"),
            ("CashSubmittedBy", "m.allory")), NoFiles); // a server-set field: the client value is not used

        AssertSaved(saved);
        Assert.Equal("1|Received|c.write|2026-10-21T10:00:00|1", await _db.ScalarAsync($"""
            SELECT CONCAT_WS('|', CAST(FundsReceived AS int), Comment, CashSubmittedBy, CONVERT(varchar(19), CashSubmittedDate, 126),
                             CAST(CashConfirm AS int))
            FROM CFP.Inflow WHERE ID = {id}
            """));
        // CashSubmittedBy has a control, so it is in the History. CashSubmittedDate has no control.
        string? log = Convert.ToString(await _db.ScalarAsync(
            $"SELECT TOP (1) LogMessage FROM CFP.AuditLog WHERE ReferenceID = {id} ORDER BY ID DESC"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains("CashSubmittedBy", log, StringComparison.Ordinal);
        Assert.DoesNotContain("CashSubmittedDate", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("FundsReceived", "Funds Received")]
    [InlineData(null, "(none)")]
    public async Task A_state_with_no_step_hides_Submit_and_refuses_the_save(string? state, string label)
    {
        CPAtlasPage first = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(first);
        await SetStateAsync(first.ID, state);
        string? before = Convert.ToString(await _db.ScalarAsync($"SELECT CONVERT(varchar(20), RowVersion, 1) FROM CFP.Inflow WHERE ID = {first.ID}"),
            System.Globalization.CultureInfo.InvariantCulture);

        CPApplicationSection loaded = Svc(Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = first.ID });
        Assert.False(loaded.Buttons!.Single(x => x.Name == "Submit").Visible);
        Assert.All(loaded.Controls!, x => Assert.True(x.Disabled, x.Name));

        CPAtlasPage refused = Svc(Writer, CfpRoles.Write).SavePage(EditInput(first.ID, loaded.Version, ("Comment", "Late")), NoFiles);

        Assert.Equal(0, refused.ID);
        CPPageError error = Assert.Single(refused.Errors!);
        Assert.Equal(("", "NotAllowed"), (error.Field, error.Code));
        Assert.Equal($"You cannot submit this record in the state '{label}'.", error.Message);
        Assert.Equal(before, Convert.ToString(await _db.ScalarAsync($"SELECT CONVERT(varchar(20), RowVersion, 1) FROM CFP.Inflow WHERE ID = {first.ID}"),
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task The_Label_of_a_step_is_the_text_of_the_submit_button()
    {
        int id = await SubmittedRecordAsync();
        using ConfigCopy config = new ConfigCopy().Replace("CFPINFLOW.json",
            "\"Submitted\": {\n", "\"Submitted\": {\n          \"Label\": \"Confirm receipt\",\n");
        using SectionHarness harness = new(_db, AtlasAccessMode.Enforce, config.Folder);

        CPApplicationSection submitted = harness.ServiceFor(App, _registry, Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = id });
        CPApplicationSection draft = harness.ServiceFor(App, _registry, Writer, CfpRoles.Write).LoadPage(new CPAtlasPage { AppCode = App, ID = 0 });

        Assert.Equal("Confirm receipt", submitted.Buttons!.Single(x => x.Name == "Submit").Label);
        Assert.Equal("Submit", draft.Buttons!.Single(x => x.Name == "Submit").Label);
    }

    private ConfigSectionService Svc(string lanId, params AtlasRole[] roles) => _harness.ServiceFor(App, _registry, lanId, roles);

    /// <summary>Saves a new record and sets its state to Submitted, as the first submit does from build step 6.</summary>
    private async Task<int> SubmittedRecordAsync()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        AssertSaved(saved);
        await SetStateAsync(saved.ID, "Submitted");
        return saved.ID;
    }

    private Task<object?> SetStateAsync(int id, string? state) =>
        _db.ScalarAsync($"UPDATE CFP.Inflow SET WorkflowState = {(state == null ? "NULL" : $"'{state}'")} WHERE ID = {id}; SELECT @@ROWCOUNT;");

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
