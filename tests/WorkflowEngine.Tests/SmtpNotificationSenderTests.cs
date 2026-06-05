using Microsoft.Extensions.Configuration;
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

    internal static NotificationDelivery Delivery(
        string subject = "Task overdue",
        string body = "",
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
