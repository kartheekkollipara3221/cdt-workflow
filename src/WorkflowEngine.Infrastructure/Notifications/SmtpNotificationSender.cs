using System.Text;
using System.Text.Encodings.Web;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly string host;
    private readonly int port;
    private readonly string from;
    private readonly string? recipientOverride;

    public SmtpNotificationSender(IConfiguration configuration)
    {
        ValidateConfiguration(configuration);
        host = configuration["Smtp:Host"]!.Trim();
        from = configuration["Smtp:From"]!.Trim();
        port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 25;
        recipientOverride = EmptyToNull(configuration["Smtp:RecipientOverride"]);
    }

    public string SenderName => "Smtp";

    public static void ValidateConfiguration(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
        {
            throw new InvalidOperationException("Smtp:Host is required when Notifications:Sender is Smtp.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Smtp:From"]))
        {
            throw new InvalidOperationException("Smtp:From is required when Notifications:Sender is Smtp.");
        }
    }

    public async Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken)
    {
        using var message = BuildMessage(notification);
        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.Auto, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    internal MimeMessage BuildMessage(NotificationDelivery notification)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));

        if (recipientOverride is not null)
        {
            message.To.Add(MailboxAddress.Parse(recipientOverride));
        }
        else
        {
            foreach (var toEmailAddress in notification.ToEmailAddresses)
            {
                message.To.Add(MailboxAddress.Parse(toEmailAddress));
            }

            foreach (var ccEmailAddress in notification.CcEmailAddresses)
            {
                message.Cc.Add(MailboxAddress.Parse(ccEmailAddress));
            }
        }

        message.Subject = notification.Subject;
        message.Body = BuildMessageBody(notification);
        return message;
    }

    private MimeEntity BuildMessageBody(NotificationDelivery notification)
    {
        var plainBody = BuildBody(notification);
        if (string.IsNullOrWhiteSpace(notification.HtmlBody))
        {
            return new TextPart("plain")
            {
                Text = plainBody
            };
        }

        return new MultipartAlternative
        {
            new TextPart("plain")
            {
                Text = plainBody
            },
            new TextPart("html")
            {
                Text = BuildHtmlBody(notification)
            }
        };
    }

    private string BuildBody(NotificationDelivery notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Body))
        {
            return recipientOverride is null
                ? notification.Body
                : BuildPlainOverrideHeader(notification) + notification.Body;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"WorkflowInstanceId: {notification.DisplayReference}");
        builder.AppendLine($"NotificationType: {notification.NotificationType}");
        builder.AppendLine($"BusinessObjectType: {notification.BusinessObjectType}");
        builder.AppendLine($"BusinessObjectId: {notification.BusinessObjectId}");
        if (recipientOverride is not null)
        {
            builder.Append(BuildPlainOverrideHeader(notification));
        }

        builder.AppendLine($"Payload: {notification.PayloadJson}");
        return builder.ToString();
    }

    private string BuildHtmlBody(NotificationDelivery notification)
    {
        return recipientOverride is null
            ? notification.HtmlBody
            : BuildHtmlOverrideHeader(notification) + notification.HtmlBody;
    }

    private static string BuildPlainOverrideHeader(NotificationDelivery notification)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"IntendedToEmailAddresses: {FormatRecipients(notification.ToEmailAddresses)}");
        builder.AppendLine($"IntendedCcEmailAddresses: {FormatRecipients(notification.CcEmailAddresses)}");
        return builder.ToString();
    }

    private static string BuildHtmlOverrideHeader(NotificationDelivery notification)
    {
        var builder = new StringBuilder();
        builder.Append("<p><strong>IntendedToEmailAddresses:</strong> ")
            .Append(HtmlEncoder.Default.Encode(FormatRecipients(notification.ToEmailAddresses)))
            .AppendLine("</p>");
        builder.Append("<p><strong>IntendedCcEmailAddresses:</strong> ")
            .Append(HtmlEncoder.Default.Encode(FormatRecipients(notification.CcEmailAddresses)))
            .AppendLine("</p>");
        return builder.ToString();
    }

    private static string FormatRecipients(IReadOnlyList<string> recipients)
    {
        return string.Join("; ", recipients);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
