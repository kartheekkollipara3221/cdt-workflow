using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class BranchScriptTests
{
    [Fact]
    public void HappyPath_RunsToCompletion()
    {
        using var host = new TestConsoleHost("burrito_phase6_branch_happy_tests");
        host.ResetDatabase();

        ExecuteAll(host, BranchScriptDefinitions.HappyPath_Section25_1);

        var instance = host.DbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == "WF-PT-1001");
        Assert.Equal("Completed", instance.Status);
        Assert.Equal("Completed", instance.CurrentStateKey);
        Assert.Empty(host.DbContext.WorkflowTasks.Where(row => row.WorkflowInstanceId == "WF-PT-1001" && row.Status == "Pending"));
    }

    [Fact]
    public void CheckerReturnBranch_RunsToCompletion()
    {
        using var host = new TestConsoleHost("burrito_phase6_branch_checker_tests");
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);

        ExecuteAll(host, BranchScriptDefinitions.CheckerReturnBranch_Section25_2);

        AssertReturnedToMaker(host, "WF-PT-1002");
    }

    [Fact]
    public void TradingReturnBranch_RunsToCompletion()
    {
        using var host = new TestConsoleHost("burrito_phase6_branch_trading_tests");
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);

        ExecuteAll(host, BranchScriptDefinitions.TradingReturnBranch_Section25_3);

        AssertReturnedToMaker(host, "WF-PT-1003");
    }

    private static void ExecuteAll(TestConsoleHost host, IEnumerable<ConsoleCommand> commands)
    {
        foreach (var command in commands)
        {
            var result = host.Execute(command);
            Assert.True(result.ExitCode == 0, $"{command.CommandName} failed: {result.Message}");
        }
    }

    private static void AssertReturnedToMaker(TestConsoleHost host, string workflowInstanceId)
    {
        var instance = host.DbContext.WorkflowInstances.Single(row => row.WorkflowInstanceId == workflowInstanceId);
        Assert.Equal("ReturnedToMaker", instance.CurrentStateKey);
        Assert.Equal("Active", instance.Status);

        var pending = Assert.Single(host.DbContext.WorkflowTasks.Where(row => row.WorkflowInstanceId == workflowInstanceId && row.Status == "Pending"));
        Assert.Equal("MakerReworkTask", pending.TaskKey);
        Assert.Null(pending.DueAtUtc);

        Assert.Contains(host.DbContext.WorkflowTasks, row => row.WorkflowInstanceId == workflowInstanceId && row.Status == "Completed");
        var notification = Assert.Single(host.DbContext.NotificationOutbox.Where(row => row.WorkflowInstanceId == workflowInstanceId && row.NotificationType == "WorkflowReturnedToMaker"));
        Assert.Equal("Maker", notification.RecipientRoleKey);
    }
}
