using System.Reflection;
using Microsoft.Data.SqlClient;

namespace WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowSchemaInitializer
{
    public static void Apply(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        // Ensure the database exists by connecting to master first.
        builder.InitialCatalog = "master";
        using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            masterConnection.Open();
            using var createCmd = masterConnection.CreateCommand();
            createCmd.CommandText = $"IF DB_ID(N'{databaseName}') IS NULL CREATE DATABASE [{databaseName}]";
            createCmd.CommandTimeout = 120;
            createCmd.ExecuteNonQuery();
        }

        // Read the embedded schema script.
        var assembly = typeof(WorkflowSchemaInitializer).Assembly;
        const string resourceName = "WorkflowEngine.Infrastructure.Persistence.Scripts.schema.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        var schemaSql = reader.ReadToEnd();

        // Apply the schema to the target database.
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = schemaSql;
        cmd.CommandTimeout = 120;
        cmd.ExecuteNonQuery();
    }
}
