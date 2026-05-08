using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;

namespace WorkflowEngine.Application.Commands;

public sealed class ExecuteJsonCommandHandler(
    WorkflowActionEnvelopeValidator validator,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    WorkflowActionResultBuilder resultBuilder,
    IWorkflowExecutionService workflowExecutionService) : IExecuteJsonCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseExecuteJson(args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var options = (ExecuteJsonCommandArguments)parseResult.Arguments!;

        if (!File.Exists(options.FilePath))
        {
            return CommandResult.Failure($"Envelope file not found: {options.FilePath}");
        }

        WorkflowActionEnvelope? envelope;
        try
        {
            var json = File.ReadAllText(options.FilePath);
            envelope = JsonSerializer.Deserialize<WorkflowActionEnvelope>(json, WorkflowJson.Options);
        }
        catch (JsonException exception)
        {
            return CommandResult.Failure($"Invalid JSON envelope: {exception.Message}");
        }

        if (envelope is null)
        {
            return CommandResult.Failure("Invalid JSON envelope: root object is required.");
        }

        var normalized = normalizer.Normalize(envelope, options.BusinessObjectOverride, options.WorkflowInstanceOverride);
        var validationErrors = validator.Validate(normalized.Envelope);
        var resultEnvelope = validationErrors.Count == 0
            ? workflowExecutionService.Execute(new WorkflowExecutionRequest(
                normalized,
                hasher.ComputeHash(normalized.Envelope),
                options.AdminOverride))
            : resultBuilder.BuildValidationFailed(normalized, validationErrors);

        return options.FormatJson
            ? CommandResult.Success(JsonSerializer.Serialize(resultEnvelope, WorkflowJson.Options))
            : CommandResult.Success(ToReadableText(resultEnvelope.Result));
    }

    private static string ToReadableText(WorkflowActionResult result)
    {
        var lines = new List<string>
        {
            $"Result: {result.ResultCode}",
            $"Success: {result.Success}",
            $"Workflow instance: {result.WorkflowInstanceId}",
            $"Business object: {result.BusinessObject?.ObjectType} / {result.BusinessObject?.ObjectId}",
            $"Action: {result.ActionKey}",
            $"Request hash: {result.RequestHash}",
            $"Message: {result.Message}"
        };

        if (result.Diagnostics.Replay)
        {
            lines.Add($"Idempotent replay: Yes (original action executed at {FormatTimestamp(result.Diagnostics.OriginalCreatedAtUtc)})");
        }

        if (!string.IsNullOrWhiteSpace(result.OldState) || !string.IsNullOrWhiteSpace(result.NewState))
        {
            lines.Add($"State: {result.OldState} -> {result.NewState}");
        }

        if (result.ClosedTask is not null)
        {
            lines.Add($"Closed task: {result.ClosedTask.TaskKey}");
        }

        if (result.CreatedTask is not null)
        {
            lines.Add($"Created task: {result.CreatedTask.TaskKey} ({result.CreatedTask.AssignedRole})");
        }

        if (result.Notification is not null)
        {
            lines.Add($"Notification queued: {result.Notification.Queued}");
        }

        if (result.Audit is not null)
        {
            lines.Add($"Audit written: {result.Audit.ActionKey}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        return value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

}
