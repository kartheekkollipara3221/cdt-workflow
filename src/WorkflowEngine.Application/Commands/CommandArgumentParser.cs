using System.Globalization;

namespace WorkflowEngine.Application.Commands;

public interface IParsedCommandArguments;

public sealed record ParseResult(bool Success, string? ErrorMessage, IParsedCommandArguments? Arguments)
{
    public static ParseResult Ok(IParsedCommandArguments arguments) => new(true, null, arguments);

    public static ParseResult Fail(string errorMessage) => new(false, errorMessage, null);
}

public sealed record NoCommandArguments : IParsedCommandArguments;

public sealed record StartWorkflowCommandArguments(
    string WorkflowKey,
    string BusinessObjectId,
    string UserId,
    bool Reset,
    bool FormatJson) : IParsedCommandArguments;

public sealed record ExecuteJsonCommandArguments(
    string FilePath,
    string? BusinessObjectOverride,
    string? WorkflowInstanceOverride,
    bool AdminOverride,
    bool FormatJson) : IParsedCommandArguments;

public sealed record ShowContextCommandArguments(
    string WorkflowInstanceId,
    string UserId,
    IReadOnlyList<string> Roles) : IParsedCommandArguments;

public sealed record WorkflowInstanceCommandArguments(string WorkflowInstanceId) : IParsedCommandArguments;

public sealed record RunSlaCommandArguments(DateTimeOffset? Now) : IParsedCommandArguments;

public static class CommandArgumentParser
{
    public static ParseResult Parse(string commandName, IReadOnlyList<string> args)
    {
        return commandName.ToLowerInvariant() switch
        {
            "init-db" => ParseNoArgs(args, "Usage: init-db"),
            "verify-seed" => ParseNoArgs(args, "Usage: verify-seed"),
            "start-workflow" => ParseStartWorkflow(args),
            "execute-json" => ParseExecuteJson(args),
            "show-context" => ParseShowContext(args),
            "show" => ParseWorkflowInstance(args, "Usage: show <workflowInstanceId>"),
            "show-tasks" => ParseWorkflowInstance(args, "Usage: show-tasks <workflowInstanceId>"),
            "show-history" => ParseWorkflowInstance(args, "Usage: show-history <workflowInstanceId>"),
            "show-notifications" => ParseWorkflowInstance(args, "Usage: show-notifications <workflowInstanceId>"),
            "run-sla" => ParseRunSla(args),
            "process-notification-outbox" => ParseNoArgs(args, "Usage: process-notification-outbox"),
            "demo" => ParseNoArgs(args, "Usage: demo"),
            _ => ParseResult.Fail($"Unknown command '{commandName}'.")
        };
    }

    public static ParseResult ParseStartWorkflow(IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            return ParseResult.Fail("Usage: start-workflow <workflowKey> <businessObjectId> --user <userId> [--reset] [--format json]");
        }

        var workflowKey = args[0];
        var businessObjectId = args[1];
        var userId = string.Empty;
        var reset = false;
        var formatJson = false;

        for (var index = 2; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--user", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--user requires a value.");
                }

                userId = args[index];
                continue;
            }

            if (string.Equals(arg, "--reset", StringComparison.OrdinalIgnoreCase))
            {
                reset = true;
                continue;
            }

            if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--format requires a value.");
                }

                if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseResult.Fail("Only --format json is supported for start-workflow.");
                }

                formatJson = true;
                continue;
            }

            return ParseResult.Fail($"Unknown start-workflow option '{arg}'.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return ParseResult.Fail("--user is required.");
        }

        return ParseResult.Ok(new StartWorkflowCommandArguments(workflowKey, businessObjectId, userId, reset, formatJson));
    }

    public static ParseResult ParseExecuteJson(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ParseResult.Fail("Usage: execute-json <path-to-envelope.json> [--business-object <id>] [--workflow-instance <id>] [--admin-override] [--format json]");
        }

        var filePath = args[0];
        string? businessObjectOverride = null;
        string? workflowInstanceOverride = null;
        var adminOverride = false;
        var formatJson = false;

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--business-object", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--business-object requires a value.");
                }

                businessObjectOverride = args[index];
                continue;
            }

            if (string.Equals(arg, "--workflow-instance", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--workflow-instance requires a value.");
                }

                workflowInstanceOverride = args[index];
                continue;
            }

            if (string.Equals(arg, "--admin-override", StringComparison.OrdinalIgnoreCase))
            {
                adminOverride = true;
                continue;
            }

            if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--format requires a value.");
                }

                if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseResult.Fail("Only --format json is supported for execute-json.");
                }

                formatJson = true;
                continue;
            }

            return ParseResult.Fail($"Unknown execute-json option '{arg}'.");
        }

        return ParseResult.Ok(new ExecuteJsonCommandArguments(filePath, businessObjectOverride, workflowInstanceOverride, adminOverride, formatJson));
    }

    public static ParseResult ParseShowContext(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ParseResult.Fail("Usage: show-context <workflowInstanceId> --user <userId> --roles <comma-separated-roles> [--format json]");
        }

        var workflowInstanceId = args[0];
        string? userId = null;
        IReadOnlyList<string>? roles = null;

        for (var index = 1; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--user", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--user requires a value.");
                }

                userId = args[index];
                continue;
            }

            if (string.Equals(arg, "--roles", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--roles requires a value.");
                }

                roles = args[index]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .ToArray();
                continue;
            }

            if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Count)
                {
                    return ParseResult.Fail("--format requires a value.");
                }

                if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseResult.Fail("Only --format json is supported for show-context.");
                }

                continue;
            }

            return ParseResult.Fail($"Unknown show-context option '{arg}'.");
        }

        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            return ParseResult.Fail("workflowInstanceId is required.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return ParseResult.Fail("--user is required.");
        }

        if (roles is null || roles.Count == 0)
        {
            return ParseResult.Fail("--roles must contain at least one role.");
        }

        return ParseResult.Ok(new ShowContextCommandArguments(workflowInstanceId, userId, roles));
    }

    public static ParseResult ParseWorkflowInstance(IReadOnlyList<string> args, string usage)
    {
        return args.Count == 1 && !string.IsNullOrWhiteSpace(args[0])
            ? ParseResult.Ok(new WorkflowInstanceCommandArguments(args[0]))
            : ParseResult.Fail(usage);
    }

    public static ParseResult ParseRunSla(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return ParseResult.Ok(new RunSlaCommandArguments(null));
        }

        if (args.Count != 2 || !string.Equals(args[0], "--now", StringComparison.OrdinalIgnoreCase))
        {
            return ParseResult.Fail("Usage: run-sla [--now <iso8601-utc>]");
        }

        if (!DateTimeOffset.TryParse(args[1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var now))
        {
            return ParseResult.Fail($"Invalid --now value '{args[1]}'. Use an ISO-8601 UTC timestamp.");
        }

        return ParseResult.Ok(new RunSlaCommandArguments(now.ToUniversalTime()));
    }

    private static ParseResult ParseNoArgs(IReadOnlyList<string> args, string usage)
    {
        return args.Count == 0
            ? ParseResult.Ok(new NoCommandArguments())
            : ParseResult.Fail(usage);
    }
}
