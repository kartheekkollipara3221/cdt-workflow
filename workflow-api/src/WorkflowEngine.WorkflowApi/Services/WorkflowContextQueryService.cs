using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowContextQueryService(
    IWorkflowContextReader workflowContextReader,
    IWorkflowActorResolver actorResolver) : IWorkflowContextQueryService
{
    public WorkflowContextResponse GetWorkflowContext(HttpContext httpContext, string workflowInstanceId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            throw new WorkflowApiRequestException(
                StatusCodes.Status400BadRequest,
                "Invalid workflow context request.",
                "workflowInstanceId is required.");
        }

        var actor = actorResolver.Resolve(httpContext);

        return workflowContextReader.Read(new WorkflowContextReadRequest(
            workflowInstanceId.Trim(),
            actor.UserId,
            actor.Roles));
    }
}
