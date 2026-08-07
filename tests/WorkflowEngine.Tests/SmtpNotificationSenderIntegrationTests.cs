using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Persistence.Seed;

namespace WorkflowEngine.Tests;

[Trait("Category", "Smtp")]
public sealed class SmtpNotificationSenderIntegrationTests
{
    private static readonly Uri Smtp4DevApi = new("http://localhost:5000");

    [SmtpFact]
    public async Task SmtpSenderDeliversMessageToSmtp4Dev()
    {
        await EnsureSmtp4DevAvailableAsync();
        var subject = "Phase 5 SMTP " + Guid.NewGuid().ToString("N");
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test"
        }));

        await sender.SendAsync(SmtpNotificationSenderTests.Delivery(subject), CancellationToken.None);

        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
        var plaintext = await client.GetStringAsync($"/api/messages/{message.Id}/plaintext");

        Assert.Contains("first@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manager@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operations@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"taskKey\":\"CheckerReviewTask\"", plaintext, StringComparison.Ordinal);
    }

    [SmtpFact]
    public async Task RecipientOverrideDeliversToOperatorInSmtp4Dev()
    {
        await EnsureSmtp4DevAvailableAsync();
        var subject = "Phase 5 SMTP override " + Guid.NewGuid().ToString("N");
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test",
            ["Smtp:RecipientOverride"] = "operator@example.test"
        }));

        await sender.SendAsync(SmtpNotificationSenderTests.Delivery(subject), CancellationToken.None);

        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
        var plaintext = await client.GetStringAsync($"/api/messages/{message.Id}/plaintext");

        Assert.Contains("operator@example.test", message.Json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IntendedToEmailAddresses: first@example.test; second@example.test", plaintext, StringComparison.Ordinal);
        Assert.Contains("IntendedCcEmailAddresses: manager@example.test; operations@example.test", plaintext, StringComparison.Ordinal);
    }

    [SmtpFact]
    public async Task TypedRecipientsIgnorePayloadRecipientFieldsInSmtp4Dev()
    {
        await EnsureSmtp4DevAvailableAsync();
        var subject = "Phase 5 SMTP per record " + Guid.NewGuid().ToString("N");
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test"
        }));

        await sender.SendAsync(SmtpNotificationSenderTests.Delivery(
            subject,
            toEmailAddresses: ["typed.one@example.test", "typed.two@example.test"],
            ccEmailAddresses: ["typed.cc@example.test"],
            payloadJson: """{"recipientEmail":"payload@example.test","ccEmailAddresses":"payload.cc@example.test","taskKey":"MakerReworkTask"}"""), CancellationToken.None);

        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
        var plaintext = await client.GetStringAsync($"/api/messages/{message.Id}/plaintext");
        var rawMessageJson = message.Json.GetRawText();

        Assert.Contains("typed.one@example.test", rawMessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed.two@example.test", rawMessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("typed.cc@example.test", rawMessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload@example.test", rawMessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload.cc@example.test", rawMessageJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"taskKey\":\"MakerReworkTask\"", plaintext, StringComparison.Ordinal);
    }

    [SmtpFact]
    public async Task InactiveSlaScheduleOccurrenceNeverReachesSmtp4Dev()
    {
        await EnsureSmtp4DevAvailableAsync();
        var now = DateTimeOffset.Parse("2030-01-15T16:00:00Z");
        var subject = "Cancelled SLA SMTP " + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("burrito_smtp_stale_sla_tests"))
            .Options;
        await using var dbContext = new WorkflowDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        new WorkflowSeedService(dbContext, new FixedTimeProvider(now)).SeedPortfolioTrade();
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == "PortfolioTrade");
        var version = dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.IsActive);
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = "WF-SMTP-STALE-SLA",
            WorkflowKey = "PortfolioTrade",
            WorkflowDefinitionVersionId = version.Id,
            BusinessObjectType = "PortfolioTrade",
            BusinessObjectId = "PT-SMTP-STALE-SLA",
            CurrentStateKey = "TradingDeskReview",
            Status = "Active",
            StartedAtUtc = now.AddDays(-1),
            StartedByUserId = "maker1",
            Version = 1
        };
        dbContext.WorkflowInstances.Add(instance);
        var task = new WorkflowTask
        {
            WorkflowInstanceId = instance.WorkflowInstanceId,
            TaskKey = "TradingDeskReviewTask",
            AssignedRoleKey = "TradingDeskTaxable",
            Status = "Pending",
            CreatedAtUtc = now.AddDays(-1),
            NotificationRecipientsJson = """{"toEmailAddresses":["desk@example.test"],"ccEmailAddresses":[]}"""
        };
        dbContext.WorkflowTasks.Add(task);
        await dbContext.SaveChangesAsync();
        var schedule = new WorkflowTaskSlaSchedule
        {
            WorkflowTaskId = task.Id,
            ScheduleVersion = 1,
            SlaPolicyKey = "TradingDeskReviewSla",
            TargetDate = new DateOnly(2030, 1, 15),
            DueReminderAtUtc = now,
            OverdueAtUtc = now.AddDays(1),
            CalculationMode = "ApplicationTargetDate",
            TimeZoneId = "America/New_York",
            ReminderLocalTime = new TimeOnly(9, 0),
            OverdueStartOffsetDays = 1,
            RecurrenceIntervalDays = 1,
            DueNotificationRouteKey = "RouteSlaTaskDueToday",
            OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
            Status = "Superseded",
            CreatedAtUtc = now.AddDays(-1),
            CreatedByUserId = "maker1",
            CorrelationId = "corr-stale-smtp",
            IdempotencyKey = "idem-stale-smtp",
            SupersededAtUtc = now,
            SupersededByUserId = "maker1"
        };
        dbContext.WorkflowTaskSlaSchedules.Add(schedule);
        await dbContext.SaveChangesAsync();
        dbContext.NotificationOutbox.Add(new NotificationOutbox
        {
            WorkflowInstanceId = instance.WorkflowInstanceId,
            WorkflowTaskId = task.Id,
            WorkflowTaskSlaScheduleId = schedule.Id,
            SlaOccurrenceDate = new DateOnly(2030, 1, 15),
            SlaReminderKind = "DueToday",
            BusinessObjectType = instance.BusinessObjectType,
            BusinessObjectId = instance.BusinessObjectId,
            NotificationType = "SlaTaskDueToday",
            NotificationRecipientsJson = task.NotificationRecipientsJson,
            Subject = subject,
            Body = "body",
            HtmlBody = "<p>body</p>",
            PayloadJson = "{}",
            Status = "Pending",
            CorrelationId = "sla-stale-smtp",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        var sender = new SmtpNotificationSender(SmtpNotificationSenderTests.Configuration(new()
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "workflow@example.test"
        }));

        var result = await new ProcessNotificationOutboxService(
            dbContext,
            sender,
            new FixedTimeProvider(now)).ProcessAsync(CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Empty(result.Processed);
        dbContext.ChangeTracker.Clear();
        Assert.Equal("Cancelled", dbContext.NotificationOutbox.Single().Status);
        using var client = new HttpClient { BaseAddress = Smtp4DevApi };
        Assert.False(await HasMessageAsync(client, subject, CancellationToken.None));
    }

    private static async Task EnsureSmtp4DevAvailableAsync()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = Smtp4DevApi, Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync("/api/messages");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                "Tier 2 SMTP tests require smtp4dev running locally: docker run --rm -p 3025:25 -p 5000:80 rnwood/smtp4dev. Then set RUN_SMTP_TESTS=1 and run dotnet test --filter Category=Smtp.",
                exception);
        }
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

    private static async Task<bool> HasMessageAsync(HttpClient client, string subject, CancellationToken cancellationToken)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/api/messages", cancellationToken);
        return EnumerateObjects(json).Any(message =>
            message.TryGetProperty("subject", out var lowerSubject) && lowerSubject.GetString() == subject
            || message.TryGetProperty("Subject", out var upperSubject) && upperSubject.GetString() == subject);
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
