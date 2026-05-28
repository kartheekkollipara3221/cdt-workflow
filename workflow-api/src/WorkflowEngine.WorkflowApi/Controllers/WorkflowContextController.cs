using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Controllers;

[ApiController]
public sealed class WorkflowContextController(
    IWorkflowContextQueryService workflowContextQueryService) : ControllerBase
{
    [HttpGet]
    [Route("v1/WorkflowContext")]
    [ProducesResponseType(typeof(WorkflowContextApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult GetWorkflowContext(
        [FromQuery] string workflowInstanceId,
        [FromHeader(Name = "X-Workflow-UserId")] string? workflowUserId = null,
        [FromHeader(Name = "X-Workflow-Roles")] string? workflowRoles = null,
        [FromHeader(Name = "X-Workflow-DisplayName")] string? workflowDisplayName = null)
    {
        var workflowContext = workflowContextQueryService.GetWorkflowContext(HttpContext, workflowInstanceId);

        return Ok(new WorkflowContextApiResponse(new WorkflowContextApiResult(workflowContext.WorkflowContext)));
    }
}
