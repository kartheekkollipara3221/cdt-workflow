using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.WorkflowContext;

namespace WorkflowEngine.Infrastructure.WorkflowExecution;

public sealed class WorkflowExecutionService(
    DatabaseFirstWorkflowStore store,
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

        var instance = store.GetWorkflowInstance(workflowInstanceId);
        if (instance is null)
        {
            return resultBuilder.BuildWorkflowInstanceNotFound(normalized, request.RequestHash, workflowInstanceId);
        }

        var identityValidation = ValidateEnvelopeIdentity(normalized, instance);
        if (identityValidation is not null)
        {
            return identityValidation;
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

        var actionDefinition = store.GetActionDefinition(instance.WorkflowDefinitionVersionId, actionKey);
        if (actionDefinition is null)
        {
            return resultBuilder.BuildInvalidTransition(
                normalized,
                request.RequestHash,
                $"Action '{actionKey}' is not defined for workflow definition version {versionNumber}.");
        }

        var actionEchoValidation = ValidateActionEcho(normalized, actionDefinition);
        if (actionEchoValidation is not null)
        {
            return actionEchoValidation;
        }

        var transitions = store
            .GetTransitionsForAction(instance.WorkflowDefinitionVersionId, instance.CurrentStateKey, actionKey)
            .ToList();
        if (transitions.Count == 0)
        {
            return resultBuilder.BuildInvalidTransition(
                normalized,
                request.RequestHash,
                $"No transition exists for state '{instance.CurrentStateKey}' with action '{actionKey}'.");
        }

        var pendingTask = store.GetPendingTask(instance.WorkflowInstanceId);

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

        var transition = request.AdminOverride
            ? transitions[0]
            : matchingTransitions[0];
        foreach (var warning in BuildOptionWarnings(envelope.Workflow?.Options, transition, instance.WorkflowDefinitionVersionId))
        {
            Console.Error.WriteLine(warning);
        }

        return ExecuteSideEffects(request, instance, pendingTask, transition, performedByRoleKey);
    }

    private WorkflowActionResultEnvelope? ValidateEnvelopeIdentity(
        NormalizedWorkflowAction normalized,
        WorkflowInstance instance)
    {
        var envelope = normalized.Envelope;
        var errors = new List<WorkflowValidationError>();
        AddMismatch(
            errors,
            "workflow.workflowKey",
            envelope.Workflow?.WorkflowKey,
            instance.WorkflowKey);
        AddMismatch(
            errors,
            "businessObject.objectType",
            envelope.BusinessObject?.ObjectType,
            instance.BusinessObjectType);
        AddMismatch(
            errors,
            "businessObject.objectId",
            envelope.BusinessObject?.ObjectId,
            instance.BusinessObjectId);

        return errors.Count == 0
            ? null
            : resultBuilder.BuildValidationFailed(normalized, errors);
    }

    private WorkflowActionResultEnvelope? ValidateActionEcho(
        NormalizedWorkflowAction normalized,
        WorkflowActionDefinition actionDefinition)
    {
        var envelope = normalized.Envelope;
        var errors = new List<WorkflowValidationError>();
        var uiSectionKey = envelope.UiAction?.SectionKey;
        var dataSectionKey = envelope.DataSave?.SectionKey;
        if (!string.Equals(uiSectionKey, dataSectionKey, StringComparison.Ordinal))
        {
            errors.Add(new WorkflowValidationError(
                "dataSave.sectionKey",
                $"dataSave.sectionKey must match uiAction.sectionKey '{uiSectionKey}'."));
        }

        AddMismatch(errors, "uiAction.sectionKey", uiSectionKey, actionDefinition.SectionKey);
        AddMismatch(errors, "dataSave.sectionKey", dataSectionKey, actionDefinition.SectionKey);
        AddMismatch(errors, "uiAction.buttonKey", envelope.UiAction?.ButtonKey, actionDefinition.ButtonKey);

        return errors.Count == 0
            ? null
            : resultBuilder.BuildValidationFailed(normalized, errors);
    }

    private static void AddMismatch(
        ICollection<WorkflowValidationError> errors,
        string field,
        string? actual,
        string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected)
            || string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(new WorkflowValidationError(
            field,
            $"{field} must match persisted workflow metadata value '{expected}'."));
    }

    private IReadOnlyList<string> BuildOptionWarnings(
        WorkflowActionOptions? options,
        WorkflowTransitionDefinition transition,
        long workflowDefinitionVersionId)
    {
        if (options is null)
        {
            return [];
        }

        var warnings = new List<string>();
        AddWarningIfConflict(warnings, "closeCurrentTask", options.CloseCurrentTask, !string.IsNullOrWhiteSpace(transition.TaskToCloseKey));
        AddWarningIfConflict(warnings, "createNextTask", options.CreateNextTask, !string.IsNullOrWhiteSpace(transition.TaskToCreateKey));
        AddWarningIfConflict(warnings, "queueNotifications", options.QueueNotifications, !string.IsNullOrWhiteSpace(transition.NotificationRouteKey));
        AddWarningIfConflict(warnings, "evaluateSla", options.EvaluateSla, TransitionCreatesTaskWithSla(transition, workflowDefinitionVersionId));
        AddWarningIfConflict(warnings, "writeAudit", options.WriteAudit, metadataValue: true);
        return warnings;
    }

    private bool TransitionCreatesTaskWithSla(WorkflowTransitionDefinition transition, long workflowDefinitionVersionId)
    {
        if (string.IsNullOrWhiteSpace(transition.TaskToCreateKey))
        {
            return false;
        }

        return store.TaskDefinitionHasSla(workflowDefinitionVersionId, transition.TaskToCreateKey);
    }

    private static void AddWarningIfConflict(List<string> warnings, string fieldName, bool? optionValue, bool metadataValue)
    {
        if (optionValue is null || optionValue.Value == metadataValue)
        {
            return;
        }

        warnings.Add(
            string.Create(
                CultureInfo.InvariantCulture,
                $"WARN: workflow.options.{fieldName} = {optionValue.Value.ToString().ToLowerInvariant()} conflicts with metadata; using metadata"));
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

        using var connection = store.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var instance = store.GetWorkflowInstance(connection, transaction, loadedInstance.WorkflowInstanceId)
            ?? throw new InvalidOperationException($"Workflow instance '{loadedInstance.WorkflowInstanceId}' disappeared during execution.");
        var currentPendingTask = store.GetPendingTask(connection, transaction, instance.WorkflowInstanceId);
        var currentConcurrency = CheckConcurrency(request, instance, currentPendingTask)
            ?? CheckTransitionStillCurrent(request, instance, currentPendingTask, transition);
        if (currentConcurrency is not null)
        {
            transaction.Rollback();
            return TryBuildIdempotencyReplay(request) ?? currentConcurrency;
        }

        var oldState = instance.CurrentStateKey;

        store.InsertMockSectionData(
            connection,
            transaction,
            envelope.BusinessObject?.ObjectType ?? string.Empty,
            envelope.BusinessObject?.ObjectId ?? string.Empty,
            envelope.DataSave?.SectionKey ?? string.Empty,
            envelope.DataSave?.Payload.GetRawText() ?? "{}",
            envelope.Actor?.UserId ?? string.Empty,
            now,
            normalized.EffectiveCorrelationId);

        var isSaveDraft = string.Equals(workflow.ActionKey, "SaveDraft", StringComparison.Ordinal);
        if (!isSaveDraft)
        {
            var completed = string.Equals(transition.ToStateKey, "Completed", StringComparison.Ordinal);
            var updatedRows = store.UpdateWorkflowInstanceState(
                connection,
                transaction,
                instance.WorkflowInstanceId,
                instance.Version,
                transition.ToStateKey,
                completed ? "Completed" : instance.Status,
                completed ? now : instance.CompletedAtUtc);
            if (updatedRows == 0)
            {
                transaction.Rollback();
                var concurrencyConflict = resultBuilder.BuildConcurrencyConflict(
                    normalized,
                    request.RequestHash,
                    "expectedInstanceVersion",
                    instance.Version.ToString(CultureInfo.InvariantCulture),
                    ResolveCurrentInstanceVersion(instance.WorkflowInstanceId).ToString(CultureInfo.InvariantCulture),
                    $"Expected workflow instance version {instance.Version} but the workflow changed before the state update could be committed.",
                    ReadContextOrNull(instance.WorkflowInstanceId, envelope));
                return TryBuildIdempotencyReplay(request) ?? concurrencyConflict;
            }

            if (!string.IsNullOrWhiteSpace(transition.TaskToCloseKey))
            {
                var taskToClose = currentPendingTask
                    ?? throw new InvalidOperationException($"Pending task '{transition.TaskToCloseKey}' was expected but not found.");
                store.CompleteWorkflowTask(
                    connection,
                    transaction,
                    instance.WorkflowInstanceId,
                    transition.TaskToCloseKey,
                    now,
                    envelope.Actor?.UserId ?? string.Empty,
                    workflow.ActionKey ?? string.Empty);
                closedTask = new WorkflowTaskChange
                {
                    TaskId = taskToClose.Id.ToString(CultureInfo.InvariantCulture),
                    TaskKey = taskToClose.TaskKey,
                    AssignedRole = taskToClose.AssignedRoleKey,
                    DueAtUtc = taskToClose.DueAtUtc
                };
            }

            if (!string.IsNullOrWhiteSpace(transition.TaskToCreateKey))
            {
                var taskDefinition = store.GetTaskDefinition(
                    connection,
                    transaction,
                    instance.WorkflowDefinitionVersionId,
                    transition.TaskToCreateKey);
                var dueAtUtc = store.ResolveDueAtUtc(connection, transaction, instance.WorkflowDefinitionVersionId, taskDefinition.SlaPolicyKey, now);
                var created = store.InsertWorkflowTask(
                    connection,
                    transaction,
                    instance.WorkflowInstanceId,
                    taskDefinition.TaskKey,
                    taskDefinition.OwnerRoleKey,
                    assignedUserId: null,
                    "Pending",
                    now,
                    dueAtUtc);
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
                var route = store.GetNotificationRoute(connection, transaction, instance.WorkflowDefinitionVersionId, transition.NotificationRouteKey);
                store.InsertNotificationOutbox(
                    connection,
                    transaction,
                    instance.WorkflowInstanceId,
                    createdTask is null ? null : long.Parse(createdTask.TaskId!, CultureInfo.InvariantCulture),
                    envelope.BusinessObject?.ObjectType ?? string.Empty,
                    envelope.BusinessObject?.ObjectId ?? string.Empty,
                    route.NotificationType,
                    route.RecipientRoleKey,
                    recipientUserId: null,
                    route.SubjectTemplate,
                    JsonSerializer.Serialize(new
                    {
                        workflowInstanceId = instance.WorkflowInstanceId,
                        actionKey = workflow.ActionKey,
                        oldState,
                        newState = transition.ToStateKey,
                        taskKey = createdTask?.TaskKey
                    }),
                    "Pending",
                    normalized.EffectiveCorrelationId,
                    now);
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

        store.InsertWorkflowAudit(
            connection,
            transaction,
            instance.WorkflowInstanceId,
            envelope.BusinessObject?.ObjectType ?? string.Empty,
            envelope.BusinessObject?.ObjectId ?? string.Empty,
            workflow.ActionKey ?? string.Empty,
            oldState,
            isSaveDraft ? oldState : transition.ToStateKey,
            envelope.Actor?.UserId ?? string.Empty,
            performedByRoleKey,
            reason,
            normalized.EffectiveCorrelationId,
            now);

        var result = resultBuilder.BuildExecutionSuccess(
            normalized,
            request.RequestHash,
            oldState,
            isSaveDraft ? oldState : transition.ToStateKey,
            closedTask,
            createdTask,
            notification ?? new WorkflowNotificationSummary { Queued = false },
            new WorkflowAuditSummary { Written = true, ActionKey = workflow.ActionKey });
        result.WorkflowContext = ReadContextOrNull(connection, transaction, instance.WorkflowInstanceId, envelope);

        try
        {
            AddIdempotencyRecord(connection, transaction, result, request, now);
            transaction.Commit();
        }
        catch (SqlException exception) when (IsIdempotencyUniqueViolation(exception))
        {
            transaction.Rollback();
            var existing = TryBuildIdempotencyReplay(request);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
        return result;
    }

    private void AddIdempotencyRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkflowActionResultEnvelope result,
        WorkflowExecutionRequest request,
        DateTimeOffset createdAtUtc)
    {
        result.Result.Diagnostics.ResultHash = resultSerializer.ComputeResultHash(result);
        var resultJson = resultSerializer.Serialize(result);

        store.InsertIdempotencyRecord(
            connection,
            transaction,
            request.Normalized.EffectiveIdempotencyKey,
            request.Normalized.Envelope.Workflow?.WorkflowInstanceId ?? string.Empty,
            request.Normalized.Envelope.Workflow?.ActionKey ?? string.Empty,
            request.RequestHash,
            result.Result.Diagnostics.ResultHash,
            resultJson,
            createdAtUtc);
    }

    private static bool IsIdempotencyUniqueViolation(SqlException exception)
    {
        return exception.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains("IdempotencyRecord", StringComparison.OrdinalIgnoreCase));
    }

    private WorkflowActionResultEnvelope? TryBuildIdempotencyReplay(WorkflowExecutionRequest request)
    {
        var record = store.GetIdempotencyRecord(request.Normalized.EffectiveIdempotencyKey);
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

    private WorkflowActionResultEnvelope? CheckTransitionStillCurrent(
        WorkflowExecutionRequest request,
        WorkflowInstance instance,
        WorkflowTask? pendingTask,
        WorkflowTransitionDefinition transition)
    {
        if (!string.Equals(instance.CurrentStateKey, transition.FromStateKey, StringComparison.Ordinal))
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedCurrentState",
                transition.FromStateKey,
                instance.CurrentStateKey,
                $"Expected current state '{transition.FromStateKey}' but persisted state is '{instance.CurrentStateKey}'. The workflow has moved since this action was resolved.",
                ReadContextOrNull(instance.WorkflowInstanceId, request.Normalized.Envelope));
        }

        if (!string.IsNullOrWhiteSpace(transition.TaskToCloseKey)
            && !string.Equals(pendingTask?.TaskKey, transition.TaskToCloseKey, StringComparison.Ordinal))
        {
            return resultBuilder.BuildConcurrencyConflict(
                request.Normalized,
                request.RequestHash,
                "expectedCurrentTaskKey",
                transition.TaskToCloseKey,
                pendingTask?.TaskKey ?? string.Empty,
                $"Expected current task key '{transition.TaskToCloseKey}' but the open task key is '{pendingTask?.TaskKey ?? string.Empty}'.",
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

    private Application.Contracts.WorkflowContext? ReadContextOrNull(
        SqlConnection connection,
        SqlTransaction transaction,
        string workflowInstanceId,
        WorkflowActionEnvelope envelope)
    {
        var request = new WorkflowContextReadRequest(
            workflowInstanceId,
            envelope.Actor?.UserId ?? string.Empty,
            envelope.Actor?.Roles ?? []);
        var data = store.GetWorkflowContextData(connection, transaction, workflowInstanceId, request.Roles);
        return data is null
            ? null
            : WorkflowContextReader.BuildResponse(data, request, timeProvider.GetUtcNow()).WorkflowContext;
    }

    private int ResolveVersionNumber(long workflowDefinitionVersionId)
    {
        return store.GetDefinitionVersion(workflowDefinitionVersionId)?.VersionNumber
            ?? throw new InvalidOperationException($"Workflow definition version id {workflowDefinitionVersionId} was not found.");
    }

    private int ResolveCurrentInstanceVersion(string workflowInstanceId)
    {
        return store.GetWorkflowInstance(workflowInstanceId)?.Version
            ?? throw new InvalidOperationException($"Workflow instance '{workflowInstanceId}' was not found.");
    }
}
