namespace WorkflowEngine.Application.Inspection;

public interface IWorkflowInspectionReader
{
    WorkflowInspectionSnapshot Read(string workflowInstanceId);
}

public sealed record WorkflowInspectionSnapshot(
    WorkflowInstanceInspection? Instance,
    IReadOnlyList<WorkflowTaskInspection> Tasks,
    IReadOnlyList<WorkflowAuditInspection> History,
    IReadOnlyList<NotificationInspection> Notifications)
{
    public bool Found => Instance is not null;
}

public sealed record WorkflowInstanceInspection(
    string WorkflowInstanceId,
    string WorkflowKey,
    int WorkflowDefinitionVersion,
    string BusinessObjectType,
    string BusinessObjectId,
    string CurrentStateKey,
    string Status,
    DateTimeOffset StartedAtUtc,
    string StartedByUserId,
    DateTimeOffset? CompletedAtUtc,
    int Version);

public sealed record WorkflowTaskInspection(
    long Id,
    string TaskKey,
    string AssignedRoleKey,
    string? AssignedUserId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? CompletedByUserId,
    string? ClosedByActionKey);

public sealed record WorkflowAuditInspection(
    long Id,
    string ActionKey,
    string FromStateKey,
    string ToStateKey,
    string PerformedByUserId,
    string PerformedByRoleKey,
    string? Reason,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);

public sealed record NotificationInspection(
    long Id,
    long? WorkflowTaskId,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    bool HasInvalidRecipientSnapshot,
    string Subject,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SentAtUtc);
