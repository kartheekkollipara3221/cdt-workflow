using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Application.Results;

public sealed class WorkflowActionResultBuilder
{
    public WorkflowActionResultEnvelope BuildAcceptedForPhase3Validation(NormalizedWorkflowAction normalized, string requestHash)
    {
        return new WorkflowActionResultEnvelope
        {
            Result = BaseResult(normalized, requestHash, success: true, resultCode: "AcceptedForPhase3Validation")
                with
                {
                    Message = "Envelope parsed, overrides applied, validation passed, and request hash computed. Workflow transition execution is deferred to Phase 4.",
                    Notification = new WorkflowNotificationSummary { Queued = false },
                    Audit = new WorkflowAuditSummary { Written = false }
                }
        };
    }

    public WorkflowActionResultEnvelope BuildValidationFailed(
        NormalizedWorkflowAction normalized,
        IReadOnlyList<WorkflowValidationError> errors)
    {
        var result = BaseResult(normalized, requestHash: null, success: false, resultCode: "ValidationFailed") with
        {
            Message = "Envelope validation failed: " + string.Join("; ", errors.Select(error => error.Message)),
            Diagnostics = new WorkflowActionDiagnostics
            {
                Replay = false,
                ValidationErrors = errors.Select(error => new WorkflowValidationDiagnostic
                {
                    Field = error.Field,
                    Rule = InferValidationRule(error),
                    Message = error.Message
                }).ToList()
            }
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildWorkflowInstanceNotFound(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string workflowInstanceId)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "WorkflowInstanceNotFound") with
        {
            Message = $"No workflow instance found for id '{workflowInstanceId}'."
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildWorkflowDefinitionVersionMismatch(
        NormalizedWorkflowAction normalized,
        string requestHash,
        int expected,
        int actual,
        WorkflowContext? workflowContext)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "WorkflowDefinitionVersionMismatch") with
        {
            Message = $"Envelope targets workflow definition version {expected} but the persisted instance is on version {actual}. Reload the form before retrying.",
            Expected = expected.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Actual = actual.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return new WorkflowActionResultEnvelope { Result = result, WorkflowContext = workflowContext };
    }

    public WorkflowActionResultEnvelope BuildInvalidTransition(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string message)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "InvalidTransition") with
        {
            Message = message
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildForbidden(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string roles,
        string actionKey,
        string state)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "Forbidden") with
        {
            Message = $"None of the actor's roles ({roles}) are authorized to perform '{actionKey}' from state '{state}'."
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildAmbiguousRoleMatch(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string matchingRoles,
        string actionKey,
        string state)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "AmbiguousRoleMatch") with
        {
            Message = $"Multiple actor roles ({matchingRoles}) authorize '{actionKey}' from state '{state}'. Use --admin-override or restrict actor roles."
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildConcurrencyConflict(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string failedGuard,
        string expected,
        string actual,
        string message,
        WorkflowContext? workflowContext)
    {
        var result = BaseResult(normalized, requestHash, success: false, resultCode: "ConcurrencyConflict") with
        {
            Message = message,
            Expected = expected,
            Actual = actual,
            FailedGuard = failedGuard
        };

        return new WorkflowActionResultEnvelope { Result = result, WorkflowContext = workflowContext };
    }

    public WorkflowActionResultEnvelope BuildIdempotencyConflict(
        NormalizedWorkflowAction normalized,
        string currentRequestHash,
        string storedRequestHash,
        string? storedActionKey)
    {
        var key = normalized.EffectiveIdempotencyKey;
        var result = BaseResult(normalized, currentRequestHash, success: false, resultCode: "IdempotencyConflict") with
        {
            Message = $"An idempotency record already exists for key '{key}' with a different request hash. The previous request used hash {Prefix(storedRequestHash)}; this request hashes to {Prefix(currentRequestHash)}.",
            Expected = storedRequestHash,
            Actual = currentRequestHash,
            Diagnostics = new WorkflowActionDiagnostics
            {
                RequestHash = currentRequestHash,
                Replay = false,
                StoredRequestHash = storedRequestHash,
                StoredActionKey = storedActionKey
            }
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildAlreadyExists(
        string workflowKey,
        string businessObjectType,
        string businessObjectId,
        ExistingWorkflowInstance existing)
    {
        return new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = false,
                ResultCode = "AlreadyExists",
                WorkflowInstanceId = existing.WorkflowInstanceId,
                BusinessObject = new WorkflowActionResultBusinessObject
                {
                    ObjectType = businessObjectType,
                    ObjectId = businessObjectId
                },
                Message = $"A workflow instance already exists for {workflowKey} / {businessObjectId} (Status: {existing.Status}, Version: {existing.Version}). v1 does not support starting a second workflow on the same business object identifier. Assign a new BusinessObjectId or use --reset for demo purposes only.",
                ExistingInstanceStatus = existing.Status,
                ExistingInstanceVersion = existing.Version,
                ExistingInstanceCurrentState = existing.CurrentState,
                ExistingInstanceStartedAtUtc = existing.StartedAtUtc,
                ExistingInstanceCompletedAtUtc = existing.CompletedAtUtc,
                Diagnostics = new WorkflowActionDiagnostics()
            }
        };
    }

    public WorkflowActionResultEnvelope BuildStartWorkflowSuccess(
        string workflowKey,
        string businessObjectType,
        string businessObjectId,
        string workflowInstanceId,
        string currentState,
        string status,
        int version,
        DateTimeOffset startedAtUtc,
        string startedByUserId)
    {
        return new WorkflowActionResultEnvelope
        {
            Result = new WorkflowActionResult
            {
                Success = true,
                ResultCode = "Success",
                WorkflowInstanceId = workflowInstanceId,
                BusinessObject = new WorkflowActionResultBusinessObject
                {
                    ObjectType = businessObjectType,
                    ObjectId = businessObjectId
                },
                Message = $"Workflow instance {workflowInstanceId} started for {workflowKey} / {businessObjectId}.",
                CurrentState = currentState,
                Status = status,
                Version = version,
                StartedAtUtc = startedAtUtc,
                StartedByUserId = startedByUserId,
                Diagnostics = new WorkflowActionDiagnostics()
            }
        };
    }

    public WorkflowActionResultEnvelope BuildExecutionValidatedSuccess(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string message)
    {
        var result = BaseResult(normalized, requestHash, success: true, resultCode: "Success") with
        {
            Message = message
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    public WorkflowActionResultEnvelope BuildExecutionSuccess(
        NormalizedWorkflowAction normalized,
        string requestHash,
        string oldState,
        string newState,
        WorkflowTaskChange? closedTask,
        WorkflowTaskChange? createdTask,
        WorkflowNotificationSummary? notification,
        WorkflowAuditSummary audit)
    {
        var result = BaseResult(normalized, requestHash, success: true, resultCode: "Success") with
        {
            Message = $"Action '{normalized.Envelope.Workflow?.ActionKey}' completed successfully.",
            OldState = oldState,
            NewState = newState,
            ClosedTask = closedTask,
            CreatedTask = createdTask,
            Notification = notification,
            Audit = audit
        };

        return new WorkflowActionResultEnvelope { Result = result };
    }

    private static WorkflowActionResultRecord BaseResult(
        NormalizedWorkflowAction normalized,
        string? requestHash,
        bool success,
        string resultCode)
    {
        var envelope = normalized.Envelope;
        return new WorkflowActionResultRecord
        {
            Success = success,
            ResultCode = resultCode,
            CorrelationId = normalized.EffectiveCorrelationId,
            WorkflowInstanceId = envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
            BusinessObject = new WorkflowActionResultBusinessObject
            {
                ObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                ObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty
            },
            ActionKey = envelope.Workflow?.ActionKey ?? string.Empty,
            RequestHash = requestHash,
            EffectiveIdempotencyKey = normalized.EffectiveIdempotencyKey,
            Diagnostics = new WorkflowActionDiagnostics
            {
                RequestHash = requestHash,
                Replay = false
            }
        };
    }

    private static string Prefix(string hash)
    {
        return hash.Length <= 8 ? hash : hash[..8];
    }

    private static string InferValidationRule(WorkflowValidationError error)
    {
        if (error.Message.Contains("required", StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return "required";
        }

        if (error.Message.Contains("at least one", StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains("empty", StringComparison.OrdinalIgnoreCase))
        {
            return "non-empty-array";
        }

        return "invalid";
    }

    private sealed record WorkflowActionResultRecord
    {
        public bool Success { get; init; }

        public string ResultCode { get; init; } = string.Empty;

        public string CorrelationId { get; init; } = string.Empty;

        public string WorkflowInstanceId { get; init; } = string.Empty;

        public WorkflowActionResultBusinessObject? BusinessObject { get; init; }

        public string ActionKey { get; init; } = string.Empty;

        public string? Message { get; init; }

        public string? RequestHash { get; init; }

        public string? EffectiveIdempotencyKey { get; init; }

        public string? Expected { get; init; }

        public string? Actual { get; init; }

        public string? FailedGuard { get; init; }

        public WorkflowTaskChange? ClosedTask { get; init; }

        public WorkflowTaskChange? CreatedTask { get; init; }

        public string? OldState { get; init; }

        public string? NewState { get; init; }

        public WorkflowNotificationSummary? Notification { get; init; }

        public WorkflowAuditSummary? Audit { get; init; }

        public WorkflowActionDiagnostics Diagnostics { get; init; } = new();

        public static implicit operator WorkflowActionResult(WorkflowActionResultRecord source)
        {
            return new WorkflowActionResult
            {
                Success = source.Success,
                ResultCode = source.ResultCode,
                CorrelationId = source.CorrelationId,
                WorkflowInstanceId = source.WorkflowInstanceId,
                BusinessObject = source.BusinessObject,
                ActionKey = source.ActionKey,
                Message = source.Message,
                RequestHash = source.RequestHash,
                EffectiveIdempotencyKey = source.EffectiveIdempotencyKey,
                Expected = source.Expected,
                Actual = source.Actual,
                FailedGuard = source.FailedGuard,
                OldState = source.OldState,
                NewState = source.NewState,
                ClosedTask = source.ClosedTask,
                CreatedTask = source.CreatedTask,
                Notification = source.Notification,
                Audit = source.Audit,
                Diagnostics = source.Diagnostics
            };
        }
    }
}

public sealed record ExistingWorkflowInstance(
    string WorkflowInstanceId,
    string Status,
    int Version,
    string CurrentState,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
