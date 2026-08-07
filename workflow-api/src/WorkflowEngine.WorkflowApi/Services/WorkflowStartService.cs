using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Models;
using ApplicationStartWorkflowRequest = WorkflowEngine.Application.WorkflowStart.StartWorkflowRequest;
using ApplicationStartWorkflowService = WorkflowEngine.Application.WorkflowStart.IStartWorkflowService;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowStartService(
    IWorkflowActorResolver actorResolver,
    ApplicationStartWorkflowService startWorkflowService,
    IWorkflowContextReader workflowContextReader,
    IWorkflowActionNotificationDispatcher notificationDispatcher) : IWorkflowStartService
{
    public WorkflowStartApiResponse StartWorkflow(HttpContext httpContext, WorkflowStartRequest? request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (request is null)
        {
            throw new WorkflowApiRequestException(
                StatusCodes.Status400BadRequest,
                "Invalid workflow start request.",
                "Workflow start request body is required.");
        }

        var workflowKey = RequiredText(request.WorkflowKey, "workflowKey");
        var businessObjectType = RequiredText(request.BusinessObjectType, "businessObjectType");
        var businessObjectId = RequiredText(request.BusinessObjectId, "businessObjectId");
        var initialActionKey = OptionalText(request.InitialActionKey);
        var actor = actorResolver.Resolve(httpContext);

        var startResult = startWorkflowService.Start(new ApplicationStartWorkflowRequest(
            workflowKey,
            businessObjectType,
            businessObjectId,
            actor.UserId,
            Reset: false,
            InitialActionKey: initialActionKey,
            StartedByDisplayName: actor.DisplayName,
            StartedByRoles: actor.Roles,
            NotificationContext: NormalizeNotificationContext(request.NotificationContext),
            NotificationRecipients: request.NotificationRecipients,
            SlaInput: request.SlaInput is null
                ? null
                : new WorkflowSlaInput { TargetDate = request.SlaInput.TargetDate },
            IdempotencyKey: OptionalText(request.IdempotencyKey)));

        var actionResult = startResult.Envelope.Result;
        var workflowContext = actionResult.Success
            ? workflowContextReader.Read(new WorkflowContextReadRequest(
                actionResult.WorkflowInstanceId,
                actor.UserId,
                actor.Roles)).WorkflowContext
            : null;

        DispatchInitialActionNotifications(initialActionKey, actionResult);

        return new WorkflowStartApiResponse(new WorkflowStartApiResult(actionResult, workflowContext));
    }

    private void DispatchInitialActionNotifications(
        string? initialActionKey,
        WorkflowActionResult actionResult)
    {
        if (string.IsNullOrWhiteSpace(initialActionKey)
            || !actionResult.Success
            || actionResult.Notification?.Queued != true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(actionResult.WorkflowInstanceId)
            || string.IsNullOrWhiteSpace(actionResult.CorrelationId))
        {
            MarkNotificationDeliveryFailed(
                actionResult,
                "Workflow start initial action notification delivery requires workflow instance id and correlation id.");
            return;
        }

        var dispatchResult = notificationDispatcher.DispatchCreatedNotifications(
            actionResult.WorkflowInstanceId,
            actionResult.CorrelationId);
        if (!dispatchResult.Success)
        {
            MarkNotificationDeliveryFailed(
                actionResult,
                dispatchResult.FailureMessage ?? "Workflow start initial action notification delivery failed.");
        }
    }

    private static void MarkNotificationDeliveryFailed(
        WorkflowActionResult actionResult,
        string message)
    {
        actionResult.Success = false;
        actionResult.ResultCode = "NotificationDeliveryFailed";
        actionResult.Message = message;
    }

    private static string RequiredText(string? value, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new WorkflowApiRequestException(
            StatusCodes.Status400BadRequest,
            "Invalid workflow start request.",
            $"{fieldName} is required.");
    }

    private static Dictionary<string, string?>? NormalizeNotificationContext(Dictionary<string, string?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            normalized[pair.Key.Trim()] = pair.Value!.Trim();
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static string? OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
