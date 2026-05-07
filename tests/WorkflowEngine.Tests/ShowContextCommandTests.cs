using System.Text.Json;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowContext")]
public sealed class ShowContextCommandTests
{
    [Fact]
    public void ShowContextMissingArgumentsReturnNonZero()
    {
        using var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        var handler = new ShowContextCommandHandler(WorkflowContextReaderTests.CreateReader(dbContext));

        var result = handler.Execute([]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Usage: show-context", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowContextDefaultsToJsonAgainstSql()
    {
        using var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        WorkflowContextReaderTests.AddInstance(dbContext, "WF-PT-CTX-CHECKER", "PT-CTX-CHECKER", "CheckerReview", "Active", 3);
        WorkflowContextReaderTests.AddTask(dbContext, "WF-PT-CTX-CHECKER", "CheckerReviewTask", "Checker", DateTimeOffset.Parse("2026-05-07T12:10:00Z"));
        dbContext.SaveChanges();
        var handler = new ShowContextCommandHandler(WorkflowContextReaderTests.CreateReader(dbContext));

        var result = handler.Execute(["WF-PT-CTX-CHECKER", "--user", "checker1", "--roles", "Checker"]);
        var response = JsonSerializer.Deserialize<WorkflowContextResponse>(result.Message, WorkflowJson.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(response);
        Assert.Equal("checker1", response.WorkflowContext.ActorAccess.DisplayName);
        Assert.True(response.WorkflowContext.ActorAccess.CanAct);
        Assert.Equal("CheckerReview", response.WorkflowContext.Workflow.CurrentState);
    }

    [Fact]
    public void ShowContextSupportsExplicitJsonFormatAndWrongRole()
    {
        using var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        WorkflowContextReaderTests.AddInstance(dbContext, "WF-PT-CTX-WRONG-ROLE", "PT-CTX-WRONG-ROLE", "CheckerReview", "Active", 1);
        WorkflowContextReaderTests.AddTask(dbContext, "WF-PT-CTX-WRONG-ROLE", "CheckerReviewTask", "Checker", DateTimeOffset.Parse("2026-05-07T12:10:00Z"));
        dbContext.SaveChanges();
        var handler = new ShowContextCommandHandler(WorkflowContextReaderTests.CreateReader(dbContext));

        var result = handler.Execute(["WF-PT-CTX-WRONG-ROLE", "--user", "maker1", "--roles", "Maker", "--format", "json"]);
        var response = JsonSerializer.Deserialize<WorkflowContextResponse>(result.Message, WorkflowJson.Options);

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(response);
        Assert.False(response.WorkflowContext.ActorAccess.CanAct);
        Assert.Empty(response.WorkflowContext.AvailableActions);
    }

    [Fact]
    public void ShowContextUnknownInstanceReturnsNonZero()
    {
        using var dbContext = WorkflowContextReaderTests.CreateSeededContext();
        var handler = new ShowContextCommandHandler(WorkflowContextReaderTests.CreateReader(dbContext));

        var result = handler.Execute(["WF-PT-MISSING", "--user", "checker1", "--roles", "Checker"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("was not found", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandRegistryDispatchesShowContextWhenRegistered()
    {
        var handler = new StubShowContextCommandHandler();
        var registry = new CommandRegistry(null, null, null, null, handler);

        var result = registry.Dispatch(["show-context", "WF-PT-CTX", "--user", "checker1", "--roles", "Checker"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("ok", result.Message);
    }

    private sealed class StubShowContextCommandHandler : IShowContextCommandHandler
    {
        public CommandResult Execute(IReadOnlyList<string> args) => CommandResult.Success("ok");
    }
}
