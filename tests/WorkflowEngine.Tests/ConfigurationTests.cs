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
}
