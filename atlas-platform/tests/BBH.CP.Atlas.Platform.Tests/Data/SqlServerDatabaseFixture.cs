using System.Text.RegularExpressions;
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

    public async Task RunScriptFileAsync(string path, params (string Placeholder, string Value)[] replacements)
    {
        string script = await File.ReadAllTextAsync(path);
        foreach ((string placeholder, string value) in replacements)
            script = script.Replace(placeholder, value, StringComparison.Ordinal);

        await using SqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        foreach (string batch in GoSeparator().Split(script).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
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
