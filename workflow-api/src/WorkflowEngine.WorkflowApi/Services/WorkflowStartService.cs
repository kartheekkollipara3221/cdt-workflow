using WorkflowEngine.Application.Context;
using WorkflowEngine.WorkflowApi.Models;
using ApplicationStartWorkflowRequest = WorkflowEngine.Application.WorkflowStart.StartWorkflowRequest;
using ApplicationStartWorkflowService = WorkflowEngine.Application.WorkflowStart.IStartWorkflowService;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowStartService(
    IWorkflowActorResolver actorResolver,
    ApplicationStartWorkflowService startWorkflowService,
    IWorkflowContextReader workflowContextReader) : IWorkflowStartService
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
        var actor = actorResolver.Resolve(httpContext);

        var startResult = startWorkflowService.Start(new ApplicationStartWorkflowRequest(
            workflowKey,
            businessObjectType,
            businessObjectId,
            actor.UserId,
            Reset: false));

        var actionResult = startResult.Envelope.Result;
        var workflowContext = actionResult.Success
            ? workflowContextReader.Read(new WorkflowContextReadRequest(
                actionResult.WorkflowInstanceId,
                actor.UserId,
                actor.Roles)).WorkflowContext
            : null;

        return new WorkflowStartApiResponse(new WorkflowStartApiResult(actionResult, workflowContext));
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
}
