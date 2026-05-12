using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Api.Services;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;

namespace WorkflowEngine.Api.Controllers;

[ApiController]
[Route("api/workflows/instances/{workflowInstanceId}/actions")]
public sealed class WorkflowActionsController(
    IWorkflowExecutionService workflowExecutionService,
    IWorkflowActorResolver actorResolver,
    WorkflowActionEnvelopeNormalizer envelopeNormalizer,
    WorkflowActionEnvelopeValidator envelopeValidator,
    CanonicalRequestHasher requestHasher,
    WorkflowActionResultBuilder resultBuilder,
    WorkflowHttpResultMapper resultMapper) : ControllerBase
{
    [HttpPost]
    public ActionResult<WorkflowActionResultEnvelope> Execute(
        string workflowInstanceId,
        [FromBody] WorkflowActionEnvelope? envelope)
    {
        if (envelope is null)
        {
            return BadRequestProblem("Workflow action request body is required.");
        }

        if (string.IsNullOrWhiteSpace(workflowInstanceId))
        {
            return BadRequestProblem("workflowInstanceId is required.");
        }

        envelope.Workflow ??= new WorkflowActionBlock();
        if (!string.IsNullOrWhiteSpace(envelope.Workflow.WorkflowInstanceId)
            && !string.Equals(envelope.Workflow.WorkflowInstanceId, workflowInstanceId, StringComparison.Ordinal))
        {
            return BadRequestProblem("Route workflowInstanceId must match workflow.workflowInstanceId when the body value is provided.");
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

        envelope.Workflow.WorkflowInstanceId = workflowInstanceId.Trim();
        envelope.Actor = actor.ToWorkflowActor();

        var normalized = envelopeNormalizer.Normalize(envelope, businessObjectOverride: null, workflowInstanceOverride: null);
        var validationErrors = envelopeValidator.Validate(normalized.Envelope);
        if (validationErrors.Count > 0)
        {
            return resultMapper.ToObjectResult(resultBuilder.BuildValidationFailed(normalized, validationErrors));
        }

        var requestHash = requestHasher.ComputeHash(normalized.Envelope);
        var result = workflowExecutionService.Execute(new WorkflowExecutionRequest(
            normalized,
            requestHash,
            AdminOverride: false));

        return resultMapper.ToObjectResult(result);
    }

    private ObjectResult BadRequestProblem(string detail)
    {
        return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid workflow action request.",
            Detail = detail
        });
    }
}
