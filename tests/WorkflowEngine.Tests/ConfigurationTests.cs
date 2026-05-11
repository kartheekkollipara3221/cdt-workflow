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
    public void TestSqlConnectionFactoryFailsClearlyWhenNoSqlConnectionIsConfigured()
    {
        var exception = Assert.Throws<InvalidOperationException>(TestSqlConnectionFactory.ThrowMissingConnectionString);

        Assert.Contains("SQL-backed workflow tests require", exception.Message, StringComparison.Ordinal);
    }
}
