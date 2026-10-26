using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Review DA-08 (B59): script 040 makes TTDV2.General.IsDeleted bit NOT NULL, also in a database where an earlier
/// run made it bit NULL. Own database (it changes the column).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class Ttdv2AppScriptTests : IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _db = new();
    private static readonly string Script = Path.Combine(TestPaths.AtlasPlatformRoot(), "sql", "release-p1", "040-ttdv2-app.sql");

    private const string IsNullable =
        "SELECT CAST(is_nullable AS int) FROM sys.columns WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IsDeleted'";
    private const string FilteredIndexCount =
        "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.General') AND has_filter = 1 " +
        "AND name IN (N'IX_General_CreatedBy', N'IX_General_TaxPreparerLanId')";

    public Task InitializeAsync() => _db.InitializeAsync();

    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task A_new_database_gets_IsDeleted_NOT_NULL_with_the_filtered_indexes()
    {
        Assert.Equal(0, await _db.ScalarAsync(IsNullable));
        Assert.Equal(2, await _db.ScalarAsync(FilteredIndexCount));
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
        Assert.Equal(2, await _db.ScalarAsync(FilteredIndexCount));
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
        Assert.Equal(2, await _db.ScalarAsync(FilteredIndexCount));
        Assert.Equal(1, await _db.ScalarAsync("SELECT COUNT(*) FROM TTDV2.General WHERE IsDeleted IS NULL"));
    }

    // The column as the script made it before DA-08: bit NULL, with the same default and filtered indexes.
    private Task MakeEarlierSchemaAsync() => _db.ScalarAsync("""
        DROP INDEX IX_General_CreatedBy ON TTDV2.General;
        DROP INDEX IX_General_TaxPreparerLanId ON TTDV2.General;
        ALTER TABLE TTDV2.General ALTER COLUMN IsDeleted bit NULL;
        CREATE INDEX IX_General_CreatedBy ON TTDV2.General (CreatedBy) WHERE IsDeleted = 0;
        CREATE INDEX IX_General_TaxPreparerLanId ON TTDV2.General (TaxPreparerLanId) WHERE IsDeleted = 0;
        """);

    private Task RunScriptAsync() => _db.RunScriptFileAsync(Script, ("<RUN_BY>", "test.runner"));
}
