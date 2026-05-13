using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Tests;

public sealed class DemoCommandTests
{
    [Fact]
    public void Demo_FailsAtStep_PrintsFailureLine()
    {
        var handler = CreateHandler(new FailAtStepDemoStepRunner(3, "ConcurrencyConflict", "stale workflow"));

        var result = handler.Execute([]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Demo failed at step 3: ConcurrencyConflict stale workflow", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_FailsAtStep_DoesNotPrintBranchGuidance()
    {
        var handler = CreateHandler(new FailAtStepDemoStepRunner(0, "ValidationFailed", "bad request"));

        var result = handler.Execute([]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("To explore Return-to-Maker branches", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_FailsAtStep_DoesNotPrintAuditHistory()
    {
        var history = new FakeShowHistoryCommandHandler();
        var handler = new DemoCommandHandler(new FailAtStepDemoStepRunner(0, "Forbidden", "not allowed"), history);

        var result = handler.Execute([]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(0, history.CallCount);
        Assert.DoesNotContain("History for WF-PT-DEMO", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_FailsAtStep_StopsExecution()
    {
        var runner = new FailAtStepDemoStepRunner(3, "ConcurrencyConflict", "stale workflow");
        var handler = CreateHandler(runner);

        handler.Execute([]);

        Assert.Equal(4, runner.CallCount);
    }

    [Theory]
    [InlineData("ValidationFailed")]
    [InlineData("Forbidden")]
    [InlineData("InvalidTransition")]
    [InlineData("ConcurrencyConflict")]
    [InlineData("WorkflowDefinitionVersionMismatch")]
    [InlineData("IdempotencyConflict")]
    [InlineData("AmbiguousRoleMatch")]
    [InlineData("WorkflowInstanceNotFound")]
    public void Demo_FailsWithEachResultCode(string resultCode)
    {
        var handler = CreateHandler(new FailAtStepDemoStepRunner(1, resultCode, "configured failure"));

        var result = handler.Execute([]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains($"Demo failed at step 1: {resultCode} configured failure", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("To explore Return-to-Maker branches", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_AllStepsSucceed_PrintsBranchGuidance()
    {
        var handler = CreateHandler(new AlwaysSuccessDemoStepRunner());

        var result = handler.Execute([]);

        Assert.True(result.ExitCode == 0, result.Message);
        Assert.Contains("Demo completed successfully.", result.Message, StringComparison.Ordinal);
        Assert.Contains("To explore Return-to-Maker branches", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_AllStepsSucceed_PrintsAuditHistory()
    {
        var handler = CreateHandler(new AlwaysSuccessDemoStepRunner());

        var result = handler.Execute([]);

        Assert.True(result.ExitCode == 0, result.Message);
        Assert.Contains("History for WF-PT-DEMO", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_AllStepsSucceed_ExitsZero()
    {
        var handler = CreateHandler(new AlwaysSuccessDemoStepRunner());

        var result = handler.Execute([]);

        Assert.True(result.ExitCode == 0, result.Message);
    }

    [Fact]
    public void Demo_RejectsArguments()
    {
        var handler = CreateHandler(new AlwaysSuccessDemoStepRunner());

        var result = handler.Execute(["--skip-branch-guidance"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Usage: demo", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Demo_AgainstRealEngine_RunsHappyPath()
    {
        using var host = new TestConsoleHost("burrito_phase6_demo_tests");
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);

        var result = host.Execute("demo");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Demo completed successfully.", result.Message, StringComparison.Ordinal);
        Assert.Contains("History for WF-PT-DEMO", result.Message, StringComparison.Ordinal);
        Assert.Contains("To explore Return-to-Maker branches", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PT-1001", host.DbContext.WorkflowInstances.Select(row => row.BusinessObjectId));
        Assert.DoesNotContain("PT-1002", host.DbContext.WorkflowInstances.Select(row => row.BusinessObjectId));
        Assert.DoesNotContain("PT-1003", host.DbContext.WorkflowInstances.Select(row => row.BusinessObjectId));
        Assert.Contains(host.DbContext.WorkflowInstances, row => row.BusinessObjectId == "PT-DEMO" && row.Status == "Completed");
    }

    [Fact]
    public void Demo_AgainstRealEngine_RunsTwiceWithReset()
    {
        using var host = new TestConsoleHost("burrito_phase6_demo_twice_tests");
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);

        var first = host.Execute("demo");
        var second = host.Execute("demo");

        Assert.True(first.ExitCode == 0, first.Message);
        Assert.True(second.ExitCode == 0, second.Message);
        Assert.Contains("Demo completed successfully.", second.Message, StringComparison.Ordinal);
        Assert.Single(host.DbContext.WorkflowInstances.Where(row => row.BusinessObjectId == "PT-DEMO"));
    }

    private static DemoCommandHandler CreateHandler(IDemoStepRunner runner)
    {
        return new DemoCommandHandler(runner, new FakeShowHistoryCommandHandler());
    }

    private sealed class AlwaysSuccessDemoStepRunner : IDemoStepRunner
    {
        public Task<DemoStepResult> RunStepAsync(DemoStep step, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DemoStepResult(true, "Success", $"{step.Name} succeeded.", step.Expectations));
        }
    }

    public sealed class FailAtStepDemoStepRunner : IDemoStepRunner
    {
        private readonly int failAtStepIndex;
        private readonly string failureResultCode;
        private readonly string failureMessage;

        public FailAtStepDemoStepRunner(int failAtStepIndex, string failureResultCode, string failureMessage)
        {
            this.failAtStepIndex = failAtStepIndex;
            this.failureResultCode = failureResultCode;
            this.failureMessage = failureMessage;
        }

        public int CallCount { get; private set; }

        public Task<DemoStepResult> RunStepAsync(DemoStep step, CancellationToken cancellationToken)
        {
            var index = CallCount++;
            if (index == failAtStepIndex)
            {
                return Task.FromResult(new DemoStepResult(false, failureResultCode, failureMessage, null));
            }

            return Task.FromResult(new DemoStepResult(true, "Success", $"Step {index} succeeded (test double)", step.Expectations));
        }
    }

    private sealed class FakeShowHistoryCommandHandler : IShowHistoryCommandHandler
    {
        public int CallCount { get; private set; }

        public CommandResult Execute(IReadOnlyList<string> args)
        {
            CallCount++;
            return CommandResult.Success("History for WF-PT-DEMO:" + Environment.NewLine + "  #1 SaveDraft | Draft -> Draft");
        }
    }
}
