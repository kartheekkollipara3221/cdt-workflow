using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Platform.Tests.Sections;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Security;

namespace BBH.CP.Atlas.Platform.Tests.Dashboard;

/// <summary>The 3 TTDV2 tabs (spec 12, 3.2) through the module service, AccessMode Enforce.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class Ttdv2DashboardTests(SqlServerDatabaseFixture database) : IAsyncLifetime, IDisposable
{
    private readonly SectionHarness _harness = new(database);
    private readonly string _me = "d." + Guid.NewGuid().ToString("N")[..8];
    private AtlasTableRegistry _registry = null!;
    private int _created, _prepared, _modified, _other, _deleted;

    public async Task InitializeAsync()
    {
        await database.SeedUserAsync(_me, "Dana Dashboard", "TTDV2", "Prepare");
        await database.SeedUserAsync("d.other", "Otto Other", "TTDV2", "Prepare");
        _registry = await SectionHarness.RegistryAsync(database);
        _created = await InsertAsync(createdBy: _me, preparer: "d.other", modifiedBy: "d.other");
        _prepared = await InsertAsync(createdBy: "d.other", preparer: _me, modifiedBy: "d.other");
        _modified = await InsertAsync(createdBy: "d.other", preparer: "d.other", modifiedBy: _me);
        _other = await InsertAsync(createdBy: "d.other", preparer: "d.other", modifiedBy: "d.other");
        _deleted = await InsertAsync(createdBy: _me, preparer: _me, modifiedBy: _me, deleted: true);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public void Current_tab_lists_records_that_the_user_created_prepares_or_modified_and_never_deleted_ones()
    {
        CPAtlasDashboard dashboard = Load(_me, Ttdv2Roles.Prepare);

        List<int> ids = Ids(dashboard, "CurrentTaxTimesheet");
        Assert.Contains(_created, ids);
        Assert.Contains(_prepared, ids);
        Assert.Contains(_modified, ids);
        Assert.DoesNotContain(_other, ids);
        Assert.DoesNotContain(_deleted, ids);
        Assert.Equal("TTDV2", dashboard.AppCode);
    }

    [Fact]
    public void Report_tab_lists_the_records_of_the_preparer_and_the_all_report_lists_all()
    {
        CPAtlasDashboard dashboard = Load(_me, Ttdv2Roles.Prepare);

        Assert.Equal([_prepared], Ids(dashboard, "TaxTimeReport").Where(x => x >= _created));
        List<int> all = Ids(dashboard, "TaxTimeAllReport");
        Assert.Contains(_other, all);
        Assert.DoesNotContain(_deleted, all);

        Dictionary<string, object> row = Tab(dashboard, "TaxTimeAllReport").KeyValues!.Single(x => Convert.ToInt32(x["ID"]) == _prepared);
        Assert.Equal("Dana Dashboard", row["TaxPreparerLanId"]);   // Type user: display name
        Assert.Equal("2026-10-18", row["WeekEnding"]);
        Assert.Equal(15, row["SaturdayMinute"]);
    }

    [Fact]
    public void Menus_and_the_add_button_come_from_Permissions()
    {
        CPAtlasDashboard prepare = Load(_me, Ttdv2Roles.Prepare);
        List<CPMenu> menus = Tab(prepare, "CurrentTaxTimesheet").Menus!;
        Assert.True(menus.Single(x => x.Name == "Edit").Visible);
        Assert.False(menus.Single(x => x.Name == "Delete").Visible);
        Assert.True(prepare.Buttons!.Single().Visible);

        CPAtlasDashboard read = Load("d.reader", Ttdv2Roles.Read);
        Assert.False(read.Buttons!.Single().Visible);
        Assert.False(Tab(read, "CurrentTaxTimesheet").Menus!.Single(x => x.Name == "Edit").Visible);
        Assert.True(Tab(read, "CurrentTaxTimesheet").Menus!.Single(x => x.Name == "History").Visible);

        CPAtlasDashboard owner = Load("d.owner", Ttdv2Roles.Owner);
        Assert.True(Tab(owner, "TaxTimeAllReport").Menus!.Single(x => x.Name == "Delete").Visible);
    }

    [Fact]
    public void A_user_with_no_role_gets_403()
    {
        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() => Load("d.nobody"));
        Assert.Equal(403, error.StatusCode);
    }

    private CPAtlasDashboard Load(string lanId, params AtlasRole[] roles) => _harness.Service(_registry, lanId, roles).LoadDashboard();

    private static CPAtlasTab Tab(CPAtlasDashboard dashboard, string id) => dashboard.Tabs!.Single(x => x.ID == id);

    private static List<int> Ids(CPAtlasDashboard dashboard, string tab) =>
        [.. Tab(dashboard, tab).KeyValues!.Select(x => Convert.ToInt32(x["ID"]))];

    private async Task<int> InsertAsync(string createdBy, string preparer, string modifiedBy, bool deleted = false) =>
        (int)(await database.ScalarAsync($"""
            INSERT INTO TTDV2.General (AccountNumber, AccountName, TaxYear, Category, WeekEnding, SaturdayMinute, WeeklyTotal,
                                      TaxPreparerLanId, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted)
            OUTPUT INSERTED.ID
            VALUES ('100100', 'Smith Family Trust', 2026, 'TAX_N', '2026-10-18', 15, 15, '{preparer}', '{createdBy}', '2026-10-01',
                    '{modifiedBy}', '2026-10-02', {(deleted ? 1 : 0)});
            """))!;
}
