using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Notifications;

public sealed record QueueWorkflowNotificationRequest(
    string WorkflowKey,
    int WorkflowDefinitionVersion,
    string NotificationRouteKey,
    string NotificationOccurrenceKey,
    string BusinessObjectType,
    string BusinessObjectId,
    WorkflowNotificationInput NotificationInput,
    string CorrelationId);

public sealed record QueueWorkflowNotificationResult(
    bool Success,
    string ResultCode,
    long? NotificationOutboxId,
    bool Replay,
    IReadOnlyList<string> Errors);

public interface IQueueWorkflowNotificationService
{
    Task<QueueWorkflowNotificationResult> QueueAsync(
        QueueWorkflowNotificationRequest request,
        CancellationToken cancellationToken);
}
