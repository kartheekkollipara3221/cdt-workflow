using Microsoft.Data.SqlClient;

namespace WorkflowEngine.Tests;

internal static class TestSqlConnectionFactory
{
    private const string DefaultConnectionString =
        "Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionString(string databaseName)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var source = string.IsNullOrWhiteSpace(configured)
            ? DefaultConnectionString
            : configured;

        return ConnectionStringFromSource(databaseName, source);
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
