using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Script 045 renames the TTDV2.AuditLog columns RefID and RefType (an older 040) to the target names
/// ReferenceID and ReferenceType, and keeps the rows. Own database (it changes columns).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AuditReferenceColumnsScriptTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _db = new() { WorkflowEngine = false };
    private static readonly string Script =
        Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1", "045-ttdv2-audit-reference-columns.sql");

    private const string Columns = """
        SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) FROM sys.columns
        WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name IN (N'RefID', N'RefType', N'ReferenceID', N'ReferenceType')
        """;
    private const string Indexes = """
        SELECT STRING_AGG(name, ',') WITHIN GROUP (ORDER BY name) FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name LIKE N'IX_AuditLog_%'
        """;

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task A_new_database_has_the_target_names_and_045_changes_nothing()
    {
        Assert.Equal("ReferenceID,ReferenceType", await _db.ScalarAsync(Columns));
        Assert.Equal("IX_AuditLog_ReferenceID", await _db.ScalarAsync(Indexes));

        await _db.RunScriptFileAsync(Script);

        Assert.Equal("ReferenceID,ReferenceType", await _db.ScalarAsync(Columns));
        Assert.Equal("IX_AuditLog_ReferenceID", await _db.ScalarAsync(Indexes));
        Assert.Equal(1, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'045-ttdv2-audit-reference-columns.sql'"));
    }

    [Fact]
    public async Task A_run_on_the_old_names_renames_the_columns_and_the_index_and_keeps_the_rows()
    {
        await MakeOldNamesAsync();
        await _db.ScalarAsync("""
            INSERT INTO TTDV2.AuditLog (RefID, RefType, CreatedBy, LogMessage) VALUES (7, 'TTDV2', 'Dana', N'[]'), (8, 'TTD', 'Lee', N'[]');
            """);

        await _db.RunScriptFileAsync(Script);

        Assert.Equal("ReferenceID,ReferenceType", await _db.ScalarAsync(Columns));
        Assert.Equal("IX_AuditLog_ReferenceID", await _db.ScalarAsync(Indexes));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.AuditLog WHERE ReferenceID = 7 AND ReferenceType = 'TTDV2'"));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.AuditLog WHERE ReferenceID = 8 AND ReferenceType = 'TTD'"));

        await _db.RunScriptFileAsync(Script); // safe to run again
        Assert.Equal("ReferenceID,ReferenceType", await _db.ScalarAsync(Columns));
    }

    [Fact]
    public async Task A_table_with_the_old_and_the_new_names_stops_the_script_and_changes_nothing()
    {
        await _db.ScalarAsync("ALTER TABLE TTDV2.AuditLog ADD RefID int NULL;");

        SqlException error = await Assert.ThrowsAsync<SqlException>(() => _db.RunScriptFileAsync(Script));

        Assert.Equal(55021, error.Number);
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name = N'RefID'"));
        Assert.Equal("ReferenceID,ReferenceType", await _db.ScalarAsync(Columns.Replace("N'RefID', ", "")));
    }

    [Fact]
    public async Task Script_040_on_the_old_names_adds_no_second_index()
    {
        await MakeOldNamesAsync();

        await _db.RunScriptFileAsync(Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1", "040-ttdv2-app.sql"),
            ("<RUN_BY>", "test.runner"));

        Assert.Equal("RefID,RefType", await _db.ScalarAsync(Columns));
        Assert.Equal("IX_AuditLog_RefID", await _db.ScalarAsync(Indexes));
    }

    /// <summary>Gives TTDV2.AuditLog the names of an older 040.</summary>
    private Task MakeOldNamesAsync() => _db.ScalarAsync("""
        EXEC sp_rename N'TTDV2.AuditLog.ReferenceID', N'RefID', N'COLUMN';
        EXEC sp_rename N'TTDV2.AuditLog.ReferenceType', N'RefType', N'COLUMN';
        EXEC sp_rename N'TTDV2.AuditLog.IX_AuditLog_ReferenceID', N'IX_AuditLog_RefID', N'INDEX';
        SELECT 1;
        """);
}
