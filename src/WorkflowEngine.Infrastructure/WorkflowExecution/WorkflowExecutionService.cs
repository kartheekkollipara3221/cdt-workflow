using System.Globalization;
using System.Text.Json;
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
    WorkflowActionResultBuilder resultBuilder) : IWorkflowExecutionService
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

        return resultBuilder.BuildExecutionValidatedSuccess(
            normalized,
            request.RequestHash,
            "Persisted validation passed. Workflow transition side effects are deferred to Phase 4 Plan 04.");
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
