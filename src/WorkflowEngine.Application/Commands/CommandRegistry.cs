namespace WorkflowEngine.Application.Commands;

using WorkflowEngine.Application.Persistence;

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

    private readonly IWorkflowDatabaseInitializer? databaseInitializer;
    private readonly ISeedVerificationService? seedVerificationService;
    private readonly IStartWorkflowCommandHandler? startWorkflowCommandHandler;
    private readonly IExecuteJsonCommandHandler? executeJsonCommandHandler;
    private readonly IShowContextCommandHandler? showContextCommandHandler;
    private readonly HashSet<string> knownCommands = new(RequiredCommandNames, StringComparer.OrdinalIgnoreCase);

    public CommandRegistry(
        IWorkflowDatabaseInitializer? databaseInitializer = null,
        ISeedVerificationService? seedVerificationService = null,
        IStartWorkflowCommandHandler? startWorkflowCommandHandler = null,
        IExecuteJsonCommandHandler? executeJsonCommandHandler = null,
        IShowContextCommandHandler? showContextCommandHandler = null)
    {
        this.databaseInitializer = databaseInitializer;
        this.seedVerificationService = seedVerificationService;
        this.startWorkflowCommandHandler = startWorkflowCommandHandler;
        this.executeJsonCommandHandler = executeJsonCommandHandler;
        this.showContextCommandHandler = showContextCommandHandler;
    }

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

        if (string.Equals(commandName, "init-db", StringComparison.OrdinalIgnoreCase))
        {
            if (databaseInitializer is null)
            {
                return CommandResult.Failure("Command 'init-db' requires database services that are not registered.");
            }

            return CommandResult.Success(databaseInitializer.Initialize());
        }

        if (string.Equals(commandName, "verify-seed", StringComparison.OrdinalIgnoreCase))
        {
            if (seedVerificationService is null)
            {
                return CommandResult.Failure("Command 'verify-seed' requires database services that are not registered.");
            }

            var result = seedVerificationService.Verify();
            return result.Passed
                ? CommandResult.Success("Seed verification passed.")
                : CommandResult.Failure("Seed verification failed:" + Environment.NewLine + string.Join(Environment.NewLine, result.Errors));
        }

        if (string.Equals(commandName, "execute-json", StringComparison.OrdinalIgnoreCase))
        {
            if (executeJsonCommandHandler is null)
            {
                return CommandResult.Failure("Command 'execute-json' requires command services that are not registered.");
            }

            return executeJsonCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "start-workflow", StringComparison.OrdinalIgnoreCase))
        {
            if (startWorkflowCommandHandler is null)
            {
                return CommandResult.Failure("Command 'start-workflow' requires command services that are not registered.");
            }

            return startWorkflowCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "show-context", StringComparison.OrdinalIgnoreCase))
        {
            if (showContextCommandHandler is null)
            {
                return CommandResult.Failure("Command 'show-context' requires command services that are not registered.");
            }

            return showContextCommandHandler.Execute(args.Skip(1).ToArray());
        }

        return CommandResult.Success($"Command '{commandName}' is recognized but not implemented yet.");
    }
}
