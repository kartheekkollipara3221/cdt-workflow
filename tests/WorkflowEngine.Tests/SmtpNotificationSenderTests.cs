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
    public async Task MissingRoleMappingThrowsBeforeSmtpConnection()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(Delivery(), CancellationToken.None));

        Assert.Contains("No SMTP recipient mapping", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RoleMappingBuildsMessageFromPersistedBody()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(body: "Rendered email body"));

        Assert.Equal("Task overdue", message.Subject);
        Assert.Contains("cdt-checkers@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.Equal("Rendered email body", message.TextBody);
    }

    [Fact]
    public void HtmlBodyBuildsMultipartAlternativeMessage()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
        }));

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
    public void RecipientEmailInPayloadWinsOverRoleMapping()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RoleEmailMap:Maker"] = "maker-group@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            recipientRoleKey: "Maker",
            payloadJson: """{"recipientEmail":"maker.one@example.test"}"""));

        Assert.Contains("maker.one@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.DoesNotContain("maker-group@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientEmailInPayloadDoesNotRequireRoleMapping()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            recipientRoleKey: "Maker",
            payloadJson: """{"recipientEmail":"maker.one@example.test"}"""));

        Assert.Contains("maker.one@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
    }

    [Fact]
    public void CcEmailAddressesInPayloadAddsCcRecipients()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            recipientRoleKey: "Maker",
            payloadJson: """{"recipientEmail":"maker.one@example.test","ccEmailAddresses":"cc.one@example.test; cc.two@example.test, cc.three@example.test"}"""));

        var ccRecipients = string.Join(",", message.Cc.Select(address => address.ToString()));
        Assert.Contains("cc.one@example.test", ccRecipients, StringComparison.Ordinal);
        Assert.Contains("cc.two@example.test", ccRecipients, StringComparison.Ordinal);
        Assert.Contains("cc.three@example.test", ccRecipients, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientOverrideWinsAndBodyRecordsOriginalRole()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RecipientOverride"] = "operator@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
        }));

        using var message = sender.BuildMessage(Delivery());

        Assert.Contains("operator@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.Contains("OriginalRecipientRoleKey: Checker", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientOverrideWinsOverRecipientEmailInPayload()
    {
        var sender = new SmtpNotificationSender(Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RecipientOverride"] = "operator@example.test"
        }));

        using var message = sender.BuildMessage(Delivery(
            recipientRoleKey: "Maker",
            payloadJson: """{"recipientEmail":"maker.one@example.test"}"""));

        Assert.Contains("operator@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.DoesNotContain("maker.one@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.Contains("OriginalRecipientRoleKey: Maker", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("OriginalRecipientEmail: maker.one@example.test", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientOverrideSuppressesCcAndBodyRecordsOriginalCc()
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
            recipientRoleKey: "Maker",
            payloadJson: """{"recipientEmail":"maker.one@example.test","ccEmailAddresses":"cc.one@example.test; cc.two@example.test"}"""));

        Assert.Contains("operator@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
        Assert.Empty(message.Cc);
        Assert.Contains("OriginalRecipientEmail: maker.one@example.test", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("OriginalCcEmailAddresses: cc.one@example.test; cc.two@example.test", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("Rendered email body", message.TextBody, StringComparison.Ordinal);
        Assert.Contains("<strong>OriginalCcEmailAddresses:</strong> cc.one@example.test; cc.two@example.test", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("<p>Rendered email body</p>", message.HtmlBody, StringComparison.Ordinal);
    }

    internal static NotificationDelivery Delivery(
        string subject = "Task overdue",
        string body = "",
        string htmlBody = "",
        string? recipientRoleKey = "Checker",
        string payloadJson = """{"taskKey":"CheckerReviewTask","dueAtUtc":"2030-01-15T16:00:00Z"}""")
    {
        return new NotificationDelivery(
            10,
            "WF-PT-1001",
            20,
            "PortfolioTrade",
            "PT-1001",
            "SlaTaskOverdue",
            recipientRoleKey,
            null,
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
}
