namespace WorkflowEngine.Application.Notifications;

public interface INotificationSender
{
    string SenderName { get; }

    Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken);
}

public sealed record NotificationDelivery(
    long Id,
    string WorkflowInstanceId,
    long? WorkflowTaskId,
    string BusinessObjectType,
    string BusinessObjectId,
    string NotificationType,
    string? RecipientRoleKey,
    string? RecipientUserId,
    string Subject,
    string Body,
    string PayloadJson,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);
