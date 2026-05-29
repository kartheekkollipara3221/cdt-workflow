# Codex Handoff

## Project goal

- We are adjusting the existing `WorkflowEngine.WorkflowApi` ASP.NET Core API project.
- The goal is to align it with the conventions shown in `workflow-api/final-state-project.txt`.
- `WorkflowEngine.WorkflowApi` already exists. Do not recreate it from scratch.
- It is an intermediate migration API layer for workflow engine logic originally from a console app.
- Later, the code should be easy to move into the existing target API project.

## Source-of-truth references

- `PROJECT.md`: repository authority. Key guardrails: .NET 10, SQL Server, EF Core, generic metadata-driven workflow logic, `TimeProvider`, no `DateTime.UtcNow` outside composition root, no Portfolio Trade hardcoding in the reusable engine, no request JSON `toState`, no `workflow.options` metadata overrides, no CQRS/MediatR/event sourcing/BPMN/brokers/microservices/plugin frameworks.
- `AGENTS.MD`: repo-specific instructions. Requires reading `PROJECT.md` before planning/implementation and requires the completion summary format.
- `workflow-api/final-state-project.txt`: main reference for target API conventions. It shows target-style controller routes such as `v1/Application`, Swagger/CORS/ProblemDetails shape, and target response style `Ok(new { result = ... })`.
- `workflow-api/WORKFLOW_API_BOUNDARIES.md`: boundary decisions created during this work. Read before changing workflow API behavior.
- `workflow-api/src/WorkflowEngine.WorkflowApi`: current API project being aligned.
- `src/WorkflowEngine.Domain`, `src/WorkflowEngine.Application`, `src/WorkflowEngine.Infrastructure`: reusable workflow engine projects referenced by the API.
- `src/WorkflowEngine.Console`: CLI/demo/admin host. Do not move console command parsing into the API.

## What has already been done

### Program.cs

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Program.cs`

- Changed:
  - Added `AddSwaggerGen` and Swagger UI in Development.
  - Added `OpenApiInfo` title/version.
  - Added `AddControllers().AddJsonOptions(...)` with `PropertyNamingPolicy = null` to match target casing behavior.
  - Added `AddExceptionHandler<GlobalExceptionHandler>()` and `AddProblemDetails()`.
  - Added CORS policy `_corsOrigins`, defaulting to `http://localhost:4200`.
  - Added `AddAuthorization()` / `UseAuthorization()`.
  - Kept `TimeProvider.System` registration at composition root.
  - Kept workflow engine registration through `AddWorkflowInfrastructure(configuration)`.
  - Registered API services:
    - `IWorkflowActorResolver`
    - `IWorkflowContextQueryService`
    - `IWorkflowActionService`
    - `IWorkflowStartService`
  - Registered workflow helper services used by the API action path:
    - `WorkflowActionEnvelopeNormalizer`
    - `CanonicalRequestHasher`
    - `WorkflowActionResultBuilder`
    - `CanonicalWorkflowResultSerializer`
  - Disabled HTTPS redirection in Development so local HTTP Swagger works; non-Development still uses exception handler, HSTS, and HTTPS redirection.
- Why:
  - Match target API host style where safe without importing target-specific log4net/JWT/target EF context packages.
  - Enable local Swagger testing.
  - Centralize request error handling.
- Status: complete for current migration API shape.
- Behavior impact:
  - Swagger UI is available at app root in Development, for example `http://localhost:5068/`.
  - Development HTTP launch no longer redirects to HTTPS.

### Controllers

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowStartController.cs`

- Changed:
  - Added `POST /v1/WorkflowStart`.
  - Accepts `WorkflowStartRequest` body.
  - Displays trusted actor headers in Swagger:
    - `X-Workflow-UserId`
    - `X-Workflow-Roles`
    - `X-Workflow-DisplayName`
  - Delegates to `IWorkflowStartService`.
  - Returns `WorkflowStartApiResponse`.
  - Returns HTTP 500 with the response body for workflow-level failures such as `AlreadyExists`.
- Why:
  - Gives the form/business API a clean workflow-only endpoint to create a workflow instance after it creates a business object.
  - Does not expose console `--reset`.
- Status: complete for clean workflow start.
- Behavior impact:
  - Request body is:
    - `workflowKey`
    - `businessObjectType`
    - `businessObjectId`
  - Starting user comes from trusted headers.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowContextController.cs`

- Changed:
  - Refactored to a thin controller delegating to `IWorkflowContextQueryService`.
  - Kept only target-style route `GET /v1/WorkflowContext?workflowInstanceId=...`.
  - Removed older duplicate route `GET /api/workflow-instances/{workflowInstanceId}/context`.
  - Displays trusted actor headers in Swagger.
  - Response shape is now `{ "result": { "workflowContext": ... } }` via `WorkflowContextApiResponse`.
- Why:
  - Match target route/response style and boundary decisions.
  - Avoid duplicate Swagger entries.
- Status: complete for current context endpoint.
- Behavior impact:
  - API consumers must pass query parameter `workflowInstanceId`; actor comes from headers.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs`

- Changed:
  - Added `POST /v1/WorkflowAction`.
  - Accepts `WorkflowActionRequest`, not `WorkflowActionEnvelope`.
  - Displays trusted actor headers in Swagger.
  - Delegates to `IWorkflowActionService`.
  - Successful response shape is `{ "result": { "actionResult": ..., "workflowContext": ... } }`.
  - Returns HTTP 500 with the response body when workflow-level execution returns `Success = false`.
- Why:
  - Provide a workflow-only button-click endpoint that does not accept form payload, UI echo fields, business-object IDs, or caller-provided idempotency/correlation IDs.
- Status: complete for current workflow-only action contract.
- Behavior impact:
  - Request body contains only:
    - `workflowInstanceId`
    - `actionKey`

### Services / dependency injection

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`

- Changed:
  - Resolves starting actor through `IWorkflowActorResolver`.
  - Validates required body fields.
  - Calls application `IStartWorkflowService.Start(...)` with `Reset = false`.
  - Reads refreshed workflow context after a successful start.
  - Wraps response as `{ result: { actionResult, workflowContext } }`.
- Why:
  - Keep controller thin and keep workflow start logic in the existing application/infrastructure workflow service.
  - Preserve workflow-only API boundary.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowStartService.cs`

- Changed:
  - Added API-facing workflow start abstraction.
- Why:
  - Keeps controller thin and testable.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowContextQueryService.cs`

- Changed:
  - Resolves actor through `IWorkflowActorResolver`.
  - Validates blank `workflowInstanceId`.
  - Calls `IWorkflowContextReader.Read(new WorkflowContextReadRequest(...))`.
- Why:
  - Moves request-level context logic out of controller.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowContextQueryService.cs`

- Changed:
  - Added API-facing context query abstraction.
- Why:
  - Keeps controller thin and migration-friendly.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`

- Changed:
  - Resolves actor through `IWorkflowActorResolver`.
  - Validates body contains only required workflow fields.
  - Builds an internal `WorkflowActionEnvelope` for the existing application execution service.
  - Generates internal `ClientActionId`, `CorrelationId`, and `IdempotencyKey`.
  - Calls `IWorkflowExecutionService.Execute(...)` with:
    - `DeriveIdentityFromPersistence = true`
    - `PersistSectionData = false`
    - `ValidateActionEcho = false`
    - `AdminOverride = false`
  - Wraps response as `{ result: { actionResult, workflowContext } }`.
- Why:
  - API must be workflow-only and must not accept/save form payload.
  - Existing console `execute-json` can continue using the fuller envelope and section-data path.
- Status: complete.
- Behavior impact:
  - API action path does not persist `MockSectionData`.
  - API derives workflow key, definition version, and business object identity from persisted workflow instance data.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowActionService.cs`

- Changed:
  - Added API-facing workflow action abstraction.
- Why:
  - Keeps controller thin and testable.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/TrustedHeaderWorkflowActorResolver.cs`

- Changed earlier:
  - Trusted header adapter reads user ID, display name, and roles from configured headers.
- Why:
  - Temporary integration adapter until target auth/claims model is known.
- Status: complete as temporary adapter.
- Important boundary:
  - Do not expand into real user lookup/auth/role mapping.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowApiRequestException.cs`

- Changed:
  - Added exception carrying status/title/detail for `ProblemDetails`.
- Why:
  - Service layer can signal API request failures to global exception handler.
- Status: complete.

File: `src/WorkflowEngine.Application/WorkflowExecution/IWorkflowExecutionService.cs`

- Changed:
  - Added flags to `WorkflowExecutionRequest`:
    - `DeriveIdentityFromPersistence`
    - `ValidateActionEcho`
    - `PersistSectionData`
- Why:
  - Support API workflow-only action execution while preserving console behavior.
- Status: complete.
- Behavior impact:
  - Defaults preserve existing console behavior.

File: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- Changed:
  - If `DeriveIdentityFromPersistence` is true, applies persisted workflow/business identity before identity validation.
  - Skips action echo validation when `ValidateActionEcho = false`.
  - Skips `MockSectionData` insert when `PersistSectionData = false`.
- Why:
  - Allows API path to avoid accepting/persisting form payload and UI echo fields.
- Status: complete.
- Behavior impact:
  - Console/default execution still validates action echo and saves section data.
  - API action execution avoids form-data side effects.

### Models / DTOs

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`

- Changed:
  - Added workflow start request body:
    - `WorkflowKey`
    - `BusinessObjectType`
    - `BusinessObjectId`
- Why:
  - Workflow start must attach a workflow instance to an upstream business object.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartApiResponse.cs`

- Changed:
  - Added target-style nested response:
    - `result.actionResult`
    - `result.workflowContext`
- Why:
  - Match target response convention.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`

- Changed:
  - Added workflow-only action request body:
    - `WorkflowInstanceId`
    - `ActionKey`
- Why:
  - Boundary decision: action API accepts only pure workflow fields.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionApiResponse.cs`

- Changed:
  - Changed action response to nested shape:
    - `result.actionResult`
    - `result.workflowContext`
- Why:
  - Match target response convention and user clarification.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowContextApiResponse.cs`

- Changed:
  - Added context response nested shape:
    - `result.workflowContext`
- Why:
  - Match target response convention and user clarification.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowApiResponse.cs`

- Changed earlier:
  - Generic response wrapper with JSON property `result`.
- Status:
  - Still present but no longer the primary response for current workflow endpoints after nested response DTOs were added.
  - Can be kept for future generic use or removed later if unused after review.

### Configuration

File: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.json`

- Changed:
  - Added CORS origins:
    - `http://localhost:4200`
  - Existing `WorkflowApi:ActorHeaders` remains.
  - Existing `Notifications:Sender = "NoOp"` remains.
- Why:
  - Support browser/client testing and target-style CORS policy.
- Status: partial; real target API CORS origins must be confirmed.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.Development.json`

- Existing important value:
  - `ConnectionStrings:DefaultConnection = Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`
- Why it matters:
  - API uses this DB for local Swagger/live tests.

File: `src/WorkflowEngine.Console/appsettings.Development.json`

- Changed earlier:
  - Added same local API DB connection string.
- Why:
  - Allows console prep commands (`init-db`, `verify-seed`, `start-workflow`) to target the same DB as the API.
- Status: open decision. It is convenient locally but contains a developer-machine DB name.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`

- Changed:
  - Normalized launch settings to project-only `http` and `https` profiles.
  - Preserved existing launch URLs:
    - `http://localhost:5058`
    - `https://localhost:7176;http://localhost:5058`
  - Moved `$schema` to the conventional top-level position.
  - Removed Visual Studio IIS Express profile and `iisSettings` local tooling configuration.
- Why:
  - Keep only intentional SDK-run launch profiles for local migration/testing.
- Status: complete as of 2026-05-29.

### Logging / error handling

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Infrastructure/GlobalExceptionHandler.cs`

- Changed:
  - Added centralized exception handling using `IExceptionHandler` and `IProblemDetailsService`.
  - Maps:
    - `WorkflowApiRequestException` to configured status/title/detail.
    - `WorkflowActorResolutionException` to configured status/title/detail.
    - `KeyNotFoundException` to 404.
    - Others to 500 generic problem response.
  - Logs structured request failure details.
- Why:
  - Align with centralized API error handling rather than per-controller try/catch.
- Status: complete for current request/actor/context errors.
- Note:
  - Workflow-level failed action/start results are currently returned by controllers as HTTP 500 with the workflow response body, per user clarification.

### Documentation

File: `workflow-api/WORKFLOW_API_BOUNDARIES.md`

- Changed:
  - Added durable boundary file for future `WorkflowEngine.WorkflowApi` work.
  - Records that the API handles workflow matters only.
  - Records that form validation, form data persistence, user lookup/resolution, business-object APIs, console admin operations, and deployment orchestration are out of scope.
  - Records temporary trusted-header actor adapter decision.
  - Records exact request/response decisions for start/context/action endpoints.
- Why:
  - Prevent future sessions from expanding the workflow API into form/user/business-object responsibilities.
- Status: complete and should be read before future API changes.

Files:
- `CODEX_HANDOFF.md`
- `NEXT_CODEX_PROMPT.md`
- `TODO_WORKFLOW_API.md`

- Changed:
  - Created/updated durable handoff, next-session prompt, and task checklist.
- Status: current as of this handoff.

File: `workflow-api/final-state-project.txt`

- Status:
  - Untracked reference file supplied by user.
  - Used as input only; do not modify unless user asks.

### Tests / build changes

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowStartControllerTests.cs`

- Changed:
  - Added tests for successful start response and workflow-level failure returning HTTP 500.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`

- Changed:
  - Added tests for trusted actor use, `Reset = false`, context read on success, no context read on failed start, missing body/field validation.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowActionControllerTests.cs`

- Changed:
  - Tests thin action controller response and HTTP 500 for workflow-level failures.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`

- Changed:
  - Tests workflow-only action request mapping, generated IDs, trusted actor use, internal execution flags, and missing request/field validation.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowContextControllerTests.cs`

- Changed:
  - Updated to expect `WorkflowContextApiResponse` nested shape.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowContextQueryServiceTests.cs`

- Changed earlier:
  - Tests actor resolution, trimmed workflow instance ID, blank ID request error, actor/reader exception flow.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Startup/WorkflowApiProjectStructureTests.cs`

- Changed:
  - Verifies three controllers/routes:
    - `v1/workflowstart`
    - `v1/workflowcontext`
    - `v1/workflowaction`
  - Verifies old REST-style context route is absent.
  - Verifies no Minimal API `MapGet`/`MapPost`, no console command terms, no controller body `WorkflowActionEnvelope`.
  - Verifies DI registration for current workflow API services.
  - Verifies JSON naming policy remains null.
- Status: complete.

### Deleted or renamed files

- No existing source files were deleted or renamed.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs` was replaced during edits using delete/add patch, but the path remains.

## Current git/worktree status

Command: `git status --short --untracked-files=all`

Summary:

```text
 M CODEX_HANDOFF.md
 M TODO_WORKFLOW_API.md
 M workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json
?? ~/.codex/superpowers/
```

Command: `git diff --stat`

Summary:

```text
 CODEX_HANDOFF.md                                      | 17 +++++++++++------
 TODO_WORKFLOW_API.md                                  |  6 +++---
 .../Properties/launchSettings.json                    | 19 ++-----------------
 3 files changed, 16 insertions(+), 26 deletions(-)
```

Note: `git diff --stat` does not include the unrelated untracked `~/.codex/superpowers/` directory.

Command: `git diff --name-only`

Tracked files changed:

```text
CODEX_HANDOFF.md
TODO_WORKFLOW_API.md
workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json
```

## Current build/test status

Command:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Result:
- Passed.
- 0 warnings, 0 errors.

Command:

```powershell
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Result:
- Passed.
- 32 tests passed, 0 failed, 0 skipped.
- No executable-lock warning in the latest run.

Earlier broader command:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
```

Result:
- Passed earlier in this session.
- 206 tests passed, 0 failed, 2 skipped.
- A prior run without `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` failed because SQL-backed tests require that variable.

Live SQL/API checks completed earlier:

- `dotnet run --project src\WorkflowEngine.Console -- init-db`: passed.
- `dotnet run --project src\WorkflowEngine.Console -- verify-seed`: passed.
- Live Swagger at `http://localhost:5068/swagger/v1/swagger.json` exposed:
  - `/v1/WorkflowStart`
  - `/v1/WorkflowContext`
  - `/v1/WorkflowAction`
- Live start/context/action sequence passed:
  - `POST /v1/WorkflowStart` with `PortfolioTrade` / `PortfolioTrade` / `PT-API-START`
  - returned workflow instance `WF-PT-API-START`
  - `GET /v1/WorkflowContext` returned `Draft`
  - `POST /v1/WorkflowAction` with minimal body moved `Draft -> CheckerReview` and created `CheckerReviewTask`

## Important decisions made

- Match `workflow-api/final-state-project.txt` only where safe:
  - Use controller-based APIs.
  - Use `v1/...` routes.
  - Use nested `result` wrappers.
  - Keep Swagger UI for local testing.
  - Do not add target-specific log4net/JWT/target DbContext without full target source.
- Controller routing:
  - `POST /v1/WorkflowStart`
  - `GET /v1/WorkflowContext`
  - `POST /v1/WorkflowAction`
  - Removed older REST-style context route.
- Response style:
  - Start/action success: `{ result: { actionResult, workflowContext } }`
  - Context success: `{ result: { workflowContext } }`
  - Workflow-level start/action failures: HTTP 500 with the same API response body.
- Controller design:
  - Controllers remain thin and delegate to API-facing services.
- Service layer:
  - API-facing services adapt HTTP/body/header inputs to application workflow services.
  - Business logic remains in existing application/infrastructure workflow services.
- Actor identity:
  - Keep temporary trusted-header adapter.
  - Do not implement real user lookup, authentication, JWT/claims mapping, or SQL user-role mapping in `WorkflowEngine.WorkflowApi`.
- DTO/request decisions:
  - `WorkflowStartRequest`: `workflowKey`, `businessObjectType`, `businessObjectId`.
  - `WorkflowActionRequest`: only `workflowInstanceId`, `actionKey`.
  - `WorkflowActionEnvelope` is not the public action API request body.
  - API action path generates client action ID, correlation ID, and idempotency key internally.
  - API action path derives workflow/business identity from persisted workflow instance data.
  - API action path does not accept `dataSave`, UI echo fields, expected guards, business-object identity, workflow key/version, action reason, or caller-provided idempotency/correlation IDs.
- Form/business boundary:
  - Workflow API does not create, validate, update, or read form/business data.
  - Upstream form/business API owns business object lifecycle and form persistence.
- Console boundary:
  - Console setup/admin behavior is not automatically public API behavior.
  - Public start endpoint intentionally does not expose `reset`.

## Open issues / remaining work

- [x] Review `launchSettings.json` changes before commit.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
      Why it matters:
      It appears modified by Visual Studio and may contain local tooling noise.
      Completed:
      Kept only the project `http` and `https` profiles, preserved existing launch URLs, moved `$schema` to the top-level conventional position, and removed IIS Express local tooling settings.
      Risk level: Low

- [ ] Decide whether to keep console `appsettings.Development.json` connection string.
      Files involved:
      `src/WorkflowEngine.Console/appsettings.Development.json`
      Why it matters:
      It makes console prep commands target the API database but contains a developer-machine database name.
      Suggested next step:
      Keep if local integration setup is intended; otherwise move to environment/user-local config.
      Risk level: Medium

- [ ] Confirm unused/legacy response wrapper cleanup.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowApiResponse.cs`
      Why it matters:
      Current endpoints now use specialized nested response DTOs. The generic wrapper may be unused.
      Suggested next step:
      Search usages and remove only if unused and not intended for future endpoints.
      Risk level: Low

- [ ] Add or verify live Swagger instructions/documentation.
      Files involved:
      README or workflow-api docs if desired.
      Why it matters:
      User is testing manually through Swagger and needs stable sample bodies/headers.
      Suggested next step:
      Add a small workflow-api testing doc only if requested.
      Risk level: Low

- [ ] Decide whether audit/history read API is needed.
      Files involved:
      `WorkflowInspectionReader` / inspection services, potential new API controller/service.
      Why it matters:
      Useful for history tabs/support/admin views, but not required for current start/context/action flow.
      Suggested next step:
      Confirm UI/support requirements before implementing.
      Risk level: Medium

- [ ] Decide whether task/notification read APIs are needed.
      Files involved:
      Inspection/read services in `WorkflowEngine.Infrastructure`, potential new API controller/services.
      Why it matters:
      Useful for support/admin views; may not be needed for normal form workflow.
      Suggested next step:
      Confirm UI/support requirements before implementing.
      Risk level: Low

- [ ] Plan SLA/notification processing as background/internal work.
      Files involved:
      `IRunSlaService`, `IProcessNotificationOutboxService`, potential hosted service/admin endpoint/job.
      Why it matters:
      Console `run-sla` and `process-notification-outbox` are operational jobs, not normal public form APIs.
      Suggested next step:
      Choose scheduled job, hosted service, Windows service, job runner, or protected internal admin endpoint.
      Risk level: Medium

- [ ] Replace trusted headers with real target API identity integration when target auth model is known.
      Files involved:
      `TrustedHeaderWorkflowActorResolver`, `IWorkflowActorResolver`, target API auth code.
      Why it matters:
      Trusted headers are temporary/local integration only unless secured by upstream infrastructure.
      Suggested next step:
      Map real target identity/roles/profile data to `WorkflowActorSnapshot` after target auth is available.
      Risk level: High

- [ ] Decide how workflow EF migrations are deployed in target environment.
      Files involved:
      `WorkflowEngine.Infrastructure/Migrations`, `WorkflowDbContext`, target deployment pipeline.
      Why it matters:
      Console `init-db` is not the production deployment model.
      Suggested next step:
      Confirm whether target API or deployment pipeline applies migrations.
      Risk level: High

## Known risks and assumptions

- Full target API source is unavailable.
- Conventions are inferred only from `workflow-api/final-state-project.txt`.
- Target API may already have its own global exception handler, Swagger config, CORS policy, auth/JWT setup, logging, and EF context conventions.
- `Swashbuckle.AspNetCore` was added for local Swagger UI. If target API already has Swagger/OpenAPI, use its package/version/config instead.
- API response inner application DTOs currently serialize with PascalCase because JSON property naming policy is null, matching target sample behavior.
- HTTP 500 for workflow-level failures was recorded from user clarification as “custom error code 500”; confirm exact production error semantics before integrating with target exception middleware.
- Live API checks used local DB `cdt_workflow_api_20260527` on `Server=DOLLY`.
- Current build/test commands pass but warn when a running local API process locks `WorkflowEngine.WorkflowApi.exe`.
- Worktree contains many untracked new files; review before commit.
- `workflow-api/final-state-project.txt` is untracked and should remain a reference unless user wants it committed.

## How to continue in a new Codex session

1. Read `AGENTS.md` if present.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `CODEX_HANDOFF.md`.
4. Read `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
5. Read `workflow-api/final-state-project.txt`.
6. Inspect:
   - `git status --short`
   - `git diff --stat`
   - `git diff --name-only`
7. Review changed and untracked files before editing. Do not revert user changes unless explicitly asked.
8. Continue from the highest-priority incomplete task in `TODO_WORKFLOW_API.md`.
9. Avoid redoing completed start/context/action endpoint work.
10. Preserve existing workflow behavior and generic metadata-driven engine boundaries.
11. Keep changes minimal and migration-friendly.
12. Run before final handoff:
   - `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false`
   - `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false`
13. If touching shared workflow execution behavior, also run:
   - set `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`
   - `dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false`
14. If doing live endpoint checks, either stop/restart Visual Studio API debugging or use a separate port with `ASPNETCORE_ENVIRONMENT=Development`.
