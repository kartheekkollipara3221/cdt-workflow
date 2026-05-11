using Microsoft.Data.SqlClient;

namespace WorkflowEngine.Tests;

internal static class TestSqlConnectionFactory
{
    public static string ConnectionString(string databaseName)
    {
        var source = Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            ThrowMissingConnectionString();
        }

        return ConnectionStringFromSource(databaseName, source!);
    }

    internal static void ThrowMissingConnectionString()
    {
        throw new InvalidOperationException(
            "SQL-backed workflow tests require WORKFLOW_ENGINE_TEST_CONNECTION_STRING or ConnectionStrings__DefaultConnection.");
    }

    internal static string ConnectionStringFromSource(string databaseName, string source)
    {
        var builder = new SqlConnectionStringBuilder
        {
            ConnectionString = source
        };
        builder.InitialCatalog = databaseName;
        return builder.ConnectionString;
    }
}
