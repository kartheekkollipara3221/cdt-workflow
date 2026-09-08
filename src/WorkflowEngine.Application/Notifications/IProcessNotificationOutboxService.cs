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
    private IReadOnlyList<NotificationOutboxFailure>? failures;
    private int? notAttemptedCount;

    public IReadOnlyList<NotificationOutboxFailure> Failures
    {
        get => failures ?? (Failure is null ? [] : [Failure]);
        init => failures = value;
    }

    public int NotAttemptedCount
    {
        get => notAttemptedCount ?? (Failure is null ? 0 : PendingCount - Processed.Count - 1);
        init => notAttemptedCount = value;
    }
}

public sealed record ProcessedNotification(
    long Id,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    string Subject,
    string? WorkflowInstanceId)
{
    public string? NotificationOccurrenceKey { get; init; }

    public string DisplayReference =>
        WorkflowInstanceId ?? NotificationOccurrenceKey ?? "Unowned";
}

public sealed record NotificationOutboxFailure(
    long Id,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    string Subject,
    string? WorkflowInstanceId,
    Exception Exception)
{
    public string? NotificationOccurrenceKey { get; init; }

    public string DisplayReference =>
        WorkflowInstanceId ?? NotificationOccurrenceKey ?? "Unowned";
}
