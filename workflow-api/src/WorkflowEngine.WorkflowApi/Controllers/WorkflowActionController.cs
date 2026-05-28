using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.WorkflowApi.Models;
using WorkflowEngine.WorkflowApi.Services;

namespace WorkflowEngine.WorkflowApi.Controllers;

[ApiController]
public sealed class WorkflowActionController(
    IWorkflowActionService workflowActionService) : ControllerBase
{
    [HttpPost]
    [Route("v1/WorkflowAction")]
    [ProducesResponseType(typeof(WorkflowActionApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(WorkflowActionApiResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public ActionResult ExecuteWorkflowAction(
        [FromBody] WorkflowActionRequest? request,
        [FromHeader(Name = "X-Workflow-UserId")] string? workflowUserId = null,
        [FromHeader(Name = "X-Workflow-Roles")] string? workflowRoles = null,
        [FromHeader(Name = "X-Workflow-DisplayName")] string? workflowDisplayName = null)
    {
        var response = workflowActionService.ExecuteWorkflowAction(HttpContext, request);
        if (!response.Result.ActionResult.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }

        return Ok(response);
    }
}
