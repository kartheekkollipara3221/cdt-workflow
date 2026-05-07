using System.Text.Json;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Commands;

public sealed class ShowContextCommandHandler(IWorkflowContextReader contextReader) : IShowContextCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var options = ShowContextOptions.Parse(args);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            return CommandResult.Failure(options.Error);
        }

        try
        {
            var response = contextReader.Read(new WorkflowContextReadRequest(
                options.WorkflowInstanceId,
                options.UserId,
                options.Roles));

            return CommandResult.Success(JsonSerializer.Serialize(response, WorkflowJson.Options));
        }
        catch (KeyNotFoundException exception)
        {
            return CommandResult.Failure(exception.Message);
        }
    }

    private sealed record ShowContextOptions(
        string WorkflowInstanceId,
        string UserId,
        IReadOnlyList<string> Roles,
        string? Error)
    {
        public static ShowContextOptions Parse(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                return Failure("Usage: show-context <workflowInstanceId> --user <userId> --roles <comma-separated-roles> [--format json]");
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
                        return Failure("--user requires a value.");
                    }

                    userId = args[index];
                    continue;
                }

                if (string.Equals(arg, "--roles", StringComparison.OrdinalIgnoreCase))
                {
                    if (++index >= args.Count)
                    {
                        return Failure("--roles requires a value.");
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
                        return Failure("--format requires a value.");
                    }

                    if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                    {
                        return Failure("Only --format json is supported for show-context.");
                    }

                    continue;
                }

                return Failure($"Unknown show-context option '{arg}'.");
            }

            if (string.IsNullOrWhiteSpace(workflowInstanceId))
            {
                return Failure("workflowInstanceId is required.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Failure("--user is required.");
            }

            if (roles is null || roles.Count == 0)
            {
                return Failure("--roles must contain at least one role.");
            }

            return new ShowContextOptions(workflowInstanceId, userId, roles, null);
        }

        private static ShowContextOptions Failure(string error)
        {
            return new ShowContextOptions(string.Empty, string.Empty, [], error);
        }
    }
}

