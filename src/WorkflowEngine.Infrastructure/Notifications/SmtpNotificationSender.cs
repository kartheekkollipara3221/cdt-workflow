using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly IConfiguration configuration;
    private readonly string host;
    private readonly int port;
    private readonly string from;
    private readonly string? recipientOverride;

    public SmtpNotificationSender(IConfiguration configuration)
    {
        this.configuration = configuration;
        host = configuration["Smtp:Host"]?.Trim()
            ?? throw new InvalidOperationException("Smtp:Host is required when Notifications:Sender is Smtp.");
        if (host.Length == 0)
        {
            throw new InvalidOperationException("Smtp:Host is required when Notifications:Sender is Smtp.");
        }

        from = configuration["Smtp:From"]?.Trim()
            ?? throw new InvalidOperationException("Smtp:From is required when Notifications:Sender is Smtp.");
        if (from.Length == 0)
        {
            throw new InvalidOperationException("Smtp:From is required when Notifications:Sender is Smtp.");
        }

        port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 25;
        recipientOverride = EmptyToNull(configuration["Smtp:RecipientOverride"]);
    }

    public string SenderName => "Smtp";

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
        message.To.Add(MailboxAddress.Parse(ResolveRecipient(notification)));
        message.Subject = notification.Subject;
        message.Body = new TextPart("plain")
        {
            Text = BuildBody(notification)
        };
        return message;
    }

    private string ResolveRecipient(NotificationDelivery notification)
    {
        if (!string.IsNullOrWhiteSpace(recipientOverride))
        {
            return recipientOverride;
        }

        if (string.IsNullOrWhiteSpace(notification.RecipientRoleKey))
        {
            throw new InvalidOperationException($"Notification {notification.Id} has no recipient role and no Smtp:RecipientOverride.");
        }

        var mapped = EmptyToNull(configuration[$"Smtp:RoleEmailMap:{notification.RecipientRoleKey}"]);
        if (mapped is null)
        {
            throw new InvalidOperationException($"No SMTP recipient mapping configured for role '{notification.RecipientRoleKey}'.");
        }

        return mapped;
    }

    private string BuildBody(NotificationDelivery notification)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"WorkflowInstanceId: {notification.WorkflowInstanceId}");
        builder.AppendLine($"NotificationType: {notification.NotificationType}");
        builder.AppendLine($"BusinessObjectType: {notification.BusinessObjectType}");
        builder.AppendLine($"BusinessObjectId: {notification.BusinessObjectId}");
        if (!string.IsNullOrWhiteSpace(recipientOverride))
        {
            builder.AppendLine($"OriginalRecipientRoleKey: {notification.RecipientRoleKey ?? "(none)"}");
        }

        try
        {
            using var document = JsonDocument.Parse(notification.PayloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    builder.AppendLine($"{property.Name}: {FormatJsonValue(property.Value)}");
                }

                return builder.ToString();
            }
        }
        catch (JsonException)
        {
        }

        builder.AppendLine($"Payload: {notification.PayloadJson}");
        return builder.ToString();
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
