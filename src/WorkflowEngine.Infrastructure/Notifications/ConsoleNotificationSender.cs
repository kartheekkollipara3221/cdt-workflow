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
            "[Sent] {0,-24} | To: {1} | Cc: {2} | {3,-35} | {4}",
            notification.NotificationType,
            string.Join(", ", notification.ToEmailAddresses),
            string.Join(", ", notification.CcEmailAddresses),
            notification.Subject,
            notification.DisplayReference));
        return Task.CompletedTask;
    }
}
