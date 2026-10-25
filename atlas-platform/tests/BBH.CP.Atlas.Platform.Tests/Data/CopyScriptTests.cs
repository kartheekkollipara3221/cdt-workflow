using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Spec 12, section 5: the copy script (release P1, script 050) on a copy of TTD sample data, in its own database
/// (it uses IDENTITY_INSERT and revokes the TTD roles).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CopyScriptTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _db = new();
    private static readonly string Release = Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1");

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        await _db.RunScriptFileAsync(Path.Combine(TestPaths.AtlasPlatformRoot(), "local", "sql", "010-local-ttd-sample.sql"));
        await _db.SeedUserAsync("pprep", "Pat Preparer", "TTD", "Prepare");
        await _db.SeedUserAsync("oowner", "Olive Owner", "TTD", "Owner");
    }

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task The_copy_converts_the_values_keeps_the_IDs_moves_the_roles_and_is_safe_to_run_again()
    {
        await RunCopyAsync();

        Assert.Equal(3, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE ID IN (10001, 10002, 10003)"));
        Assert.Equal((short)2025, await _db.ScalarAsync("SELECT TaxYear FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal(new DateTime(2025, 3, 2), await _db.ScalarAsync("SELECT WeekEnding FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal(30, await _db.ScalarAsync("SELECT FridayMinute FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal(DBNull.Value, await _db.ScalarAsync("SELECT WednesdayMinute FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal(300, await _db.ScalarAsync("SELECT WeeklyTotal FROM TTDV2.General WHERE ID = 10002"));      // 999 corrected
        Assert.Equal("pprep", await _db.ScalarAsync("SELECT TaxPreparerLanId FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal("pprep", await _db.ScalarAsync("SELECT ModifiedBy FROM TTDV2.General WHERE ID = 10002"));
        Assert.Equal("system", await _db.ScalarAsync("SELECT CreatedBy FROM TTDV2.General WHERE ID = 10003"));
        Assert.Equal(true, await _db.ScalarAsync("SELECT IsDeleted FROM TTDV2.General WHERE ID = 10003"));
        // Eastern time to UTC: 2025-03-03 09:15 EST = 14:15 UTC.
        Assert.Equal(new DateTime(2025, 3, 3, 14, 15, 0), await _db.ScalarAsync("SELECT CreatedDate FROM TTDV2.General WHERE ID = 10001"));
        Assert.Equal(3, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.AuditLog WHERE RefType = 'TTD'"));

        Assert.Equal(0, await _db.ScalarAsync(RoleCount("TTD")));
        Assert.Equal(2, await _db.ScalarAsync(RoleCount("TTDV2")));

        Assert.Equal(1, await _db.ScalarAsync(LogCount("12-ttdv2-copy-from-ttd.sql")));

        await RunCopyAsync(); // safe to run again
        Assert.Equal(3, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE ID IN (10001, 10002, 10003)"));
        Assert.Equal(3, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.AuditLog WHERE RefType = 'TTD'"));
        Assert.Equal(1, await _db.ScalarAsync(LogCount("12-ttdv2-copy-from-ttd.sql")));
    }

    [Fact]
    public async Task A_failed_copy_writes_no_script_log_row_when_the_tool_goes_on_to_the_next_batch()
    {
        await _db.ScalarAsync("UPDATE TTD.General SET MondayMinute = '1:30' WHERE ID = 10002");

        List<string> errors = await _db.RunScriptFileLikeSsmsAsync(Path.Combine(Release, "050-ttdv2-copy-from-ttd.sql"),
            ("<SERVER_TIME_ZONE>", "Eastern Standard Time"), ("<RUN_BY>", "test.runner"));

        Assert.Contains(errors, x => x.Contains("cannot be converted", StringComparison.Ordinal));
        Assert.Equal(0, await _db.ScalarAsync(LogCount("12-ttdv2-copy-from-ttd.sql")));
    }

    [Fact]
    public async Task A_failed_app_script_writes_no_script_log_row_when_the_tool_goes_on_to_the_next_batch()
    {
        Assert.Equal(1, await _db.ScalarAsync(LogCount("12-ttdv2-app.sql"))); // the fixture ran it
        await _db.ScalarAsync("DELETE FROM dbo.CP_Atlas_ScriptLog WHERE ScriptName = N'12-ttdv2-app.sql'");

        // <RUN_BY> not set: the main batch stops; the result batch after GO still runs.
        List<string> errors = await _db.RunScriptFileLikeSsmsAsync(Path.Combine(Release, "040-ttdv2-app.sql"));

        Assert.Contains(errors, x => x.Contains("Set @RunBy", StringComparison.Ordinal));
        Assert.Equal(0, await _db.ScalarAsync(LogCount("12-ttdv2-app.sql")));
    }

    private static string LogCount(string script) =>
        $"SELECT COUNT(*) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'{script}'";

    [Fact]
    public async Task A_value_that_cannot_be_converted_stops_the_copy_and_copies_nothing()
    {
        await _db.ScalarAsync("UPDATE TTD.General SET MondayMinute = '1:30' WHERE ID = 10002");

        SqlException error = await Assert.ThrowsAsync<SqlException>(RunCopyAsync);

        Assert.Contains("cannot be converted", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General"));
        Assert.Equal(2, await _db.ScalarAsync(RoleCount("TTD")));
        Assert.Equal(0, await _db.ScalarAsync(LogCount("12-ttdv2-copy-from-ttd.sql")));
    }

    private Task RunCopyAsync() =>
        _db.RunScriptFileAsync(Path.Combine(Release, "050-ttdv2-copy-from-ttd.sql"),
            ("<SERVER_TIME_ZONE>", "Eastern Standard Time"), ("<RUN_BY>", "test.runner"));

    private static string RoleCount(string appCode) => $"""
        SELECT COUNT(*) FROM dbo.CP_Atlas_UserEntitlement AS ue
        INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
        INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = '{appCode}'
        WHERE ISNULL(ue.IsDeleted, 0) = 0
        """;
}
