using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsScheduleRunnerTests
{
    private const string DatabaseName = "workflowengine_sqms_schedule_tests";
    private static readonly SqmsActor MfoActor = new("sqms.mfo", "SQMS MFO", ["MFO"]);
    private static readonly SqmsActor IpgActor = new("sqms.ipg", "SQMS IPG", ["IPG"]);

    [Fact]
    public void NoticeIsAggregateIdempotentForLateRowsAndReminderWaitsForSentNotice()
    {
        using var host = CreateInitializedHost();

        try
        {
            var runner = host.GetRequiredService<SqmsScheduleRunner>();

            var first = runner.RunNoticesAndReminders();
            Assert.Equal(1, first.NoticesQueued);
            Assert.Equal(0, first.RemindersQueued);
            Assert.Equal(1, first.RemindersBlocked);
            Assert.Equal(1, CountOutbox(host, "SqmsOpenBiddingNotice"));
            Assert.Equal(0, CountOutbox(host, "SqmsOpenBiddingReminder"));

            InsertSecondOpportunityInSameCycle(host.ConnectionString);
            var repeated = runner.RunNoticesAndReminders();
            Assert.Equal(0, repeated.NoticesQueued);
            Assert.Equal(1, repeated.RemindersBlocked);
            Assert.Equal(1, CountOutbox(host, "SqmsOpenBiddingNotice"));

            MarkOutboxSent(host.ConnectionString, "SqmsOpenBiddingNotice");
            var reminder = runner.RunNoticesAndReminders();
            Assert.Equal(1, reminder.RemindersQueued);
            Assert.Equal(1, reminder.CyclesCompleted);
            Assert.Equal(1, CountOutbox(host, "SqmsOpenBiddingReminder"));

            var final = runner.RunNoticesAndReminders();
            Assert.Equal(0, final.NoticesQueued);
            Assert.Equal(0, final.RemindersQueued);
            Assert.Equal(1, CountOutbox(host, "SqmsOpenBiddingNotice"));
            Assert.Equal(1, CountOutbox(host, "SqmsOpenBiddingReminder"));
            Assert.Equal("Completed", host.DbContext.WorkflowInstances.AsNoTracking()
                .Single(row => row.WorkflowKey == "SqmsSaleCycle").CurrentStateKey);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void NoticeRunsAtExactBoundaryButNotBefore()
    {
        using var host = CreateInitializedHost();

        try
        {
            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET OpenBiddingNoticeAt = '2030-01-15T15:30:01+00:00',
                    OpenBiddingReminderAt = '2030-01-16T15:30:00+00:00'
                WHERE ID = 10000;
                """);
            var runner = host.GetRequiredService<SqmsScheduleRunner>();
            Assert.Equal(0, runner.RunNoticesAndReminders().NoticesQueued);

            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET OpenBiddingNoticeAt = '2030-01-15T15:30:00+00:00'
                WHERE ID = 10000;
                """);
            Assert.Equal(1, runner.RunNoticesAndReminders().NoticesQueued);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void ConcurrentScansProduceOneInitialNotice()
    {
        using var firstHost = CreateInitializedHost();
        using var secondHost = new TestConsoleHost(DatabaseName);

        try
        {
            var results = new SqmsScheduleRunResult[2];
            Parallel.Invoke(
                () => results[0] = firstHost.GetRequiredService<SqmsScheduleRunner>().RunNoticesAndReminders(),
                () => results[1] = secondHost.GetRequiredService<SqmsScheduleRunner>().RunNoticesAndReminders());

            Assert.Empty(results.SelectMany(result => result.Errors));
            Assert.Equal(1, results.Sum(result => result.NoticesQueued));
            Assert.Equal(1, CountOutbox(firstHost, "SqmsOpenBiddingNotice"));
        }
        finally
        {
            firstHost.ResetDatabase();
        }
    }

    [Fact]
    public void DueOpportunityClosesThenClassifiesSubmittedBidsInOneChildTransaction()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            MoveOpportunityToOpenBidding(coordinator);
            QueueAndSendInitialNotice(host);
            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET CommitmentAmountSale = '500000'
                WHERE ID = 10000;

                UPDATE SSQMS.Bid
                SET CommitmentAmountRequested = '300000', BidPricingCategory = 'FairValue'
                WHERE ID = 10000;

                INSERT SSQMS.Bid
                (
                    OpportunityId, BuyerRmteam, BidForFullCommitment,
                    CommitmentAmountRequested, AccountName, AccountNumber,
                    Status, WorkflowState, IsDeleted, BidPricingCategory,
                    BidRelationshipManagerEmail, BuyerRmTeamEmailsJson
                )
                VALUES
                    (10000, 'buyer-rm-team@example.test', 'Yes', '250000',
                     'Premium buyer', 'SQMS-BID-10001', 'DRAFT', N'Draft', 0, 'Premium',
                     'premium.rm@example.test', N'["premium.team@example.test"]'),
                    (10000, 'buyer-rm-team@example.test', 'Yes', '100000',
                     'Discount buyer', 'SQMS-BID-10002', 'DRAFT', N'Draft', 0, 'Discount',
                     'discount.rm@example.test', N'["discount.team@example.test"]');
                """);
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

            var result = host.GetRequiredService<SqmsScheduleRunner>().RunClosures();

            Assert.Empty(result.Errors);
            Assert.Equal(1, result.ParentsClosed);
            Assert.Equal(1, result.ChildBatchesCompleted);
            Assert.Equal(0, result.OpportunitiesNotFulfilled);
            AssertOpportunityProjection(host.ConnectionString, "ClosedToBidding", 3, "300000.00", "100000.00");
            AssertBidState(host.ConnectionString, "FairValue", "IpgReview");
            AssertBidState(host.ConnectionString, "Premium", "IpgReview");
            AssertBidState(host.ConnectionString, "Discount", "NotAllocated");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void ClassificationFailureLeavesParentClosedAndAllChildrenSubmittedThenRetryCompletes()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            MoveOpportunityToOpenBidding(coordinator);
            QueueAndSendInitialNotice(host);
            coordinator.StartBid(10000, MfoActor);
            coordinator.ExecuteBidAction(10000, new SqmsActionRequest("SubmitBid"), MfoActor);
            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET BiddingCloseAt = '2030-01-15T15:30:00+00:00'
                WHERE ID = 10000;
                UPDATE SSQMS.Bid
                SET CommitmentAmountRequested = 'not-money'
                WHERE ID = 10000;
                """);

            var runner = host.GetRequiredService<SqmsScheduleRunner>();
            var failed = runner.RunClosures();
            Assert.Equal(1, failed.ParentsClosed);
            Assert.Equal(0, failed.ChildBatchesCompleted);
            Assert.Single(failed.Errors);
            Assert.Contains("CommitmentAmountRequested", failed.Errors[0], StringComparison.Ordinal);
            AssertOpportunityProjection(host.ConnectionString, "ClosedToBidding", 1, "475000", "450000");
            AssertBidState(host.ConnectionString, "FairValue", "Submitted");

            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Bid SET CommitmentAmountRequested = '500000' WHERE ID = 10000;
                """);
            var retry = runner.RunClosures();
            Assert.Empty(retry.Errors);
            Assert.Equal(0, retry.ParentsClosed);
            Assert.Equal(1, retry.ChildBatchesCompleted);
            AssertBidState(host.ConnectionString, "FairValue", "IpgReview");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void ClosedOpportunityWithoutSubmittedBidsBecomesNotFulfilled()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            MoveOpportunityToOpenBidding(coordinator);
            ExecuteSql(host.ConnectionString, """
                UPDATE SSQMS.Opportunity
                SET BiddingCloseAt = '2030-01-15T15:30:00+00:00'
                WHERE ID = 10000;
                """);

            var result = host.GetRequiredService<SqmsScheduleRunner>().RunClosures();

            Assert.Empty(result.Errors);
            Assert.Equal(1, result.ParentsClosed);
            Assert.Equal(1, result.OpportunitiesNotFulfilled);
            AssertOpportunityProjection(host.ConnectionString, "NotFulfilled", 1, "475000", "450000");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    private static TestConsoleHost CreateInitializedHost()
    {
        var host = new TestConsoleHost(DatabaseName);
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/001_seed_sqms_transition_metadata.sql");
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/004_seed_sqms_end_to_end_metadata.sql");
        ExecuteSqlScript(host.ConnectionString, "sql/sqms/testing/004_create_sqms_mock_application_tables.sql");
        ExecuteSql(host.ConnectionString, """
            UPDATE SSQMS.Opportunity
            SET Status = 'OPEN TO BIDDING', WorkflowState = N'OpenToBidding'
            WHERE ID = 10000;
            """);
        return host;
    }

    private static int CountOutbox(TestConsoleHost host, string notificationType)
    {
        host.DbContext.ChangeTracker.Clear();
        return host.DbContext.NotificationOutbox.AsNoTracking().Count(row => row.NotificationType == notificationType);
    }

    private static void MoveOpportunityToOpenBidding(SqmsWorkflowCoordinator coordinator)
    {
        coordinator.StartOpportunity(10000, MfoActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("SubmitOpportunity"), MfoActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("MarkOpportunityPending"), IpgActor);
        coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("OpenOpportunityForBidding"), IpgActor);
    }

    private static void QueueAndSendInitialNotice(TestConsoleHost host)
    {
        var notice = host.GetRequiredService<SqmsScheduleRunner>().RunNoticesAndReminders();
        Assert.Equal(1, notice.NoticesQueued);
        MarkOutboxSent(host.ConnectionString, "SqmsOpenBiddingNotice");
    }

    private static void AssertOpportunityProjection(
        string connectionString,
        string expectedState,
        int expectedTotalBid,
        string expectedLargestBid,
        string expectedLowestBid)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkflowState, TotalBid, LargestBid, LowestBid
            FROM SSQMS.Opportunity WHERE ID = 10000;
            """;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(expectedState, reader.GetString(0));
        Assert.Equal(expectedTotalBid, reader.GetInt32(1));
        Assert.Equal(expectedLargestBid, reader.GetString(2));
        Assert.Equal(expectedLowestBid, reader.GetString(3));
    }

    private static void AssertBidState(
        string connectionString,
        string category,
        string expectedState)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkflowState FROM SSQMS.Bid
            WHERE OpportunityId = 10000 AND BidPricingCategory = @category;
            """;
        command.Parameters.AddWithValue("@category", category);
        Assert.Equal(expectedState, command.ExecuteScalar());
    }

    private static void MarkOutboxSent(string connectionString, string notificationType)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.NotificationOutbox
            SET Status = 'Sent', SentAtUtc = SYSDATETIMEOFFSET()
            WHERE NotificationType = @notificationType;
            """;
        command.Parameters.AddWithValue("@notificationType", notificationType);
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private static void InsertSecondOpportunityInSameCycle(string connectionString)
    {
        ExecuteSql(connectionString, """
            INSERT SSQMS.Opportunity
            (
                SellerRMTeam, Account, AccountName, AccountNumber,
                EffectiveDateForSale, Status, WorkflowState, IsDeleted,
                OpenBiddingNoticeAt, OpenBiddingReminderAt, BidResponseDueDate,
                BiddingCloseAt, SellerResponseDueDate,
                OpportunityRelationshipManagerEmail, SellerRmTeamEmailsJson
            )
            SELECT SellerRMTeam, Account, 'Second opportunity in same cycle', 'SQMS-OPP-10001',
                EffectiveDateForSale, 'OPEN TO BIDDING', N'OpenToBidding', 0,
                OpenBiddingNoticeAt, OpenBiddingReminderAt, BidResponseDueDate,
                BiddingCloseAt, SellerResponseDueDate,
                OpportunityRelationshipManagerEmail, SellerRmTeamEmailsJson
            FROM SSQMS.Opportunity WHERE ID = 10000;

            UPDATE SSQMS.Opportunity
            SET Status = 'OPEN TO BIDDING', WorkflowState = N'OpenToBidding'
            WHERE ID = 10000;
            """);
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
