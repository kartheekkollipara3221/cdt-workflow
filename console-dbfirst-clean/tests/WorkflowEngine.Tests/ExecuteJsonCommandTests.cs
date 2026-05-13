using System.Text.Json;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;

namespace WorkflowEngine.Tests;

public sealed class ExecuteJsonCommandTests
{
    private readonly ExecuteJsonCommandHandler handler = new(
        new WorkflowActionEnvelopeValidator(),
        new WorkflowActionEnvelopeNormalizer(),
        new CanonicalRequestHasher(),
        new WorkflowActionResultBuilder(),
        new FakeWorkflowExecutionService());

    [Fact]
    public void ExecuteJsonReturnsReadableTextByDefault()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);

        var result = handler.Execute([path]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Result: Success", result.Message, StringComparison.Ordinal);
        Assert.Contains("persisted validation passed", result.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("Success", envelope.Result.ResultCode);
        Assert.NotNull(envelope.Result.RequestHash);
        Assert.Equal(envelope.Result.RequestHash, envelope.Result.Diagnostics.RequestHash);
        Assert.Null(envelope.Result.Notification);
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
    public void ExecuteJsonParsesAdminOverride()
    {
        var service = new FakeWorkflowExecutionService();
        var localHandler = new ExecuteJsonCommandHandler(
            new WorkflowActionEnvelopeValidator(),
            new WorkflowActionEnvelopeNormalizer(),
            new CanonicalRequestHasher(),
            new WorkflowActionResultBuilder(),
            service);
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);

        var result = localHandler.Execute([path, "--admin-override"]);

        Assert.Equal(0, result.ExitCode);
        Assert.True(service.LastRequest?.AdminOverride);
    }

    [Fact]
    public void CommandRegistryDispatchesExecuteJsonWhenRegistered()
    {
        var path = WorkflowActionEnvelopeTestData.WriteTempEnvelope(WorkflowActionEnvelopeTestData.ValidSubmitJson);
        var registry = new CommandRegistry(null, null, null, handler);

        var result = registry.Dispatch(["execute-json", path]);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("not implemented yet", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}

file sealed class FakeWorkflowExecutionService : IWorkflowExecutionService
{
    public WorkflowExecutionRequest? LastRequest { get; private set; }

    public WorkflowActionResultEnvelope Execute(WorkflowExecutionRequest request)
    {
        LastRequest = request;
        return new WorkflowActionResultBuilder().BuildExecutionValidatedSuccess(
            request.Normalized,
            request.RequestHash,
            "Persisted validation passed.");
    }
}
