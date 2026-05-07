namespace WorkflowEngine.Application.Commands;

public sealed class CommandRegistry
{
    private static readonly string[] RequiredCommandNames =
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

    private readonly HashSet<string> knownCommands = new(RequiredCommandNames, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> KnownCommands => RequiredCommandNames;

    public bool IsKnown(string commandName) => knownCommands.Contains(commandName);

    public CommandResult Dispatch(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return CommandResult.Failure(
                "No command supplied. Known commands: " + string.Join(", ", RequiredCommandNames));
        }

        var commandName = args[0];
        if (!IsKnown(commandName))
        {
            return CommandResult.Failure(
                $"Unknown command '{commandName}'. Known commands: {string.Join(", ", RequiredCommandNames)}");
        }

        return CommandResult.Success(
            $"Command '{commandName}' is recognized but not implemented yet.");
    }
}
