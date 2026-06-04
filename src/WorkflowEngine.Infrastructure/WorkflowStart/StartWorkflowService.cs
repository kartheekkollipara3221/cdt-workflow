using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Application.WorkflowStart;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowContext;
using WorkflowEngine.Infrastructure.WorkflowExecution;

namespace WorkflowEngine.Infrastructure.WorkflowStart;

public sealed class StartWorkflowService(
    WorkflowDbContext dbContext,
    TimeProvider timeProvider,
    WorkflowActionResultBuilder resultBuilder,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    WorkflowExecutionService workflowExecutionService) : IStartWorkflowService
{
    public StartWorkflowService(
        WorkflowDbContext dbContext,
        TimeProvider timeProvider,
        WorkflowActionResultBuilder resultBuilder)
        : this(
            dbContext,
            timeProvider,
            resultBuilder,
            new WorkflowActionEnvelopeNormalizer(),
            new CanonicalRequestHasher(),
            new WorkflowExecutionService(
                dbContext,
                new WorkflowContextReader(dbContext, timeProvider),
                timeProvider,
                resultBuilder,
                new CanonicalWorkflowResultSerializer()))
    {
    }

    public StartWorkflowResult Start(StartWorkflowRequest request)
    {
        Validate(request);

        var workflowInstanceId = $"WF-{request.BusinessObjectId}";
        var initialActionKey = OptionalText(request.InitialActionKey);
        if (request.Reset)
        {
            if (initialActionKey is not null)
            {
                return ResetAndStartWithInitialAction(
                    request with { InitialActionKey = initialActionKey },
                    workflowInstanceId);
            }

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

        if (initialActionKey is not null)
        {
            return StartWithInitialAction(
                request with { InitialActionKey = initialActionKey },
                workflowInstanceId);
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

        var counts = DeleteRuntimeRows(request, workflowInstanceId);

        dbContext.ChangeTracker.Clear();
        var envelope = InsertFreshInstance(request, workflowInstanceId);
        transaction.Commit();
        return new StartWorkflowResult(envelope, counts);
    }

    private StartWorkflowResult ResetAndStartWithInitialAction(
        StartWorkflowRequest request,
        string workflowInstanceId)
    {
        using var transaction = dbContext.Database.BeginTransaction();

        var counts = DeleteRuntimeRows(request, workflowInstanceId);

        dbContext.ChangeTracker.Clear();
        InsertFreshInstance(request, workflowInstanceId);
        var actionEnvelope = ExecuteInitialAction(request, workflowInstanceId);
        if (!actionEnvelope.Result.Success)
        {
            transaction.Rollback();
            return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
        }

        transaction.Commit();
        return new StartWorkflowResult(actionEnvelope, counts);
    }

    private StartWorkflowResult StartWithInitialAction(
        StartWorkflowRequest request,
        string workflowInstanceId)
    {
        using var transaction = dbContext.Database.BeginTransaction();

        try
        {
            InsertFreshInstance(request, workflowInstanceId);
            var actionEnvelope = ExecuteInitialAction(request, workflowInstanceId);
            if (!actionEnvelope.Result.Success)
            {
                transaction.Rollback();
                return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
            }

            transaction.Commit();
            return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
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

    private WorkflowActionResultEnvelope ExecuteInitialAction(
        StartWorkflowRequest request,
        string workflowInstanceId)
    {
        var generatedId = Guid.NewGuid().ToString("N");
        var envelope = new WorkflowActionEnvelope
        {
            Request = new WorkflowRequest
            {
                CommandType = "ExecuteWorkflowAction",
                CommandVersion = 1,
                ClientActionId = generatedId,
                CorrelationId = generatedId,
                SourceSystem = "WorkflowEngine.StartWorkflow"
            },
            Actor = new WorkflowActor
            {
                UserId = request.StartedByUserId,
                DisplayName = string.IsNullOrWhiteSpace(request.StartedByDisplayName)
                    ? request.StartedByUserId
                    : request.StartedByDisplayName,
                Roles = request.StartedByRoles?
                    .Where(role => !string.IsNullOrWhiteSpace(role))
                    .Select(role => role.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? []
            },
            Workflow = new WorkflowActionBlock
            {
                WorkflowInstanceId = workflowInstanceId,
                ActionKey = request.InitialActionKey,
                NotificationContext = CopyNotificationContext(request.NotificationContext),
                IdempotencyKey = generatedId
            }
        };

        var normalized = normalizer.Normalize(
            envelope,
            businessObjectOverride: null,
            workflowInstanceOverride: null);

        return workflowExecutionService.Execute(new WorkflowExecutionRequest(
                normalized,
                hasher.ComputeHash(normalized.Envelope),
                AdminOverride: false)
            {
                DeriveIdentityFromPersistence = true,
                PersistSectionData = false,
                ValidateActionEcho = false
            });
    }

    private StartWorkflowResetCounts DeleteRuntimeRows(
        StartWorkflowRequest request,
        string workflowInstanceId)
    {
        return new StartWorkflowResetCounts(
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

    private static string? OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, string?>? CopyNotificationContext(
        IReadOnlyDictionary<string, string?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var copied = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            copied[pair.Key.Trim()] = pair.Value!.Trim();
        }

        return copied.Count == 0 ? null : copied;
    }

    private static bool IsWorkflowInstanceUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("WorkflowInstance", StringComparison.OrdinalIgnoreCase));
    }
}
