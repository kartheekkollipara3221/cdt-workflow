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
    [MemberData(nameof(RequiredCommandCases))]
    public void RequiredCommandsReturnIntentionalStubOutput(string command)
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch([command]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(command, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not implemented yet", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCommandReturnsNonZeroResult()
    {
        var registry = new CommandRegistry();

        var result = registry.Dispatch(["not-a-command"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown command", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public static TheoryData<string> RequiredCommandCases()
    {
        var data = new TheoryData<string>();
        foreach (var command in RequiredCommands)
        {
            data.Add(command);
        }

        return data;
    }
}
