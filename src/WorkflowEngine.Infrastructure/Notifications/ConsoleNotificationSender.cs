using System.Globalization;
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class ConsoleNotificationSender : INotificationSender
{
    private readonly TextWriter writer;

    public ConsoleNotificationSender()
        : this(Console.Out)
    {
    }

    public ConsoleNotificationSender(TextWriter writer)
    {
        this.writer = writer;
    }

    public string SenderName => "Console";

    public Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
    {
        writer.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "[Sent] {0,-24} | {1,-14} | {2,-35} | {3}",
            notification.NotificationType,
            notification.RecipientRoleKey ?? "(none)",
            notification.Subject,
            notification.WorkflowInstanceId));
        return Task.CompletedTask;
    }
}
