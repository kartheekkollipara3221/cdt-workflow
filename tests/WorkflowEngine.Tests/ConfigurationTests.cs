using Microsoft.Extensions.Configuration;
using WorkflowEngine.Domain.Workflow;

namespace WorkflowEngine.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ConfigurationReadsDefaultConnectionStringKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=example;Database=workflow;"
            })
            .Build();

        var connectionString = configuration.GetConnectionString(WorkflowKeys.DefaultConnectionName);

        Assert.Equal("Server=example;Database=workflow;", connectionString);
    }

    [Fact]
    public void TestSqlConnectionFactoryUsesConfiguredSourceWithRequestedDatabase()
    {
        var connectionString = TestSqlConnectionFactory.ConnectionStringFromSource(
            "workflow_tests",
            "Server=example;Database=template;Integrated Security=true;TrustServerCertificate=True;Encrypt=False");

        Assert.Contains("Data Source=example", connectionString, StringComparison.Ordinal);
        Assert.Contains("Initial Catalog=workflow_tests", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSqlConnectionFactoryUsesApprovedLocalFallbackWhenNoSqlConnectionIsConfigured()
    {
        var connectionString = TestSqlConnectionFactory.ConnectionString("workflow_fallback_tests");

        Assert.Contains("Data Source=DOLLY", connectionString, StringComparison.Ordinal);
        Assert.Contains("Initial Catalog=workflow_fallback_tests", connectionString, StringComparison.Ordinal);
    }
}
