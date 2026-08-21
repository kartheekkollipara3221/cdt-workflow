using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
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
    WorkflowExecutionService workflowExecutionService,
    WorkflowParticipantCaptureService participantCaptureService) : IStartWorkflowService
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
                new CanonicalWorkflowResultSerializer(),
                new NotificationTemplateRenderer()),
            new WorkflowParticipantCaptureService(dbContext, timeProvider))
    {
    }

    public StartWorkflowService(
        WorkflowDbContext dbContext,
        TimeProvider timeProvider,
        WorkflowActionResultBuilder resultBuilder,
        WorkflowActionEnvelopeNormalizer normalizer,
        CanonicalRequestHasher hasher,
        WorkflowExecutionService workflowExecutionService)
        : this(
            dbContext,
            timeProvider,
            resultBuilder,
            normalizer,
            hasher,
            workflowExecutionService,
            new WorkflowParticipantCaptureService(dbContext, timeProvider))
    {
    }

    public StartWorkflowResult Start(StartWorkflowRequest request)
    {
        Validate(request);
        var activeDefinition = ResolveActiveDefinition(request.WorkflowKey);
        request = request with { BusinessObjectType = activeDefinition.BusinessObjectType };

        var workflowInstanceId = $"WF-{request.BusinessObjectId}";
        var initialActionKey = OptionalText(request.InitialActionKey);
        if (request.Reset)
        {
            if (initialActionKey is not null)
            {
                return ResetAndStartWithInitialAction(
                    request with { InitialActionKey = initialActionKey },
                    workflowInstanceId,
                    activeDefinition.VersionId);
            }

            return ResetAndStart(request, workflowInstanceId, activeDefinition.VersionId);
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
                workflowInstanceId,
                activeDefinition.VersionId);
        }

        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;
        try
        {
            var envelope = InsertFreshInstance(request, workflowInstanceId, activeDefinition.VersionId);
            var capture = CaptureParticipants(request, workflowInstanceId, activeDefinition.VersionId);
            if (capture.Errors.Count > 0)
            {
                transaction?.Rollback();
                dbContext.ChangeTracker.Clear();
                return BuildParticipantValidationFailed(request, workflowInstanceId, capture.Errors);
            }

            dbContext.SaveChanges();
            transaction?.Commit();
            return new StartWorkflowResult(envelope, ResetCounts: null);
        }
        catch (DbUpdateException exception) when (IsWorkflowInstanceUniqueViolation(exception))
        {
            transaction?.Rollback();
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

    private StartWorkflowResult ResetAndStart(
        StartWorkflowRequest request,
        string workflowInstanceId,
        long workflowDefinitionVersionId)
    {
        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;

        var counts = DeleteRuntimeRows(request, workflowInstanceId);

        dbContext.ChangeTracker.Clear();
        var envelope = InsertFreshInstance(request, workflowInstanceId, workflowDefinitionVersionId);
        var capture = CaptureParticipants(request, workflowInstanceId, workflowDefinitionVersionId);
        if (capture.Errors.Count > 0)
        {
            transaction?.Rollback();
            dbContext.ChangeTracker.Clear();
            return BuildParticipantValidationFailed(request, workflowInstanceId, capture.Errors);
        }

        dbContext.SaveChanges();
        transaction?.Commit();
        return new StartWorkflowResult(envelope, counts);
    }

    private StartWorkflowResult ResetAndStartWithInitialAction(
        StartWorkflowRequest request,
        string workflowInstanceId,
        long workflowDefinitionVersionId)
    {
        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;

        var counts = DeleteRuntimeRows(request, workflowInstanceId);

        dbContext.ChangeTracker.Clear();
        InsertFreshInstance(request, workflowInstanceId, workflowDefinitionVersionId);
        var capture = CaptureParticipants(request, workflowInstanceId, workflowDefinitionVersionId);
        if (capture.Errors.Count > 0)
        {
            transaction?.Rollback();
            dbContext.ChangeTracker.Clear();
            return BuildParticipantValidationFailed(request, workflowInstanceId, capture.Errors);
        }

        dbContext.SaveChanges();
        var actionEnvelope = ExecuteInitialAction(request, workflowInstanceId, capture.ActionNotificationInput);
        if (!actionEnvelope.Result.Success)
        {
            transaction?.Rollback();
            return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
        }

        transaction?.Commit();
        return new StartWorkflowResult(actionEnvelope, counts);
    }

    private StartWorkflowResult StartWithInitialAction(
        StartWorkflowRequest request,
        string workflowInstanceId,
        long workflowDefinitionVersionId)
    {
        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;

        try
        {
            InsertFreshInstance(request, workflowInstanceId, workflowDefinitionVersionId);
            var capture = CaptureParticipants(request, workflowInstanceId, workflowDefinitionVersionId);
            if (capture.Errors.Count > 0)
            {
                transaction?.Rollback();
                dbContext.ChangeTracker.Clear();
                return BuildParticipantValidationFailed(request, workflowInstanceId, capture.Errors);
            }

            dbContext.SaveChanges();
            var actionEnvelope = ExecuteInitialAction(request, workflowInstanceId, capture.ActionNotificationInput);
            if (!actionEnvelope.Result.Success)
            {
                transaction?.Rollback();
                return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
            }

            transaction?.Commit();
            return new StartWorkflowResult(actionEnvelope, ResetCounts: null);
        }
        catch (DbUpdateException exception) when (IsWorkflowInstanceUniqueViolation(exception))
        {
            transaction?.Rollback();
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
        string workflowInstanceId,
        WorkflowNotificationInput? actionNotificationInput)
    {
        var generatedId = Guid.NewGuid().ToString("N");
        var effectiveIdempotencyKey = OptionalText(request.IdempotencyKey) ?? generatedId;
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
                SlaInput = request.SlaInput is null
                    ? null
                    : new WorkflowSlaInput { TargetDate = request.SlaInput.TargetDate },
                NotificationContext = CopyNotificationContext(
                    request.NotificationInput is null
                        ? request.NotificationContext
                        : actionNotificationInput?.TemplateData),
                NotificationRecipients = request.NotificationInput is null
                    ? CopyNotificationRecipients(request.NotificationRecipients)
                    : null,
                IdempotencyKey = effectiveIdempotencyKey
            }
        };

        var normalized = normalizer.Normalize(
            envelope,
            businessObjectOverride: null,
            workflowInstanceOverride: null);

        return workflowExecutionService.Execute(new WorkflowExecutionRequest(
                normalized,
                hasher.ComputeHash(normalized.Envelope, actionNotificationInput),
                AdminOverride: false)
        {
            DeriveIdentityFromPersistence = true,
            PersistSectionData = false,
            ValidateActionEcho = false,
            NotificationInput = actionNotificationInput
        });
    }

    private StartParticipantCaptureOutcome CaptureParticipants(
        StartWorkflowRequest request,
        string workflowInstanceId,
        long workflowDefinitionVersionId)
    {
        if (request.NotificationInput is null)
        {
            return new StartParticipantCaptureOutcome(null, []);
        }

        var result = participantCaptureService.Capture(
            workflowDefinitionVersionId,
            workflowInstanceId,
            request.NotificationInput);
        return new StartParticipantCaptureOutcome(
            result.ActionNotificationInput,
            result.Errors);
    }

    private StartWorkflowResult BuildParticipantValidationFailed(
        StartWorkflowRequest request,
        string workflowInstanceId,
        IReadOnlyList<string> errors)
    {
        var validationErrors = errors
            .Select(error => new WorkflowValidationError(
                "notification.recipientInputs",
                error))
            .ToArray();
        return new StartWorkflowResult(
            resultBuilder.BuildStartWorkflowValidationFailed(
                request.BusinessObjectType,
                request.BusinessObjectId,
                workflowInstanceId,
                validationErrors),
            ResetCounts: null);
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
            dbContext.WorkflowTaskSlaSchedules
                .Where(schedule => dbContext.WorkflowTasks.Any(task =>
                    task.Id == schedule.WorkflowTaskId
                    && task.WorkflowInstanceId == workflowInstanceId))
                .ExecuteDelete(),
            dbContext.WorkflowTasks
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete(),
            dbContext.WorkflowInstances
                .Where(row => row.WorkflowInstanceId == workflowInstanceId)
                .ExecuteDelete());
    }

    private WorkflowActionResultEnvelope InsertFreshInstance(
        StartWorkflowRequest request,
        string workflowInstanceId,
        long workflowDefinitionVersionId)
    {
        var now = timeProvider.GetUtcNow();
        var instance = new WorkflowInstance
        {
            WorkflowInstanceId = workflowInstanceId,
            WorkflowKey = request.WorkflowKey,
            WorkflowDefinitionVersionId = workflowDefinitionVersionId,
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

    private ActiveWorkflowDefinition ResolveActiveDefinition(string workflowKey)
    {
        var definition = dbContext.WorkflowDefinitionVersions
            .AsNoTracking()
            .Where(row => row.IsActive && row.WorkflowDefinition != null && row.WorkflowDefinition.WorkflowKey == workflowKey)
            .Select(row => new ActiveWorkflowDefinition(
                row.Id,
                row.WorkflowDefinition!.BusinessObjectType))
            .SingleOrDefault();

        return definition ?? throw new InvalidOperationException($"Workflow key '{workflowKey}' does not have an active definition version.");
    }

    private static void Validate(StartWorkflowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowKey))
        {
            throw new ArgumentException("workflowKey is required.", nameof(request));
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

    private static NotificationRecipients? CopyNotificationRecipients(NotificationRecipients? recipients)
    {
        return recipients is null
            ? null
            : new NotificationRecipients
            {
                ToEmailAddresses = recipients.ToEmailAddresses is null
                    ? null!
                    : [.. recipients.ToEmailAddresses],
                CcEmailAddresses = recipients.CcEmailAddresses is null
                    ? null!
                    : [.. recipients.CcEmailAddresses]
            };
    }

    private static bool IsWorkflowInstanceUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("WorkflowInstance", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ActiveWorkflowDefinition(long VersionId, string BusinessObjectType);

    private sealed record StartParticipantCaptureOutcome(
        WorkflowNotificationInput? ActionNotificationInput,
        IReadOnlyList<string> Errors);
}
