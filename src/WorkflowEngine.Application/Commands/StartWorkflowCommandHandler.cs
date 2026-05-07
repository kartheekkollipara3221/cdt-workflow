using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Application.Commands;

public sealed class StartWorkflowCommandHandler(IStartWorkflowService startWorkflowService) : IStartWorkflowCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var options = StartWorkflowOptions.Parse(args);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            return CommandResult.Failure(options.Error);
        }

        try
        {
            var result = startWorkflowService.Start(new StartWorkflowRequest(
                options.WorkflowKey,
                options.WorkflowKey,
                options.BusinessObjectId,
                options.UserId,
                options.Reset));

            return options.FormatJson
                ? CommandResult.Success(JsonSerializer.Serialize(result.Envelope, WorkflowJson.Options))
                : CommandResult.Success(ToReadableText(options, result));
        }
        catch (ArgumentException exception)
        {
            return CommandResult.Failure(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return CommandResult.Failure(exception.Message);
        }
    }

    private static string ToReadableText(StartWorkflowOptions options, StartWorkflowResult startResult)
    {
        var result = startResult.Envelope.Result;
        var lines = new List<string>
        {
            "Command: StartWorkflow",
            $"Result code: {result.ResultCode}"
        };

        if (options.Reset)
        {
            lines.Add("Reset: Yes");
        }

        lines.Add($"Workflow key: {options.WorkflowKey}");
        lines.Add($"Business object: {options.WorkflowKey} / {options.BusinessObjectId}");
        lines.Add($"Workflow instance: {result.WorkflowInstanceId}");
        lines.Add(string.Empty);

        if (startResult.ResetCounts is not null)
        {
            lines.Add("Cleared runtime data:");
            lines.Add($"  IdempotencyRecord rows:    {startResult.ResetCounts.IdempotencyRecordRows}");
            lines.Add($"  NotificationOutbox rows:   {startResult.ResetCounts.NotificationOutboxRows}");
            lines.Add($"  WorkflowAudit rows:        {startResult.ResetCounts.WorkflowAuditRows}");
            lines.Add($"  MockSectionData rows:      {startResult.ResetCounts.MockSectionDataRows}");
            lines.Add($"  WorkflowTask rows:         {startResult.ResetCounts.WorkflowTaskRows}");
            lines.Add($"  WorkflowInstance rows:     {startResult.ResetCounts.WorkflowInstanceRows}");
            lines.Add(string.Empty);
            lines.Add("Created fresh workflow instance:");
        }

        if (string.Equals(result.ResultCode, "AlreadyExists", StringComparison.Ordinal))
        {
            lines.Add($"Existing instance status: {result.ExistingInstanceStatus}");
            lines.Add($"Existing instance version: {result.ExistingInstanceVersion}");
            lines.Add($"Existing instance current state: {result.ExistingInstanceCurrentState}");
            lines.Add($"Started at: {FormatTimestamp(result.ExistingInstanceStartedAtUtc)}");
            lines.Add($"Completed at: {FormatTimestamp(result.ExistingInstanceCompletedAtUtc)}");
            lines.Add(string.Empty);
            lines.Add($"Message: {result.Message}");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add($"Current state: {result.CurrentState}");
        lines.Add($"Status: {result.Status}");
        lines.Add($"Version: {result.Version}");
        lines.Add($"Started at: {FormatTimestamp(result.StartedAtUtc)}");
        lines.Add($"Started by: {result.StartedByUserId}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        return value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed record StartWorkflowOptions(
        string WorkflowKey,
        string BusinessObjectId,
        string UserId,
        bool Reset,
        bool FormatJson,
        string? Error)
    {
        public static StartWorkflowOptions Parse(IReadOnlyList<string> args)
        {
            if (args.Count < 2)
            {
                return Failure("Usage: start-workflow <workflowKey> <businessObjectId> --user <userId> [--reset] [--format json]");
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
                        return Failure("--user requires a value.");
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
                        return Failure("--format requires a value.");
                    }

                    if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                    {
                        return Failure("Only --format json is supported for start-workflow.");
                    }

                    formatJson = true;
                    continue;
                }

                return Failure($"Unknown start-workflow option '{arg}'.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Failure("--user is required.");
            }

            return new StartWorkflowOptions(workflowKey, businessObjectId, userId, reset, formatJson, null);
        }

        private static StartWorkflowOptions Failure(string error)
        {
            return new StartWorkflowOptions(string.Empty, string.Empty, string.Empty, false, false, error);
        }
    }
}
