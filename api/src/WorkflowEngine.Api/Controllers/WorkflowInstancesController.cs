using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Contracts;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.WorkflowStart;

namespace WorkflowEngine.Api.Controllers;

[ApiController]
[Route("api/workflows/{workflowKey}/instances")]
public sealed class WorkflowInstancesController(
    IStartWorkflowService startWorkflowService,
    IWorkflowActorResolver actorResolver,
    WorkflowHttpResultMapper resultMapper) : ControllerBase
{
    [HttpPost]
    public ActionResult<WorkflowActionResultEnvelope> Start(
        string workflowKey,
        [FromBody] StartWorkflowApiRequest? request)
    {
        if (request is null)
        {
            return BadRequestProblem("Start workflow request body is required.");
        }

        if (string.IsNullOrWhiteSpace(workflowKey)
            || string.IsNullOrWhiteSpace(request.BusinessObjectType)
            || string.IsNullOrWhiteSpace(request.BusinessObjectId))
        {
            return BadRequestProblem("workflowKey, businessObjectType, and businessObjectId are required.");
        }

        WorkflowActorSnapshot actor;
        try
        {
            actor = actorResolver.Resolve(HttpContext);
        }
        catch (WorkflowActorResolutionException exception)
        {
            return ActorProblem(exception);
        }

        var result = startWorkflowService.Start(new StartWorkflowRequest(
            workflowKey.Trim(),
            request.BusinessObjectType.Trim(),
            request.BusinessObjectId.Trim(),
            actor.UserId,
            Reset: false));

        return resultMapper.ToObjectResult(result.Envelope);
    }

    private ObjectResult BadRequestProblem(string detail)
    {
        return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid start workflow request.",
            Detail = detail
        });
    }

    private ObjectResult ActorProblem(WorkflowActorResolutionException exception)
    {
        return StatusCode(exception.StatusCode, new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Title,
            Detail = exception.Detail
        });
    }
}
