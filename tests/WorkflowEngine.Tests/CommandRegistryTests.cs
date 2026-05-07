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
    public void DeferredCommandsReturnIntentionalStubOutput(string command)
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch([command]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(command, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not implemented yet", result.Message, StringComparison.OrdinalIgnoreCase);
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
        foreach (var command in RequiredCommands.Where(command => command is not ("init-db" or "verify-seed" or "execute-json")))
        {
            data.Add(command);
        }

        return data;
    }

    private sealed class FakeInitializer : global::WorkflowEngine.Application.Persistence.IWorkflowDatabaseInitializer
    {
        public string Initialize() => "initialized";
    }

    private sealed class FakeVerifier(IReadOnlyList<string> errors) : global::WorkflowEngine.Application.Persistence.ISeedVerificationService
    {
        public global::WorkflowEngine.Application.Persistence.SeedVerificationResult Verify() => new(errors);
    }
}
