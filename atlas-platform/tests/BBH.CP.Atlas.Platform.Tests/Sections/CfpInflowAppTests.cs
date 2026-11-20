using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>
/// The ClientInflows app (CFPINFLOW) of build step 1 (module 25, section 13): the real app files of the Config
/// folder on the release P2 tables. A plain record saves; the dashboard menus and the Add button follow the roles.
/// Own database (the P2 scripts change tables).
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
        await using (SqlConnection connection = new(_db.ConnectionString))
            await new AtlasColumnMetadataReader().LoadAsync(connection, _registry, ["CFP.Inflow", "CFP.AuditLog"]);
        _harness = new SectionHarness(_db);
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    public void Dispose() => _harness?.Dispose();

    [Fact]
    public async Task A_Write_user_saves_a_new_record_with_the_default_and_the_account_values()
    {
        CPAtlasPage result = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);

        Assert.True(result.Errors is null or { Count: 0 }, string.Join("; ", (result.Errors ?? []).Select(x => $"{x.Field}: {x.Message}")));
        Assert.True(result.ID >= 10000);
        Assert.Equal("100200|Brown Foundation|Equity|1250000.5000|2026-10-20|c.write|1|c.write|0", await _db.ScalarAsync($"""
            SELECT CONCAT_WS('|', AccountNumber, AccountName, InvestmentType, Amount, CONVERT(varchar(10), InflowDate, 23),
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
        List<int> all = Ids(dashboard, "Inflows");
        Assert.Contains(Convert.ToInt32(received), all); // IsDeleted NULL means not deleted

        Dictionary<string, object> row = dashboard.Tabs!.Single(x => x.ID == "InflowPipeline").KeyValues!
            .Single(x => Convert.ToInt32(x["ID"]) == Convert.ToInt32(submitted));
        Assert.Equal(1250000.5m, Convert.ToDecimal(row["Amount"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.StartsWith("2033-10-01", Convert.ToString(row["DestructionDate"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_Read_user_opens_a_record_read_only()
    {
        CPAtlasPage saved = Svc(Writer, CfpRoles.Write).SavePage(NewInput(), NoFiles);
        Assert.True(saved.ID > 0);

        CPApplicationSection section = Svc(Reader, CfpRoles.Read).LoadPage(new CPAtlasPage { AppCode = App, ID = saved.ID });

        IEnumerable<CPApplicationControl> controls = section.Controls!.SelectMany(x => x.ChildControls ?? []);
        Assert.All(controls, x => Assert.True(x.Disabled, x.Name));
        Assert.Equal("Equity", controls.Single(x => x.Name == "InvestmentType").Value);
        Assert.Equal(1, await _db.ScalarAsync($"SELECT COUNT(*) FROM CFP.Inflow WHERE ID = {saved.ID}"));
    }

    private ConfigSectionService Svc(string lanId, params AtlasRole[] roles) => _harness.ServiceFor(App, _registry, lanId, roles);

    private static CPMenu Menu(CPAtlasTab tab, string name) => tab.Menus!.Single(x => x.Name == name);

    private static List<int> Ids(CPAtlasDashboard dashboard, string tab) =>
        [.. dashboard.Tabs!.Single(x => x.ID == tab).KeyValues!.Select(x => Convert.ToInt32(x["ID"]))];

    private static CPAtlasPage NewInput() => new()
    {
        AppCode = App,
        ID = 0,
        Outputs =
        [
            new() { Name = "Account", Value = "100200 - Brown Foundation" },
            new() { Name = "InvestmentType", Value = "Equity" },
            new() { Name = "Amount", Value = "1250000.50" },
            new() { Name = "InflowDate", Value = "2026-10-20" },
            new() { Name = "Description", Value = "Wire from the client" },
            new() { Name = "FlowConfirm", Value = "true" }
        ]
    };
}
