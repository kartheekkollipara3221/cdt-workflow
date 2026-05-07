using System.Net.Http.Json;
using System.Text.Json;
using WorkflowEngine.Infrastructure.Notifications;

namespace WorkflowEngine.Tests;

[Trait("Category", "Smtp")]
public sealed class SmtpNotificationSenderIntegrationTests
{
    private static readonly Uri Smtp4DevApi = new("http://localhost:5000");

    [SmtpFact]
    public async Task SmtpSenderDeliversMessageToSmtp4Dev()
    {
        var subject = "Phase 5 SMTP " + Guid.NewGuid().ToString("N");
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
        }));

        await sender.SendAsync(SmtpNotificationSenderTests.Delivery(subject), CancellationToken.None);

        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
        var plaintext = await client.GetStringAsync($"/api/messages/{message.Id}/plaintext");

        Assert.Contains("cdt-checkers@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("taskKey: CheckerReviewTask", plaintext, StringComparison.Ordinal);
    }

    [SmtpFact]
    public async Task RecipientOverrideDeliversToOperatorInSmtp4Dev()
    {
        var subject = "Phase 5 SMTP override " + Guid.NewGuid().ToString("N");
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RecipientOverride"] = "operator@example.test",
            ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
        }));

        await sender.SendAsync(SmtpNotificationSenderTests.Delivery(subject), CancellationToken.None);

        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
        var plaintext = await client.GetStringAsync($"/api/messages/{message.Id}/plaintext");

        Assert.Contains("operator@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OriginalRecipientRoleKey: Checker", plaintext, StringComparison.Ordinal);
    }

    private static async Task<Smtp4DevMessage> WaitForMessageAsync(HttpClient client, string subject, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var json = await client.GetFromJsonAsync<JsonElement>("/api/messages", cancellationToken);
            foreach (var message in EnumerateObjects(json))
            {
                if (message.TryGetProperty("subject", out var lowerSubject) && lowerSubject.GetString() == subject
                    || message.TryGetProperty("Subject", out var upperSubject) && upperSubject.GetString() == subject)
                {
                    return new Smtp4DevMessage(FindId(message), message);
                }
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException($"smtp4dev did not capture message with subject '{subject}'.");
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var child in EnumerateObjects(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in EnumerateObjects(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static string FindId(JsonElement message)
    {
        if (message.TryGetProperty("id", out var lowerId))
        {
            return lowerId.GetString() ?? throw new InvalidOperationException("smtp4dev message id was null.");
        }

        if (message.TryGetProperty("Id", out var upperId))
        {
            return upperId.GetString() ?? throw new InvalidOperationException("smtp4dev message id was null.");
        }

        throw new InvalidOperationException("smtp4dev message id was not present.");
    }

    private sealed record Smtp4DevMessage(string Id, JsonElement Json);
}
