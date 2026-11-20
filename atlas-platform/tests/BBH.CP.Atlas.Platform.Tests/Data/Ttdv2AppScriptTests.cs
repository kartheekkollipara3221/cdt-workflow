using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Review DA-08 (B59) and DA-12 (B60): script 040 makes TTDV2.General.IsDeleted bit NOT NULL and the free-text columns
/// nvarchar, also in a database where an earlier run made them bit NULL / varchar. Own database (it changes columns).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class Ttdv2AppScriptTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _db = new() { WorkflowEngine = false };
    private static readonly string Script = Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1", "040-ttdv2-app.sql");

    private const string IsNullable =
        "SELECT CAST(is_nullable AS int) FROM sys.columns WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IsDeleted'";
    private const string FilteredIndexCount =
        "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.General') AND has_filter = 1 " +
        "AND name IN (N'IX_General_CreatedBy', N'IX_General_TaxPreparerLanId', N'IX_General_ModifiedBy')";

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task A_new_database_gets_IsDeleted_NOT_NULL_with_the_filtered_indexes()
    {
        Assert.Equal(0, await _db.ScalarAsync(IsNullable));
        Assert.Equal(3, await _db.ScalarAsync(FilteredIndexCount));
    }

    [Fact]
    public async Task A_run_on_the_earlier_bit_NULL_column_makes_it_NOT_NULL_and_keeps_the_rows_and_indexes()
    {
        await MakeEarlierSchemaAsync();
        await _db.ScalarAsync("""
            SET IDENTITY_INSERT TTDV2.General ON;
            INSERT INTO TTDV2.General (ID, AccountNumber, CreatedBy, IsDeleted) VALUES (20001, 'A1', 'pprep', 0), (20002, 'A2', 'pprep', 1);
            SET IDENTITY_INSERT TTDV2.General OFF;
            """);

        await RunScriptAsync();

        Assert.Equal(0, await _db.ScalarAsync(IsNullable));
        Assert.Equal(3, await _db.ScalarAsync(FilteredIndexCount));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE ID = 20001 AND IsDeleted = 0"));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE ID = 20002 AND IsDeleted = 1"));

        await RunScriptAsync(); // safe to run again
        Assert.Equal(0, await _db.ScalarAsync(IsNullable));
    }

    [Fact]
    public async Task A_NULL_IsDeleted_row_stops_the_script_and_changes_nothing()
    {
        await MakeEarlierSchemaAsync();
        await _db.ScalarAsync("INSERT INTO TTDV2.General (AccountNumber, IsDeleted) VALUES ('A3', NULL)");

        SqlException error = await Assert.ThrowsAsync<SqlException>(RunScriptAsync);

        Assert.Equal(55008, error.Number);
        Assert.Equal(1, await _db.ScalarAsync(IsNullable));
        Assert.Equal(2, await _db.ScalarAsync(FilteredIndexCount)); // the earlier 2 indexes; nothing added
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE IsDeleted IS NULL"));
    }

    [Fact]
    public async Task A_run_on_the_earlier_varchar_columns_makes_them_nvarchar_and_keeps_the_text()
    {
        await _db.ScalarAsync("""
            ALTER TABLE TTDV2.General ALTER COLUMN Account varchar(1000) NULL;
            ALTER TABLE TTDV2.General ALTER COLUMN AccountName varchar(1000) NULL;
            ALTER TABLE TTDV2.General ALTER COLUMN WorkDescription varchar(max) NULL;
            INSERT INTO TTDV2.General (Account, AccountName, WorkDescription) VALUES ('Acct 1', 'Name 1', 'Work 1');
            """);

        await RunScriptAsync();

        Assert.Equal("nvarchar(1000) nvarchar(1000) nvarchar(-1)", await _db.ScalarAsync("""
            SELECT STRING_AGG(TYPE_NAME(system_type_id) + '(' + CAST(CASE WHEN max_length = -1 THEN -1 ELSE max_length / 2 END AS varchar(10)) + ')', ' ')
                   WITHIN GROUP (ORDER BY name)
            FROM sys.columns WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name IN (N'Account', N'AccountName', N'WorkDescription')
            """));
        Assert.Equal(1, await _db.ScalarAsync(
            "SELECT COUNT(*) FROM TTDV2.General WHERE Account = N'Acct 1' AND AccountName = N'Name 1' AND WorkDescription = N'Work 1'"));

        await RunScriptAsync(); // safe to run again
    }

    // The column as the script made it before DA-08: bit NULL, with the same default and filtered indexes.
    private Task MakeEarlierSchemaAsync() => _db.ScalarAsync("""
        DROP INDEX IX_General_CreatedBy ON TTDV2.General;
        DROP INDEX IX_General_TaxPreparerLanId ON TTDV2.General;
        DROP INDEX IF EXISTS IX_General_ModifiedBy ON TTDV2.General; -- not in the earlier schema (review 2 AR-05)
        ALTER TABLE TTDV2.General ALTER COLUMN IsDeleted bit NULL;
        CREATE INDEX IX_General_CreatedBy ON TTDV2.General (CreatedBy) WHERE IsDeleted = 0;
        CREATE INDEX IX_General_TaxPreparerLanId ON TTDV2.General (TaxPreparerLanId) WHERE IsDeleted = 0;
        """);

    private Task RunScriptAsync() => _db.RunScriptFileAsync(Script, ("<RUN_BY>", "test.runner"));
}
