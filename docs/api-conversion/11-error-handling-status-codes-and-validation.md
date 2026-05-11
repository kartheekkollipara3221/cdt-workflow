# Error Handling, Status Codes, and Validation

The console reports errors through `CommandResult` and text/JSON output. The API must convert malformed HTTP/model errors, workflow business result codes, and unexpected exceptions into consistent HTTP responses.

## Result Mapping Table

| Scenario | Current Console Behavior | Workflow Result Code | Recommended HTTP Status | API Response Body |
|---|---|---|---|---|
| Malformed JSON | `ExecuteJsonCommandHandler` returns failure text `Invalid JSON envelope`. | None | `400 Bad Request` | `ProblemDetails` |
| Model binding failure | Not applicable to console. | None | `400 Bad Request` | API validation problem response |
| Validation failed | `WorkflowActionResultBuilder.BuildValidationFailed`. | `ValidationFailed` | `400 Bad Request` | `WorkflowActionResultEnvelope` |
| Workflow instance not found | Result envelope or `show-context` command failure. | `WorkflowInstanceNotFound` | `404 Not Found` | `WorkflowActionResultEnvelope` or `ProblemDetails` for context |
| Forbidden | Result envelope. | `Forbidden` | `403 Forbidden` | `WorkflowActionResultEnvelope` |
| Ambiguous role match | Result envelope. | `AmbiguousRoleMatch` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Invalid transition | Result envelope. | `InvalidTransition` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Completed workflow action attempted | `InvalidTransition` with completed message. | `InvalidTransition` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Concurrency conflict | Result envelope with failed guard. | `ConcurrencyConflict` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Workflow definition version mismatch | Result envelope with context. | `WorkflowDefinitionVersionMismatch` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Idempotency conflict | Result envelope. | `IdempotencyConflict` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Idempotent replay | Result envelope with `diagnostics.replay = true`. | `Success` | `200 OK` | `WorkflowActionResultEnvelope` |
| Already exists | Start workflow result envelope. | `AlreadyExists` | `409 Conflict` | `WorkflowActionResultEnvelope` |
| Missing connection string | Startup exception in `AddWorkflowInfrastructure`. | None | Startup failure | Startup logs, no endpoint response |
| Invalid SMTP configuration | Startup exception in `AddWorkflowInfrastructure`. | None | Startup failure | Startup logs, no endpoint response |
| Unexpected exception | Console often fails command. | None | `500 Internal Server Error` | `ProblemDetails`, no raw exception to normal clients |

## When To Return `WorkflowActionResultEnvelope`

Return `WorkflowActionResultEnvelope` when the request reached workflow-level processing and a workflow business outcome can be represented:

- `Success`
- `ValidationFailed`
- `WorkflowInstanceNotFound`
- `Forbidden`
- `AmbiguousRoleMatch`
- `InvalidTransition`
- `ConcurrencyConflict`
- `WorkflowDefinitionVersionMismatch`
- `IdempotencyConflict`
- `AlreadyExists`

## When To Return `ProblemDetails`

Return `ProblemDetails` when the error is outside the workflow business contract:

- Malformed HTTP JSON.
- Route/body mismatch before building normalized workflow request.
- Missing authentication.
- Missing required route value.
- Unsupported content type.
- API authorization policy failure.
- Startup/configuration failure in logs.
- Unexpected exceptions.

## `[ApiController]` Validation Is Not Enough

`[ApiController]` handles model binding and data annotations, but it does not enforce workflow rules. Always run `WorkflowActionEnvelopeValidator` after actor stamping.

Existing validator checks:

- Required blocks and text values.
- Non-empty actor roles.
- JSON object payload.
- MakerPrimary `saveAsDraft` consistency.

`WorkflowExecutionService` then checks persisted workflow identity, action echo against metadata, transition validity, roles, concurrency, idempotency, completed state, and definition version.

## Recommended `WorkflowHttpResultMapper`

```csharp
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application.Contracts;

public sealed class WorkflowHttpResultMapper
{
    public ActionResult<WorkflowActionResultEnvelope> Map(WorkflowActionResultEnvelope envelope)
    {
        var code = envelope.Result.ResultCode;

        return code switch
        {
            "Success" => new OkObjectResult(envelope),
            "ValidationFailed" => new BadRequestObjectResult(envelope),
            "WorkflowInstanceNotFound" => new NotFoundObjectResult(envelope),
            "Forbidden" => new ObjectResult(envelope) { StatusCode = StatusCodes.Status403Forbidden },
            "AlreadyExists" => new ConflictObjectResult(envelope),
            "InvalidTransition" => new ConflictObjectResult(envelope),
            "ConcurrencyConflict" => new ConflictObjectResult(envelope),
            "WorkflowDefinitionVersionMismatch" => new ConflictObjectResult(envelope),
            "IdempotencyConflict" => new ConflictObjectResult(envelope),
            "AmbiguousRoleMatch" => new ConflictObjectResult(envelope),
            _ => new ObjectResult(envelope) { StatusCode = StatusCodes.Status422UnprocessableEntity }
        };
    }
}
```

If your API uses a custom response envelope, wrap this logic without changing workflow result codes.

## Route/Body Mismatch

Treat route/body mismatches as HTTP request errors:

- `workflow.workflowInstanceId` differs from route: `400 Bad Request`.
- `businessObject.objectId` differs from domain route `{tradeId}`: `400 Bad Request`.

Do this before calling `IWorkflowExecutionService`.

## Exception Handling

Use the API's existing exception middleware. Add explicit handlers for:

- `KeyNotFoundException` from `IWorkflowContextReader.Read`: `404`.
- `UnauthorizedAccessException` from actor resolver: `401` or `403` depending on API convention.
- `InvalidOperationException` from startup validation: fail startup rather than catch in controllers.

Do not expose raw SQL or SMTP exception details to normal users.

## Consistent Result Shapes

Recommended rule:

- Workflow action/start endpoints return `WorkflowActionResultEnvelope` for expected business outcomes.
- Context and inspection GET endpoints return typed read models on success and `ProblemDetails` or `404` for missing resources.
- Admin operation endpoints return typed service result on success and `ProblemDetails` for operational failures.

