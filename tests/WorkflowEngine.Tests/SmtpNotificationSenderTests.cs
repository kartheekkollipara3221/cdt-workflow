using Microsoft.Extensions.Configuration;
using MimeKit;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;

namespace WorkflowEngine.Tests;

public sealed class SmtpNotificationSenderTests
{
    [Fact]
    public void SmtpSenderRequiresHostAtConstruction()
    {
        var configuration = Configuration(new()
        {
            ["Smtp:From"] = "workflow@example.test"
        });

        var exception = Assert.Throws<InvalidOperationException>(() => new SmtpNotificationSender(configuration));

        Assert.Contains("Smtp:Host", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRecipientsBuildOneMessageWithOrderedToAndCcCollections()
    {
        var sender = Sender();

        using var message = sender.BuildMessage(Delivery(
            toEmailAddresses: ["first@example.test", "second@example.test", "first@example.test"],
            ccEmailAddresses: ["manager@example.test", "operations@example.test"]));

        Assert.Equal(
            ["first@example.test", "second@example.test", "first@example.test"],
            message.To.Mailboxes.Select(address => address.Address));
        Assert.Equal(
            ["manager@example.test", "operations@example.test"],
            message.Cc.Mailboxes.Select(address => address.Address));
        Assert.Equal("Task overdue", message.Subject);
    }

    [Fact]
    public void HtmlBodyBuildsMultipartAlternativeMessage()
    {
        var sender = Sender();

        using var message = sender.BuildMessage(Delivery(
            body: "Plain email body",
            htmlBody: """<p>Account <strong>EFG</strong> <a href="https://sharepoint.example.test/trades/1050">Open trade</a></p>"""));

        var multipart = Assert.IsType<MultipartAlternative>(message.Body);
        Assert.Equal(2, multipart.Count);
        Assert.Equal("Plain email body", message.TextBody);
        Assert.Contains("<strong>EFG</strong>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("""<a href="https://sharepoint.example.test/trades/1050">Open trade</a>""", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadRecipientFieldsAndRoleMapDoNotAffectTypedRecipients()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "role-map@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            toEmailAddresses: ["typed.to@example.test"],
            ccEmailAddresses: ["typed.cc@example.test"],
            payloadJson: """{"recipientEmail":"payload@example.test","ccEmailAddresses":"payload.cc@example.test"}"""));

        Assert.Equal(["typed.to@example.test"], message.To.Mailboxes.Select(address => address.Address));
        Assert.Equal(["typed.cc@example.test"], message.Cc.Mailboxes.Select(address => address.Address));
        var allRecipients = string.Join(",", message.To.Concat(message.Cc).Select(address => address.ToString()));
        Assert.DoesNotContain("payload@example.test", allRecipients, StringComparison.Ordinal);
        Assert.DoesNotContain("payload.cc@example.test", allRecipients, StringComparison.Ordinal);
        Assert.DoesNotContain("role-map@example.test", allRecipients, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientOverrideSuppressesOriginalsAndAddsOrderedPlainAndHtmlDiagnostics()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RecipientOverride"] = "operator@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            body: "Rendered email body",
            htmlBody: "<p>Rendered email body</p>",
            toEmailAddresses: ["first@example.test", "second@example.test"],
            ccEmailAddresses: ["manager@example.test", "operations@example.test"]));

        Assert.Equal(["operator@example.test"], message.To.Mailboxes.Select(address => address.Address));
        Assert.Empty(message.Cc);
        Assert.Contains(
            "IntendedToEmailAddresses: first@example.test; second@example.test",
            message.TextBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "IntendedCcEmailAddresses: manager@example.test; operations@example.test",
            message.TextBody,
            StringComparison.Ordinal);
        Assert.Contains("Rendered email body", message.TextBody, StringComparison.Ordinal);
        Assert.Contains(
            "<strong>IntendedToEmailAddresses:</strong> first@example.test; second@example.test",
            message.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "<strong>IntendedCcEmailAddresses:</strong> manager@example.test; operations@example.test",
            message.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains("<p>Rendered email body</p>", message.HtmlBody, StringComparison.Ordinal);
    }

    internal static NotificationDelivery Delivery(
        string subject = "Task overdue",
        string body = "",
        string htmlBody = "",
        IReadOnlyList<string>? toEmailAddresses = null,
        IReadOnlyList<string>? ccEmailAddresses = null,
        string payloadJson = """{"taskKey":"CheckerReviewTask","dueAtUtc":"2030-01-15T16:00:00Z"}""")
    {
        return new NotificationDelivery(
            10,
            "WF-PT-1001",
            20,
            "PortfolioTrade",
            "PT-1001",
            "SlaTaskOverdue",
            toEmailAddresses ?? ["first@example.test", "second@example.test"],
            ccEmailAddresses ?? ["manager@example.test", "operations@example.test"],
            subject,
            body,
            htmlBody,
            payloadJson,
            "Pending",
            "corr-1",
            DateTimeOffset.Parse("2030-01-15T16:01:00Z"));
    }

    internal static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static SmtpNotificationSender Sender()
    {
        return new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test"
        }));
    }
}
