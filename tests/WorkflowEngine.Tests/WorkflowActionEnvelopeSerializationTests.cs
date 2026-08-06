using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;

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
        Assert.Equal(
            ["checker.primary@example.com", "checker.secondary@example.com"],
            envelope.Workflow?.NotificationRecipients?.ToEmailAddresses);
        Assert.Equal(
            ["maker.manager@example.com", "workflow.operations@example.com"],
            envelope.Workflow?.NotificationRecipients?.CcEmailAddresses);
    }

    [Fact]
    public void StartWorkflowRequestCarriesTypedNotificationRecipients()
    {
        var recipients = new NotificationRecipients
        {
            ToEmailAddresses = ["first@example.com", "second@example.com"],
            CcEmailAddresses = ["copy@example.com"]
        };

        var request = new StartWorkflowRequest(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-1001",
            "maker1",
            false,
            NotificationRecipients: recipients);

        Assert.Same(recipients, request.NotificationRecipients);
    }

    [Fact]
    public void NotificationRecipientsRoundTripPreservesExactCollections()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Workflow!.NotificationRecipients = new NotificationRecipients
        {
            ToEmailAddresses = [" first@example.test ", "second@example.test", "first@example.test"],
            CcEmailAddresses = ["manager@example.test", " operations@example.test "]
        };

        var json = JsonSerializer.Serialize(envelope, WorkflowJson.Options);
        var roundTripped = JsonSerializer.Deserialize<WorkflowActionEnvelope>(json, WorkflowJson.Options);

        Assert.NotNull(roundTripped?.Workflow?.NotificationRecipients);
        Assert.Equal(
            [" first@example.test ", "second@example.test", "first@example.test"],
            roundTripped.Workflow.NotificationRecipients.ToEmailAddresses);
        Assert.Equal(
            ["manager@example.test", " operations@example.test "],
            roundTripped.Workflow.NotificationRecipients.CcEmailAddresses);
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
                },
                Diagnostics = new WorkflowActionDiagnostics()
            }
        };

        var json = JsonSerializer.Serialize(result, WorkflowJson.Options);

        Assert.Contains("\"resultCode\"", json, StringComparison.Ordinal);
        Assert.Contains("\"workflowInstanceId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"workflowContext\"", json, StringComparison.Ordinal);
        Assert.Contains("\"diagnostics\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultCode", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureResultSerializesNullSideEffectFields()
    {
        var result = new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = false,
                ResultCode = "Forbidden",
                Diagnostics = new WorkflowActionDiagnostics()
            }
        };

        var json = JsonSerializer.Serialize(result, WorkflowJson.Options);

        Assert.Contains("\"oldState\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"closedTask\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"createdTask\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"notification\": null", json, StringComparison.Ordinal);
        Assert.Contains("\"audit\": null", json, StringComparison.Ordinal);
    }
}
