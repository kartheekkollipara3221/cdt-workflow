using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Tests;

public sealed class WorkflowActionResultBuilderTests
{
    private readonly WorkflowActionResultBuilder builder = new();
    private readonly NormalizedWorkflowAction normalized = new WorkflowActionEnvelopeNormalizer()
        .Normalize(WorkflowActionEnvelopeTestData.DeserializeValid(), null, null);

    [Fact]
    public void ValidationFailedCarriesValidationErrorsInDiagnostics()
    {
        var result = builder.BuildValidationFailed(
            normalized,
            [
                new WorkflowValidationError("actor.roles", "actor.roles is empty"),
                new WorkflowValidationError("dataSave.payload", "dataSave.payload is missing")
            ]).Result;

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ResultCode);
        Assert.Null(result.Expected);
        Assert.Null(result.Actual);
        Assert.Null(result.FailedGuard);
        Assert.Null(result.Diagnostics.RequestHash);
        Assert.Equal(2, result.Diagnostics.ValidationErrors?.Count);
    }

    [Fact]
    public void ConcurrencyConflictSetsExpectedActualAndFailedGuard()
    {
        var envelope = builder.BuildConcurrencyConflict(
            normalized,
            "a3f8b2c1d4e5f6a7",
            "expectedCurrentState",
            "CheckerReview",
            "TradingDeskReview",
            "Expected current state 'CheckerReview' but persisted state is 'TradingDeskReview'. The workflow has moved since you opened the form.",
            workflowContext: null);

        Assert.False(envelope.Result.Success);
        Assert.Equal("ConcurrencyConflict", envelope.Result.ResultCode);
        Assert.Equal("expectedCurrentState", envelope.Result.FailedGuard);
        Assert.Equal("CheckerReview", envelope.Result.Expected);
        Assert.Equal("TradingDeskReview", envelope.Result.Actual);
    }

    [Fact]
    public void DefinitionVersionMismatchCarriesScalarExpectedActualValues()
    {
        var envelope = builder.BuildWorkflowDefinitionVersionMismatch(
            normalized,
            "hash",
            expected: 1,
            actual: 2,
            workflowContext: new WorkflowContext());

        Assert.Equal("WorkflowDefinitionVersionMismatch", envelope.Result.ResultCode);
        Assert.Equal("1", envelope.Result.Expected);
        Assert.Equal("2", envelope.Result.Actual);
        Assert.NotNull(envelope.WorkflowContext);
    }

    [Fact]
    public void IdempotencyConflictUsesStoredHashAsExpectedAndCurrentHashAsActual()
    {
        var result = builder.BuildIdempotencyConflict(
            normalized,
            "b7d4e6f2c8a9d0e1",
            "a3f8b2c1d4e5f6a7",
            "CheckerReturnToMaker").Result;

        Assert.Equal("IdempotencyConflict", result.ResultCode);
        Assert.Equal("a3f8b2c1d4e5f6a7", result.Expected);
        Assert.Equal("b7d4e6f2c8a9d0e1", result.Actual);
        Assert.Equal("a3f8b2c1d4e5f6a7", result.Diagnostics.StoredRequestHash);
        Assert.Contains("previous request used hash a3f8b2c1", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AlreadyExistsCarriesExistingInstanceFields()
    {
        var envelope = builder.BuildAlreadyExists(
            "PortfolioTrade",
            "PortfolioTrade",
            "PT-1001",
            new ExistingWorkflowInstance(
                "WF-PT-1001",
                "Completed",
                8,
                "Completed",
                DateTimeOffset.Parse("2030-01-15T15:30:00Z"),
                DateTimeOffset.Parse("2030-01-15T15:45:00Z")));
        var result = envelope.Result;

        Assert.Equal("AlreadyExists", result.ResultCode);
        Assert.Equal("Completed", result.ExistingInstanceStatus);
        Assert.Equal(8, result.ExistingInstanceVersion);
        Assert.Equal("Completed", result.ExistingInstanceCurrentState);
        Assert.Null(envelope.WorkflowContext);
    }
}
