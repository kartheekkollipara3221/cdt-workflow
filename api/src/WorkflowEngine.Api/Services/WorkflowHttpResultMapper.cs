using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Api.Services;

public sealed class WorkflowHttpResultMapper
{
    public int MapStatusCode(WorkflowActionResultEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Result.ResultCode switch
        {
            "Success" => StatusCodes.Status200OK,
            "ValidationFailed" => StatusCodes.Status400BadRequest,
            "WorkflowInstanceNotFound" => StatusCodes.Status404NotFound,
            "Forbidden" => StatusCodes.Status403Forbidden,
            "AmbiguousRoleMatch" => StatusCodes.Status403Forbidden,
            "InvalidTransition" => StatusCodes.Status409Conflict,
            "ConcurrencyConflict" => StatusCodes.Status409Conflict,
            "WorkflowDefinitionVersionMismatch" => StatusCodes.Status409Conflict,
            "IdempotencyConflict" => StatusCodes.Status409Conflict,
            "AlreadyExists" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    public ObjectResult ToObjectResult(WorkflowActionResultEnvelope envelope)
    {
        return new ObjectResult(envelope)
        {
            StatusCode = MapStatusCode(envelope)
        };
    }
}
