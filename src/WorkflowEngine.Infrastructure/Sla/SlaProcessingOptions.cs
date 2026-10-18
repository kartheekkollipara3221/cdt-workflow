using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace WorkflowEngine.Infrastructure.Sla;

public sealed class SlaProcessingOptions
{
    public SlaProcessingOptions(int batchSize = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        BatchSize = batchSize;
    }

    public int BatchSize { get; }

    public static SlaProcessingOptions FromConfiguration(IConfiguration configuration)
    {
        var value = configuration["Sla:BatchSize"];
        if (value is null)
            return new SlaProcessingOptions();
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            throw new InvalidOperationException("Sla:BatchSize must be a positive integer.");
        return new SlaProcessingOptions(size);
    }
}
