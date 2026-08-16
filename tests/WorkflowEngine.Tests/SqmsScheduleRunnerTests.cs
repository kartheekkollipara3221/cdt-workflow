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
