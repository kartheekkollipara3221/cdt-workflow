using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

namespace WorkflowEngine.Infrastructure.WorkflowStart;

public sealed class StartWorkflowService(
    DatabaseFirstWorkflowStore store,
    TimeProvider timeProvider,
    WorkflowActionResultBuilder resultBuilder) : IStartWorkflowService
{
    public StartWorkflowResult Start(StartWorkflowRequest request)
    {
        Validate(request);

        var workflowInstanceId = $"WF-{request.BusinessObjectId}";
        if (request.Reset)
        {
            return ResetAndStart(request, workflowInstanceId);
        }

        var existing = FindExisting(request);
        if (existing is not null)
        {
            return new StartWorkflowResult(
                resultBuilder.BuildAlreadyExists(
                    request.WorkflowKey,
                    request.BusinessObjectType,
                    request.BusinessObjectId,
                    existing),
                ResetCounts: null);
        }

        using var connection = store.OpenConnection();
        using var transaction = connection.BeginTransaction();
        try
        {
            var envelope = InsertFreshInstance(connection, transaction, request, workflowInstanceId);
            transaction.Commit();
            return new StartWorkflowResult(envelope, ResetCounts: null);
        }
        catch (SqlException exception) when (IsWorkflowInstanceUniqueViolation(exception))
        {
            transaction.Rollback();
            var duplicate = FindExisting(request);
            if (duplicate is null)
            {
                throw;
            }

            return new StartWorkflowResult(
                resultBuilder.BuildAlreadyExists(
                    request.WorkflowKey,
                    request.BusinessObjectType,
                    request.BusinessObjectId,
                    duplicate),
                ResetCounts: null);
        }
    }

    private StartWorkflowResult ResetAndStart(StartWorkflowRequest request, string workflowInstanceId)
    {
        using var connection = store.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var counts = store.ResetRuntimeData(
            connection,
            transaction,
            workflowInstanceId,
            request.BusinessObjectType,
            request.BusinessObjectId);

        var envelope = InsertFreshInstance(connection, transaction, request, workflowInstanceId);
        transaction.Commit();
        return new StartWorkflowResult(envelope, counts);
    }

    private WorkflowActionResultEnvelope InsertFreshInstance(
        SqlConnection connection,
        SqlTransaction transaction,
        StartWorkflowRequest request,
        string workflowInstanceId)
    {
        var now = timeProvider.GetUtcNow();
        var version = store.GetActiveDefinitionVersion(request.WorkflowKey)
            ?? throw new InvalidOperationException($"Workflow key '{request.WorkflowKey}' does not have an active definition version.");
        store.InsertWorkflowInstance(
            connection,
            transaction,
            workflowInstanceId,
            request.WorkflowKey,
            version.WorkflowDefinitionVersionId,
            request.BusinessObjectType,
            request.BusinessObjectId,
            "Draft",
            "Active",
            now,
            request.StartedByUserId,
            completedAtUtc: null,
            version: 1);

        return resultBuilder.BuildStartWorkflowSuccess(
            request.WorkflowKey,
            request.BusinessObjectType,
            request.BusinessObjectId,
            workflowInstanceId,
            "Draft",
            "Active",
            1,
            now,
            request.StartedByUserId);
    }

    private ExistingWorkflowInstance? FindExisting(StartWorkflowRequest request)
    {
        return store.GetExistingWorkflowInstance(
            request.WorkflowKey,
            request.BusinessObjectType,
            request.BusinessObjectId);
    }

    private static void Validate(StartWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowKey))
        {
            throw new ArgumentException("workflowKey is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.BusinessObjectType))
        {
            throw new ArgumentException("businessObjectType is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.BusinessObjectId))
        {
            throw new ArgumentException("businessObjectId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.StartedByUserId))
        {
            throw new ArgumentException("startedByUserId is required.", nameof(request));
        }
    }

    private static bool IsWorkflowInstanceUniqueViolation(SqlException exception)
    {
        return exception.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("WorkflowInstance", StringComparison.OrdinalIgnoreCase));
    }
}
