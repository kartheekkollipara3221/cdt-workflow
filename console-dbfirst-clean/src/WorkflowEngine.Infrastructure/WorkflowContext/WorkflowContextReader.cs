using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.WorkflowContext;

public sealed class WorkflowContextReader(
    DatabaseFirstWorkflowStore store,
    TimeProvider timeProvider) : IWorkflowContextReader
{
    public WorkflowContextResponse Read(WorkflowContextReadRequest request)
    {
        var data = store.GetWorkflowContextData(request.WorkflowInstanceId, request.Roles);
        if (data is null)
        {
            throw new KeyNotFoundException($"Workflow instance '{request.WorkflowInstanceId}' was not found.");
        }

        return BuildResponse(data, request, timeProvider.GetUtcNow());
    }

    public static WorkflowContextResponse BuildResponse(
        WorkflowContextData data,
        WorkflowContextReadRequest request,
        DateTimeOffset serverTimeUtc)
    {
        var instance = data.Instance;
        var availableActions = BuildAvailableActions(data);
        var currentTask = data.PendingTask is null
            ? null
            : new WorkflowContextTask
            {
                TaskId = data.PendingTask.Task.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TaskKey = data.PendingTask.Task.TaskKey,
                TaskLabel = data.PendingTask.TaskLabel,
                AssignedRole = data.PendingTask.Task.AssignedRoleKey,
                AssignedUserId = data.PendingTask.Task.AssignedUserId,
                Status = data.PendingTask.Task.Status,
                CreatedAtUtc = data.PendingTask.Task.CreatedAtUtc,
                DueAtUtc = data.PendingTask.Task.DueAtUtc
            };

        return new WorkflowContextResponse
        {
            WorkflowContext = new global::WorkflowEngine.Application.Contracts.WorkflowContext
            {
                Workflow = new WorkflowContextWorkflow
                {
                    WorkflowKey = instance.WorkflowKey,
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowDefinitionVersion = data.WorkflowDefinitionVersionNumber,
                    WorkflowInstanceVersion = instance.Version,
                    CurrentState = instance.CurrentStateKey,
                    CurrentStateLabel = data.CurrentStateLabel,
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
                    ReadOnlyReason = availableActions.Count > 0 ? null : BuildReadOnlyReason(instance.Status, instance.CurrentStateKey, data.PendingTask?.Task, request.Roles)
                },
                CurrentTask = currentTask,
                Sla = BuildSla(data.PendingTask?.Task, serverTimeUtc),
                AvailableActions = availableActions,
                ServerTimeUtc = serverTimeUtc
            }
        };
    }

    private static List<AvailableWorkflowAction> BuildAvailableActions(WorkflowContextData data)
    {
        return data.AvailableActions
            .Select(action =>
            {
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
                        WorkflowKey = data.Instance.WorkflowKey,
                        WorkflowInstanceId = data.Instance.WorkflowInstanceId,
                        WorkflowDefinitionVersion = data.WorkflowDefinitionVersionNumber,
                        ExpectedCurrentState = data.Instance.CurrentStateKey,
                        ExpectedCurrentTaskId = data.PendingTask?.Task.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ExpectedCurrentTaskKey = data.PendingTask?.Task.TaskKey,
                        ExpectedInstanceVersion = data.Instance.Version
                    }
                };
            })
            .ToList();
    }

    private static WorkflowContextSla BuildSla(WorkflowTask? pendingTask, DateTimeOffset now)
    {
        if (pendingTask?.DueAtUtc is null || string.Equals(pendingTask.Status, "Completed", StringComparison.Ordinal))
        {
            return new WorkflowContextSla();
        }

        if (pendingTask.DueAtUtc <= now)
        {
            return new WorkflowContextSla
            {
                Status = "Overdue",
                DueAtUtc = pendingTask.DueAtUtc,
                Overdue = true,
                OverdueByMinutes = Math.Max(0, (int)Math.Ceiling((now - pendingTask.DueAtUtc.Value).TotalMinutes))
            };
        }

        return new WorkflowContextSla
        {
            Status = "OnTrack",
            DueAtUtc = pendingTask.DueAtUtc,
            Overdue = false,
            OverdueByMinutes = 0
        };
    }

    private static string BuildReadOnlyReason(
        string workflowStatus,
        string currentState,
        WorkflowTask? pendingTask,
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
