# Controller API Surface

The target API is controller-style ASP.NET Core. All examples use `ControllerBase`, `[ApiController]`, `[Route(...)]`, constructor injection, `ActionResult<T>` or `IActionResult`, and service calls. Do not use Minimal API style for this migration.

Controllers should be thin HTTP adapters. They must not manually reproduce workflow transition logic, inspect SQL transition metadata directly, create `WorkflowDbContext` manually, or trust actor roles from the body.

## Proposed Controllers

- `WorkflowInstancesController`
- `WorkflowActionsController`
- `WorkflowContextController`
- `WorkflowInspectionController`
- `WorkflowAdminController`

`WorkflowInspectionController` and `WorkflowAdminController` are optional. Gate them behind authorization.

## Endpoint Table

| Operation | Controller | HTTP Method | Route | Request | Response | Service Called | Success Status | Error Statuses |
|---|---|---|---|---|---|---|---|---|
| Start workflow | `WorkflowInstancesController` | `POST` | `/api/workflows/{workflowKey}/instances` | `StartWorkflowApiRequest` | `WorkflowActionResultEnvelope` | `IStartWorkflowService.Start` | `201 Created` or `200 OK` | `400`, `401`, `403`, `409`, `500` |
| Get workflow context | `WorkflowContextController` | `GET` | `/api/workflows/instances/{workflowInstanceId}/context` | Route only | `WorkflowContextResponse` | `IWorkflowContextReader.Read` | `200 OK` | `401`, `403`, `404`, `500` |
| Execute workflow action | `WorkflowActionsController` | `POST` | `/api/workflows/instances/{workflowInstanceId}/actions` | `WorkflowActionEnvelope` or wrapper | `WorkflowActionResultEnvelope` | Validator, normalizer, hasher, `IWorkflowExecutionService.Execute` | `200 OK` | `400`, `401`, `403`, `404`, `409`, `422`, `500` |
| Get inspection summary | `WorkflowInspectionController` | `GET` | `/api/workflows/instances/{workflowInstanceId}/inspection` | Route only | `WorkflowInspectionSnapshot` | `IWorkflowInspectionReader.Read` | `200 OK` | `401`, `403`, `404`, `500` |
| Get workflow tasks | `WorkflowInspectionController` | `GET` | `/api/workflows/instances/{workflowInstanceId}/tasks` | Route only | `IReadOnlyList<WorkflowTaskInspection>` | `IWorkflowInspectionReader.Read` | `200 OK` | `401`, `403`, `404`, `500` |
| Get workflow history | `WorkflowInspectionController` | `GET` | `/api/workflows/instances/{workflowInstanceId}/history` | Route only | `IReadOnlyList<WorkflowAuditInspection>` | `IWorkflowInspectionReader.Read` | `200 OK` | `401`, `403`, `404`, `500` |
| Get workflow notifications | `WorkflowInspectionController` | `GET` | `/api/workflows/instances/{workflowInstanceId}/notifications` | Route only | `IReadOnlyList<NotificationInspection>` | `IWorkflowInspectionReader.Read` | `200 OK` | `401`, `403`, `404`, `500` |
| Run SLA scan | `WorkflowAdminController` | `POST` | `/api/admin/workflow/sla/run` | Optional admin request | `RunSlaResult` | `IRunSlaService.RunAsync` | `200 OK` | `401`, `403`, `500` |
| Process notification outbox | `WorkflowAdminController` | `POST` | `/api/admin/workflow/notifications/process` | Empty or admin request | `ProcessNotificationOutboxResult` | `IProcessNotificationOutboxService.ProcessAsync` | `200 OK` | `401`, `403`, `500` |
| Verify seed | `WorkflowAdminController` | `POST` | `/api/admin/workflow/seed/verify` | Empty | `SeedVerificationResult` or API wrapper | `ISeedVerificationService.Verify` | `200 OK` | `401`, `403`, `500` |

## Generic Routes Versus Domain-Nested Routes

Generic workflow routes are better when:

- The engine must support more workflows than Portfolio Trade.
- The API exposes reusable workflow operations.
- Clients can work with `workflowKey`, `workflowInstanceId`, and `businessObjectId`.
- The API team wants one controller surface for all metadata-driven workflows.

Domain-nested routes are better when:

- Existing API conventions are domain-resource oriented.
- The UI is already structured around Portfolio Trade routes.
- The workflow context is part of a form-open Portfolio Trade response.
- You need route-level authorization per domain object.

Domain-oriented alternatives:

- `POST /api/portfolio-trades/{tradeId}/workflow/start`
- `GET /api/portfolio-trades/{tradeId}/workflow-context`
- `POST /api/portfolio-trades/{tradeId}/workflow-actions`
- `GET /api/portfolio-trades/{tradeId}/workflow-history`

Even with domain-nested routes, do not hardcode Portfolio Trade transition logic in controllers. The route can be domain-specific while the service call remains generic and metadata-driven.

## Controller Example: Execute Action

This example uses real workflow service names. Replace namespaces and helper types with `<ApiNamespace>` equivalents.

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Application.WorkflowExecution;

namespace <ApiNamespace>.<ControllerFolder>;

[ApiController]
[Authorize]
[Route("api/workflows/instances/{workflowInstanceId}/actions")]
public sealed class WorkflowActionsController : ControllerBase
{
    private readonly IWorkflowActorResolver actorResolver;
    private readonly WorkflowActionEnvelopeValidator validator;
    private readonly WorkflowActionEnvelopeNormalizer normalizer;
    private readonly CanonicalRequestHasher hasher;
    private readonly WorkflowActionResultBuilder resultBuilder;
    private readonly IWorkflowExecutionService executionService;
    private readonly WorkflowHttpResultMapper httpResultMapper;

    public WorkflowActionsController(
        IWorkflowActorResolver actorResolver,
        WorkflowActionEnvelopeValidator validator,
        WorkflowActionEnvelopeNormalizer normalizer,
        CanonicalRequestHasher hasher,
        WorkflowActionResultBuilder resultBuilder,
        IWorkflowExecutionService executionService,
        WorkflowHttpResultMapper httpResultMapper)
    {
        this.actorResolver = actorResolver;
        this.validator = validator;
        this.normalizer = normalizer;
        this.hasher = hasher;
        this.resultBuilder = resultBuilder;
        this.executionService = executionService;
        this.httpResultMapper = httpResultMapper;
    }

    [HttpPost]
    public ActionResult<WorkflowActionResultEnvelope> Execute(
        string workflowInstanceId,
        [FromBody] WorkflowActionEnvelope envelope)
    {
        if (envelope.Workflow?.WorkflowInstanceId is not null
            && envelope.Workflow.WorkflowInstanceId != workflowInstanceId)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Route/body workflow instance mismatch",
                Detail = "workflow.workflowInstanceId must match the route workflowInstanceId."
            });
        }

        envelope.Workflow ??= new WorkflowActionBlock();
        envelope.Workflow.WorkflowInstanceId = workflowInstanceId;

        var actor = actorResolver.Resolve(User);
        envelope.Actor = new WorkflowActor
        {
            UserId = actor.UserId,
            DisplayName = actor.DisplayName,
            Roles = actor.WorkflowRoles.ToList()
        };

        var normalized = normalizer.Normalize(envelope, null, null);
        var validationErrors = validator.Validate(normalized.Envelope);
        var result = validationErrors.Count == 0
            ? executionService.Execute(new WorkflowExecutionRequest(
                normalized,
                hasher.ComputeHash(normalized.Envelope),
                AdminOverride: false))
            : resultBuilder.BuildValidationFailed(normalized, validationErrors);

        return httpResultMapper.Map(result);
    }
}
```

Important order:

1. Bind JSON body.
2. Compare route/body `workflowInstanceId`.
3. Stamp actor from authentication.
4. Normalize.
5. Validate.
6. Hash.
7. Execute.
8. Map result code to HTTP status.

## Controller Example: Context

```csharp
[ApiController]
[Authorize]
[Route("api/workflows/instances/{workflowInstanceId}/context")]
public sealed class WorkflowContextController : ControllerBase
{
    private readonly IWorkflowActorResolver actorResolver;
    private readonly IWorkflowContextReader contextReader;

    public WorkflowContextController(
        IWorkflowActorResolver actorResolver,
        IWorkflowContextReader contextReader)
    {
        this.actorResolver = actorResolver;
        this.contextReader = contextReader;
    }

    [HttpGet]
    public ActionResult<WorkflowContextResponse> Get(string workflowInstanceId)
    {
        var actor = actorResolver.Resolve(User);

        try
        {
            return Ok(contextReader.Read(new WorkflowContextReadRequest(
                workflowInstanceId,
                actor.UserId,
                actor.WorkflowRoles)));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
```

## Controller Example: Start Workflow

```csharp
[ApiController]
[Authorize]
[Route("api/workflows/{workflowKey}/instances")]
public sealed class WorkflowInstancesController : ControllerBase
{
    private readonly IWorkflowActorResolver actorResolver;
    private readonly IStartWorkflowService startWorkflowService;
    private readonly WorkflowHttpResultMapper httpResultMapper;

    public WorkflowInstancesController(
        IWorkflowActorResolver actorResolver,
        IStartWorkflowService startWorkflowService,
        WorkflowHttpResultMapper httpResultMapper)
    {
        this.actorResolver = actorResolver;
        this.startWorkflowService = startWorkflowService;
        this.httpResultMapper = httpResultMapper;
    }

    [HttpPost]
    public ActionResult<WorkflowActionResultEnvelope> Start(
        string workflowKey,
        [FromBody] StartWorkflowApiRequest request)
    {
        var actor = actorResolver.Resolve(User);
        var result = startWorkflowService.Start(new StartWorkflowRequest(
            workflowKey,
            request.BusinessObjectType,
            request.BusinessObjectId,
            actor.UserId,
            Reset: false));

        return httpResultMapper.Map(result.Envelope);
    }
}

public sealed record StartWorkflowApiRequest(
    string BusinessObjectType,
    string BusinessObjectId);
```

## Admin And Background Operations

Prefer background services or external scheduled jobs for:

- SLA scan
- Notification outbox processing

If admin endpoints are allowed:

- Require strict admin authorization.
- Add audit logging around each operation.
- Do not expose them to normal authenticated users.
- Do not put them on domain user request paths.
- Treat `init-db` as a deployment operation, not a normal endpoint.

## Controller Rules

- Controllers should not manually reproduce workflow transition logic.
- Controllers should not inspect SQL workflow transition metadata directly.
- Controllers should not create `WorkflowDbContext` manually.
- Controllers should not trust actor roles from the body.
- Controllers should call validator, normalizer, hasher, result builder, and execution service in the correct order.
- Controllers should map workflow result codes to HTTP statuses.
- Controllers should return `WorkflowActionResultEnvelope` for workflow business outcomes.
- Controllers should return `ProblemDetails` for malformed HTTP requests, model binding failures, route/body mismatches, missing auth, and unexpected exceptions.

