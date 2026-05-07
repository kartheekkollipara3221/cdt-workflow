using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class NoOpNotificationSender : INotificationSender
{
    public string SenderName => "NoOp";

    public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
