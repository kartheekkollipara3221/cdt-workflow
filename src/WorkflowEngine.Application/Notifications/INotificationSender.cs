namespace WorkflowEngine.Application.Notifications;

public interface INotificationSender
{
    string SenderName { get; }

    Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken);
}

public sealed record NotificationDelivery(
    long Id,
    string? WorkflowInstanceId,
    long? WorkflowTaskId,
    string BusinessObjectType,
    string BusinessObjectId,
    string NotificationType,
    IReadOnlyList<string> ToEmailAddresses,
    IReadOnlyList<string> CcEmailAddresses,
    string Subject,
    string Body,
    string HtmlBody,
    string PayloadJson,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc)
{
    public string? NotificationOccurrenceKey { get; init; }

    public string DisplayReference =>
        WorkflowInstanceId ?? NotificationOccurrenceKey ?? "Unowned";
}
