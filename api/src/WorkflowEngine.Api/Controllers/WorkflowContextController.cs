using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Api.Controllers;

[ApiController]
[Route("api/workflows/instances/{workflowInstanceId}/context")]
public sealed class WorkflowContextController(
    IWorkflowContextReader workflowContextReader,
    IWorkflowActorResolver actorResolver) : ControllerBase
{
    [HttpGet]
    public ActionResult<WorkflowContextResponse> Get(string workflowInstanceId)
    {
        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid workflow context request.",
                Detail = "workflowInstanceId is required."
            });
        }

        WorkflowActorSnapshot actor;
        try
        {
            actor = actorResolver.Resolve(HttpContext);
        }
        catch (WorkflowActorResolutionException exception)
        {
            return StatusCode(exception.StatusCode, new ProblemDetails
            {
                Status = exception.StatusCode,
                Title = exception.Title,
                Detail = exception.Detail
            });
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
            return StatusCode(StatusCodes.Status404NotFound, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Workflow instance was not found.",
                Detail = exception.Message
            });
        }
    }
}
