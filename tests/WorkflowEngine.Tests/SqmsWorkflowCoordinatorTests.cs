using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsWorkflowCoordinatorTests
{
    private const string DatabaseName = "workflowengine_sqms_coordinator_tests";
    private static readonly SqmsActor MfoActor = new("sqms.mfo", "SQMS MFO", ["MFO"]);
    private static readonly SqmsActor IpgActor = new("sqms.ipg", "SQMS IPG", ["IPG"]);

    [Fact]
    public void StartAndExecuteProjectOpportunityAndBidStateAndReturnMetadataActions()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();

            var opportunityDraft = coordinator.StartOpportunity(10000, MfoActor);
            Assert.Equal("Draft", opportunityDraft.WorkflowState);
            Assert.Equal("DRAFT", opportunityDraft.Status);
            Assert.Equal(["SubmitOpportunity"], ActionKeys(opportunityDraft.WorkflowContext));
            AssertProjection(host.ConnectionString, "Opportunity", 10000, "Draft", "DRAFT", "sqms.mfo");

            var opportunitySubmitted = coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("SubmitOpportunity"),
                MfoActor);
            Assert.Equal("Submitted", opportunitySubmitted.WorkflowState);
            Assert.Equal("SUBMITTED", opportunitySubmitted.Status);
            AssertProjection(host.ConnectionString, "Opportunity", 10000, "Submitted", "SUBMITTED", "sqms.mfo");

            var opportunityContext = coordinator.GetOpportunityContext(10000, IpgActor);
            Assert.Equal(
                ["CancelOpportunity", "MarkOpportunityPending", "ReturnOpportunityToDraft"],
                ActionKeys(opportunityContext));

            coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("MarkOpportunityPending"),
                IpgActor);
            coordinator.ExecuteOpportunityAction(
                10000,
                new SqmsActionRequest("OpenOpportunityForBidding"),
                IpgActor);
            Assert.Equal(1, host.GetRequiredService<SqmsScheduleRunner>()
                .RunNoticesAndReminders().NoticesQueued);
            ExecuteSql(host.ConnectionString, """
                UPDATE dbo.NotificationOutbox
                SET Status = 'Sent', SentAtUtc = SYSDATETIMEOFFSET()
                WHERE NotificationType = 'SqmsOpenBiddingNotice';
                """);

            var bidDraft = coordinator.StartBid(10000, MfoActor);
            Assert.Equal("Draft", bidDraft.WorkflowState);
            Assert.Equal("DRAFT", bidDraft.Status);
            Assert.Equal(["SubmitBid"], ActionKeys(bidDraft.WorkflowContext));
            AssertProjection(host.ConnectionString, "Bid", 10000, "Draft", "DRAFT", "sqms.mfo");

            var bidSubmitted = coordinator.ExecuteBidAction(
                10000,
                new SqmsActionRequest("SubmitBid"),
                MfoActor);
            Assert.Equal("Submitted", bidSubmitted.WorkflowState);
            Assert.Equal("SUBMITTED", bidSubmitted.Status);
            AssertProjection(host.ConnectionString, "Bid", 10000, "Submitted", "SUBMITTED", "sqms.mfo");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void RepeatedStartReturnsExistingWorkflowWithoutDuplication()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            var first = coordinator.StartOpportunity(10000, MfoActor);
            var second = coordinator.StartOpportunity(10000, MfoActor);

            Assert.Equal(first.WorkflowContext.Workflow.WorkflowInstanceId, second.WorkflowContext.Workflow.WorkflowInstanceId);
            Assert.Equal(first.WorkflowState, second.WorkflowState);
            Assert.Equal(1, host.DbContext.WorkflowInstances.Count(row =>
                row.WorkflowInstanceId == SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(10000)));
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void ProjectionFailureRollsBackWorkflowAndApplicationChanges()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.StartOpportunity(10000, MfoActor);
            ExecuteSql(host.ConnectionString, """
                CREATE OR ALTER TRIGGER SSQMS.TR_Opportunity_BlockSubmitted
                ON SSQMS.Opportunity
                AFTER UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted WHERE WorkflowState = N'Submitted')
                        THROW 51000, 'Forced SQMS projection failure.', 1;
                END
                """);

            var exception = Assert.Throws<SqmsIntegrationException>(() =>
                coordinator.ExecuteOpportunityAction(
                    10000,
                    new SqmsActionRequest("SubmitOpportunity"),
                    MfoActor));

            Assert.Contains("Forced SQMS projection failure", exception.Message, StringComparison.Ordinal);
            AssertProjection(host.ConnectionString, "Opportunity", 10000, "Draft", "DRAFT", "sqms.mfo");
            host.DbContext.ChangeTracker.Clear();
            Assert.Equal(
                "Draft",
                host.DbContext.WorkflowInstances.AsNoTracking().Single(row =>
                    row.WorkflowInstanceId == SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(10000)).CurrentStateKey);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void SubmitBidRequiresParentOpenToBidding()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.StartBid(10000, MfoActor);

            var exception = Assert.Throws<SqmsIntegrationException>(() =>
                coordinator.ExecuteBidAction(
                    10000,
                    new SqmsActionRequest("SubmitBid"),
                    MfoActor));

            Assert.Contains("OpenToBidding", exception.Message, StringComparison.Ordinal);
            AssertProjection(host.ConnectionString, "Bid", 10000, "Draft", "DRAFT", "sqms.mfo");
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    [Fact]
    public void SubmitBidRequiresSentNoticeAndFutureCloseTime()
    {
        using var host = CreateInitializedHost();

        try
        {
            var coordinator = host.GetRequiredService<SqmsWorkflowCoordinator>();
            coordinator.StartOpportunity(10000, MfoActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("SubmitOpportunity"), MfoActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("MarkOpportunityPending"), IpgActor);
            coordinator.ExecuteOpportunityAction(10000, new SqmsActionRequest("OpenOpportunityForBidding"), IpgActor);
            coordinator.StartBid(10000, MfoActor);

            var noticeException = Assert.Throws<SqmsIntegrationException>(() =>
                coordinator.ExecuteBidAction(10000, new SqmsActionRequest("SubmitBid"), MfoActor));
            Assert.Contains("notice is Sent", noticeException.Message, StringComparison.Ordinal);

            Assert.Equal(1, host.GetRequiredService<SqmsScheduleRunner>()
                .RunNoticesAndReminders().NoticesQueued);
            ExecuteSql(host.ConnectionString, """
                UPDATE dbo.NotificationOutbox
                SET Status = 'Sent', SentAtUtc = SYSDATETIMEOFFSET()
                WHERE NotificationType = 'SqmsOpenBiddingNotice';
                UPDATE SSQMS.Opportunity
                SET BiddingCloseAt = '2030-01-15T15:30:00+00:00'
                WHERE ID = 10000;
                """);

            var closeException = Assert.Throws<SqmsIntegrationException>(() =>
                coordinator.ExecuteBidAction(10000, new SqmsActionRequest("SubmitBid"), MfoActor));
            Assert.Contains("bidding is closed", closeException.Message, StringComparison.Ordinal);
            AssertProjection(host.ConnectionString, "Bid", 10000, "Draft", "DRAFT", "sqms.mfo");
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
        return host;
    }

    private static string[] ActionKeys(WorkflowContext context)
    {
        return context.AvailableActions
            .Select(action => action.ActionKey)
            .OrderBy(actionKey => actionKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertProjection(
        string connectionString,
        string table,
        int id,
        string workflowState,
        string status,
        string modifiedBy)
    {
        Assert.Contains(table, new[] { "Opportunity", "Bid" }, StringComparer.Ordinal);
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT WorkflowState, Status, ModifiedBy, ModifiedDate
            FROM SSQMS.[{table}]
            WHERE ID = @id
            """;
        command.Parameters.AddWithValue("@id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(workflowState, reader.GetString(0));
        Assert.Equal(status, reader.GetString(1));
        Assert.Equal(modifiedBy, reader.GetString(2));
        Assert.False(reader.IsDBNull(3));
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
