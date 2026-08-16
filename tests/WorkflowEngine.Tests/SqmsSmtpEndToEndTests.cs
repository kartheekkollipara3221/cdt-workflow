using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
[Trait("Category", "Smtp")]
public sealed class SqmsSmtpEndToEndTests
{
    private const string DatabaseName = "workflowengine_sqms_smtp_end_to_end_tests";
    private static readonly Uri Smtp4DevApi = new("http://localhost:5000");
    private static readonly SqmsActor MfoActor = new("sqms.mfo", "SQMS MFO", ["MFO"]);
    private static readonly SqmsActor IpgActor = new("sqms.ipg", "SQMS IPG", ["IPG"]);

    [SmtpFact]
    public async Task FullSqmsFlowDeliversAllSixBusinessEmailsThroughConfiguredSender()
    {
        await EnsureSmtp4DevAvailableAsync();
        using var host = new TestConsoleHost(DatabaseName, new Dictionary<string, string?>
        {
            ["Notifications:Sender"] = "Smtp",
            ["Smtp:Host"] = "localhost",
            ["Smtp:Port"] = "3025",
            ["Smtp:From"] = "sqms-workflow@example.test"
        });
        host.ResetDatabase();

        try
        {
            Assert.Equal(0, host.Execute("init-db").ExitCode);
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/001_seed_sqms_transition_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/004_seed_sqms_end_to_end_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/testing/004_create_sqms_mock_application_tables.sql");

            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.StartOpportunity(10000, MfoActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("SubmitOpportunity"), MfoActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("MarkOpportunityPending"), IpgActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("OpenOpportunityForBidding"), IpgActor);

            var scheduler = host.GetRequiredService<SqmsScheduleRunner>();
            var initialRun = scheduler.RunNoticesAndReminders();
            Assert.Equal(1, initialRun.NoticesQueued);
            Assert.Equal(1, initialRun.RemindersBlocked);
            Assert.Empty(initialRun.Errors);
            await AssertOutboxBatchSentAsync(host, expectedCount: 1);

            var reminderRun = scheduler.RunNoticesAndReminders();
            Assert.Equal(1, reminderRun.RemindersQueued);
            Assert.Equal(1, reminderRun.CyclesCompleted);
            Assert.Empty(reminderRun.Errors);
            await AssertOutboxBatchSentAsync(host, expectedCount: 1);

            InsertAdditionalBids(host.ConnectionString);
            foreach (var bidId in new[] { 10000, 10001, 10002 })
            {
                coordinator.StartBid(bidId, MfoActor);
                coordinator.ExecuteBidAction(bidId, new SqmsActionRequest("SubmitBid"), MfoActor);
            }

            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET BiddingCloseAt = '2030-01-15T15:30:00+00:00'
                WHERE ID = 10000;
                """);
            var closeRun = scheduler.RunClosures();
            Assert.Equal(1, closeRun.ParentsClosed);
            Assert.Equal(1, closeRun.ChildBatchesCompleted);
            Assert.Empty(closeRun.Errors);

            coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("RequestSellerApproval"),
                IpgActor);
            coordinator.ExecuteBidAction(10000, new SqmsActionRequest("AcceptBid"), MfoActor);
            coordinator.ExecuteBidAction(10001, new SqmsActionRequest("PartiallyAcceptBid"), MfoActor);
            coordinator.ExecuteBidAction(10002, new SqmsActionRequest("RejectBid"), MfoActor);
            await AssertOutboxBatchSentAsync(host, expectedCount: 4);

            host.DbContext.ChangeTracker.Clear();
            var deliveries = await host.DbContext.NotificationOutbox
                .AsNoTracking()
                .Where(row => row.NotificationType.StartsWith("Sqms"))
                .OrderBy(row => row.Id)
                .Select(row => new { row.NotificationType, row.Subject, row.Status, row.RetryCount, row.LastError })
                .ToListAsync();
            Assert.Equal(6, deliveries.Count);
            Assert.Equal(
                [
                    "SqmsOpenBiddingNotice",
                    "SqmsOpenBiddingReminder",
                    "SqmsSellerApprovalRequested",
                    "SqmsBidAccepted",
                    "SqmsBidPartiallyAccepted",
                    "SqmsBidNotAccepted"
                ],
                deliveries.Select(row => row.NotificationType).ToArray());
            Assert.All(deliveries, row =>
            {
                Assert.Equal("Sent", row.Status);
                Assert.Equal(0, row.RetryCount);
                Assert.Null(row.LastError);
            });

            using var client = new HttpClient { BaseAddress = Smtp4DevApi };
            foreach (var subject in deliveries.Select(row => row.Subject))
            {
                var message = await WaitForMessageAsync(client, subject, CancellationToken.None);
                Assert.False(message.GetProperty("hasWarnings").GetBoolean());
                var messageId = message.GetProperty("id").GetString()
                    ?? throw new InvalidOperationException("smtp4dev returned a message without an id.");
                var plaintext = await client.GetStringAsync(
                    $"/api/messages/{messageId}/plaintext",
                    CancellationToken.None);
                var html = await client.GetStringAsync(
                    $"/api/messages/{messageId}/html",
                    CancellationToken.None);
                Assert.Contains("https://example.test/sqms/", plaintext, StringComparison.Ordinal);
                Assert.Contains("https://example.test/sqms/", html, StringComparison.Ordinal);
            }

            Assert.Equal("PartiallyAllocated", ReadOpportunityState(host.ConnectionString));
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    private static async Task AssertOutboxBatchSentAsync(TestConsoleHost host, int expectedCount)
    {
        var result = await host.GetRequiredService<IProcessNotificationOutboxService>()
            .ProcessAsync(CancellationToken.None);
        Assert.Equal("Smtp", result.SenderName);
        Assert.Equal(expectedCount, result.PendingCount);
        Assert.Equal(expectedCount, result.Processed.Count);
        Assert.Empty(result.Warnings);
        Assert.Null(result.Failure);
    }

    private static void InsertAdditionalBids(string connectionString)
    {
        ExecuteSql(connectionString, """
            INSERT SSQMS.Bid
            (
                OpportunityId, BuyerRmteam, BidForFullCommitment,
                CommitmentAmountRequested, AccountName, AccountNumber,
                Status, WorkflowState, IsDeleted, BidPricingCategory,
                BidRelationshipManagerEmail, BuyerRmTeamEmailsJson
            )
            VALUES
                (10000, 'buyer-rm-team@example.test', 'No', '100000',
                 'SQMS Partial Buyer', 'SQMS-BID-10001', 'DRAFT', N'Draft', 0, 'FairValue',
                 'partial.rm@example.test', N'["partial.team@example.test"]'),
                (10000, 'buyer-rm-team@example.test', 'No', '100000',
                 'SQMS Rejected Buyer', 'SQMS-BID-10002', 'DRAFT', N'Draft', 0, 'FairValue',
                 'rejected.rm@example.test', N'["rejected.team@example.test"]');
            """);
    }

    private static string ReadOpportunityState(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT WorkflowState FROM SSQMS.Opportunity WHERE ID = 10000;";
        return (string)command.ExecuteScalar()!;
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
                "SQMS SMTP acceptance requires smtp4dev on SMTP localhost:3025 and API http://localhost:5000.",
                exception);
        }
    }

    private static async Task<JsonElement> WaitForMessageAsync(
        HttpClient client,
        string subject,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var json = await client.GetFromJsonAsync<JsonElement>(
                "/api/messages?page=1&pageSize=100",
                cancellationToken);
            var message = EnumerateObjects(json).FirstOrDefault(candidate => HasSubject(candidate, subject));
            if (message.ValueKind == JsonValueKind.Object)
            {
                return message;
            }

            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException($"smtp4dev did not capture message with subject '{subject}'.");
    }

    private static bool HasSubject(JsonElement message, string subject)
    {
        return message.TryGetProperty("subject", out var lowerSubject) && lowerSubject.GetString() == subject
            || message.TryGetProperty("Subject", out var upperSubject) && upperSubject.GetString() == subject;
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

    private static void ExecuteSqlScript(string connectionString, string relativePath)
    {
        var script = File.ReadAllText(ResolveRepositoryFile(relativePath));
        foreach (var batch in Regex.Split(
                     script,
                     @"^\s*GO\s*(?:--.*)?$",
                     RegexOptions.IgnoreCase | RegexOptions.Multiline)
                 .Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            ExecuteSql(connectionString, batch);
        }
    }

    private static void ExecuteSql(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        command.ExecuteNonQuery();
    }

    private static string ResolveRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Repository file was not found: {relativePath}", relativePath);
    }
}
