namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class Section21QualityGateTests
{
    [Fact]
    public void AllQualityGateCommandsExecute()
    {
        using var host = new TestConsoleHost("burrito_phase6_quality_gate_tests");
        host.ResetDatabase();

        Execute(host, "init-db");
        Execute(host, "verify-seed");
        foreach (var command in BranchScriptDefinitions.CheckerReturnBranch_Section25_2)
        {
            Execute(host, command);
        }

        foreach (var command in BranchScriptDefinitions.TradingReturnBranch_Section25_3)
        {
            Execute(host, command);
        }

        Execute(host, "demo");
    }

    private static void Execute(TestConsoleHost host, params string[] args)
    {
        var result = host.Execute(args);
        Assert.True(result.ExitCode == 0, result.Message);
    }

    private static void Execute(TestConsoleHost host, WorkflowEngine.Application.Demo.ConsoleCommand command)
    {
        var result = host.Execute(command);
        Assert.True(result.ExitCode == 0, $"{command.CommandName} failed: {result.Message}");
    }
}
