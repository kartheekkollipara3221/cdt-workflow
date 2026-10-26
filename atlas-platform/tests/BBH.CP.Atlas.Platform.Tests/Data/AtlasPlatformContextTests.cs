using BBH.CP.Atlas.Data;
using BBH.CP.Atlas.Data.Models;
using BBH.CP.Atlas.Data.Platform;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Platform.Tests.Data;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public class AtlasPlatformContextTests(SqlServerDatabaseFixture database)
{
    private const string General = "TTDV2.General";
    private const string AuditLog = "TTDV2.AuditLog";

    [Fact]
    public async Task Metadata_reader_reads_the_TTDV2_columns()
    {
        await using SqlConnection connection = new(database.ConnectionString);

        AtlasTable? table = await new AtlasColumnMetadataReader().ReadAsync(connection, General);

        Assert.NotNull(table);
        Assert.Equal("TTDV2", table.Schema);
        Assert.True(table.Find("ID")!.IsIdentity);
        Assert.True(table.Find("RowVersion")!.IsRowVersion);
        Assert.Equal(typeof(int?), table.Find("MondayMinute")!.ClrType);
        Assert.Equal(typeof(short?), table.Find("TaxYear")!.ClrType);
        Assert.Equal("date", table.Find("WeekEnding")!.StoreType);
        Assert.Equal("nvarchar(max)", table.Find("WorkDescription")!.StoreType); // review DA-12
        Assert.Equal("varchar(100)", table.Find("AccountNumber")!.StoreType);
        Assert.Equal(50, table.Find("TaxPreparerLanId")!.MaxLength);
        Assert.True(table.HasSoftDelete);
    }

    [Fact]
    public async Task Unicode_free_text_is_stored_and_loaded_with_no_loss()
    {
        // Review DA-12: the free-text columns are nvarchar, so pasted Unicode text is not stored as '?'.
        AtlasTableRegistry registry = await LoadRegistryAsync();
        const string text = "東京 Zürich ✓ €";
        int id;

        await using (AtlasPlatformContext context = CreateContext(registry))
        {
            Dictionary<string, object?> record = registry.Get(General).NewRecord();
            record["Account"] = text;
            record["AccountName"] = text;
            record["WorkDescription"] = text;
            context.Records(General).Add(record);
            await context.SaveChangesAsync();
            id = (int)record["ID"]!;
        }

        await using (AtlasPlatformContext context = CreateContext(registry))
        {
            Dictionary<string, object?> loaded = (await context.FindActiveAsync(General, id))!;
            Assert.Equal(text, loaded["Account"]);
            Assert.Equal(text, loaded["AccountName"]);
            Assert.Equal(text, loaded["WorkDescription"]);
        }
    }

    [Fact]
    public async Task Metadata_reader_reports_a_missing_table()
    {
        await using SqlConnection connection = new(database.ConnectionString);
        AtlasTableRegistry registry = new();

        IReadOnlyList<string> missing = await new AtlasColumnMetadataReader()
            .LoadAsync(connection, registry, [General, "TTDV2.NoSuchTable"]);

        Assert.Equal(["TTDV2.NoSuchTable"], missing);
        Assert.True(registry.TryGet(General, out _));
    }

    [Fact]
    public async Task A_record_is_inserted_and_loaded_by_key_with_no_class()
    {
        AtlasTableRegistry registry = await LoadRegistryAsync();
        int id;

        await using (AtlasPlatformContext context = CreateContext(registry))
        {
            Dictionary<string, object?> record = registry.Get(General).NewRecord();
            record["AccountNumber"] = "100100";
            record["MondayMinute"] = 90;
            record["TaxYear"] = (short)2026;
            record["WeekEnding"] = new DateTime(2026, 10, 25);
            record["CreatedBy"] = "jdoe";
            record["IsDeleted"] = false;
            context.Records(General).Add(record);
            await context.SaveChangesAsync();
            id = (int)record["ID"]!;
        }

        Assert.True(id >= 10000);

        await using (AtlasPlatformContext context = CreateContext(registry))
        {
            Dictionary<string, object?>? loaded = await context.FindActiveAsync(General, id);

            Assert.NotNull(loaded);
            Assert.Equal("100100", loaded["AccountNumber"]);
            Assert.Equal(90, loaded["MondayMinute"]);
            Assert.Equal((short)2026, loaded["TaxYear"]);
            Assert.Equal(new DateTime(2026, 10, 25), loaded["WeekEnding"]);
            Assert.Null(loaded["TuesdayMinute"]);
            Assert.NotNull(loaded["RowVersion"]);
        }
    }

    [Fact]
    public async Task A_soft_deleted_record_is_not_found()
    {
        AtlasTableRegistry registry = await LoadRegistryAsync();
        int id = await InsertAsync(registry, isDeleted: true);

        await using AtlasPlatformContext context = CreateContext(registry);

        Assert.Null(await context.FindActiveAsync(General, id));
        Assert.Null(await context.FindActiveAsync(General, int.MaxValue));
    }

    [Fact]
    public async Task A_save_of_an_old_version_gives_a_concurrency_error()
    {
        AtlasTableRegistry registry = await LoadRegistryAsync();
        int id = await InsertAsync(registry);

        await using AtlasPlatformContext first = CreateContext(registry);
        await using AtlasPlatformContext second = CreateContext(registry);
        Dictionary<string, object?> firstCopy = (await first.FindActiveAsync(General, id))!;
        Dictionary<string, object?> secondCopy = (await second.FindActiveAsync(General, id))!;

        firstCopy["MondayMinute"] = 60;
        await first.SaveChangesAsync();

        secondCopy["MondayMinute"] = 30;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task The_database_check_refuses_minutes_over_1440()
    {
        AtlasTableRegistry registry = await LoadRegistryAsync();
        await using AtlasPlatformContext context = CreateContext(registry);
        Dictionary<string, object?> record = registry.Get(General).NewRecord();
        record["MondayMinute"] = 1441;
        context.Records(General).Add(record);

        DbUpdateException error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_TTDV2_General_Minutes", error.InnerException?.Message);
    }

    [Fact]
    public async Task The_platform_context_joins_the_main_transaction()
    {
        AtlasTableRegistry registry = await LoadRegistryAsync();
        string marker = "TX" + Guid.NewGuid().ToString("N")[..10];

        await using SqlConnection connection = new(database.ConnectionString);
        await using (CPSharepointContext main = new(
            new DbContextOptionsBuilder<CPSharepointContext>().UseSqlServer(connection).Options,
            new ConfigurationBuilder().Build()))
        await using (AtlasPlatformContext platform = new(
            new DbContextOptionsBuilder<AtlasPlatformContext>().UseSqlServer(connection).Options, registry))
        {
            await using var transaction = await platform.BeginSharedTransactionAsync(main);

            main.CPAtlasApplications.Add(new CPAtlasApplication { Code = marker[..10], Name = marker, IsDeleted = false });
            await main.SaveChangesAsync();

            Dictionary<string, object?> record = registry.Get(General).NewRecord();
            record["AccountNumber"] = marker;
            platform.Records(General).Add(record);
            await platform.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await database.ScalarAsync($"SELECT COUNT(*) FROM dbo.CP_Atlas_Application WHERE Name = '{marker}'"));
        Assert.Equal(0, await database.ScalarAsync($"SELECT COUNT(*) FROM TTDV2.General WHERE AccountNumber = '{marker}'"));
    }

    [Fact]
    public async Task Two_registries_with_different_tables_get_different_models()
    {
        AtlasTableRegistry generalOnly = await LoadRegistryAsync(General);
        AtlasTableRegistry both = await LoadRegistryAsync(General, AuditLog);

        await using AtlasPlatformContext first = CreateContext(generalOnly);
        await using AtlasPlatformContext second = CreateContext(both);

        Assert.Null(first.Model.FindEntityType(AuditLog));
        Assert.NotNull(second.Model.FindEntityType(AuditLog));
    }

    [Fact]
    public async Task An_insert_with_no_value_uses_the_database_default()
    {
        string name = "Defaults" + Guid.NewGuid().ToString("N")[..6];
        await database.ScalarAsync($"""
            CREATE TABLE TTDV2.{name} (
                ID int IDENTITY PRIMARY KEY,
                Status varchar(20) NOT NULL DEFAULT 'Draft',
                Note nvarchar(50) NULL DEFAULT N'none',
                Active bit NULL DEFAULT 1,
                Flag bit NOT NULL DEFAULT 1);
            """);
        try
        {
            AtlasTableRegistry registry = await LoadRegistryAsync($"TTDV2.{name}");
            AtlasTable table = registry.Get($"TTDV2.{name}");
            Assert.True(table.Find("Status")!.UsesDatabaseDefault);
            Assert.True(table.Find("Note")!.UsesDatabaseDefault);
            Assert.False(table.Find("Flag")!.UsesDatabaseDefault); // false from the user must stay false

            int id;
            await using (AtlasPlatformContext context = CreateContext(registry))
            {
                Dictionary<string, object?> record = table.NewRecord();
                record["Active"] = false;
                record["Flag"] = false;
                context.Records(table.FullName).Add(record);
                await context.SaveChangesAsync();
                id = (int)record["ID"]!;
            }

            await using (AtlasPlatformContext context = CreateContext(registry))
            {
                Dictionary<string, object?> loaded = (await context.FindActiveAsync(table.FullName, id))!;
                Assert.Equal("Draft", loaded["Status"]);
                Assert.Equal("none", loaded["Note"]);
                Assert.Equal(false, loaded["Active"]);
                Assert.Equal(false, loaded["Flag"]);
            }
        }
        finally
        {
            await database.ScalarAsync($"DROP TABLE TTDV2.{name};");
        }
    }

    private async Task<AtlasTableRegistry> LoadRegistryAsync(params string[] tables)
    {
        AtlasTableRegistry registry = new();
        await using SqlConnection connection = new(database.ConnectionString);
        IReadOnlyList<string> missing = await new AtlasColumnMetadataReader()
            .LoadAsync(connection, registry, tables.Length == 0 ? [General, AuditLog] : tables);
        Assert.Empty(missing);
        return registry;
    }

    private AtlasPlatformContext CreateContext(AtlasTableRegistry registry) =>
        new(new DbContextOptionsBuilder<AtlasPlatformContext>().UseSqlServer(database.ConnectionString).Options, registry);

    private async Task<int> InsertAsync(AtlasTableRegistry registry, bool isDeleted = false)
    {
        await using AtlasPlatformContext context = CreateContext(registry);
        Dictionary<string, object?> record = registry.Get(General).NewRecord();
        record["AccountNumber"] = "100200";
        record["IsDeleted"] = isDeleted;
        context.Records(General).Add(record);
        await context.SaveChangesAsync();
        return (int)record["ID"]!;
    }
}
