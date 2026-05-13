using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

namespace WorkflowEngine.Tests;

internal static class TestSqlConnectionFactory
{
    private const string LocalFallbackConnectionString =
        "Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionString(string databaseName)
    {
        var source = Environment.GetEnvironmentVariable("WORKFLOW_ENGINE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(source))
        {
            source = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            source = LocalFallbackConnectionString;
        }

        return ConnectionStringFromSource(databaseName, source!);
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

    public static DatabaseFirstWorkflowStore StoreFor(WorkflowDbContext dbContext)
    {
        return new DatabaseFirstWorkflowStore(
            new WorkflowSqlConnectionFactory(dbContext.Database.GetDbConnection().ConnectionString));
    }
}
