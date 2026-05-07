using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.WorkflowExecution;

public sealed class WorkflowExecutionService(
    WorkflowDbContext dbContext,
    IWorkflowContextReader contextReader,
    TimeProvider timeProvider,
    WorkflowActionResultBuilder resultBuilder,
    CanonicalWorkflowResultSerializer resultSerializer) : IWorkflowExecutionService
{
    public WorkflowActionResultEnvelope Execute(WorkflowExecutionRequest request)
    {
        var normalized = request.Normalized;
        var envelope = normalized.Envelope;
        var workflow = envelope.Workflow ?? throw new InvalidOperationException("Validated envelope is missing workflow.");
        var actionKey = workflow.ActionKey ?? string.Empty;
        var workflowInstanceId = workflow.WorkflowInstanceId ?? string.Empty;

        var replay = TryBuildIdempotencyReplay(request);
        if (replay is not null)
        {
            return replay;
        }

        var instance = dbContext.WorkflowInstances
            .AsNoTracking()
            .SingleOrDefault(row => row.WorkflowInstanceId == workflowInstanceId);
        if (instance is null)
        {
            return resultBuilder.BuildWorkflowInstanceNotFound(normalized, request.RequestHash, workflowInstanceId);
        }

        var versionNumber = ResolveVersionNumber(instance.WorkflowDefinitionVersionId);
        if (workflow.WorkflowDefinitionVersion is not null && workflow.WorkflowDefinitionVersion.Value != versionNumber)
        {
            return resultBuilder.BuildWorkflowDefinitionVersionMismatch(
                normalized,
                request.RequestHash,
                workflow.WorkflowDefinitionVersion.Value,
                versionNumber,
                ReadContextOrNull(instance.WorkflowInstanceId, envelope));
        }

        if (string.Equals(instance.Status, "Completed", StringComparison.Ordinal))
        {
            return resultBuilder.BuildInvalidTransition(
                normalized,
                request.RequestHash,
                "Workflow is Completed; no actions are permitted.");
        }

        var actionExists = dbContext.WorkflowActionDefinitions
            .AsNoTracking()
            .Any(row =>
                row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                && row.ActionKey == actionKey
                && row.IsActive);
        if (!actionExists)
        {
            return resultBuilder.BuildInvalidTransition(
                normalized,
                request.RequestHash,
                $"Action '{actionKey}' is not defined for workflow definition version {versionNumber}.");
        }

        var transitions = dbContext.WorkflowTransitionDefinitions
            .AsNoTracking()
            .Where(row =>
                row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                && row.FromStateKey == instance.CurrentStateKey
                && row.ActionKey == actionKey
                && row.IsActive)
            .ToList();
        if (transitions.Count == 0)
        {
            return resultBuilder.BuildInvalidTransition(
                normalized,
                request.RequestHash,
                $"No transition exists for state '{instance.CurrentStateKey}' with action '{actionKey}'.");
        }

        var pendingTask = dbContext.WorkflowTasks
            .AsNoTracking()
            .OrderBy(row => row.Id)
            .FirstOrDefault(row => row.WorkflowInstanceId == instance.WorkflowInstanceId && row.Status == "Pending");

        var concurrency = CheckConcurrency(request, instance, pendingTask);
        if (concurrency is not null)
        {
            return concurrency;
        }

        var actorRoles = envelope.Actor?.Roles?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        var matchingTransitions = transitions
            .Where(row => actorRoles.Contains(row.AllowedRoleKey, StringComparer.Ordinal))
            .ToList();
        var matchingRoles = matchingTransitions
            .Select(row => row.AllowedRoleKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var performedByRoleKey = matchingRoles.Length == 1
            ? matchingRoles[0]
            : transitions[0].AllowedRoleKey;

        if (!request.AdminOverride)
        {
            if (matchingRoles.Length == 0)
            {
                return resultBuilder.BuildForbidden(
                    normalized,
                    request.RequestHash,
                    string.Join(", ", actorRoles),
                    actionKey,
                    instance.CurrentStateKey);
            }

            if (matchingRoles.Length > 1)
            {
                return resultBuilder.BuildAmbiguousRoleMatch(
                    normalized,
                    request.RequestHash,
                    string.Join(", ", matchingRoles),
                    actionKey,
                    instance.CurrentStateKey);
            }
        }
        else
        {
            performedByRoleKey = "AdminDemoOverride";
        }

        return ExecuteSideEffects(request, instance, pendingTask, transitions[0], performedByRoleKey);
    }

    private WorkflowActionResultEnvelope ExecuteSideEffects(
        WorkflowExecutionRequest request,
        WorkflowInstance loadedInstance,
        WorkflowTask? pendingTask,
        WorkflowTransitionDefinition transition,
        string performedByRoleKey)
    {
        var normalized = request.Normalized;
        var envelope = normalized.Envelope;
        var workflow = envelope.Workflow!;
        var now = timeProvider.GetUtcNow();
        WorkflowTaskChange? closedTask = null;
        WorkflowTaskChange? createdTask = null;
        WorkflowNotificationSummary? notification = null;

        using (var transaction = dbContext.Database.BeginTransaction())
        {
            var instance = dbContext.WorkflowInstances.Single(row => row.Id == loadedInstance.Id);
            var oldState = instance.CurrentStateKey;

            dbContext.MockSectionData.Add(new MockSectionData
            {
                BusinessObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                BusinessObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty,
                SectionKey = envelope.DataSave?.SectionKey ?? string.Empty,
                PayloadJson = envelope.DataSave?.Payload.GetRawText() ?? "{}",
                SavedByUserId = envelope.Actor?.UserId ?? string.Empty,
                SavedAtUtc = now,
                CorrelationId = normalized.EffectiveCorrelationId
            });

            var isSaveDraft = string.Equals(workflow.ActionKey, "SaveDraft", StringComparison.Ordinal);
            if (!isSaveDraft)
            {
                if (!string.IsNullOrWhiteSpace(transition.TaskToCloseKey))
                {
                    var taskToClose = dbContext.WorkflowTasks.Single(row =>
                        row.WorkflowInstanceId == instance.WorkflowInstanceId
                        && row.TaskKey == transition.TaskToCloseKey
                        && row.Status == "Pending");
                    taskToClose.Status = "Completed";
                    taskToClose.CompletedAtUtc = now;
                    taskToClose.CompletedByUserId = envelope.Actor?.UserId ?? string.Empty;
                    taskToClose.ClosedByActionKey = workflow.ActionKey;
                    closedTask = new WorkflowTaskChange
                    {
                        TaskId = taskToClose.Id.ToString(CultureInfo.InvariantCulture),
                        TaskKey = taskToClose.TaskKey,
                        AssignedRole = taskToClose.AssignedRoleKey,
                        DueAtUtc = taskToClose.DueAtUtc
                    };
                }

                instance.CurrentStateKey = transition.ToStateKey;
                instance.Version++;
                if (string.Equals(transition.ToStateKey, "Completed", StringComparison.Ordinal))
                {
                    instance.Status = "Completed";
                    instance.CompletedAtUtc = now;
                }

                if (!string.IsNullOrWhiteSpace(transition.TaskToCreateKey))
                {
                    var taskDefinition = dbContext.WorkflowTaskDefinitions
                        .AsNoTracking()
                        .Single(row =>
                            row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                            && row.TaskKey == transition.TaskToCreateKey);
                    var dueAtUtc = ResolveDueAtUtc(instance.WorkflowDefinitionVersionId, taskDefinition.SlaPolicyKey, now);
                    var created = new WorkflowTask
                    {
                        WorkflowInstanceId = instance.WorkflowInstanceId,
                        TaskKey = taskDefinition.TaskKey,
                        AssignedRoleKey = taskDefinition.OwnerRoleKey,
                        AssignedUserId = null,
                        Status = "Pending",
                        CreatedAtUtc = now,
                        DueAtUtc = dueAtUtc
                    };
                    dbContext.WorkflowTasks.Add(created);
                    dbContext.SaveChanges();
                    createdTask = new WorkflowTaskChange
                    {
                        TaskId = created.Id.ToString(CultureInfo.InvariantCulture),
                        TaskKey = created.TaskKey,
                        AssignedRole = created.AssignedRoleKey,
                        DueAtUtc = created.DueAtUtc
                    };
                }

                if (!string.IsNullOrWhiteSpace(transition.NotificationRouteKey))
                {
                    var route = dbContext.NotificationRoutes.AsNoTracking().Single(row =>
                        row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                        && row.RouteKey == transition.NotificationRouteKey
                        && row.IsActive);
                    var outbox = new NotificationOutbox
                    {
                        WorkflowInstanceId = instance.WorkflowInstanceId,
                        WorkflowTaskId = createdTask is null ? null : long.Parse(createdTask.TaskId!, CultureInfo.InvariantCulture),
                        BusinessObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                        BusinessObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty,
                        NotificationType = route.NotificationType,
                        RecipientRoleKey = route.RecipientRoleKey,
                        RecipientUserId = null,
                        Subject = route.SubjectTemplate,
                        PayloadJson = JsonSerializer.Serialize(new
                        {
                            workflowInstanceId = instance.WorkflowInstanceId,
                            actionKey = workflow.ActionKey,
                            oldState,
                            newState = transition.ToStateKey,
                            taskKey = createdTask?.TaskKey
                        }),
                        Status = "Pending",
                        CorrelationId = normalized.EffectiveCorrelationId,
                        CreatedAtUtc = now
                    };
                    dbContext.NotificationOutbox.Add(outbox);
                    notification = new WorkflowNotificationSummary
                    {
                        Queued = true,
                        NotificationType = route.NotificationType,
                        RecipientRole = route.RecipientRoleKey
                    };
                }
            }

            var reason = workflow.ActionReason;
            if (request.AdminOverride)
            {
                reason = string.IsNullOrWhiteSpace(reason)
                    ? "[ADMIN OVERRIDE]"
                    : "[ADMIN OVERRIDE] " + reason;
            }

            dbContext.WorkflowAudits.Add(new WorkflowAudit
            {
                WorkflowInstanceId = instance.WorkflowInstanceId,
                BusinessObjectType = envelope.BusinessObject?.ObjectType ?? string.Empty,
                BusinessObjectId = envelope.BusinessObject?.ObjectId ?? string.Empty,
                ActionKey = workflow.ActionKey ?? string.Empty,
                FromStateKey = oldState,
                ToStateKey = isSaveDraft ? oldState : transition.ToStateKey,
                PerformedByUserId = envelope.Actor?.UserId ?? string.Empty,
                PerformedByRoleKey = performedByRoleKey,
                Reason = reason,
                CorrelationId = normalized.EffectiveCorrelationId,
                CreatedAtUtc = now
            });

            dbContext.SaveChanges();
            transaction.Commit();

            var result = resultBuilder.BuildExecutionSuccess(
                normalized,
                request.RequestHash,
                oldState,
                isSaveDraft ? oldState : transition.ToStateKey,
                closedTask,
                createdTask,
                notification ?? new WorkflowNotificationSummary { Queued = false },
                new WorkflowAuditSummary { Written = true, ActionKey = workflow.ActionKey });
            result.WorkflowContext = ReadContextOrNull(instance.WorkflowInstanceId, envelope);
            PersistIdempotencyRecord(result, request, now);
            return result;
        }
    }

    private DateTimeOffset? ResolveDueAtUtc(long workflowDefinitionVersionId, string? slaPolicyKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(slaPolicyKey))
        {
            return null;
        }

        var durationMinutes = dbContext.SlaPolicies.AsNoTracking()
            .Where(row =>
                row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
                && row.SlaPolicyKey == slaPolicyKey
                && row.IsActive)
            .Select(row => (int?)row.DurationMinutes)
            .SingleOrDefault();

        return durationMinutes is null ? null : now.AddMinutes(durationMinutes.Value);
    }

    private void PersistIdempotencyRecord(
        WorkflowActionResultEnvelope result,
        WorkflowExecutionRequest request,
        DateTimeOffset createdAtUtc)
    {
        result.Result.Diagnostics.ResultHash = resultSerializer.ComputeResultHash(result);
        var resultJson = resultSerializer.Serialize(result);

        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            IdempotencyKey = request.Normalized.EffectiveIdempotencyKey,
            WorkflowInstanceId = request.Normalized.Envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
            ActionKey = request.Normalized.Envelope.Workflow?.ActionKey ?? string.Empty,
            RequestHash = request.RequestHash,
            ResultHash = result.Result.Diagnostics.ResultHash,
            ResultJson = resultJson,
            CreatedAtUtc = createdAtUtc
        });

        try
        {
            dbContext.SaveChanges();
        }
        catch (DbUpdateException exception) when (IsIdempotencyUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = TryBuildIdempotencyReplay(request);
            if (existing is null)
            {
                throw;
            }

            result.Result.Success = existing.Result.Success;
            result.Result.ResultCode = existing.Result.ResultCode;
            result.Result.Message = existing.Result.Message;
            result.WorkflowContext = existing.WorkflowContext;
        }
    }

    private static bool IsIdempotencyUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("IdempotencyRecord", StringComparison.OrdinalIgnoreCase));
    }

    private WorkflowActionResultEnvelope? TryBuildIdempotencyReplay(WorkflowExecutionRequest request)
    {
        var record = dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefault(row => row.IdempotencyKey == request.Normalized.EffectiveIdempotencyKey);
        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, request.RequestHash, StringComparison.Ordinal))
        {
            return resultBuilder.BuildIdempotencyConflict(
                request.Normalized,
                request.RequestHash,
                record.RequestHash,
                record.ActionKey);
        }

        var replay = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(record.ResultJson, WorkflowJson.Options)
            ?? throw new InvalidOperationException($"Stored idempotency result for key '{record.IdempotencyKey}' could not be deserialized.");
        replay.Result.Diagnostics.Replay = true;
        replay.Result.Diagnostics.OriginalCreatedAtUtc = record.CreatedAtUtc;
        replay.Result.Diagnostics.RequestHash = record.RequestHash;
        replay.Result.Diagnostics.ResultHash = record.ResultHash;
        replay.Result.RequestHash = record.RequestHash;
        return replay;
    }

    private WorkflowActionResultEnvelope? CheckConcurrency(
        WorkflowExecutionRequest request,
        WorkflowInstance instance,
        WorkflowTask? pendingTask)
    {
        var workflow = request.Normalized.Envelope.Workflow!;
        if (!string.IsNullOrWhiteSpace(workflow.ExpectedCurrentState)
            && !string.Equals(workflow.ExpectedCurrentState, instance.CurrentStateKey, StringComparison.Ordinal))
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedCurrentState",
                workflow.ExpectedCurrentState,
                instance.CurrentStateKey,
                $"Expected current state '{workflow.ExpectedCurrentState}' but persisted state is '{instance.CurrentStateKey}'. The workflow has moved since you opened the form.",
                ReadContextOrNull(instance.WorkflowInstanceId, request.Normalized.Envelope));
        }

        var actualTaskId = pendingTask?.Id.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(workflow.ExpectedCurrentTaskId)
            && !string.Equals(workflow.ExpectedCurrentTaskId, actualTaskId, StringComparison.Ordinal))
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedCurrentTaskId",
                workflow.ExpectedCurrentTaskId,
                actualTaskId,
                $"Expected current task id '{workflow.ExpectedCurrentTaskId}' but the open task is '{actualTaskId}'.",
                ReadContextOrNull(instance.WorkflowInstanceId, request.Normalized.Envelope));
        }

        var actualTaskKey = pendingTask?.TaskKey ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(workflow.ExpectedCurrentTaskKey)
            && !string.Equals(workflow.ExpectedCurrentTaskKey, actualTaskKey, StringComparison.Ordinal))
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedCurrentTaskKey",
                workflow.ExpectedCurrentTaskKey,
                actualTaskKey,
                $"Expected current task key '{workflow.ExpectedCurrentTaskKey}' but the open task key is '{actualTaskKey}'.",
                ReadContextOrNull(instance.WorkflowInstanceId, request.Normalized.Envelope));
        }

        if (workflow.ExpectedInstanceVersion is not null && workflow.ExpectedInstanceVersion.Value != instance.Version)
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedInstanceVersion",
                workflow.ExpectedInstanceVersion.Value.ToString(CultureInfo.InvariantCulture),
                instance.Version.ToString(CultureInfo.InvariantCulture),
                $"Expected workflow instance version {workflow.ExpectedInstanceVersion.Value} but persisted version is {instance.Version}. The workflow has moved since you opened the form.",
                ReadContextOrNull(instance.WorkflowInstanceId, request.Normalized.Envelope));
        }

        return null;
    }

    private Application.Contracts.WorkflowContext? ReadContextOrNull(string workflowInstanceId, WorkflowActionEnvelope envelope)
    {
        try
        {
            return contextReader.Read(new WorkflowContextReadRequest(
                workflowInstanceId,
                envelope.Actor?.UserId ?? string.Empty,
                envelope.Actor?.Roles ?? [])).WorkflowContext;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private int ResolveVersionNumber(long workflowDefinitionVersionId)
    {
        return dbContext.WorkflowDefinitionVersions
            .AsNoTracking()
            .Where(row => row.Id == workflowDefinitionVersionId)
            .Select(row => row.VersionNumber)
            .Single();
    }
}
