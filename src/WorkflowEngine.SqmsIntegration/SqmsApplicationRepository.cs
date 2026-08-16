using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.SqmsIntegration;

public sealed class SqmsApplicationRepository(
    WorkflowDbContext dbContext)
{
    internal SqmsApplicationState ReadForUpdate(SqmsEntityKind entityKind, int entityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entityId);
        var tableName = TableName(entityKind);

        using var command = CreateCommand($"""
            SELECT [WorkflowState], [Status]
            FROM [SSQMS].[{tableName}] WITH (UPDLOCK, ROWLOCK)
            WHERE [ID] = @entityId AND ISNULL([IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@entityId", SqlDbType.Int) { Value = entityId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"SQMS {tableName} row '{entityId}' was not found or is deleted.");
        }

        return new SqmsApplicationState(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    internal void ProjectWorkflowState(
        SqmsEntityKind entityKind,
        int entityId,
        string? expectedWorkflowState,
        string workflowState,
        string status,
        string modifiedBy,
        DateTime modifiedDateUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entityId);
        var tableName = TableName(entityKind);

        using var command = CreateCommand($"""
            UPDATE [SSQMS].[{tableName}]
            SET [WorkflowState] = @workflowState,
                [Status] = @status,
                [ModifiedBy] = @modifiedBy,
                [ModifiedDate] = @modifiedDateUtc
            WHERE [ID] = @entityId
              AND ISNULL([IsDeleted], 0) = 0
              AND (([WorkflowState] = @expectedWorkflowState)
                   OR ([WorkflowState] IS NULL AND @expectedWorkflowState IS NULL));
            """);
        command.Parameters.Add(new SqlParameter("@workflowState", SqlDbType.NVarChar, 100) { Value = workflowState });
        command.Parameters.Add(new SqlParameter("@status", SqlDbType.VarChar, 200) { Value = status });
        command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.NVarChar, 50) { Value = modifiedBy });
        command.Parameters.Add(new SqlParameter("@modifiedDateUtc", SqlDbType.DateTime) { Value = modifiedDateUtc });
        command.Parameters.Add(new SqlParameter("@entityId", SqlDbType.Int) { Value = entityId });
        command.Parameters.Add(new SqlParameter("@expectedWorkflowState", SqlDbType.NVarChar, 100)
        {
            Value = (object?)expectedWorkflowState ?? DBNull.Value
        });

        if (command.ExecuteNonQuery() != 1)
        {
            throw new SqmsIntegrationException(
                $"SQMS {tableName} row '{entityId}' changed while its workflow action was executing.");
        }
    }

    private SqlCommand CreateCommand(string commandText)
    {
        dbContext.Database.OpenConnection();
        var command = (SqlCommand)dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = 30;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;
        return command;
    }

    private static string TableName(SqmsEntityKind entityKind)
    {
        return entityKind switch
        {
            SqmsEntityKind.Opportunity => "Opportunity",
            SqmsEntityKind.Bid => "Bid",
            _ => throw new ArgumentOutOfRangeException(nameof(entityKind), entityKind, "Unknown SQMS entity kind.")
        };
    }
}
