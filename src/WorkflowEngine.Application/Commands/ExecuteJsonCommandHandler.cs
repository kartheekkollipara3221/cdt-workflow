using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Application.Commands;

public sealed class ExecuteJsonCommandHandler(
    WorkflowActionEnvelopeValidator validator,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher) : IExecuteJsonCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var options = ExecuteJsonOptions.Parse(args);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            return CommandResult.Failure(options.Error);
        }

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
        var requestHash = hasher.ComputeHash(normalized.Envelope);
        var result = validationErrors.Count == 0
            ? CreateDeferredResult(normalized, requestHash)
            : CreateValidationFailedResult(normalized, requestHash, validationErrors);

        return options.FormatJson
            ? CommandResult.Success(JsonSerializer.Serialize(new WorkflowActionResultEnvelope { Result = result }, WorkflowJson.Options))
            : CommandResult.Success(ToReadableText(result));
    }

    private static WorkflowActionResult CreateDeferredResult(NormalizedWorkflowAction normalized, string requestHash)
    {
        var envelope = normalized.Envelope;
        return new WorkflowActionResult
        {
            Success = true,
            ResultCode = "AcceptedForPhase3Validation",
            CorrelationId = normalized.EffectiveCorrelationId,
            WorkflowInstanceId = envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
            BusinessObject = new WorkflowActionResultBusinessObject
            {
                ObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                ObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty
            },
            ActionKey = envelope.Workflow?.ActionKey ?? string.Empty,
            Message = "Envelope parsed, overrides applied, validation passed, and request hash computed. Workflow transition execution is deferred to Phase 4.",
            RequestHash = requestHash,
            EffectiveIdempotencyKey = normalized.EffectiveIdempotencyKey,
            Notification = new WorkflowNotificationSummary { Queued = false },
            Audit = new WorkflowAuditSummary { Written = false }
        };
    }

    private static WorkflowActionResult CreateValidationFailedResult(
        NormalizedWorkflowAction normalized,
        string requestHash,
        IReadOnlyList<WorkflowValidationError> errors)
    {
        var envelope = normalized.Envelope;
        return new WorkflowActionResult
        {
            Success = false,
            ResultCode = "ValidationFailed",
            CorrelationId = normalized.EffectiveCorrelationId,
            WorkflowInstanceId = envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
            BusinessObject = new WorkflowActionResultBusinessObject
            {
                ObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                ObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty
            },
            ActionKey = envelope.Workflow?.ActionKey ?? string.Empty,
            Message = string.Join("; ", errors.Select(error => $"{error.Field}: {error.Message}")),
            RequestHash = requestHash,
            EffectiveIdempotencyKey = normalized.EffectiveIdempotencyKey,
            Notification = new WorkflowNotificationSummary { Queued = false },
            Audit = new WorkflowAuditSummary { Written = false }
        };
    }

    private static string ToReadableText(WorkflowActionResult result)
    {
        return string.Join(
            Environment.NewLine,
            $"Result: {result.ResultCode}",
            $"Success: {result.Success}",
            $"Workflow instance: {result.WorkflowInstanceId}",
            $"Business object: {result.BusinessObject?.ObjectType} / {result.BusinessObject?.ObjectId}",
            $"Action: {result.ActionKey}",
            $"Request hash: {result.RequestHash}",
            $"Message: {result.Message}");
    }

    private sealed record ExecuteJsonOptions(
        string FilePath,
        string? BusinessObjectOverride,
        string? WorkflowInstanceOverride,
        bool FormatJson,
        string? Error)
    {
        public static ExecuteJsonOptions Parse(IReadOnlyList<string> args)
        {
            if (args.Count == 0)
            {
                return new ExecuteJsonOptions(string.Empty, null, null, false, "Usage: execute-json <path-to-envelope.json> [--business-object <id>] [--workflow-instance <id>] [--format json]");
            }

            var filePath = args[0];
            string? businessObjectOverride = null;
            string? workflowInstanceOverride = null;
            var formatJson = false;

            for (var index = 1; index < args.Count; index++)
            {
                var arg = args[index];
                if (string.Equals(arg, "--business-object", StringComparison.OrdinalIgnoreCase))
                {
                    if (++index >= args.Count)
                    {
                        return new ExecuteJsonOptions(filePath, null, null, false, "--business-object requires a value.");
                    }

                    businessObjectOverride = args[index];
                    continue;
                }

                if (string.Equals(arg, "--workflow-instance", StringComparison.OrdinalIgnoreCase))
                {
                    if (++index >= args.Count)
                    {
                        return new ExecuteJsonOptions(filePath, null, null, false, "--workflow-instance requires a value.");
                    }

                    workflowInstanceOverride = args[index];
                    continue;
                }

                if (string.Equals(arg, "--format", StringComparison.OrdinalIgnoreCase))
                {
                    if (++index >= args.Count)
                    {
                        return new ExecuteJsonOptions(filePath, null, null, false, "--format requires a value.");
                    }

                    if (!string.Equals(args[index], "json", StringComparison.OrdinalIgnoreCase))
                    {
                        return new ExecuteJsonOptions(filePath, null, null, false, "Only --format json is supported for execute-json.");
                    }

                    formatJson = true;
                    continue;
                }

                return new ExecuteJsonOptions(filePath, null, null, false, $"Unknown execute-json option '{arg}'.");
            }

            return new ExecuteJsonOptions(filePath, businessObjectOverride, workflowInstanceOverride, formatJson, null);
        }
    }
}

