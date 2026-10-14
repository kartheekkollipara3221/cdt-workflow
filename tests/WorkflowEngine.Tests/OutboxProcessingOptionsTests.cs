using Microsoft.Extensions.Configuration;
using WorkflowEngine.Infrastructure.Notifications;

namespace WorkflowEngine.Tests;

public sealed class OutboxProcessingOptionsTests
{
    [Theory]
    [InlineData("BatchSize", "0")]
    [InlineData("BatchSize", "-1")]
    [InlineData("BatchSize", "abc")]
    [InlineData("RetryInitialDelaySeconds", "-1")]
    [InlineData("RetryInitialDelaySeconds", "3601")]
    [InlineData("RetryMaxDelaySeconds", "-1")]
    public void InvalidConfigurationIsRejected(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [$"Notifications:Outbox:{key}"] = value }).Build();
        Assert.ThrowsAny<Exception>(() => OutboxProcessingOptions.FromConfiguration(configuration));
    }

    [Fact]
    public void DefaultsPreserveUnlimitedBatchAndImmediateRetries()
    {
        var options = OutboxProcessingOptions.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.Null(options.BatchSize);
        Assert.Null(options.GetNextAttemptAtUtc(int.MaxValue, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void VeryLargeRetryCountRemainsCappedWithoutOverflow()
    {
        var options = new OutboxProcessingOptions(100, 60, 3600);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(1), options.GetNextAttemptAtUtc(int.MaxValue, DateTimeOffset.UnixEpoch));
    }
}
