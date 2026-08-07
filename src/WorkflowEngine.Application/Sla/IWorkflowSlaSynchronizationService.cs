namespace WorkflowEngine.Application.Sla;

public interface IWorkflowSlaSynchronizationService
{
    WorkflowSlaSynchronizationResult Synchronize(WorkflowSlaSynchronizationRequest request);

    WorkflowSlaSynchronizationResult Complete(WorkflowSlaCompletionRequest request);
}

public sealed record WorkflowSlaSynchronizationRequest(
    string WorkflowInstanceId,
    long? WorkflowTaskId,
    DateOnly? TargetDate,
    string ActorUserId,
    string CorrelationId,
    string IdempotencyKey);

public sealed record WorkflowSlaCompletionRequest(
    string WorkflowInstanceId,
    long WorkflowTaskId,
    string ActorUserId,
    string CorrelationId);

public sealed record WorkflowSlaSynchronizationResult(
    bool Changed,
    int ScheduleVersion,
    DateOnly? TargetDate,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? NextReminderAtUtc);

public sealed class WorkflowSlaValidationException(string field, string message) : InvalidOperationException(message)
{
    public string Field { get; } = field;
}
