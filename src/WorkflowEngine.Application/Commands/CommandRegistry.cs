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
    private readonly IShowCommandHandler? showCommandHandler;
    private readonly IShowTasksCommandHandler? showTasksCommandHandler;
    private readonly IShowHistoryCommandHandler? showHistoryCommandHandler;
    private readonly IShowNotificationsCommandHandler? showNotificationsCommandHandler;
    private readonly IRunSlaCommandHandler? runSlaCommandHandler;
    private readonly IProcessNotificationOutboxCommandHandler? processNotificationOutboxCommandHandler;
    private readonly IDemoCommandHandler? demoCommandHandler;
    private readonly HashSet<string> knownCommands = new(RequiredCommandNames, StringComparer.OrdinalIgnoreCase);

    public CommandRegistry(
        IWorkflowDatabaseInitializer? databaseInitializer = null,
        ISeedVerificationService? seedVerificationService = null,
        IStartWorkflowCommandHandler? startWorkflowCommandHandler = null,
        IExecuteJsonCommandHandler? executeJsonCommandHandler = null,
        IShowContextCommandHandler? showContextCommandHandler = null,
        IShowCommandHandler? showCommandHandler = null,
        IShowTasksCommandHandler? showTasksCommandHandler = null,
        IShowHistoryCommandHandler? showHistoryCommandHandler = null,
        IShowNotificationsCommandHandler? showNotificationsCommandHandler = null,
        IRunSlaCommandHandler? runSlaCommandHandler = null,
        IProcessNotificationOutboxCommandHandler? processNotificationOutboxCommandHandler = null,
        IDemoCommandHandler? demoCommandHandler = null)
    {
        this.databaseInitializer = databaseInitializer;
        this.seedVerificationService = seedVerificationService;
        this.startWorkflowCommandHandler = startWorkflowCommandHandler;
        this.executeJsonCommandHandler = executeJsonCommandHandler;
        this.showContextCommandHandler = showContextCommandHandler;
        this.showCommandHandler = showCommandHandler;
        this.showTasksCommandHandler = showTasksCommandHandler;
        this.showHistoryCommandHandler = showHistoryCommandHandler;
        this.showNotificationsCommandHandler = showNotificationsCommandHandler;
        this.runSlaCommandHandler = runSlaCommandHandler;
        this.processNotificationOutboxCommandHandler = processNotificationOutboxCommandHandler;
        this.demoCommandHandler = demoCommandHandler;
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

        if (string.Equals(commandName, "show", StringComparison.OrdinalIgnoreCase))
        {
            if (showCommandHandler is null)
            {
                return CommandResult.Failure("Command 'show' requires command services that are not registered.");
            }

            return showCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "show-tasks", StringComparison.OrdinalIgnoreCase))
        {
            if (showTasksCommandHandler is null)
            {
                return CommandResult.Failure("Command 'show-tasks' requires command services that are not registered.");
            }

            return showTasksCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "show-history", StringComparison.OrdinalIgnoreCase))
        {
            if (showHistoryCommandHandler is null)
            {
                return CommandResult.Failure("Command 'show-history' requires command services that are not registered.");
            }

            return showHistoryCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "show-notifications", StringComparison.OrdinalIgnoreCase))
        {
            if (showNotificationsCommandHandler is null)
            {
                return CommandResult.Failure("Command 'show-notifications' requires command services that are not registered.");
            }

            return showNotificationsCommandHandler.Execute(args.Skip(1).ToArray());
        }

        if (string.Equals(commandName, "run-sla", StringComparison.OrdinalIgnoreCase))
        {
            if (runSlaCommandHandler is null)
            {
                return CommandResult.Failure("Command 'run-sla' requires command services that are not registered.");
            }

            return runSlaCommandHandler.RunAsync(args.Skip(1).ToArray(), CancellationToken.None).GetAwaiter().GetResult();
        }

        if (string.Equals(commandName, "process-notification-outbox", StringComparison.OrdinalIgnoreCase))
        {
            if (processNotificationOutboxCommandHandler is null)
            {
                return CommandResult.Failure("Command 'process-notification-outbox' requires command services that are not registered.");
            }

            return processNotificationOutboxCommandHandler.RunAsync(args.Skip(1).ToArray(), CancellationToken.None).GetAwaiter().GetResult();
        }

        if (string.Equals(commandName, "demo", StringComparison.OrdinalIgnoreCase))
        {
            if (demoCommandHandler is null)
            {
                return CommandResult.Failure("Command 'demo' requires command services that are not registered.");
            }

            return demoCommandHandler.Execute(args.Skip(1).ToArray());
        }

        return CommandResult.Failure($"Command '{commandName}' is known but has no registered handler.");
    }
}
