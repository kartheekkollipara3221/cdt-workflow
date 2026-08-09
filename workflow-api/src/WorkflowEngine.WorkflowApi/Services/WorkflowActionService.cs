using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActionService(
    IWorkflowActorResolver actorResolver,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    IWorkflowExecutionService workflowExecutionService,
    IWorkflowSlaSynchronizationService slaSynchronizationService,
    IWorkflowContextReader workflowContextReader,
    WorkflowActionResultBuilder resultBuilder,
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
        var actionKey = OptionalText(request.ActionKey);
        if (actionKey is null && request.SlaTargetDate is null)
        {
            RequiredText(request.ActionKey, "actionKey");
        }

        var assignedRoleKey = OptionalText(request.AssignedRoleKey);
        var actor = actorResolver.Resolve(httpContext);
        var generatedId = Guid.NewGuid().ToString("N");
        var effectiveIdempotencyKey = generatedId;
        var notificationInput = WorkflowNotificationRequestMapper.Map(request.Notification);
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
                SlaInput = request.SlaTargetDate is null
                    ? null
                    : new WorkflowSlaInput { TargetDate = request.SlaTargetDate },
                NotificationContext = null,
                NotificationRecipients = null,
                IdempotencyKey = effectiveIdempotencyKey
            }
        };

        var normalized = normalizer.Normalize(envelope, businessObjectOverride: null, workflowInstanceOverride: null);
        var requestHash = hasher.ComputeHash(normalized.Envelope, notificationInput);
        if (actionKey is null)
        {
            WorkflowSlaSynchronizationResult synchronization;
            try
            {
                synchronization = slaSynchronizationService.Synchronize(new WorkflowSlaSynchronizationRequest(
                    workflowInstanceId,
                    WorkflowTaskId: null,
                    request.SlaTargetDate,
                    actor.UserId,
                    generatedId,
                    effectiveIdempotencyKey));
            }
            catch (WorkflowSlaValidationException exception)
            {
                throw new WorkflowApiRequestException(
                    StatusCodes.Status400BadRequest,
                    "Invalid workflow SLA input.",
                    exception.Message);
            }

            var synchronizedEnvelope = resultBuilder.BuildSlaSynchronized(
                normalized,
                requestHash,
                synchronization.Changed,
                synchronization.ScheduleVersion);
            var workflowContext = workflowContextReader.Read(new WorkflowContextReadRequest(
                workflowInstanceId,
                actor.UserId,
                actor.Roles)).WorkflowContext;
            synchronizedEnvelope.WorkflowContext = workflowContext;
            synchronizedEnvelope.Result.BusinessObject = new WorkflowActionResultBusinessObject
            {
                ObjectType = workflowContext.BusinessObject.ObjectType,
                ObjectId = workflowContext.BusinessObject.ObjectId
            };

            return new WorkflowActionApiResponse(new WorkflowActionApiResult(
                synchronizedEnvelope.Result,
                synchronizedEnvelope.WorkflowContext));
        }

        var resultEnvelope = workflowExecutionService.Execute(new WorkflowExecutionRequest(
                normalized,
                requestHash,
                AdminOverride: false)
            {
                DeriveIdentityFromPersistence = true,
                PersistSectionData = false,
                ValidateActionEcho = false,
                NotificationInput = notificationInput
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

    private static string? OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
