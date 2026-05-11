using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.WorkflowStart;

public sealed class StartWorkflowService(
    WorkflowDbContext dbContext,
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

        using var transaction = dbContext.Database.BeginTransaction();
        try
        {
            var envelope = InsertFreshInstance(request, workflowInstanceId);
            transaction.Commit();
            return new StartWorkflowResult(envelope, ResetCounts: null);
        }
        catch (DbUpdateException exception) when (IsWorkflowInstanceUniqueViolation(exception))
        {
            transaction.Rollback();
            dbContext.ChangeTracker.Clear();
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
        using var transaction = dbContext.Database.BeginTransaction();

        var counts = new StartWorkflowResetCounts(
            dbContext.IdempotencyRecords
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete(),
            dbContext.NotificationOutbox
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete(),
            dbContext.WorkflowAudits
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete(),
            dbContext.MockSectionData
                .Where(row => row.BusinessObjectType == request.BusinessObjectType
                    && row.BusinessObjectId == request.BusinessObjectId)
                .ExecuteDelete(),
            dbContext.WorkflowTasks
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete(),
            dbContext.WorkflowInstances
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete());

        var envelope = InsertFreshInstance(request, workflowInstanceId);
        transaction.Commit();
        return new StartWorkflowResult(envelope, counts);
    }

    private WorkflowActionResultEnvelope InsertFreshInstance(StartWorkflowRequest request, string workflowInstanceId)
    {
        var now = timeProvider.GetUtcNow();
        var versionId = ResolveActiveDefinitionVersionId(request.WorkflowKey);
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = request.WorkflowKey,
            WorkflowDefinitionVersionId = versionId,
            BusinessObjectType = request.BusinessObjectType,
            BusinessObjectId = request.BusinessObjectId,
            CurrentStateKey = "Draft",
            Status = "Active",
            StartedAtUtc = now,
            StartedByUserId = request.StartedByUserId,
            CompletedAtUtc = null,
            Version = 1
        };

        dbContext.WorkflowInstances.Add(instance);
        dbContext.SaveChanges();

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
        return dbContext.WorkflowInstances
            .AsNoTracking()
            .Where(row =>
                row.WorkflowKey == request.WorkflowKey
                && row.BusinessObjectType == request.BusinessObjectType
                && row.BusinessObjectId == request.BusinessObjectId)
            .Select(row => new ExistingWorkflowInstance(
                row.WorkflowInstanceId,
                row.Status,
                row.Version,
                row.CurrentStateKey,
                row.StartedAtUtc,
                row.CompletedAtUtc))
            .SingleOrDefault();
    }

    private long ResolveActiveDefinitionVersionId(string workflowKey)
    {
        var versionId = dbContext.WorkflowDefinitionVersions
            .AsNoTracking()
            .Where(row => row.IsActive && row.WorkflowDefinition != null && row.WorkflowDefinition.WorkflowKey == workflowKey)
            .Select(row => (long?)row.Id)
            .SingleOrDefault();

        return versionId ?? throw new InvalidOperationException($"Workflow key '{workflowKey}' does not have an active definition version.");
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

    private static bool IsWorkflowInstanceUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("WorkflowInstance", StringComparison.OrdinalIgnoreCase));
    }
}
