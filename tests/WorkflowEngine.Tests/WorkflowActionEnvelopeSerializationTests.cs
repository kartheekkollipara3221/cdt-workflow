using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

public sealed class WorkflowActionEnvelopeSerializationTests
{
    [Fact]
    public void DeserializesRequiredEnvelopeShape()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();

        Assert.Equal("ExecuteWorkflowAction", envelope.Request?.CommandType);
        Assert.Equal("PortfolioTrade", envelope.BusinessObject?.ObjectType);
        Assert.Equal("maker1", envelope.Actor?.UserId);
        Assert.Equal("MakerPrimary", envelope.UiAction?.ButtonKey);
        Assert.Equal("SubmitForChecker", envelope.Workflow?.ActionKey);
        Assert.Equal(JsonValueKind.Object, envelope.DataSave?.Payload.ValueKind);
    }

    [Fact]
    public void SerializesWorkflowActionResultWithCamelCaseNames()
    {
        var result = new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "AcceptedForPhase3Validation",
                CorrelationId = "corr",
                WorkflowInstanceId = "WF-1",
                ActionKey = "SaveDraft",
                BusinessObject = new WorkflowActionResultBusinessObject
                {
                    ObjectType = "PortfolioTrade",
                    ObjectId = "PT-1"
                }
            }
        };

        var json = JsonSerializer.Serialize(result, WorkflowJson.Options);

        Assert.Contains("\"resultCode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"workflowInstanceId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultCode", json, StringComparison.Ordinal);
    }
}

