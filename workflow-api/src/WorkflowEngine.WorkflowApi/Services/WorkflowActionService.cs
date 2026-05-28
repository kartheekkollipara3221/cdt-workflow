using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActionService(
    IWorkflowActorResolver actorResolver,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    IWorkflowExecutionService workflowExecutionService) : IWorkflowActionService
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

        return new WorkflowActionApiResponse(new WorkflowActionApiResult(
            resultEnvelope.Result,
            resultEnvelope.WorkflowContext));
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
}
