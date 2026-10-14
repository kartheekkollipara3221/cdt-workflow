using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace WorkflowEngine.Infrastructure.Notifications;

/// <summary>Optional backlog limits and durable retry delays. Defaults preserve legacy behavior.</summary>
public sealed class OutboxProcessingOptions
{
    public OutboxProcessingOptions(int? batchSize = null, int retryInitialDelaySeconds = 0, int retryMaxDelaySeconds = 3600)
    {
        if (batchSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive or omitted.");
        if (retryInitialDelaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(retryInitialDelaySeconds));
        if (retryMaxDelaySeconds < retryInitialDelaySeconds || retryMaxDelaySeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(retryMaxDelaySeconds), "Maximum delay must be at least the initial delay.");
        BatchSize = batchSize;
        RetryInitialDelaySeconds = retryInitialDelaySeconds;
        RetryMaxDelaySeconds = retryMaxDelaySeconds;
    }

    public int? BatchSize { get; }
    public int RetryInitialDelaySeconds { get; }
    public int RetryMaxDelaySeconds { get; }

    public DateTimeOffset? GetNextAttemptAtUtc(int priorRetryCount, DateTimeOffset failedAtUtc)
    {
        if (RetryInitialDelaySeconds == 0)
            return null;
        var delay = Math.Min(RetryMaxDelaySeconds,
            RetryInitialDelaySeconds * Math.Pow(2, Math.Clamp(priorRetryCount, 0, 31)));
        return failedAtUtc.AddSeconds(delay);
    }

    public static OutboxProcessingOptions FromConfiguration(IConfiguration configuration)
    {
        const string prefix = "Notifications:Outbox:";
        int? Read(string key)
        {
            var value = configuration[prefix + key];
            if (value is null)
                return null;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                throw new InvalidOperationException($"{prefix}{key} must be an integer.");
            return number;
        }
        return new OutboxProcessingOptions(Read("BatchSize"), Read("RetryInitialDelaySeconds") ?? 0,
            Read("RetryMaxDelaySeconds") ?? 3600);
    }
}
