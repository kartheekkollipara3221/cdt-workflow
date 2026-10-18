using Microsoft.Extensions.Configuration;
using WorkflowEngine.Infrastructure.Sla;

namespace WorkflowEngine.Tests;

public sealed class SlaProcessingOptionsTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void InvalidBatchSizeIsRejected(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Sla:BatchSize"] = value }).Build();
        Assert.ThrowsAny<Exception>(() => SlaProcessingOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void DefaultBatchSizeIsOneHundred()
    {
        Assert.Equal(100, SlaProcessingOptions.FromConfiguration(new ConfigurationBuilder().Build()).BatchSize);
    }
}
