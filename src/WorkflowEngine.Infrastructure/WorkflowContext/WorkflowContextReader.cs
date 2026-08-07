using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Infrastructure.WorkflowContext;

public sealed class WorkflowContextReader(
    WorkflowDbContext dbContext,
    TimeProvider timeProvider) : IWorkflowContextReader
{
    public WorkflowContextResponse Read(WorkflowContextReadRequest request)
    {
        var instance = dbContext.WorkflowInstances
            .AsNoTracking()
            .SingleOrDefault(row => row.WorkflowInstanceId == request.WorkflowInstanceId);
        if (instance is null)
        {
            throw new KeyNotFoundException($"Workflow instance '{request.WorkflowInstanceId}' was not found.");
        }

        var definitionVersion = dbContext.WorkflowDefinitionVersions
            .AsNoTracking()
            .Single(row => row.Id == instance.WorkflowDefinitionVersionId);
        var state = dbContext.WorkflowStateDefinitions
            .AsNoTracking()
            .SingleOrDefault(row =>
                row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                && row.StateKey == instance.CurrentStateKey);
        var pendingTask = dbContext.WorkflowTasks
            .AsNoTracking()
            .SingleOrDefault(row =>
                row.WorkflowInstanceId == instance.WorkflowInstanceId
                && row.Status == "Pending");
        var taskDefinition = pendingTask is null
            ? null
            : dbContext.WorkflowTaskDefinitions
                .AsNoTracking()
                .SingleOrDefault(row =>
                    row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                    && row.TaskKey == pendingTask.TaskKey);
        var activeSchedule = pendingTask is null
            ? null
            : dbContext.WorkflowTaskSlaSchedules
                .AsNoTracking()
                .SingleOrDefault(row =>
                    row.WorkflowTaskId == pendingTask.Id
                    && row.Status == "Active");

        var availableActions = BuildAvailableActions(request, instance, definitionVersion.VersionNumber, pendingTask);
        var currentTask = pendingTask is null
            ? null
            : new WorkflowContextTask
            {
                TaskId = pendingTask.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TaskKey = pendingTask.TaskKey,
                TaskLabel = taskDefinition?.DisplayName ?? pendingTask.TaskKey,
                AssignedRole = pendingTask.AssignedRoleKey,
                AssignedUserId = pendingTask.AssignedUserId,
                Status = pendingTask.Status,
                CreatedAtUtc = pendingTask.CreatedAtUtc,
                DueAtUtc = pendingTask.DueAtUtc
            };

        return new WorkflowContextResponse
        {
            WorkflowContext = new global::WorkflowEngine.Application.Contracts.WorkflowContext
            {
                Workflow = new WorkflowContextWorkflow
                {
                    WorkflowKey = instance.WorkflowKey,
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowDefinitionVersion = definitionVersion.VersionNumber,
                    WorkflowInstanceVersion = instance.Version,
                    CurrentState = instance.CurrentStateKey,
                    CurrentStateLabel = state?.DisplayName ?? instance.CurrentStateKey,
                    Status = instance.Status,
                    StartedAtUtc = instance.StartedAtUtc,
                    CompletedAtUtc = instance.CompletedAtUtc
                },
                BusinessObject = new WorkflowContextBusinessObject
                {
                    ObjectType = instance.BusinessObjectType,
                    ObjectId = instance.BusinessObjectId
                },
                ActorAccess = new WorkflowContextActorAccess
                {
                    UserId = request.UserId,
                    DisplayName = request.UserId,
                    EffectiveRoles = request.Roles.ToList(),
                    CanAct = availableActions.Count > 0,
                    ReadOnlyReason = availableActions.Count > 0 ? null : BuildReadOnlyReason(instance.Status, instance.CurrentStateKey, pendingTask, request.Roles)
                },
                CurrentTask = currentTask,
                Sla = BuildSla(pendingTask, activeSchedule),
                AvailableActions = availableActions,
                ServerTimeUtc = timeProvider.GetUtcNow()
            }
        };
    }

    private List<AvailableWorkflowAction> BuildAvailableActions(
        WorkflowContextReadRequest request,
        Persistence.Entities.WorkflowInstance instance,
        int versionNumber,
        Persistence.Entities.WorkflowTask? pendingTask)
    {
        if (string.Equals(instance.Status, "Completed", StringComparison.Ordinal))
        {
            return [];
        }

        var roleSet = request.Roles.ToHashSet(StringComparer.Ordinal);
        var transitions = dbContext.WorkflowTransitionDefinitions
            .AsNoTracking()
            .Where(row =>
                row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                && row.FromStateKey == instance.CurrentStateKey
                && row.IsActive
                && roleSet.Contains(row.AllowedRoleKey))
            .OrderBy(row => row.Id)
            .ToList();
        if (transitions.Count == 0)
        {
            return [];
        }

        var actionKeys = transitions.Select(row => row.ActionKey).ToHashSet(StringComparer.Ordinal);
        var actions = dbContext.WorkflowActionDefinitions
            .AsNoTracking()
            .Where(row =>
                row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                && actionKeys.Contains(row.ActionKey)
                && row.IsActive)
            .ToDictionary(row => row.ActionKey, StringComparer.Ordinal);

        return transitions
            .Where(row => actions.ContainsKey(row.ActionKey))
            .Select(row =>
            {
                var action = actions[row.ActionKey];
                return new AvailableWorkflowAction
                {
                    ActionKey = action.ActionKey,
                    SectionKey = action.SectionKey,
                    ButtonKey = action.ButtonKey,
                    ButtonLabel = action.ButtonLabel,
                    Intent = action.Intent,
                    RequiresReason = action.RequiresReason,
                    RequestDefaults = new WorkflowActionRequestDefaults
                    {
                        WorkflowKey = instance.WorkflowKey,
                        WorkflowInstanceId = instance.WorkflowInstanceId,
                        WorkflowDefinitionVersion = versionNumber,
                        ExpectedCurrentState = instance.CurrentStateKey,
                        ExpectedCurrentTaskId = pendingTask?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ExpectedCurrentTaskKey = pendingTask?.TaskKey,
                        ExpectedInstanceVersion = instance.Version
                    }
                };
            })
            .ToList();
    }

    private WorkflowContextSla BuildSla(
        Persistence.Entities.WorkflowTask? pendingTask,
        Persistence.Entities.WorkflowTaskSlaSchedule? activeSchedule)
    {
        if (pendingTask?.DueAtUtc is null || string.Equals(pendingTask.Status, "Completed", StringComparison.Ordinal))
        {
            return new WorkflowContextSla();
        }

        var now = timeProvider.GetUtcNow();
        if (pendingTask.DueAtUtc <= now)
        {
            return new WorkflowContextSla
            {
                Status = "Overdue",
                DueAtUtc = pendingTask.DueAtUtc,
                Overdue = true,
                OverdueByMinutes = Math.Max(0, (int)Math.Ceiling((now - pendingTask.DueAtUtc.Value).TotalMinutes)),
                CalculationMode = activeSchedule?.CalculationMode,
                TargetDate = activeSchedule?.TargetDate,
                ScheduleVersion = activeSchedule?.ScheduleVersion,
                NextReminderAtUtc = activeSchedule?.NextReminderAtUtc
            };
        }

        return new WorkflowContextSla
        {
            Status = "OnTrack",
            DueAtUtc = pendingTask.DueAtUtc,
            Overdue = false,
            OverdueByMinutes = 0,
            CalculationMode = activeSchedule?.CalculationMode,
            TargetDate = activeSchedule?.TargetDate,
            ScheduleVersion = activeSchedule?.ScheduleVersion,
            NextReminderAtUtc = activeSchedule?.NextReminderAtUtc
        };
    }

    private static string BuildReadOnlyReason(
        string workflowStatus,
        string currentState,
        Persistence.Entities.WorkflowTask? pendingTask,
        IReadOnlyList<string> actorRoles)
    {
        if (string.Equals(workflowStatus, "Completed", StringComparison.Ordinal))
        {
            return "Workflow is completed.";
        }

        if (pendingTask is not null && !actorRoles.Contains(pendingTask.AssignedRoleKey, StringComparer.Ordinal))
        {
            return $"Current task is assigned to {pendingTask.AssignedRoleKey}.";
        }

        if (actorRoles.Count == 0)
        {
            return "You do not have a role on this workflow.";
        }

        return $"Workflow is in {currentState} and you do not have an available workflow action.";
    }
}
