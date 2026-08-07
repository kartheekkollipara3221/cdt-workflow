using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.WorkflowStart;

public interface IStartWorkflowService
{
    StartWorkflowResult Start(StartWorkflowRequest request);
}

public sealed record StartWorkflowRequest(
    string WorkflowKey,
    string BusinessObjectType,
    string BusinessObjectId,
    string StartedByUserId,
    bool Reset,
    string? InitialActionKey = null,
    string? StartedByDisplayName = null,
    IReadOnlyList<string>? StartedByRoles = null,
    IReadOnlyDictionary<string, string?>? NotificationContext = null,
    NotificationRecipients? NotificationRecipients = null,
    WorkflowSlaInput? SlaInput = null,
    string? IdempotencyKey = null);

public sealed record StartWorkflowResult(
    WorkflowActionResultEnvelope Envelope,
    StartWorkflowResetCounts? ResetCounts);

public sealed record StartWorkflowResetCounts(
    int IdempotencyRecordRows,
    int NotificationOutboxRows,
    int WorkflowAuditRows,
    int MockSectionDataRows,
    int WorkflowTaskSlaScheduleRows,
    int WorkflowTaskRows,
    int WorkflowInstanceRows);
