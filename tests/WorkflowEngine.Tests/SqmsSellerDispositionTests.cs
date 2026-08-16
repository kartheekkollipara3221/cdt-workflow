using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsSellerDispositionTests
{
    private const string DatabaseName = "workflowengine_sqms_seller_disposition_tests";
    private static readonly SqmsActor MfoActor = new("sqms.mfo", "SQMS MFO", ["MFO"]);
    private static readonly SqmsActor IpgActor = new("sqms.ipg", "SQMS IPG", ["IPG"]);

    [Fact]
    public void RequestSellerApprovalMovesAllBidsAndQueuesOneExactEmail()
    {
        using var host = CreatePreparedHost(bidCount: 2);

        try
        {
            var result = host.GetRequiredService<SqmsWorkflowCoordinator>()
                .ExecuteOpportunityAction(
                    10000,
                    ReadActionSample("samples/sqms/03-request-seller-approval.json"),
                    IpgActor);

            Assert.Equal("PendingSellerApproval", result.WorkflowState);
            Assert.Equal(2, CountBidsInState(host.ConnectionString, "UnderReviewBySeller"));
            var outbox = host.DbContext.NotificationOutbox.AsNoTracking()
                .Single(row => row.NotificationType == "SqmsSellerApprovalRequested");
            Assert.Equal(
                "Q1 2030 Secondary Sale Bids- \"Private Fund Vehicle\"/\"SQMS-OPP-10000\" - \"SQMS Console Mock Opportunity\" : Response required by 01/24/2030",
                outbox.Subject);
            Assert.Contains("Total amount requested to be sold: $500,000.00", outbox.Body, StringComparison.Ordinal);
            Assert.Contains(
                "https://example.test/sqms/opportunities/10000/seller-review",
                outbox.Body,
                StringComparison.Ordinal);
            AssertRecipients(
                outbox.NotificationRecipientsJson,
                ["seller.rm@example.test", "seller.team1@example.test", "seller.team2@example.test"],
                ["sqms-static-cc1@example.test", "sqms-static-cc2@example.test"]);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void SellerApprovalChildFailureRollsBackAllBidsParentAndEmail()
    {
        using var host = CreatePreparedHost(bidCount: 2);

        try
        {
            ExecuteSql(host.ConnectionString, """
                CREATE OR ALTER TRIGGER SSQMS.TR_Bid_BlockSecondSellerReview
                ON SSQMS.Bid
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS
                    (
                        SELECT 1 FROM inserted
                        WHERE ID = 10001 AND WorkflowState = N'UnderReviewBySeller'
                    )
                        THROW 51001, 'Forced second Bid seller-review failure.', 1;
                END;
                """);

            Assert.Throws<SqmsIntegrationException>(() =>
                host.GetRequiredService<SqmsWorkflowCoordinator>()
                    .ExecuteOpportunityAction(
                        10000,
                        new SqmsActionRequest("RequestSellerApproval"),
                        IpgActor));

            Assert.Equal(2, CountBidsInState(host.ConnectionString, "IpgReview"));
            Assert.Equal("ClosedToBidding", ReadOpportunityState(host.ConnectionString));
            host.DbContext.ChangeTracker.Clear();
            Assert.DoesNotContain(
                host.DbContext.NotificationOutbox.AsNoTracking(),
                row => row.NotificationType == "SqmsSellerApprovalRequested");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Theory]
    [InlineData("samples/sqms/04-accept-bid.json", "Accepted", "SqmsBidAccepted", "AwaitingAllocation")]
    [InlineData("samples/sqms/05-partially-accept-bid.json", "PartiallyAccepted", "SqmsBidPartiallyAccepted", "PartiallyAllocated")]
    [InlineData("samples/sqms/06-reject-bid.json", "NotAccepted", "SqmsBidNotAccepted", "NotFulfilled")]
    public void FinalBidDispositionQueuesExactEmailAndRollsUpOpportunity(
        string samplePath,
        string bidState,
        string notificationType,
        string opportunityState)
    {
        using var host = CreatePreparedHost(bidCount: 1);

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("RequestSellerApproval"),
                IpgActor);

            var result = coordinator.ExecuteBidAction(
                10000,
                ReadActionSample(samplePath),
                MfoActor);

            Assert.Equal(bidState, result.WorkflowState);
            Assert.Equal(opportunityState, ReadOpportunityState(host.ConnectionString));
            host.DbContext.ChangeTracker.Clear();
            var outbox = host.DbContext.NotificationOutbox.AsNoTracking()
                .Single(row => row.NotificationType == notificationType);
            Assert.Equal(
                "Q1 2030 Secondary Sale Bids - \"Private Fund Vehicle\"/\"SQMS-BID-10000\" - \"SQMS Console Mock Bid\"",
                outbox.Subject);
            Assert.Contains("Buyer Account Number: SQMS-BID-10000", outbox.Body, StringComparison.Ordinal);
            Assert.Contains("Total Amount Request to be Purchased: $500,000.00", outbox.Body, StringComparison.Ordinal);
            Assert.Contains("https://example.test/sqms/bids/10000", outbox.Body, StringComparison.Ordinal);
            AssertRecipients(
                outbox.NotificationRecipientsJson,
                ["buyer.rm@example.test", "buyer.team1@example.test", "buyer.team2@example.test"],
                ["sqms-static-cc1@example.test", "sqms-static-cc2@example.test"]);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void MixedAcceptedAndRejectedBidsRollUpToPartiallyAllocatedOnlyAfterLastDecision()
    {
        using var host = CreatePreparedHost(bidCount: 2);

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("RequestSellerApproval"),
                IpgActor);

            coordinator.ExecuteBidAction(10000, new SqmsActionRequest("AcceptBid"), MfoActor);
            Assert.Equal("PendingSellerApproval", ReadOpportunityState(host.ConnectionString));

            coordinator.ExecuteBidAction(10001, new SqmsActionRequest("RejectBid"), MfoActor);
            Assert.Equal("PartiallyAllocated", ReadOpportunityState(host.ConnectionString));
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    private static TestConsoleHost CreatePreparedHost(int bidCount)
    {
        var host = new TestConsoleHost(DatabaseName);
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/001_seed_sqms_transition_metadata.sql");
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/004_seed_sqms_end_to_end_metadata.sql");
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/testing/004_create_sqms_mock_application_tables.sql");
        if (bidCount == 2)
        {
            ExecuteSql(host.ConnectionString, """
                INSERT SSQMS.Bid
                (
                    OpportunityId, BuyerRmteam, BidForFullCommitment,
                    CommitmentAmountRequested, AccountName, AccountNumber,
                    Status, WorkflowState, IsDeleted, BidPricingCategory,
                    BidRelationshipManagerEmail, BuyerRmTeamEmailsJson
                )
                VALUES
                    (10000, 'buyer-rm-team@example.test', 'Yes', '100000',
                     'Second buyer', 'SQMS-BID-10001', 'DRAFT', N'Draft', 0, 'Discount',
                     'buyer2.rm@example.test', N'["buyer2.team@example.test"]');
                """);
        }

        var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
        coordinator.StartOpportunity(10000, MfoActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("SubmitOpportunity"), MfoActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("MarkOpportunityPending"), IpgActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("OpenOpportunityForBidding"), IpgActor);
        Assert.Equal(1, host.GetRequiredService<SqmsScheduleRunner>()
            .RunNoticesAndReminders().NoticesQueued);
        ExecuteSql(host.ConnectionString, """
            UPDATE dbo.NotificationOutbox
            SET Status = 'Sent', SentAtUtc = SYSDATETIMEOFFSET()
            WHERE NotificationType = 'SqmsOpenBiddingNotice';
            """);

        foreach (var bidId in Enumerable.Range(10000, bidCount))
        {
            coordinator.StartBid(bidId, MfoActor);
            coordinator.ExecuteBidAction(bidId, new SqmsActionRequest("SubmitBid"), MfoActor);
        }

        ExecuteSql(host.ConnectionString, """
            UPDATE SSQMS.Opportunity
            SET BiddingCloseAt = '2030-01-15T15:30:00+00:00'
            WHERE ID = 10000;
            """);
        var close = host.GetRequiredService<SqmsScheduleRunner>().RunClosures();
        Assert.Empty(close.Errors);
        Assert.Equal(1, close.ChildBatchesCompleted);
        Assert.Equal(bidCount, CountBidsInState(host.ConnectionString, "IpgReview"));
        return host;
    }

    private static void AssertRecipients(
        string json,
        string[] expectedTo,
        string[] expectedCc)
    {
        var recipients = JsonSerializer.Deserialize<NotificationRecipients>(json, WorkflowJson.Options);
        var typed = Assert.IsType<NotificationRecipients>(recipients);
        Assert.Equal(expectedTo, typed.ToEmailAddresses);
        Assert.Equal(expectedCc, typed.CcEmailAddresses);
    }

    private static SqmsActionRequest ReadActionSample(string relativePath)
    {
        var json = File.ReadAllText(ResolveRepositoryFile(relativePath));
        return JsonSerializer.Deserialize<SqmsActionRequest>(json, WorkflowJson.Options)
            ?? throw new InvalidOperationException($"SQMS action sample '{relativePath}' is empty.");
    }

    private static int CountBidsInState(string connectionString, string state)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM SSQMS.Bid
            WHERE OpportunityId = 10000 AND WorkflowState = @state AND IsDeleted = 0;
            """;
        command.Parameters.AddWithValue("@state", state);
        return (int)command.ExecuteScalar()!;
    }

    private static string ReadOpportunityState(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT WorkflowState FROM SSQMS.Opportunity WHERE ID = 10000;";
        return (string)command.ExecuteScalar()!;
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
