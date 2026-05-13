using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

public sealed partial class DatabaseFirstSchemaInitializer(WorkflowDbContext dbContext)
{
    private static readonly string[] SchemaResourceNames =
    [
        "WorkflowEngine.DatabaseFirst.Schema.001-schema.sql",
        "WorkflowEngine.DatabaseFirst.Schema.002-stored-procedures.sql"
    ];

    public void InitializeSchema()
    {
        EnsureDatabaseExists();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        try
        {
            foreach (var resourceName in SchemaResourceNames)
            {
                foreach (var batch in SplitBatches(ReadSchemaScript(resourceName)))
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = batch;
                    command.CommandTimeout = 60;
                    command.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            connection.Close();
        }
    }

    private void EnsureDatabaseExists()
    {
        var builder = new SqlConnectionStringBuilder(dbContext.Database.GetDbConnection().ConnectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("The workflow database connection string must specify a database name.");
        }

        builder.InitialCatalog = "master";
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{SqlLiteral(databaseName)}') IS NULL CREATE DATABASE {SqlIdentifier(databaseName)};";
        command.ExecuteNonQuery();
    }

    private static string ReadSchemaScript(string resourceName)
    {
        var assembly = typeof(DatabaseFirstSchemaInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded database-first schema resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitBatches(string script)
    {
        foreach (var batch in GoBatchRegex().Split(script))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                yield return batch;
            }
        }
    }

    private static string SqlIdentifier(string value)
    {
        return "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    private static string SqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoBatchRegex();
}
