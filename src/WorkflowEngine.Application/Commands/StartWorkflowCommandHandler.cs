using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Application.Commands;

public sealed class StartWorkflowCommandHandler(IStartWorkflowService startWorkflowService) : IStartWorkflowCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseStartWorkflow(args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var options = (StartWorkflowCommandArguments)parseResult.Arguments!;

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

    private static string ToReadableText(StartWorkflowCommandArguments options, StartWorkflowResult startResult)
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

}
