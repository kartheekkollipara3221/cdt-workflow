using System.Text.Json;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;
using BBH.CP.Atlas.Platform.Tests.Data;
using BBH.CP.Atlas.Service.Application;
using BBH.CP.Atlas.Service.Platform;
using BBH.CP.Atlas.Service.Platform.Registry;
using BBH.CP.Atlas.Service.Platform.Sections;
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
        _registry = database.Ttdv2Registry;
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
        Assert.Equal("2026-10-25", Control(section, "WeekEnding").Value);     // coming Sunday: {Today.Next(Sunday)}
        Assert.Equal(["2026-10-18", "2026-10-25", "2026-11-01", "2026-11-08"], Control(section, "WeekEnding").Options!.Select(x => x.Value));
        Assert.Equal(Prep, Control(section, "TaxPreparer").Value);
        Assert.Contains(Control(section, "TaxPreparer").Options!, x => x.Value == Prep && x.Label == "Pat Preparer");
        Assert.DoesNotContain(Control(section, "TaxPreparer").Options!, x => x.Value == Reader);

        CPKeyValue account = Control(section, "Account").Options![0];
        Assert.Equal("100100 - Smith Family Trust", account.Value);
        Assert.Equal("100100", account.Data!["AccountNumber"]);

        CPApplicationControl grid = Control(section, "WeeklyHour");
        // The client shows the dates in the headers from HeaderFromDate (review FABLE F03: no server hook).
        Assert.Equal("Monday", grid.Grid!.Columns![0].headerName);
        Assert.Equal(("WeekEnding", -6), (grid.Grid.Columns[0].HeaderFromDate!.Control, grid.Grid.Columns[0].HeaderFromDate!.AddDays));
        Assert.Equal(0, grid.Grid.Columns[6].HeaderFromDate!.AddDays);
        Assert.Null(grid.Grid.Columns[7].HeaderFromDate);
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
        // As the target History Log: oldest first; LogMessage is JSON text of Name/Value items (no _Event item).
        Assert.Equal(["Create", "Update", "Delete"], audit.Select(x => x.Event));
        Assert.Matches(@"^\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}$", audit[0].CreatedDate);

        JsonElement changes = JsonDocument.Parse(Assert.IsType<string>(audit[1].LogMessage)).RootElement;
        Assert.DoesNotContain(changes.EnumerateArray(), x => x.GetProperty("Name").GetString() == "_Event");
        JsonElement category = changes.EnumerateArray().Single(x => x.GetProperty("Name").GetString() == "Category");
        Assert.Equal("TAX_N", category.GetProperty("Old").GetString());
        Assert.Equal("OTHER", category.GetProperty("Value").GetString());
        Assert.Equal(true, await database.ScalarAsync($"SELECT IsDeleted FROM TTDV2.General WHERE ID = {created.ID}"));
        Assert.Throws<AtlasProblemException>(() => Svc(Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = created.ID }));
    }

    [Fact]
    public async Task A_disabled_number_sent_in_another_form_is_no_change()
    {
        // Review DA-06: "02026" and the stored 2026 are the same value, so a disabled field does not give NotAllowed.
        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.json", "\"Name\": \"TaxYear\",", "\"Name\": \"TaxYear\", \"Disabled\": true,");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner, taxYear: 2026);
        string? version = custom.Service(_registry, Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = id }).Version;

        CPAtlasPage saved = custom.Service(_registry, Owner, Ttdv2Roles.Owner)
            .SavePage(EditInput(id, version, ("TaxYear", "02026"), ("Category", "OTHER")), NoFiles);

        Assert.True(saved.ID > 0, Describe(saved));
        Assert.Equal("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
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
    public void LogOnly_writes_AccessDenied_and_continues()
    {
        using SectionHarness logOnly = new(database, AtlasAccessMode.LogOnly);

        logOnly.Service(_registry, Reader, Ttdv2Roles.Read).LoadPage(new CPAtlasPage { ID = 0 });

        Assert.Contains(logOnly.AccessLog.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Two_saves_with_the_same_service_and_contexts_both_commit()
    {
        // Review 3 RV-L01: the platform context kept the finished shared transaction, so a second save in one scope failed.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        ConfigSectionService service = Svc(Owner, Ttdv2Roles.Owner);

        CPApplicationSection first = service.LoadPage(new CPAtlasPage { ID = id });
        CPAtlasPage one = service.SavePage(EditInput(id, first.Version, ("Category", "OTHER")), NoFiles);
        CPAtlasPage two = service.SavePage(EditInput(id, one.Version, ("Category", "TAX_N")), NoFiles);

        Assert.True(one.Errors is null or { Count: 0 }, Describe(one));
        Assert.True(two.Errors is null or { Count: 0 }, Describe(two));
        Assert.Equal("TAX_N", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task LogOnly_does_not_let_a_user_without_the_Create_permission_save_a_new_record()
    {
        // Review 3 RV-04: before, a Read user (or a user with no role) inserted a row of defaults in LogOnly.
        using SectionHarness logOnly = new(database, AtlasAccessMode.LogOnly);
        object? before = await database.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General");

        AtlasProblemException error = Assert.Throws<AtlasProblemException>(() =>
            logOnly.Service(_registry, Reader, Ttdv2Roles.Read).SavePage(new CPAtlasPage { AppCode = "TTDV2", ID = 0, Outputs = [] }, NoFiles));
        AtlasProblemException noRole = Assert.Throws<AtlasProblemException>(() =>
            logOnly.Service(_registry, "t.nobody").SavePage(new CPAtlasPage { AppCode = "TTDV2", ID = 0, Outputs = [] }, NoFiles));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal(403, noRole.StatusCode);
        Assert.Equal(before, await database.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General"));
        Assert.Contains(logOnly.AccessLog.Entries, x => x.EventId.Name == "Atlas.AccessDenied" && x.Message.Contains("Create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogOnly_does_not_let_a_user_without_the_Delete_permission_delete()
    {
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        using SectionHarness logOnly = new(database, AtlasAccessMode.LogOnly);

        Assert.Equal(403, Status(() => logOnly.Service(_registry, "t.nobody").RemovePage(id)));
        Assert.Equal(403, Status(() => logOnly.Service(_registry, Reader, Ttdv2Roles.Read).RemovePage(id)));
        Assert.Equal(false, await database.ScalarAsync($"SELECT IsDeleted FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task History_reads_only_the_AuditRefTypes_of_the_app()
    {
        // Review DA-03: a shared audit table has entries of other apps with the same RefID.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        await database.ScalarAsync($"""
            INSERT INTO TTDV2.AuditLog (RefID, RefType, CreatedBy, CreatedByLanId, CreatedDate, LogMessage)
            VALUES ({id}, 'TTD', 'Old', 'old', '2026-01-01', '[]'), ({id}, 'OTHERAPP', 'Other', 'other', '2026-01-02', '[]');
            """);

        Assert.Equal(2, Svc(Owner, Ttdv2Roles.Owner).GetAuditLog("TTDV2", id).Count); // no setting: all entries (as before)

        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.app.json", "\"Enabled\": true", "\"Enabled\": true, \"AuditRefTypes\": [ \"TTDV2\", \"TTD\" ]");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        List<CPAuditLog> audit = custom.Service(_registry, Owner, Ttdv2Roles.Owner).GetAuditLog("TTDV2", id);

        CPAuditLog entry = Assert.Single(audit);
        Assert.Equal("Old", entry.CreatedBy);
    }

    [Fact]
    public async Task A_failing_OnAfterSave_hook_is_logged_and_the_save_still_succeeds()
    {
        // Review SV-06: the record is committed; a 500 would make the user save again and create a duplicate.
        using ListLoggerFactory logs = new();
        ThrowingAfterSave service = new(_harness.ModuleServices(_registry, Prep, [Ttdv2Roles.Prepare], logs)) { AppCode = "TTDV2" };

        CPAtlasPage result = service.SavePage(NewInput(), NoFiles);

        Assert.True(result.ID > 0, Describe(result));
        Assert.Equal(1, await database.ScalarAsync($"SELECT COUNT(*) FROM TTDV2.General WHERE ID = {result.ID}"));
        Assert.Contains(logs.Logger.Entries, x => x.EventId.Name == "Atlas.AfterSaveFailed" && x.Message.Contains($"Record={result.ID}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_save_leaves_no_changed_entity_in_the_change_tracker()
    {
        // Review SV-10: Category is mapped onto the tracked record before the MondayMinute error stops the save.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        AtlasModuleServices services = _harness.ModuleServices(_registry, Owner, [Ttdv2Roles.Owner], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        ConfigSectionService service = new(services) { AppCode = "TTDV2" };
        string? version = service.LoadPage(new CPAtlasPage { ID = id }).Version;

        CPAtlasPage result = service.SavePage(EditInput(id, version, ("Category", "OTHER"), ("MondayMinute", "1441")), NoFiles);

        Assert.Equal(0, result.ID);
        services.PlatformContext.SaveChanges();
        Assert.Equal("TAX_N", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task A_refused_field_is_logged_and_grid_JSON_that_does_not_parse_is_an_error()
    {
        // Review SV-12: Atlas.FieldRefused (Warning) for each refusal; bad grid JSON is not an empty row.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        using ListLoggerFactory logs = new();
        ConfigSectionService service = new(_harness.ModuleServices(_registry, Owner, [Ttdv2Roles.Owner], logs)) { AppCode = "TTDV2" };
        string? version = service.LoadPage(new CPAtlasPage { ID = id }).Version;
        CPAtlasPage input = EditInput(id, version, ("Salary", "999999"), ("WeeklyHour", "{ not json"));

        CPAtlasPage result = service.SavePage(input, NoFiles);

        Assert.Equal(0, result.ID);
        Assert.Contains(result.Errors!, x => x.Field == "WeeklyHour" && x.Code == "InvalidGrid");
        Assert.Contains(logs.Logger.Entries, x => x.EventId.Name == "Atlas.FieldRefused" && x.Message.Contains("Field=Salary Code=NotAllowed", StringComparison.Ordinal));
        Assert.Contains(logs.Logger.Entries, x => x.EventId.Name == "Atlas.FieldRefused" && x.Message.Contains("Field=WeeklyHour Code=InvalidGrid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_save_after_load_and_history_in_the_same_scope_commits()
    {
        // Review DA-10: the read-only paths keep nothing that a later save in the same scope trips on.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        AtlasModuleServices services = _harness.ModuleServices(_registry, Owner, [Ttdv2Roles.Owner], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        ConfigSectionService service = new(services) { AppCode = "TTDV2" };

        string? version = service.LoadPage(new CPAtlasPage { ID = id }).Version;
        service.GetAuditLog("TTDV2", id);
        CPAtlasPage result = service.SavePage(EditInput(id, version, ("Category", "OTHER")), NoFiles);

        Assert.True(result.Errors is null or { Count: 0 }, Describe(result));
        Assert.Equal(id, result.ID);
        Assert.Equal("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData("Local", 5)]
    public async Task SystemTime_decides_if_ModifiedDate_is_UTC_or_local_time(string? systemTime, int hour)
    {
        // Review DA-11: the clock is 10:00 UTC; the local zone is UTC-5.
        using ConfigCopy config = new();
        if (systemTime != null)
            config.Replace("TTDV2.app.json", "\"Enabled\": true", $"\"Enabled\": true, \"SystemTime\": \"{systemTime}\"");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        custom.Time.Zone = TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");

        CPAtlasPage result = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).SavePage(NewInput(), NoFiles);

        Assert.True(result.ID > 0, Describe(result));
        DateTime modified = (DateTime)(await database.ScalarAsync($"SELECT ModifiedDate FROM TTDV2.General WHERE ID = {result.ID}"))!;
        Assert.Equal(hour, modified.Hour);
    }

    private sealed class ThrowingAfterSave(AtlasModuleServices services) : ConfigSectionService(services)
    {
        protected override void OnAfterSave(AtlasSaveContext context) => throw new InvalidOperationException("The notification failed.");
    }

    [Fact]
    public async Task An_OnLoad_hook_sees_the_record_and_its_change_reaches_the_user()
    {
        // FABLE review 4.2 item 1: the hooks that the next apps override.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);
        HookModule service = new(_harness.ModuleServices(_registry, Owner, [Ttdv2Roles.Owner], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)) { AppCode = "TTDV2" };

        CPApplicationSection created = service.LoadPage(new CPAtlasPage { ID = 0 });
        CPApplicationSection stored = service.LoadPage(new CPAtlasPage { ID = id });

        Assert.Equal("new", Control(created, "WorkDescription").Value);
        Assert.Equal("stored " + id, Control(stored, "WorkDescription").Value);
    }

    [Fact]
    public async Task An_error_from_the_OnBeforeSave_hook_stops_the_save()
    {
        HookModule service = new(_harness.ModuleServices(_registry, Prep, [Ttdv2Roles.Prepare], Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance))
        {
            AppCode = "TTDV2",
            RefuseCategory = "TAX_N"
        };
        object? before = await database.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General");

        CPAtlasPage result = service.SavePage(NewInput(), NoFiles);

        Assert.Equal(0, result.ID);
        Assert.Contains(result.Errors!, x => x.Field == "Category" && x.Code == "HookRefused");
        Assert.Equal(before, await database.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General"));
    }

    private sealed class HookModule(AtlasModuleServices services) : ConfigSectionService(services)
    {
        public string? RefuseCategory { get; init; }

        protected override void OnLoad(AtlasLoadContext context) =>
            context.Section.FindControlByName("WorkDescription")!.Value = context.IsNew ? "new" : "stored " + context.Record!["ID"];

        protected override void OnBeforeSave(AtlasSaveContext context)
        {
            if (Equals(context.Record["Category"], RefuseCategory))
                context.Errors.Add(new CPPageError { Field = "Category", Code = "HookRefused", Message = "The app refuses this category." });
        }
    }

    [Fact]
    public async Task An_update_with_no_Version_is_refused()
    {
        // Review DA-02: a caller that does not send Version must not overwrite changes of other users.
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);

        CPAtlasPage result = Svc(Owner, Ttdv2Roles.Owner).SavePage(EditInput(id, null, ("Category", "OTHER")), NoFiles);

        Assert.Equal(0, result.ID);
        Assert.Contains(result.Errors!, x => x.Code == "VersionRequired");
        Assert.NotEqual("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public async Task With_RequireVersion_false_an_update_with_no_Version_is_saved()
    {
        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.app.json", "\"Enabled\": true", "\"Enabled\": true, \"RequireVersion\": false");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        int id = await InsertAsync(createdBy: Owner, taxPreparer: Owner);

        CPAtlasPage result = custom.Service(_registry, Owner, Ttdv2Roles.Owner).SavePage(EditInput(id, null, ("Category", "OTHER")), NoFiles);

        Assert.True(result.Errors is null or { Count: 0 }, Describe(result));
        Assert.Equal("OTHER", await database.ScalarAsync($"SELECT Category FROM TTDV2.General WHERE ID = {id}"));
    }

    [Fact]
    public void An_effect_cannot_show_a_field_that_a_rule_hid()
    {
        // Review SV-03: a Prepare rule hides WorkDescription; an effect on Category tries to show it again.
        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.json", """{ "Roles": [ "@Admin" ], "Disabled": false }""",
            """{ "Roles": [ "@Admin" ], "Disabled": false }, { "Roles": [ "Prepare" ], "HideControls": [ "WorkDescription" ] }""")
            .Replace("TTDV2.json", "\"Name\": \"Category\",", "\"Name\": \"Category\", \"Effects\": [ { \"When\": { \"Equals\": \"OTHER\" }, \"Show\": [ \"WorkDescription\" ] } ],");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        CPApplicationSection loaded = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = 0 });
        Assert.False(Control(loaded, "WorkDescription").Visible); // the rule hides it

        CPAtlasPage input = NewInput();
        input.Outputs!.Single(x => x.Name == "Category").Value = "OTHER";
        input.Outputs!.Single(x => x.Name == "WorkDescription").Value = "sent anyway";

        CPAtlasPage result = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);

        Assert.Equal(0, result.ID);
        Assert.Contains(result.Errors!, x => x.Field == "WorkDescription" && x.Code == "NotAllowed");
    }

    [Fact]
    public async Task A_field_that_a_rule_hides_has_no_value_in_the_load_the_save_result_or_the_History()
    {
        // Review 3 RV-02: hidden on the client only is not enough; the value must not be in the responses.
        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.json", """{ "Roles": [ "@Admin" ], "Disabled": false }""",
            """{ "Roles": [ "@Admin" ], "Disabled": false }, { "Roles": [ "Prepare" ], "HideControls": [ "Category" ] }""");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        int id = await InsertAsync(createdBy: Prep, taxPreparer: Prep);

        // The owner changes Category, so the History has a Category entry.
        CPApplicationSection ownerView = custom.Service(_registry, Owner, Ttdv2Roles.Owner).LoadPage(new CPAtlasPage { ID = id });
        Assert.Equal("TAX_N", Control(ownerView, "Category").Value);
        CPAtlasPage ownerSave = custom.Service(_registry, Owner, Ttdv2Roles.Owner)
            .SavePage(EditInput(id, ownerView.Version, ("Category", "OTHER")), NoFiles);
        Assert.True(ownerSave.Errors is null or { Count: 0 }, Describe(ownerSave));
        Assert.Contains(ownerSave.Outputs!, x => x.Name == "Category" && x.Value == "OTHER");

        // 1. Load: no value and no options.
        CPApplicationSection loaded = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).LoadPage(new CPAtlasPage { ID = id });
        CPApplicationControl category = Control(loaded, "Category");
        Assert.False(category.Visible);
        Assert.Null(category.Value);
        Assert.True(category.Options is null or { Count: 0 });
        Assert.Equal("Smith Family Trust", Control(loaded, "AccountName").Value); // other fields keep their values

        // 2. Save result: no output for the hidden field.
        CPAtlasPage saved = custom.Service(_registry, Prep, Ttdv2Roles.Prepare)
            .SavePage(EditInput(id, loaded.Version, ("WorkDescription", "Prep text")), NoFiles);
        Assert.True(saved.Errors is null or { Count: 0 }, Describe(saved));
        Assert.DoesNotContain(saved.Outputs!, x => x.Name == "Category");
        Assert.Contains(saved.Outputs!, x => x.Name == "WorkDescription" && x.Value == "Prep text");

        // 3. History: the entry stays, the values are ***. The owner still sees them.
        string prepHistory = string.Join(' ',
            custom.Service(_registry, Prep, Ttdv2Roles.Prepare).GetAuditLog("TTDV2", id).Select(x => x.LogMessage));
        Assert.Contains("\"Name\":\"Category\"", prepHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("OTHER", prepHistory, StringComparison.Ordinal);
        Assert.DoesNotContain("TAX_N", prepHistory, StringComparison.Ordinal);
        Assert.Contains("Prep text", prepHistory, StringComparison.Ordinal);

        string ownerHistory = string.Join(' ',
            custom.Service(_registry, Owner, Ttdv2Roles.Owner).GetAuditLog("TTDV2", id).Select(x => x.LogMessage));
        Assert.Contains("OTHER", ownerHistory, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_server_applies_Set_and_Disable_effects_as_the_client_does()
    {
        // Review SV-04: Category OTHER sets WorkDescription (server value wins) and disables TaxYear.
        using ConfigCopy config = new ConfigCopy().Replace("TTDV2.json", "\"Name\": \"Category\",",
            "\"Name\": \"Category\", \"Effects\": [ { \"When\": { \"Equals\": \"OTHER\" }, \"Set\": { \"WorkDescription\": \"Other work\" }, \"Disable\": [ \"TaxYear\" ] } ],");
        using SectionHarness custom = new(database, AtlasAccessMode.Enforce, config.Folder);
        CPAtlasPage input = NewInput();
        input.Outputs!.Single(x => x.Name == "Category").Value = "OTHER";
        input.Outputs!.Single(x => x.Name == "WorkDescription").Value = "client text";

        CPAtlasPage changedYear = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);
        Assert.Contains(changedYear.Errors!, x => x.Field == "TaxYear" && x.Code == "NotAllowed"); // disabled by the effect

        input.Outputs!.RemoveAll(x => x.Name == "TaxYear");
        CPAtlasPage saved = custom.Service(_registry, Prep, Ttdv2Roles.Prepare).SavePage(input, NoFiles);
        Assert.True(saved.ID > 0, Describe(saved));
        Assert.Equal("Other work", await database.ScalarAsync($"SELECT WorkDescription FROM TTDV2.General WHERE ID = {saved.ID}"));
    }

    // ---------- Helpers ----------

    private ConfigSectionService Svc(string lanId, params AtlasRole[] roles) => _harness.Service(_registry, lanId, roles);

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

    private Task<int> InsertAsync(string createdBy, string taxPreparer, int taxYear = 2026, string weekEnding = "2026-10-18", int monday = 60) =>
        database.InsertGeneralAsync(
            ("Account", "100100 - Smith Family Trust"), ("AccountNumber", "100100"), ("AccountName", "Smith Family Trust"),
            ("TaxYear", taxYear), ("Category", "TAX_N"), ("WeekEnding", DateTime.Parse(weekEnding, System.Globalization.CultureInfo.InvariantCulture)),
            ("MondayMinute", monday), ("WeeklyTotal", monday), ("TaxPreparerLanId", taxPreparer), ("CreatedBy", createdBy),
            ("CreatedDate", new DateTime(2026, 10, 1)), ("IsDeleted", false));
}
