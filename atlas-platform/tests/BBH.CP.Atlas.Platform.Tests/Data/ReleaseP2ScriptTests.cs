using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// The release P2 scripts 100 to 140 (module 25, section 4) on the target shape of CFP.Inflow, CFP.InflowAttachment
/// and AIN.AccountInventory (local/sql/020-local-cfp-ain-tables.sql). Own database (the scripts change tables).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ReleaseP2ScriptTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _db = new() { ReleaseP2 = false, WorkflowEngine = false };

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Each_script_runs_two_times_and_changes_the_target_tables_in_place()
    {
        await ReleaseP2.MakeTargetTablesAsync(_db);
        await _db.ScalarAsync("""
            INSERT INTO CFP.Inflow (AccountNumber, Description, Comment, CreatedDate, IsDeleted)
            VALUES ('700100', 'Old description', 'Old comment', '2026-01-15', 0);
            INSERT INTO CFP.InflowAttachment (InflowId, Type, FileName) VALUES (10000, 'FlowAttachments', 'old.pdf');
            """);

        await ReleaseP2.RunScriptsAsync(_db);
        await ReleaseP2.RunScriptsAsync(_db); // safe to run again

        Assert.Equal(
            "CashSubmittedDate datetime 1 0;Comment nvarchar -1 0;Description nvarchar -1 0;DestructionDate datetime 1 1;" +
            "FundsReceived bit 1 0;RMCC varchar 1000 0;RowVersion timestamp 0 0;Status varchar 100 0;WorkflowState varchar 100 0",
            await Columns("CFP.Inflow", "Description", "Comment", "RowVersion", "WorkflowState", "Status", "FundsReceived",
                "CashSubmittedDate", "RMCC", "DestructionDate"));
        Assert.Equal(1, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM CFP.Inflow WHERE Description = N'Old description' AND Comment = N'Old comment' " +
            "AND DestructionDate = '2033-01-15'"));

        Assert.Equal(
            "ContentType varchar 200 0;CreatedByLanId nvarchar 50 0;FileName nvarchar 1000 0;ModifiedByLanId nvarchar 50 0;SizeBytes bigint 1 0",
            await Columns("CFP.InflowAttachment", "FileName", "ContentType", "SizeBytes", "CreatedByLanId", "ModifiedByLanId"));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM CFP.InflowAttachment WHERE FileName = N'old.pdf'"));
        Assert.Equal(1, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'CFP.InflowAttachment') AND name = N'IX_InflowAttachment_InflowId'"));

        Assert.Equal(
            "CreatedBy nvarchar 50 0;CreatedByLanId nvarchar 50 0;CreatedDate datetime 1 0;ID int 0 0;LogMessage nvarchar -1 0;" +
            "ReferenceID int 1 0;ReferenceType varchar 50 0",
            await Columns("CFP.AuditLog", "ID", "ReferenceID", "ReferenceType", "CreatedBy", "CreatedByLanId", "CreatedDate", "LogMessage"));
        Assert.Equal(1, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'CFP.AuditLog') AND name = N'IX_AuditLog_ReferenceID'"));

        Assert.Equal("RMCC varchar 1000 0", await Columns("AIN.AccountInventory", "RMCC"));

        Assert.Equal("Client Flows Portal - Inflow", await _db.ScalarAsync(
            "SELECT Name FROM dbo.CP_Atlas_Application WHERE Code = 'CFPINFLOW' AND ParentID IS NULL AND IsDeleted = 0"));
        Assert.Equal("Read 0 0 1;Write 0 1 1", await _db.ScalarAsync("""
            SELECT STRING_AGG(e.Name + ' ' + CAST(e.IsAdmin AS varchar(1)) + ' ' + CAST(e.IsWrite AS varchar(1)) + ' ' + CAST(e.IsRead AS varchar(1)), ';')
                   WITHIN GROUP (ORDER BY e.Name)
            FROM dbo.CP_Atlas_Entitlement AS e
            INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
            WHERE a.Code = 'CFPINFLOW'
            """));

        Assert.Equal(5, await _db.ScalarAsync("SELECT COUNT(*) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2'"));
    }

    [Theory]
    [InlineData("100-cfp-inflow-columns.sql", 55100)]
    [InlineData("110-cfp-inflow-attachment-columns.sql", 55110)]
    [InlineData("120-cfp-audit-log.sql", 55120)]
    [InlineData("140-ain-account-inventory-rmcc.sql", 55140)]
    public async Task A_script_stops_and_logs_nothing_when_its_target_table_is_not_there(string script, int number)
    {
        SqlException error = await Assert.ThrowsAsync<SqlException>(() => _db.RunScriptFileAsync(ReleaseP2.Path(script)));

        Assert.Equal(number, error.Number);
        Assert.Equal(0, await _db.ScalarAsync("SELECT COUNT(*) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2'"));
    }

    [Fact]
    public async Task The_app_script_needs_RunBy_and_stops_at_a_deleted_app_row()
    {
        SqlException noRunBy = await Assert.ThrowsAsync<SqlException>(() => _db.RunScriptFileAsync(ReleaseP2.Path("130-cfpinflow-app.sql")));
        Assert.Equal(55130, noRunBy.Number);

        await _db.ScalarAsync("""
            INSERT INTO dbo.CP_Atlas_Application (Name, Code, CreatedBy, CreatedDate, IsDeleted, ParentID)
            VALUES ('Client Flows Portal - Inflow', 'CFPINFLOW', 'test.runner', GETUTCDATE(), 1, NULL);
            """);
        SqlException deleted = await Assert.ThrowsAsync<SqlException>(() =>
            _db.RunScriptFileAsync(ReleaseP2.Path("130-cfpinflow-app.sql"), ("<RUN_BY>", "test.runner")));
        Assert.Equal(55131, deleted.Number);
        Assert.Equal(0, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.CP_Atlas_Entitlement AS e INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID WHERE a.Code = 'CFPINFLOW'"));
    }

    [Fact]
    public async Task A_RowVersion_column_of_another_type_stops_the_column_script()
    {
        await ReleaseP2.MakeTargetTablesAsync(_db);
        await _db.ScalarAsync("ALTER TABLE CFP.Inflow ADD RowVersion int NULL;");

        SqlException error = await Assert.ThrowsAsync<SqlException>(() => _db.RunScriptFileAsync(ReleaseP2.Path("100-cfp-inflow-columns.sql")));

        Assert.Equal(55101, error.Number);
        Assert.Equal(0, await _db.ScalarAsync("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CFP.Inflow') AND name = N'WorkflowState'"));
    }

    // "name type size computed" for each column, in name order. Size: the length in characters for text (-1 = max),
    // else 1 for a nullable column and 0 for NOT NULL.
    private async Task<object?> Columns(string table, params string[] names) =>
        await _db.ScalarAsync($"""
            SELECT STRING_AGG(c.name + ' ' + TYPE_NAME(c.system_type_id) + ' ' +
                       CAST(CASE WHEN TYPE_NAME(c.system_type_id) IN ('varchar', 'nvarchar')
                                 THEN CASE WHEN c.max_length = -1 THEN -1 WHEN TYPE_NAME(c.system_type_id) = 'nvarchar' THEN c.max_length / 2 ELSE c.max_length END
                                 ELSE CAST(c.is_nullable AS int) END AS varchar(10)) + ' ' + CAST(c.is_computed AS varchar(1)), ';')
                   WITHIN GROUP (ORDER BY c.name)
            FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(N'{table}') AND c.name IN ({string.Join(", ", names.Select(x => $"N'{x}'"))})
            """);
}

/// <summary>Runs the local target tables and the release P2 scripts on a test database.</summary>
internal static class ReleaseP2
{
    public static readonly string[] Scripts =
    [
        "100-cfp-inflow-columns.sql", "110-cfp-inflow-attachment-columns.sql", "120-cfp-audit-log.sql",
        "130-cfpinflow-app.sql", "140-ain-account-inventory-rmcc.sql"
    ];

    public static string Path(string script) =>
        System.IO.Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p2", script);

    public static Task MakeTargetTablesAsync(SqlServerDatabaseFixture db) =>
        db.RunScriptFileAsync(System.IO.Path.Combine(TestPaths.AtlasPlatformRoot(), "local", "sql", "020-local-cfp-ain-tables.sql"));

    public static async Task RunScriptsAsync(SqlServerDatabaseFixture db)
    {
        foreach (string script in Scripts)
            await db.RunScriptFileAsync(Path(script), ("<RUN_BY>", "test.runner"));
    }
}
