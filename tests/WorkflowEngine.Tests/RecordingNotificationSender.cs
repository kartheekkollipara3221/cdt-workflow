using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Tests;

internal sealed class RecordingNotificationSender : INotificationSender
{
    private readonly long? throwOnId;

    public RecordingNotificationSender(long? throwOnId = null)
    {
        this.throwOnId = throwOnId;
    }

    public string SenderName => "Recording";

    public List<NotificationDelivery> SentRows { get; } = [];

    public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
    {
        if (throwOnId == notification.Id)
        {
            throw new InvalidOperationException($"send failed for {notification.Id}");
        }

        SentRows.Add(notification);
        return Task.CompletedTask;
    }
}
