using System.Text.Json;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Tests;

public sealed class ExecuteJsonCommandTests
{
    private readonly ExecuteJsonCommandHandler handler = new(
        new WorkflowActionEnvelopeValidator(),
        new WorkflowActionEnvelopeNormalizer(),
        new CanonicalRequestHasher(),
        new WorkflowActionResultBuilder());

    [Fact]
    public void ExecuteJsonReturnsReadableTextByDefault()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);

        var result = handler.Execute([path]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Result: AcceptedForPhase3Validation", result.Message, StringComparison.Ordinal);
        Assert.Contains("Workflow transition execution is deferred to Phase 4", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteJsonCanReturnWorkflowActionResultJson()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);

        var result = handler.Execute([path, "--format", "json"]);
        var envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(envelope);
        Assert.True(envelope.Result.Success);
        Assert.Equal("AcceptedForPhase3Validation", envelope.Result.ResultCode);
        Assert.NotNull(envelope.Result.RequestHash);
        Assert.Equal(envelope.Result.RequestHash, envelope.Result.Diagnostics.RequestHash);
        Assert.False(envelope.Result.Notification?.Queued);
    }

    [Fact]
    public void ExecuteJsonReturnsValidationFailedForInvalidEnvelope()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope("""{"actor":{"roles":[]}}""");

        var result = handler.Execute([path, "--format", "json"]);
        var envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(envelope);
        Assert.False(envelope.Result.Success);
        Assert.Equal("ValidationFailed", envelope.Result.ResultCode);
        Assert.Null(envelope.Result.Diagnostics.RequestHash);
        Assert.NotEmpty(envelope.Result.Diagnostics.ValidationErrors ?? []);
    }

    [Fact]
    public void ExecuteJsonAppliesOverridesBeforeResultConstruction()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);

        var result = handler.Execute([path, "--business-object", "PT-3003", "--workflow-instance", "WF-PT-3003", "--format", "json"]);
        var envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);

        Assert.Equal("PT-3003", envelope?.Result.BusinessObject?.ObjectId);
        Assert.Equal("WF-PT-3003", envelope?.Result.WorkflowInstanceId);
        Assert.Contains(":", envelope?.Result.EffectiveIdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteJsonMissingFileReturnsNonZero()
    {
        var result = handler.Execute(["missing-file.json"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandRegistryDispatchesExecuteJsonWhenRegistered()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);
        var registry = new CommandRegistry(null, null, handler);

        var result = registry.Dispatch(["execute-json", path]);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("not implemented yet", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
