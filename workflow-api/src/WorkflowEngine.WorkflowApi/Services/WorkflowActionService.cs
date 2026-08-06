using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActionService(
    IWorkflowActorResolver actorResolver,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    IWorkflowExecutionService workflowExecutionService,
    IWorkflowActionNotificationDispatcher notificationDispatcher) : IWorkflowActionService
{
    public WorkflowActionApiResponse ExecuteWorkflowAction(HttpContext httpContext, WorkflowActionRequest? request)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (request is null)
        {
            throw new WorkflowApiRequestException(
                StatusCodes.Status400BadRequest,
                "Invalid workflow action request.",
                "Workflow action request body is required.");
        }

        var workflowInstanceId = RequiredText(request.WorkflowInstanceId, "workflowInstanceId");
        var actionKey = RequiredText(request.ActionKey, "actionKey");
        var assignedRoleKey = OptionalText(request.AssignedRoleKey);
        var actor = actorResolver.Resolve(httpContext);
        var generatedId = Guid.NewGuid().ToString("N");
        var envelope = new WorkflowActionEnvelope
        {
            Request = new WorkflowRequest
            {
                CommandType = "ExecuteWorkflowAction",
                CommandVersion = 1,
                ClientActionId = generatedId,
                CorrelationId = generatedId,
                SourceSystem = "WorkflowEngine.WorkflowApi"
            },
            Actor = new WorkflowActor
            {
                UserId = actor.UserId,
                DisplayName = actor.DisplayName,
                Roles = actor.Roles.ToList()
            },
            Workflow = new WorkflowActionBlock
            {
                WorkflowInstanceId = workflowInstanceId,
                ActionKey = actionKey,
                AssignedRoleKey = assignedRoleKey,
                NotificationContext = NormalizeNotificationContext(request.NotificationContext),
                NotificationRecipients = request.NotificationRecipients,
                IdempotencyKey = generatedId
            }
        };

        var normalized = normalizer.Normalize(envelope, businessObjectOverride: null, workflowInstanceOverride: null);
        var resultEnvelope = workflowExecutionService.Execute(new WorkflowExecutionRequest(
                normalized,
                hasher.ComputeHash(normalized.Envelope),
                AdminOverride: false)
            {
                DeriveIdentityFromPersistence = true,
                PersistSectionData = false,
                ValidateActionEcho = false
            });

        DispatchCreatedNotifications(resultEnvelope.Result);

        return new WorkflowActionApiResponse(new WorkflowActionApiResult(
            resultEnvelope.Result,
            resultEnvelope.WorkflowContext));
    }

    private void DispatchCreatedNotifications(WorkflowActionResult actionResult)
    {
        if (!actionResult.Success || actionResult.Notification?.Queued != true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(actionResult.WorkflowInstanceId)
            || string.IsNullOrWhiteSpace(actionResult.CorrelationId))
        {
            MarkNotificationDeliveryFailed(
                actionResult,
                "Workflow action notification delivery requires workflow instance id and correlation id.");
            return;
        }

        var dispatchResult = notificationDispatcher.DispatchCreatedNotifications(
            actionResult.WorkflowInstanceId,
            actionResult.CorrelationId);
        if (!dispatchResult.Success)
        {
            MarkNotificationDeliveryFailed(
                actionResult,
                dispatchResult.FailureMessage ?? "Workflow action notification delivery failed.");
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
            "Invalid workflow action request.",
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
