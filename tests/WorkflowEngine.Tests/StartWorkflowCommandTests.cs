using System.Text.Json;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Tests;

public sealed class StartWorkflowCommandTests
{
    [Fact]
    public void MissingUserReturnsNonZero()
    {
        var handler = new StartWorkflowCommandHandler(new FakeStartWorkflowService());

        var result = handler.Execute(["PortfolioTrade", "PT-1001"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--user is required", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FreshSuccessReadableOutputShowsStartedFields()
    {
        var handler = new StartWorkflowCommandHandler(new FakeStartWorkflowService());

        var result = handler.Execute(["PortfolioTrade", "PT-1001", "--user", "maker1"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Command: StartWorkflow", result.Message, StringComparison.Ordinal);
        Assert.Contains("Result code: Success", result.Message, StringComparison.Ordinal);
        Assert.Contains("Current state: Draft", result.Message, StringComparison.Ordinal);
        Assert.Contains("Started by: maker1", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResetReadableOutputShowsCounts()
    {
        var handler = new StartWorkflowCommandHandler(new FakeStartWorkflowService());

        var result = handler.Execute(["PortfolioTrade", "PT-1001", "--user", "maker1", "--reset"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Reset: Yes", result.Message, StringComparison.Ordinal);
        Assert.Contains("IdempotencyRecord rows:    3", result.Message, StringComparison.Ordinal);
        Assert.Contains("WorkflowInstance rows:     1", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonOutputUsesWorkflowActionResultEnvelope()
    {
        var handler = new StartWorkflowCommandHandler(new FakeStartWorkflowService());

        var result = handler.Execute(["PortfolioTrade", "PT-1001", "--user", "maker1", "--format", "json"]);
        var envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(envelope);
        Assert.Equal("Success", envelope.Result.ResultCode);
        Assert.Equal("WF-PT-1001", envelope.Result.WorkflowInstanceId);
    }

    [Fact]
    public void CommandRegistryDispatchesStartWorkflowWhenRegistered()
    {
        var registry = new CommandRegistry(null, null, new StartWorkflowCommandHandler(new FakeStartWorkflowService()));

        var result = registry.Dispatch(["start-workflow", "PortfolioTrade", "PT-1001", "--user", "maker1"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Result code: Success", result.Message, StringComparison.Ordinal);
    }

    private sealed class FakeStartWorkflowService : IStartWorkflowService
    {
        public StartWorkflowResult Start(StartWorkflowRequest request)
        {
            var envelope = new WorkflowActionResultBuilder().BuildStartWorkflowSuccess(
                request.WorkflowKey,
                request.BusinessObjectType,
                request.BusinessObjectId,
                $"WF-{request.BusinessObjectId}",
                "Draft",
                "Active",
                1,
                DateTimeOffset.Parse("2030-01-15T15:30:00Z"),
                request.StartedByUserId);

            return new StartWorkflowResult(
                envelope,
                request.Reset ? new StartWorkflowResetCounts(3, 2, 4, 2, 0, 1, 1) : null);
        }
    }
}
