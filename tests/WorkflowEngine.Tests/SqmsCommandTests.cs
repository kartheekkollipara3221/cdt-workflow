using WorkflowEngine.Application.Commands;
using WorkflowEngine.Sqms.Commands;
using WorkflowEngine.Sqms.Contracts;

namespace WorkflowEngine.Tests;

public sealed class SqmsCommandTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2030-01-17T14:00:00Z");

    [Fact]
    public void ParserAcceptsNoArgumentsAndRejectsAllArguments()
    {
        var valid = CommandArgumentParser.Parse("run-sqms-scheduled", []);
        var invalid = CommandArgumentParser.Parse("run-sqms-scheduled", ["--now", Now.ToString("O")]);

        Assert.True(valid.Success);
        Assert.IsType<NoCommandArguments>(valid.Arguments);
        Assert.False(invalid.Success);
        Assert.Equal("Usage: run-sqms-scheduled", invalid.ErrorMessage);
    }

    [Fact]
    public async Task SuccessfulRunPrintsEveryAggregateAndReturnsZero()
    {
        var scheduled = new FakeScheduledService(new SqmsScheduledRunResult(
            Now, 2, 3, 4, 5, 6, 7, []));

        var result = await new RunSqmsScheduledCommandHandler(scheduled)
            .RunAsync([], CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Command: RunSqmsScheduled", result.Message, StringComparison.Ordinal);
        Assert.Contains("Now: 2030-01-17T14:00:00Z", result.Message, StringComparison.Ordinal);
        Assert.Contains("Opportunities closed: 2", result.Message, StringComparison.Ordinal);
        Assert.Contains("Bids moved to IPG review: 3", result.Message, StringComparison.Ordinal);
        Assert.Contains("Bids not allocated: 4", result.Message, StringComparison.Ordinal);
        Assert.Contains("Open notifications queued: 5", result.Message, StringComparison.Ordinal);
        Assert.Contains("Reminder notifications queued: 6", result.Message, StringComparison.Ordinal);
        Assert.Contains("Already queued: 7", result.Message, StringComparison.Ordinal);
        Assert.Contains("SQMS scheduled run succeeded", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedRunPrintsEveryFailureAndReturnsNonzero()
    {
        var failures = new[]
        {
            new SqmsScheduledFailure("Opportunity", "10000", "Close", "close failed"),
            new SqmsScheduledFailure("Bid", "20000", "Classify", "amount invalid")
        };
        var scheduled = new FakeScheduledService(new SqmsScheduledRunResult(
            Now, 0, 0, 0, 0, 0, 0, failures));

        var result = await new RunSqmsScheduledCommandHandler(scheduled)
            .RunAsync([], CancellationToken.None);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("[FAIL] Opportunity 10000 | Close | close failed", result.Message, StringComparison.Ordinal);
        Assert.Contains("[FAIL] Bid 20000 | Classify | amount invalid", result.Message, StringComparison.Ordinal);
        Assert.Contains("SQMS scheduled run failed with 2 candidate failures", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryDispatchesNewHandlerWithoutBreakingExistingConstructorShape()
    {
        _ = new CommandRegistry(null, null, null, null, null, null, null, null, null, null, null, null);
        var registry = new CommandRegistry(runSqmsScheduledCommandHandler: new FakeHandler());

        var result = registry.Dispatch(["run-sqms-scheduled"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("scheduled", result.Message);
    }

    private sealed class FakeScheduledService(SqmsScheduledRunResult result) : ISqmsScheduledService
    {
        public Task<SqmsScheduledRunResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeHandler : IRunSqmsScheduledCommandHandler
    {
        public Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken) =>
            Task.FromResult(CommandResult.Success("scheduled"));
    }
}
