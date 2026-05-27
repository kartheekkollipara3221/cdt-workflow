using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Controllers;

[ApiController]
[Route("api/workflow-instances/{workflowInstanceId}/context")]
public sealed class WorkflowContextController(
    IWorkflowContextReader workflowContextReader,
    IWorkflowActorResolver actorResolver) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WorkflowContextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<WorkflowContextResponse> Get(string workflowInstanceId)
    {
        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid workflow context request.",
                detail: "workflowInstanceId is required.");
        }

        WorkflowActorSnapshot actor;
        try
        {
            actor = actorResolver.Resolve(HttpContext);
        }
        catch (WorkflowActorResolutionException exception)
        {
            return Problem(
                statusCode: exception.StatusCode,
                title: exception.Title,
                detail: exception.Detail);
        }

        try
        {
            return workflowContextReader.Read(new WorkflowContextReadRequest(
                workflowInstanceId.Trim(),
                actor.UserId,
                actor.Roles));
        }
        catch (KeyNotFoundException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Workflow instance was not found.",
                detail: exception.Message);
        }
    }
}
