using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsConsoleMockIntegrationTests
{
    private const string OpportunityBusinessObjectId = "SQMS-OPP-10000";
    private const string OpportunityWorkflowInstanceId = "WF-SQMS-OPP-10000";
    private const string BidBusinessObjectId = "SQMS-BID-10000";
    private const string BidWorkflowInstanceId = "WF-SQMS-BID-10000";

    [Fact]
    public void MockApplicationRowsDriveExistingConsoleStartContextAndExecutePaths()
    {
        using var host = new TestConsoleHost("workflowengine_sqms_console_mock_tests");
        host.ResetDatabase();

        try
        {
            AssertCommandSuccess(host.Execute("init-db"));
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/001_seed_sqms_transition_metadata.sql");
            ExecuteSqlScript(host.ConnectionString, "sql/sqms/testing/004_create_sqms_mock_application_tables.sql");

            AssertCommandSuccess(host.Execute(
                "start-workflow", "SqmsOpportunity", OpportunityBusinessObjectId,
                "--user", "sqms.mfo", "--reset", "--format", "json"));

            var opportunityDraft = ReadContext(host,
                OpportunityWorkflowInstanceId, "sqms.mfo", "MFO");
            Assert.Equal("Draft", opportunityDraft.WorkflowContext.Workflow.CurrentState);
            Assert.Equal(
                ["SubmitOpportunity"],
                opportunityDraft.WorkflowContext.AvailableActions
                    .Select(action => action.ActionKey)
                    .OrderBy(actionKey => actionKey)
                    .ToArray());

            var opportunityAction = host.Execute(
                "execute-json", "./samples/sqms/01-submit-opportunity.json", "--format", "json");
            AssertCommandSuccess(opportunityAction);
            AssertActionSuccess(opportunityAction, "Draft", "Submitted", "SubmitOpportunity");

            var opportunitySubmitted = ReadContext(host,
                OpportunityWorkflowInstanceId, "sqms.ipg", "IPG");
            Assert.Equal("Submitted", opportunitySubmitted.WorkflowContext.Workflow.CurrentState);
            Assert.Equal(
                ["CancelOpportunity", "MarkOpportunityPending", "ReturnOpportunityToDraft"],
                opportunitySubmitted.WorkflowContext.AvailableActions
                    .Select(action => action.ActionKey)
                    .OrderBy(actionKey => actionKey)
                    .ToArray());

            AssertCommandSuccess(host.Execute(
                "start-workflow", "SqmsBid", BidBusinessObjectId,
                "--user", "sqms.mfo", "--reset", "--format", "json"));

            var bidDraft = ReadContext(host, BidWorkflowInstanceId, "sqms.mfo", "MFO");
            Assert.Equal("Draft", bidDraft.WorkflowContext.Workflow.CurrentState);
            Assert.Equal(
                ["SubmitBid"],
                bidDraft.WorkflowContext.AvailableActions
                    .Select(action => action.ActionKey)
                    .OrderBy(actionKey => actionKey)
                    .ToArray());

            var bidAction = host.Execute(
                "execute-json", "./samples/sqms/02-submit-bid.json", "--format", "json");
            AssertCommandSuccess(bidAction);
            AssertActionSuccess(bidAction, "Draft", "Submitted", "SubmitBid");

            var bidSubmitted = ReadContext(host, BidWorkflowInstanceId, "sqms.ipg", "IPG");
            Assert.Equal("Submitted", bidSubmitted.WorkflowContext.Workflow.CurrentState);
            Assert.Equal(
                ["MarkBidNotAllocated", "MoveBidToIpgReview"],
                bidSubmitted.WorkflowContext.AvailableActions
                    .Select(action => action.ActionKey)
                    .OrderBy(actionKey => actionKey)
                    .ToArray());

            ExecuteSqlScript(host.ConnectionString, "sql/sqms/testing/005_verify_sqms_console_mock.sql");
            VerifyApplicationRowsRemainUnchanged(host.ConnectionString);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    private static WorkflowContextResponse ReadContext(
        TestConsoleHost host,
        string workflowInstanceId,
        string userId,
        string role)
    {
        var result = host.Execute(
            "show-context", workflowInstanceId,
            "--user", userId,
            "--roles", role,
            "--format", "json");
        AssertCommandSuccess(result);

        var response = JsonSerializer.Deserialize<WorkflowContextResponse>(result.Message, WorkflowJson.Options);
        return Assert.IsType<WorkflowContextResponse>(response);
    }

    private static void AssertActionSuccess(
        CommandResult result,
        string expectedOldState,
        string expectedNewState,
        string expectedActionKey)
    {
        var response = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);
        var envelope = Assert.IsType<WorkflowActionResultEnvelope>(response);

        Assert.True(envelope.Result.Success, envelope.Result.Message);
        Assert.Equal("Success", envelope.Result.ResultCode);
        Assert.Equal(expectedOldState, envelope.Result.OldState);
        Assert.Equal(expectedNewState, envelope.Result.NewState);
        Assert.Equal(expectedActionKey, envelope.Result.ActionKey);
        Assert.Equal(expectedActionKey, envelope.Result.Audit?.ActionKey);
        Assert.Null(envelope.Result.CreatedTask);
        Assert.NotNull(envelope.Result.Notification);
        Assert.False(envelope.Result.Notification.Queued);
        Assert.Null(envelope.Result.Notification.NotificationType);
    }

    private static void VerifyApplicationRowsRemainUnchanged(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM SSQMS.Opportunity
                 WHERE ID = 10000 AND Status = 'DRAFT' AND WorkflowState = N'Draft'
                   AND ModifiedBy IS NULL AND ModifiedDate IS NULL AND IsDeleted = 0),
                (SELECT COUNT(*) FROM SSQMS.Bid
                 WHERE ID = 10000 AND OpportunityId = 10000
                   AND Status = 'DRAFT' AND WorkflowState = N'Draft'
                   AND ModifiedBy IS NULL AND ModifiedDate IS NULL AND IsDeleted = 0)
            """;

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    private static void ExecuteSqlScript(string connectionString, string relativePath)
    {
        var script = File.ReadAllText(ResolveRepositoryFile(relativePath));
        var batches = Regex.Split(
            script,
            @"^\s*GO\s*(?:--.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        foreach (var batch in batches.Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 60;
            command.ExecuteNonQuery();
        }
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

    private static void AssertCommandSuccess(CommandResult result)
    {
        Assert.True(result.ExitCode == 0, result.Message);
    }
}
