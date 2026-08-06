namespace WorkflowEngine.Application.Notifications;

public interface IProcessNotificationOutboxService
{
    Task<ProcessNotificationOutboxResult> ProcessAsync(CancellationToken cancellationToken);
}

public sealed record ProcessNotificationOutboxResult(
    string SenderName,
    int PendingCount,
    IReadOnlyList<ProcessedNotification> Processed,
    IReadOnlyList<string> Warnings,
    NotificationOutboxFailure? Failure)
{
    public int NotAttemptedCount => Failure is null ? 0 : PendingCount - Processed.Count - 1;
}

public sealed record ProcessedNotification(
    long Id,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    string Subject,
    string WorkflowInstanceId);

public sealed record NotificationOutboxFailure(
    long Id,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    string Subject,
    string WorkflowInstanceId,
    Exception Exception);
