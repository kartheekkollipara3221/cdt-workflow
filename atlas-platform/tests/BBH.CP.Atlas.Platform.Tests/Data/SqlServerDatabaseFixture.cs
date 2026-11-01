using System.Text.RegularExpressions;
using BBH.CP.Atlas.Data.Platform;
using Microsoft.Data.SqlClient;

namespace BBH.CP.Atlas.Platform.Tests.Data;

/// <summary>
/// Makes a throw-away database on the local SQL Server (Windows authentication, as the engine tests do), runs the
/// local core tables script and the P1 scripts, and drops the database at the end.
/// Server: environment variable ATLAS_TEST_SQLSERVER, default "localhost".
/// </summary>
public sealed partial class SqlServerDatabaseFixture : IAsyncLifetime
{
    private readonly string _server = Environment.GetEnvironmentVariable("ATLAS_TEST_SQLSERVER") ?? "localhost";

    public string DatabaseName { get; } = "AtlasPlatformTest_" + Guid.NewGuid().ToString("N")[..12];

    public string ConnectionString => Build(DatabaseName);

    /// <summary>The columns of TTDV2.General and TTDV2.AuditLog, read one time after the scripts (before any test changes a table).</summary>
    public AtlasTableRegistry Ttdv2Registry { get; } = new();

    public async Task InitializeAsync()
    {
        await using (SqlConnection master = new(Build("master")))
        {
            await master.OpenAsync();
            await using SqlCommand create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{DatabaseName}];";
            await create.ExecuteNonQueryAsync();
        }

        string atlasRoot = TestPaths.AtlasPlatformRoot();
        string specSql = Path.Combine(TestPaths.RepositoryRoot(), "docs", "atlas-platform", "sql");

        await RunScriptFileAsync(Path.Combine(atlasRoot, "local", "sql", "000-cp-atlas-core-tables.sql"));
        await RunScriptFileAsync(Path.Combine(specSql, "11-script-log.sql"));
        await RunScriptFileAsync(Path.Combine(specSql, "06-active-user-roles-view.sql"));
        await RunScriptFileAsync(Path.Combine(specSql, "12-ttdv2-app.sql"), ("<RUN_BY>", "test.runner"));

        await using SqlConnection connection = new(ConnectionString);
        await new AtlasColumnMetadataReader().LoadAsync(connection, Ttdv2Registry, ["TTDV2.General", "TTDV2.AuditLog"]);
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using SqlConnection master = new(Build("master"));
        await master.OpenAsync();
        await using SqlCommand drop = master.CreateCommand();
        drop.CommandText = $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END
            """;
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>Runs each batch; the first error stops the script (sqlcmd -b).</summary>
    public async Task RunScriptFileAsync(string path, params (string Placeholder, string Value)[] replacements) =>
        await RunBatchesAsync(path, stopOnError: true, replacements);

    /// <summary>
    /// Runs each batch like SSMS (and sqlcmd without -b): an error does not stop the next batch. Returns the errors.
    /// </summary>
    public Task<List<string>> RunScriptFileLikeSsmsAsync(string path, params (string Placeholder, string Value)[] replacements) =>
        RunBatchesAsync(path, stopOnError: false, replacements);

    private async Task<List<string>> RunBatchesAsync(string path, bool stopOnError, (string Placeholder, string Value)[] replacements)
    {
        string script = await File.ReadAllTextAsync(path);
        foreach ((string placeholder, string value) in replacements)
            script = script.Replace(placeholder, value, StringComparison.Ordinal);

        List<string> errors = [];
        await using SqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        foreach (string batch in GoSeparator().Split(script).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = batch;
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (SqlException ex) when (!stopOnError)
            {
                errors.Add(ex.Message);
            }
        }

        return errors;
    }

    /// <summary>Adds an active user (if not there) and grants the roles of the app (if not there).</summary>
    public async Task SeedUserAsync(string lanId, string displayName, string appCode, params string[] roles)
    {
        await using SqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_User WHERE LanID = @LanId)
                INSERT INTO dbo.CP_Atlas_User (LanID, DisplayName, IsDeleted) VALUES (@LanId, @DisplayName, 0);
            DECLARE @UserId int = (SELECT ID FROM dbo.CP_Atlas_User WHERE LanID = @LanId);
            INSERT INTO dbo.CP_Atlas_UserEntitlement (CP_Atlas_UserID, CP_Atlas_EntitlementID, IsDeleted)
            SELECT @UserId, e.ID, 0
            FROM dbo.CP_Atlas_Entitlement AS e
            INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
            WHERE a.Code = @AppCode AND e.Name IN (SELECT value FROM STRING_SPLIT(@Roles, ','))
              AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_UserEntitlement AS x
                              WHERE x.CP_Atlas_UserID = @UserId AND x.CP_Atlas_EntitlementID = e.ID);
            """;
        command.Parameters.AddWithValue("@LanId", lanId);
        command.Parameters.AddWithValue("@DisplayName", displayName);
        command.Parameters.AddWithValue("@AppCode", appCode);
        command.Parameters.AddWithValue("@Roles", string.Join(',', roles));
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Inserts one TTDV2.General row with the given column values and returns its ID.</summary>
    public async Task<int> InsertGeneralAsync(params (string Column, object Value)[] values)
    {
        await using SqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO TTDV2.General ({string.Join(", ", values.Select(x => x.Column))}) OUTPUT INSERTED.ID " +
            $"VALUES ({string.Join(", ", values.Select(x => "@" + x.Column))});";
        foreach ((string column, object value) in values)
            command.Parameters.AddWithValue("@" + column, value);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    public async Task<object?> ScalarAsync(string sql)
    {
        await using SqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private string Build(string database) =>
        new SqlConnectionStringBuilder
        {
            DataSource = _server,
            InitialCatalog = database,
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            Encrypt = false
        }.ConnectionString;

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerDatabaseFixture>
{
    public const string Name = "SqlServer";
}
