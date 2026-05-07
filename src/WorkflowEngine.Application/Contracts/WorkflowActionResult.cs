namespace WorkflowEngine.Application.Contracts;

public sealed class WorkflowActionResultEnvelope
{
    public WorkflowActionResult Result { get; set; } = new();

    public WorkflowContext? WorkflowContext { get; set; }
}

public sealed class WorkflowActionResult
{
    public bool Success { get; set; }

    public string ResultCode { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string WorkflowInstanceId { get; set; } = string.Empty;

    public WorkflowActionResultBusinessObject? BusinessObject { get; set; }

    public string ActionKey { get; set; } = string.Empty;

    public string? Message { get; set; }

    public string? RequestHash { get; set; }

    public string? EffectiveIdempotencyKey { get; set; }

    public string? Expected { get; set; }

    public string? Actual { get; set; }

    public string? FailedGuard { get; set; }

    public string? OldState { get; set; }

    public string? NewState { get; set; }

    public WorkflowTaskChange? ClosedTask { get; set; }

    public WorkflowTaskChange? CreatedTask { get; set; }

    public WorkflowNotificationSummary? Notification { get; set; }

    public WorkflowAuditSummary? Audit { get; set; }

    public string? ExistingInstanceStatus { get; set; }

    public int? ExistingInstanceVersion { get; set; }

    public string? ExistingInstanceCurrentState { get; set; }

    public DateTimeOffset? ExistingInstanceStartedAtUtc { get; set; }

    public DateTimeOffset? ExistingInstanceCompletedAtUtc { get; set; }

    public WorkflowActionDiagnostics Diagnostics { get; set; } = new();
}

public sealed class WorkflowActionDiagnostics
{
    public string? RequestHash { get; set; }

    public string? ResultHash { get; set; }

    public bool Replay { get; set; }

    public DateTimeOffset? OriginalCreatedAtUtc { get; set; }

    public List<WorkflowValidationDiagnostic>? ValidationErrors { get; set; }

    public string? StoredRequestHash { get; set; }

    public string? StoredActionKey { get; set; }
}

public sealed class WorkflowValidationDiagnostic
{
    public string Field { get; set; } = string.Empty;

    public string Rule { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public sealed class WorkflowActionResultBusinessObject
{
    public string ObjectType { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;
}

public sealed class WorkflowTaskChange
{
    public string? TaskId { get; set; }

    public string? TaskKey { get; set; }

    public string? AssignedRole { get; set; }

    public DateTimeOffset? DueAtUtc { get; set; }
}

public sealed class WorkflowNotificationSummary
{
    public bool Queued { get; set; }

    public string? NotificationType { get; set; }

    public string? RecipientRole { get; set; }
}

public sealed class WorkflowAuditSummary
{
    public bool Written { get; set; }

    public string? ActionKey { get; set; }
}
