using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.AspNetCore.Http;

namespace BBH.CP.Atlas.Platform.Tests.Sections;

/// <summary>Spec 12, section 5: load, save, concurrency, audit and the access matrix, through the TTDV2 service.</summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public sealed class Ttdv2SectionTests(SqlServerDatabaseFixture database) : IAsyncLifetime, IDisposable
{
    private const string Prep = "t.prep";
    private const string OtherPrep = "t.prep2";
    private const string Owner = "t.owner";
    private const string Reader = "t.read";
    private static readonly IFormFileCollection NoFiles = new FormFileCollection();

    private readonly SectionHarness _harness = new(database);
    private AtlasTableRegistry _registry = null!;

    public async Task InitializeAsync()
    {
        await database.SeedUserAsync(Prep, "Pat Preparer", "TTDV2", "Prepare");
        await database.SeedUserAsync(OtherPrep, "Quinn Preparer", "TTDV2", "Prepare");
        await database.SeedUserAsync(Owner, "Olive Owner", "TTDV2", "Owner");
        await database.SeedUserAsync(Reader, "Riley Reader", "TTDV2", "Read");
        _registry = await SectionHarness.RegistryAsync(database);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    // ---------- Load ----------

    [Fact]
    public void A_new_record_gets_the_defaults_options_and_grid_dates()
    {
        CPApplicationSection section = Svc(Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = 0 });

        Assert.Equal("2026", Control(section, "TaxYear").Value);              // {Options[1]}
        Assert.Equal("2027", Control(section, "TaxYear").Options![0].Value);
        Assert.Equal("2026-10-18", Control(section, "WeekEnding").Value);     // last Sunday
        Assert.Equal(["2026-10-18", "2026-10-25", "2026-11-01", "2026-11-08"], Control(section, "WeekEnding").Options!.Select(x => x.Value));
        Assert.Equal(Prep, Control(section, "TaxPreparer").Value);
        Assert.Contains(Control(section, "TaxPreparer").Options!, x => x.Value == Prep && x.Label == "Pat Preparer");
        Assert.DoesNotContain(Control(section, "TaxPreparer").Options!, x => x.Value == Reader);

        CPKeyValue account = Control(section, "Account").Options![0];
        Assert.Equal("100100 - Smith Family Trust", account.Value);
        Assert.Equal("100100", account.Data!["AccountNumber"]);

        CPApplicationControl grid = Control(section, "WeeklyHour");
        Assert.Equal("Monday 12-Oct-2026", grid.Grid!.Columns![0].headerName);
        Assert.Equal("Sunday 18-Oct-2026", grid.Grid.Columns[6].headerName);
        Assert.Equal("Weekly Total", grid.Grid.Columns[7].headerName);
        Assert.Single(grid.Grid.Data!);

        Assert.Equal("Tax Time Database Time Sheet", section.HeaderName);
        Assert.Equal("TTDV2", section.AppCode);
        Assert.Null(section.Version);
        Assert.True(section.UserPermissions!.CanCreate);
        Assert.False(Control(section, "Category").Disabled);
        Assert.True(Control(section, "AccountNumber").Disabled);
    }

    [Fact]
    public async Task An_existing_record_keeps_saved_values_that_are_not_in_the_options()
    {
        int id = await InsertAsync(createdBy: OtherPrep, taxPreparer: "gone.user", taxYear: 2019, weekEnding: "2020-01-05", monday: 30);

        CPApplicationSection section = Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = id });

        Assert.Equal("2019", Control(section, "TaxYear").Value);
        Assert.Equal("2019", Control(section, "TaxYear").Options![0].Value);
        Assert.Equal("2020-01-05", Control(section, "WeekEnding").Options![0].Value);
        Assert.Equal("gone.user", Control(section, "TaxPreparer").Options![0].Value);
        Assert.Equal("30", Control(section, "WeeklyHour").Grid!.Data![0]["MondayMinute"]);
        Assert.Equal("Monday 30-Dec-2019", Control(section, "WeeklyHour").Grid!.Columns![0].headerName);
        Assert.Equal($"Tax Time Database Time Sheet - {id}", section.HeaderName);
        Assert.NotNull(section.Version);
        Assert.True(section.UserPermissions!.CanDelete);
    }

    [Fact]
    public void An_unknown_record_is_404()
    {
        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() => Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = int.MaxValue }));
        Assert.Equal(404, error.StatusCode);
    }

    // ---------- Save ----------

    [Fact]
    public async Task A_new_record_is_saved_with_server_values_and_a_create_audit_entry()
    {
        CPAtlasPage result = Svc(Prep, Ttdv2Roles.Prepare).SavePage(NewInput(weeklyTotal: "999", accountNumber: "HACKED"), NoFiles);

        Assert.True(result.Errors is null or { Count: 0 }, Describe(result));
        Assert.True(result.ID >= 10000);
        Assert.NotNull(result.Version);
        Assert.Equal(160, await database.ScalarAsync($"SELECT WeeklyTotal FROM TTDV2.General WHERE ID = {result.ID}"));
        Assert.Equal("100200", await database.ScalarAsync($"SELECT AccountNumber FROM TTDV2.General WHERE ID = {result.ID}"));
        Assert.Equal("Brown Foundation", await database.ScalarAsync($"SELECT AccountName FROM TTDV2.General WHERE ID = {result.ID}"));
        Assert.Equal(Prep, await database.ScalarAsync($"SELECT CreatedBy FROM TTDV2.General WHERE ID = {result.ID}"));
        Assert.Equal(0, await database.ScalarAsync($"SELECT SaturdayMinute FROM TTDV2.General WHERE ID = {result.ID}")); // empty = 0
        Assert.Equal(new DateTime(2026, 10, 21, 10, 0, 0), await database.ScalarAsync($"SELECT CreatedDate FROM TTDV2.General WHERE ID = {result.ID}"));

        List<CPAuditLog> audit = Svc(Prep, Ttdv2Roles.Prepare).GetAuditLog("TTDV2", result.ID);
        CPAuditLog entry = Assert.Single(audit);
        Assert.Equal("Create", entry.Event);
        Assert.Equal("2026-10-21T10:00:00Z", entry.CreatedDateUtc);
        Assert.Equal("User t.prep", entry.CreatedBy);
        Assert.Equal(Prep, await database.ScalarAsync($"SELECT CreatedByLanId FROM TTDV2.AuditLog WHERE RefID = {result.ID}"));
    }

    [Theory]
    [InlineData("1.5", "Integer")]
    [InlineData("1:30", "Integer")]
    [InlineData("-5", "Min")]
    [InlineData("1441", "Max")]
    public void A_bad_minute_value_is_refused_with_a_field_error(string monday, string code)
    {
        CPAtlasPage result = Svc(Prep, Ttdv2Roles.Prepare).SavePage(NewInput(monday: monday), NoFiles);

        Assert.Equal(0, result.ID);
        CPPageError error = Assert.Single(result.Errors!);
        Assert.Equal("MondayMinute", error.Field);
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void An_extra_output_name_is_refused()
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.Add(new CPPageOutput { Name = "IsAdmin", Value = "true" });

        CPAtlasPage result = Svc(Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);

        Assert.Equal(0, result.ID);
        Assert.Contains(result.Errors!, x => x.Field == "IsAdmin" && x.Code == "NotAllowed");
    }

    [Fact]
    public void A_missing_required_value_is_refused()
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.RemoveAll(x => x.Name == "Category");

        CPAtlasPage result = Svc(Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);

        Assert.Contains(result.Errors!, x => x.Field == "Category" && x.Code == "Required");
    }

    [Theory]
    [InlineData("Category", "ANYTHING")]                       // static Options
    [InlineData("WeekEnding", "2026-10-20")]                    // OptionsSource: not a Sunday in the list
    [InlineData("TaxYear", "1999")]                             // OptionsSource
    [InlineData("TaxPreparer", "t.read")]                       // a user without Prepare or Owner (an OwnerField)
    [InlineData("Account", "999999 - Not My Account")]          // not in the PB accounts of this user
    public void A_value_that_is_not_an_option_is_refused(string name, string value)
    {
        CPAtlasPage input = NewInput();
        input.Outputs!.Single(x => x.Name == name).Value = value;

        CPAtlasPage result = Svc(Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);

        Assert.Equal(0, result.ID);
        CPPageError error = Assert.Single(result.Errors!);
        Assert.Equal(name, error.Field);
        Assert.Equal("NotInOptions", error.Code);
    }

    [Fact]
    public async Task A_saved_value_that_is_no_longer_an_option_can_stay()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: "gone.user", taxYear: 2019, weekEnding: "2020-01-05");
        CPApplicationSection loaded = Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = id });

        CPAtlasPage result = Svc(Owner, Ttdv2Roles.Owner).SavePage(
            EditInput(id, loaded.Version, ("TaxPreparer", "gone.user"), ("TaxYear", "2019"), ("WeekEnding", "2020-01-05"), ("Category", "OTHER")), NoFiles);

        Assert.True(result.Errors is null or { Count: 0 }, Describe(result));
        Assert.Equal("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task A_disabled_field_change_is_refused_and_server_set_values_are_ignored()
    {
        // LogOnly: the access check lets the save continue; the rules still make the fields read-only.
        int other = await InsertAsync(createdBy: OtherPrep, taxPreparer: OtherPrep);
        using SectionHarness logOnly = new(database, AtlasAccessMode.LogOnly);
        CPApplicationSection loaded = logOnly.Service(_registry, Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = other });

        CPAtlasPage changed = logOnly.Service(_registry, Prep, Ttdv2Roles.Prepare)
            .SavePage(EditInput(other, loaded.Version, ("Category", "OTHER")), NoFiles);
        Assert.Contains(changed.Errors!, x => x.Field == "Category" && x.Code == "NotAllowed");

        // The same value of a disabled field is ignored (a client can send all values).
        CPAtlasPage same = logOnly.Service(_registry, Prep, Ttdv2Roles.Prepare)
            .SavePage(EditInput(other, loaded.Version, ("Category", "TAX_N")), NoFiles);
        Assert.True(same.Errors is null or { Count: 0 }, Describe(same));

        // System fields are set by the server only.
        CPApplicationSection mine = Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = other });
        CPAtlasPage system = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(other, mine.Version, ("ModifiedBy", "someone"), ("Category", "OTHER")), NoFiles);
        Assert.True(system.ID > 0, Describe(system));
        Assert.Equal(Owner, await database.ScalarAsync($"SELECT ModifiedBy FROM TTDV2.General WHERE ID = {other}"));
    }

    [Fact]
    public async Task Two_users_save_the_same_record_and_the_second_gets_a_conflict()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        string? version = Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = id }).Version;

        CPAtlasPage first = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(id, version, ("Category", "OTHER")), NoFiles);
        CPAtlasPage second = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(id, version, ("Category", "STMT_N")), NoFiles);

        Assert.True(first.Errors is null or { Count: 0 }, Describe(first));
        Assert.Equal(0, second.ID);
        Assert.Contains(second.Errors!, x => x.Code == "Conflict");
        Assert.Equal("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task Update_and_delete_write_one_audit_entry_each_and_no_change_writes_none()
    {
        CPAtlasPage created = Svc(Owner, Ttdv2Roles.Owner).SavePage(NewInput(), NoFiles);
        Assert.True(created.ID > 0, Describe(created));

        CPAtlasPage noChange = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(created.ID, created.Version, ("Category", "TAX_N")), NoFiles);
        Assert.True(noChange.ID > 0, Describe(noChange));
        CPAtlasPage update = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(created.ID, noChange.Version, ("Category", "OTHER")), NoFiles);
        Assert.True(update.ID > 0, Describe(update));
        CPAtlasPage removed = Svc(Owner, Ttdv2Roles.Owner).RemovePage(created.ID);
        Assert.Equal("Yes", removed.Outputs!.Single().Value);

        List<CPAuditLog> audit = Svc(Owner, Ttdv2Roles.Owner).GetAuditLog("TTDV2", created.ID);
        Assert.Equal(["Delete", "Update", "Create"], audit.Select(x => x.Event));

        JsonElement changes = (JsonElement)audit[1].LogMessage!;
        JsonElement category = changes.EnumerateArray().Single(x => x.GetProperty("Name").GetString() == "Category");
        Assert.Equal("TAX_N", category.GetProperty("Old").GetString());
        Assert.Equal("OTHER", category.GetProperty("Value").GetString());
        Assert.Equal(true, await database.ScalarAsync($"SELECT IsDeleted FROM TTDV2.General WHERE ID = {created.ID}"));
        Assert.Throws<AtlasProblemException>(() => Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = created.ID }));
    }

    // ---------- Access matrix (spec 01, 10.5), AccessMode Enforce ----------

    [Fact]
    public async Task Read_can_view_but_not_create_edit_or_delete()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);

        CPApplicationSection view = Svc(Reader, Ttdv2Roles.Read).LoadPage(new CPAtlasPage { ID = id });
        Assert.True(Control(view, "Category").Disabled);
        Assert.False(view.Buttons!.Single(x => x.Name == "Submit").Visible);

        Assert.Equal(403, Status(() => Svc(Reader, Ttdv2Roles.Read).LoadPage(new CPAtlasPage { ID = 0 })));
        Assert.Equal(403, Status(() => Svc(Reader, Ttdv2Roles.Read).SavePage(NewInput(), NoFiles)));
        Assert.Equal(403, Status(() => Svc(Reader, Ttdv2Roles.Read).SavePage(EditInput(id, null, ("Category", "OTHER")), NoFiles)));
        Assert.Equal(403, Status(() => Svc(Reader, Ttdv2Roles.Read).RemovePage(id)));
    }

    [Fact]
    public async Task Prepare_edits_own_records_only_and_cannot_delete()
    {
        int own = await InsertAsync(createdBy: OtherPrep, taxPreparer: Prep);   // own by TaxPreparerLanId
        int other = await InsertAsync(createdBy: OtherPrep, taxPreparer: OtherPrep);

        CPApplicationSection ownView = Svc(Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = own });
        Assert.False(Control(ownView, "Category").Disabled);
        Assert.True(ownView.Buttons!.Single(x => x.Name == "Submit").Visible);
        Assert.False(ownView.Buttons!.Single(x => x.Name == "Delete").Visible);
        CPAtlasPage saved = Svc(Prep, Ttdv2Roles.Prepare).SavePage(EditInput(own, ownView.Version, ("Category", "OTHER")), NoFiles);
        Assert.True(saved.ID > 0, Describe(saved));

        CPApplicationSection otherView = Svc(Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = other });
        Assert.True(Control(otherView, "Category").Disabled);
        Assert.False(otherView.Buttons!.Single(x => x.Name == "Submit").Visible);
        Assert.Equal(403, Status(() => Svc(Prep, Ttdv2Roles.Prepare).SavePage(EditInput(other, null, ("Category", "OTHER")), NoFiles)));
        Assert.Equal(403, Status(() => Svc(Prep, Ttdv2Roles.Prepare).RemovePage(own)));
    }

    [Fact]
    public async Task Owner_edits_and_deletes_all_records()
    {
        int other = await InsertAsync(createdBy: Prep, taxPreparer: Prep);

        CPApplicationSection view = Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = other });
        Assert.False(Control(view, "Category").Disabled);
        CPAtlasPage saved = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(other, view.Version, ("Category", "STMT_N")), NoFiles);
        Assert.True(saved.ID > 0, Describe(saved));
        Assert.Equal(other, Svc(Owner, Ttdv2Roles.Owner).RemovePage(other).ID);
    }

    [Fact]
    public async Task A_user_with_no_role_cannot_open_a_record()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        Assert.Equal(403, Status(() => Svc("t.nobody").LoadPage(new CPAtlasPage { ID = id })));
    }

    [Fact]
    public async Task LogOnly_writes_AccessDenied_and_continues()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        using SectionHarness logOnly = new(database, AtlasAccessMode.LogOnly);

        CPApplicationSection section = logOnly.Service(_registry, Reader, Ttdv2Roles.Read).LoadPage(new CPAtlasPage { ID = 0 });

        Assert.NotNull(section);
        Assert.Contains(logOnly.AccessLog.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Create", StringComparison.Ordinal));
        _ = id;
    }

    // ---------- Helpers ----------

    private CPTTDV2Service Svc(string lanId, params AtlasRole[] roles) => _harness.Service(_registry, lanId, roles);

    private static CPApplicationControl Control(CPApplicationSection section, string name) =>
        section.FindControlByName(name) ?? throw new InvalidOperationException($"No control {name}.");

    private static int Status(Action action) => Assert.Throws<AtlasProblemException>(action).StatusCode;

    private static string Describe(CPAtlasPage page) =>
        string.Join("; ", (page.Errors ?? []).Select(x => $"{x.Field}:{x.Code}:{x.Message}"));

    private static CPAtlasPage NewInput(string monday = "60", string weeklyTotal = "0", string? accountNumber = null)
    {
        List<CPPageOutput> outputs =
        [
            new() { Name = "Account", Value = "100200 - Brown Foundation" },
            new() { Name = "TaxYear", Value = "2026" },
            new() { Name = "Category", Value = "TAX_N" },
            new() { Name = "WorkDescription", Value = "Quarterly estimate" },
            new() { Name = "TaxPreparer", Value = Prep },
            new() { Name = "WeekEnding", Value = "2026-10-18" },
            new()
            {
                Name = "WeeklyHour",
                Value = JsonSerializer.Serialize(new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["MondayMinute"] = monday, ["TuesdayMinute"] = 40, ["WednesdayMinute"] = "30", ["ThursdayMinute"] = "20",
                        ["FridayMinute"] = "10", ["SaturdayMinute"] = "", ["SundayMinute"] = null, ["WeeklyTotal"] = weeklyTotal
                    }
                })
            }
        ];
        if (accountNumber != null)
            outputs.Add(new CPPageOutput { Name = "AccountNumber", Value = accountNumber });
        return new CPAtlasPage { AppCode = "TTDV2", ID = 0, Outputs = outputs };
    }

    private static CPAtlasPage EditInput(int id, string? version, params (string Name, string? Value)[] values) =>
        new() { AppCode = "TTDV2", ID = id, Version = version, Outputs = [.. values.Select(x => new CPPageOutput { Name = x.Name, Value = x.Value })] };

    private async Task<int> InsertAsync(string createdBy, string taxPreparer, int taxYear = 2026, string weekEnding = "2026-10-18", int monday = 60)
    {
        object? id = await database.ScalarAsync($"""
            INSERT INTO TTDV2.General (Account, AccountNumber, AccountName, TaxYear, Category, WeekEnding, MondayMinute, WeeklyTotal,
                                      TaxPreparerLanId, CreatedBy, CreatedDate, IsDeleted)
            OUTPUT INSERTED.ID
            VALUES ('100100 - Smith Family Trust', '100100', 'Smith Family Trust', {taxYear}, 'TAX_N', '{weekEnding}', {monday}, {monday},
                    '{taxPreparer}', '{createdBy}', '2026-10-01', 0);
            """);
        return (int)id!;
    }
}
