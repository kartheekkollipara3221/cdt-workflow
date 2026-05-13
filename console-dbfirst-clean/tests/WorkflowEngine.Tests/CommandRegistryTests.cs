using WorkflowEngine.Application.Commands;

namespace WorkflowEngine.Tests;

public sealed class CommandRegistryTests
{
    private static readonly string[] RequiredCommands =
    [
        "init-db",
        "verify-seed",
        "start-workflow",
        "execute-json",
        "show-context",
        "show",
        "show-tasks",
        "show-history",
        "show-notifications",
        "run-sla",
        "process-notification-outbox",
        "demo"
    ];

    [Fact]
    public void KnownCommandsContainEveryRequiredConsoleCommand()
    {
        var registry = new CommandRegistry();

        foreach (var command in RequiredCommands)
        {
            Assert.True(registry.IsKnown(command), $"Expected '{command}' to be known.");
        }
    }

    [Theory]
    [MemberData(nameof(DeferredCommandCases))]
    public void CommandsWithoutRegisteredHandlersReturnFailure(string command)
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch([command]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(command, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requires command services", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitDbDispatchesDatabaseInitializerWhenRegistered()
    {
        var registry = new CommandRegistry(new FakeInitializer(), null);

        var result = registry.Dispatch(["init-db"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("initialized", result.Message);
    }

    [Fact]
    public void VerifySeedDispatchesVerifierWhenRegistered()
    {
        var registry = new CommandRegistry(null, new FakeVerifier([]));

        var result = registry.Dispatch(["verify-seed"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("passed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VerifySeedReturnsFailureWhenVerifierReportsErrors()
    {
        var registry = new CommandRegistry(null, new FakeVerifier(["missing state"]));

        var result = registry.Dispatch(["verify-seed"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("missing state", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCommandReturnsNonZeroResult()
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch(["not-a-command"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown command", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<string> DeferredCommandCases()
    {
        var data = new TheoryData<string>();
        foreach (var command in RequiredCommands.Where(command => command is not ("init-db" or "verify-seed" or "start-workflow" or "execute-json" or "show-context" or "show" or "show-tasks" or "show-history" or "show-notifications" or "run-sla" or "process-notification-outbox")))
        {
            data.Add(command);
        }

        return data;
    }

    [Theory]
    [InlineData("show")]
    [InlineData("show-tasks")]
    [InlineData("show-history")]
    [InlineData("show-notifications")]
    [InlineData("run-sla")]
    [InlineData("process-notification-outbox")]
    public void InspectionCommandsRequireRegisteredHandlers(string command)
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch([command, "WF-PT-1002"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("not implemented yet", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requires command services", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunSlaDispatchesRegisteredHandler()
    {
        var registry = new CommandRegistry(runSlaCommandHandler: new FakeRunSlaCommandHandler());

        var result = registry.Dispatch(["run-sla", "--now", "2030-01-15T16:00:00Z"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("sla-ran", result.Message);
    }

    [Fact]
    public void ProcessNotificationOutboxDispatchesRegisteredHandler()
    {
        var registry = new CommandRegistry(processNotificationOutboxCommandHandler: new FakeProcessNotificationOutboxCommandHandler());

        var result = registry.Dispatch(["process-notification-outbox"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("outbox-ran", result.Message);
    }

    [Fact]
    public void DemoDispatchesRegisteredHandler()
    {
        var registry = new CommandRegistry(demoCommandHandler: new FakeDemoCommandHandler());

        var result = registry.Dispatch(["demo"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("demo-ran", result.Message);
    }

    private sealed class FakeInitializer : global::WorkflowEngine.Application.Persistence.IWorkflowDatabaseInitializer
    {
        public string Initialize() => "initialized";
    }

    private sealed class FakeVerifier(IReadOnlyList<string> errors) : global::WorkflowEngine.Application.Persistence.ISeedVerificationService
    {
        public global::WorkflowEngine.Application.Persistence.SeedVerificationResult Verify() => new(errors);
    }

    private sealed class FakeRunSlaCommandHandler : IRunSlaCommandHandler
    {
        public Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success("sla-ran"));
        }
    }

    private sealed class FakeProcessNotificationOutboxCommandHandler : IProcessNotificationOutboxCommandHandler
    {
        public Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
        {
            return Task.FromResult(CommandResult.Success("outbox-ran"));
        }
    }

    private sealed class FakeDemoCommandHandler : IDemoCommandHandler
    {
        public CommandResult Execute(IReadOnlyList<string> args)
        {
            return CommandResult.Success("demo-ran");
        }
    }
}
