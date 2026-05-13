using System.Data;
using Microsoft.EntityFrameworkCore;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class DatabaseFirstStoredProcedureTests
{
    private static readonly string[] ExpectedProcedures =
    [
        "usp_WorkflowDefinition_GetActiveVersion",
        "usp_WorkflowMetadata_GetForVersion",
        "usp_WorkflowInstance_Get",
        "usp_WorkflowInstance_GetByBusinessObject",
        "usp_WorkflowInstance_Insert",
        "usp_WorkflowInstance_UpdateState",
        "usp_WorkflowTask_GetPending",
        "usp_WorkflowTask_Insert",
        "usp_WorkflowTask_Complete",
        "usp_MockSectionData_Insert",
        "usp_WorkflowAudit_Insert",
        "usp_NotificationOutbox_Insert",
        "usp_IdempotencyRecord_GetByKey",
        "usp_IdempotencyRecord_Insert",
        "usp_WorkflowContext_Get",
        "usp_RuntimeData_Reset",
        "usp_WorkflowDefinitionVersion_Get",
        "usp_WorkflowActionDefinition_Get",
        "usp_WorkflowTransitionDefinition_ListForAction",
        "usp_WorkflowTaskDefinition_Get",
        "usp_WorkflowTaskDefinition_HasSla",
        "usp_SlaPolicy_GetDurationMinutes",
        "usp_NotificationRoute_GetByRouteKey",
        "usp_WorkflowInspection_Get",
        "usp_Sla_GetScanSummary",
        "usp_Sla_GetOverdueCandidates",
        "usp_Sla_InsertOverdueNotification",
        "usp_NotificationOutbox_GetClaimable",
        "usp_NotificationOutbox_Claim",
        "usp_NotificationOutbox_GetByClaim",
        "usp_NotificationOutbox_MarkSent",
        "usp_NotificationOutbox_ReleaseClaimAfterFailure"
    ];

    [Fact]
    public void DatabaseFirstInitializerCreatesStoredProcedures()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var actual = ReadStoredProcedureNames(dbContext);
        var missing = ExpectedProcedures.Except(actual, StringComparer.Ordinal).ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void ActiveWorkflowDefinitionStoredProcedureReturnsSeededWorkflow()
    {
        using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
        var connection = dbContext.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[usp_WorkflowDefinition_GetActiveVersion]";
            command.CommandType = CommandType.StoredProcedure;
            var workflowKey = command.CreateParameter();
            workflowKey.ParameterName = "@WorkflowKey";
            workflowKey.Value = "PortfolioTrade";
            command.Parameters.Add(workflowKey);

            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("PortfolioTrade", reader["WorkflowKey"]);
            Assert.Equal(1, reader["VersionNumber"]);
        }
        finally
        {
            connection.Close();
        }
    }

    private static string[] ReadStoredProcedureNames(Infrastructure.Persistence.WorkflowDbContext dbContext)
    {
        var connection = dbContext.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [name]
                FROM [sys].[procedures]
                WHERE [schema_id] = SCHEMA_ID(N'dbo')
                    AND [name] LIKE N'usp[_]%'
                ORDER BY [name];
                """;

            var names = new List<string>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            return names.ToArray();
        }
        finally
        {
            connection.Close();
        }
    }
}
