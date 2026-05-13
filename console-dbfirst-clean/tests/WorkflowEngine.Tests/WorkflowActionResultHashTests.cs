using System.Security.Cryptography;
using System.Text;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Results;

namespace WorkflowEngine.Tests;

public sealed class WorkflowActionResultHashTests
{
    [Fact]
    public void CanonicalResultSerializationIsDeterministicAndCompact()
    {
        var serializer = new CanonicalWorkflowResultSerializer();
        var envelope = CreateEnvelope();

        var first = serializer.Serialize(envelope);
        var second = serializer.Serialize(envelope);

        Assert.Equal(first, second);
        Assert.DoesNotContain(Environment.NewLine, first, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics\"", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultHashExcludesDiagnostics()
    {
        var serializer = new CanonicalWorkflowResultSerializer();
        var first = CreateEnvelope();
        var second = CreateEnvelope();
        second.Result.Diagnostics.RequestHash = "different";
        second.Result.Diagnostics.ResultHash = "also-different";

        Assert.Equal(serializer.ComputeResultHash(first), serializer.ComputeResultHash(second));
    }

    [Fact]
    public void ResultHashIsSha256OfCanonicalResultWithoutDiagnostics()
    {
        var serializer = new CanonicalWorkflowResultSerializer();
        var envelope = CreateEnvelope();
        var canonical = serializer.Serialize(envelope, excludeDiagnostics: true);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        Assert.Equal(expected, serializer.ComputeResultHash(envelope));
    }

    private static WorkflowActionResultEnvelope CreateEnvelope()
    {
        return new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                CorrelationId = "corr",
                WorkflowInstanceId = "WF-PT-1001",
                BusinessObject = new WorkflowActionResultBusinessObject
                {
                    ObjectType = "PortfolioTrade",
                    ObjectId = "PT-1001"
                },
                ActionKey = "SubmitForChecker",
                OldState = "Draft",
                NewState = "CheckerReview",
                Diagnostics = new WorkflowActionDiagnostics
                {
                    RequestHash = "a3f8b2c1d4e5f6a7",
                    ResultHash = "placeholder",
                    Replay = false
                }
            },
            WorkflowContext = new WorkflowContext
            {
                ServerTimeUtc = DateTimeOffset.Parse("2030-01-15T15:30:00Z")
            }
        };
    }
}
