# Codex Handoff

Last updated: 2026-06-04

## Current goal: notification template feature

Add generic, SQL-metadata-driven email subject/body templates for workflow notifications while keeping the reusable engine free of Directed Trade hardcoding. The external API supplies business notification values as a generic `notificationContext` dictionary. SLA overdue processing reuses the stored task notification route/context and does not call any external API.

## Notification template feature completed

Commits:

```text
c26c7ad docs: add notification template design
b2d66f2 docs: plan notification templates
c102f32 feat: add notification template renderer
53fe91b feat: add notification template metadata
c1713f5 feat: accept workflow notification context
77a752a fix: handle duplicate notification context keys
020caae feat: render workflow action notifications
a475eb8 feat: render overdue notifications from task context
2f79dc4 fix: use legacy SLA route key fallback
b98bc17 fix: warn on invalid SLA notification context
f790bb8 feat: send persisted notification body
56fcb9c test: add notification context to samples
927248c test: serialize seed smoke SQL tests
```

Changed:

- Added route-specific `NotificationRoute.BodyTemplate` and `NotificationRoute.OverdueSubjectPrefix`.
- Added persisted `NotificationOutbox.Body`.
- Added `WorkflowTask.NotificationRouteKey` and `WorkflowTask.NotificationContextJson` for task notification snapshots.
- Seeded approved Directed Trade email templates as Portfolio Trade sample metadata only.
- Removed `returnReason` from all seeded notification templates.
- Added a simple generic `{{tokenName}}` renderer in application notifications.
- Added optional `workflow.notificationContext` to workflow action envelopes and workflow start requests.
- Added optional `notificationContext` to `POST /v1/WorkflowAction` and `POST /v1/WorkflowStart`.
- Included notification context in canonical request hashing.
- Normalized duplicate notification-context keys with a last-wins trim rule instead of throwing.
- Rendered action-created notification subject/body before outbox persistence.
- Stored rendered context in `NotificationOutbox.PayloadJson`.
- Snapshotted created task route/context for later SLA processing.
- Made missing template tokens return `ValidationFailed` and roll back action side effects.
- Made SLA overdue notifications render from stored task route/context and prefix the subject, for example `Overdue: Directed Trade Pending Checker Approval`.
- Preserved legacy `RouteSlaTaskOverdue` fallback for older pending tasks without stored notification snapshots.
- Made invalid stored SLA notification context warn and continue to later tasks.
- Added `Body` to `NotificationDelivery`.
- Made both outbox delivery paths map persisted `Body`.
- Made SMTP send the persisted body first while preserving legacy key/value payload fallback for rows with empty body.
- Added `workflow.notificationContext` to sample action JSON files `02` through `10`; `01-save-draft.json` remains unchanged.
- Added `[Collection("SqlServerWorkflowExecution")]` to `SeedDataSmokeTests` because one new seed test uses the shared phase4 SQL test database.

Files updated for the feature:

```text
src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs
src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs
src/WorkflowEngine.Application/Notifications/INotificationSender.cs
src/WorkflowEngine.Application/Notifications/NotificationTemplateRenderer.cs
src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs
src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs
src/WorkflowEngine.Infrastructure/Migrations/20260604120000_AddNotificationTemplates.cs
src/WorkflowEngine.Infrastructure/Migrations/20260604120000_AddNotificationTemplates.Designer.cs
src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs
src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs
src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs
src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs
src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRoute.cs
src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs
src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs
src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs
src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs
src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs
src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs
src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs
workflow-api/WORKFLOW_API_BOUNDARIES.md
workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs
workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs
workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs
workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs
workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs
workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs
workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs
tests/WorkflowEngine.Tests/NotificationTemplateRendererTests.cs
tests/WorkflowEngine.Tests/Phase5SlaTests.cs
tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs
tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs
tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs
tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs
tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs
samples/workflow-actions/02-submit-for-checker.json
samples/workflow-actions/03-checker-return-to-maker.json
samples/workflow-actions/04-maker-resubmit-for-checker.json
samples/workflow-actions/05-checker-send-to-trading-desk.json
samples/workflow-actions/06-trading-return-to-maker.json
samples/workflow-actions/07-trading-trade-executed.json
samples/workflow-actions/08-bdrm-trade-fulfilled.json
samples/workflow-actions/09-bdrm-trade-not-executed-properly.json
samples/workflow-actions/10-trading-rework-trade-executed.json
```

Final verification run:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "NotificationTemplateRendererTests|WorkflowTransitionTests|WorkflowStartInitialActionTests|Phase5SlaTests|SmtpNotificationSenderTests|SeedDataSmokeTests|SampleExpectationTests" /p:UseAppHost=false
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-NOTIFY-TEMPLATE --user maker1 --reset
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-NOTIFY-TEMPLATE --workflow-instance WF-PT-NOTIFY-TEMPLATE
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-NOTIFY-TEMPLATE
```

Final verification results:

- API build passed with 0 warnings and 0 errors.
- API tests passed: 41 passed, 0 failed, 0 skipped.
- Initial focused workflow test run hit sandboxed NuGet signature access (`NU1301`) and was rerun outside the sandbox.
- One focused workflow test rerun exposed a real test isolation issue: `SeedDataSmokeTests.NotificationRoutes_HaveSubjectBodyAndOverduePrefix` used the shared phase4 SQL test database without the `SqlServerWorkflowExecution` collection. Fixed in `927248c`.
- Final focused workflow test run passed: 30 passed, 0 failed, 0 skipped.
- Shared `src\WorkflowEngine.sln` build passed with 0 warnings and 0 errors.
- Console `init-db` passed against `cdt_workflow_api_20260527`.
- Console `verify-seed` passed.
- Console sample path passed:
  - `start-workflow PortfolioTrade PT-NOTIFY-TEMPLATE --user maker1 --reset`: success, workflow `WF-PT-NOTIFY-TEMPLATE` in `Draft`.
  - `execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-NOTIFY-TEMPLATE --workflow-instance WF-PT-NOTIFY-TEMPLATE`: success, `Draft -> CheckerReview`, `CheckerReviewTask` created, notification queued.
  - `show-notifications WF-PT-NOTIFY-TEMPLATE`: showed `WorkflowTaskAssigned`, recipient `Checker`, subject `Directed Trade Pending Checker Approval`.

Known risks / remaining work for this feature:

- The console `show-notifications` command displays subject and recipient, not body. Body rendering is covered by unit/integration tests and persisted `NotificationOutbox.Body`.
- SMTP delivery sends the persisted plain-text body, but no live smtp4dev run was performed for this feature slice.
- Existing local uncommitted changes remain in the older `api/src/WorkflowEngine.Api/appsettings*.json` files and unrelated untracked local tool/context folders. They were not modified by this feature.
- Hosted SLA service integration still needs production hosting decisions, but the reusable `RunSlaService` now has the needed stored-context behavior.

Next-session prompt:

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.md, PROJECT.md, CODEX_HANDOFF.md, and workflow-api/WORKFLOW_API_BOUNDARIES.md first. The notification template feature is implemented and verified through API builds/tests, focused workflow tests, seed verification, and a console submit sample. Preserve unrelated local appsettings/tool/context changes. If continuing this area, next useful work is live SMTP verification with smtp4dev or production hosting decisions for SLA/outbox processing.
```

## Project goal

- We are adjusting the existing `WorkflowEngine.WorkflowApi` ASP.NET Core API project.
- The goal is to align it with the conventions shown in `workflow-api/final-state-project.txt`.
- `WorkflowEngine.WorkflowApi` already exists. Do not recreate it from scratch.
- It is an intermediate migration API layer for workflow engine logic originally from a console app.
- Later, the code should be easy to move into the existing target API project.

## Source-of-truth references

- `PROJECT.md`: repository authority. Key guardrails: .NET 10, SQL Server, EF Core, generic metadata-driven workflow logic, `TimeProvider`, no `DateTime.UtcNow` outside composition root, no Portfolio Trade hardcoding in the reusable engine, no request JSON `toState`, no `workflow.options` metadata overrides, no CQRS/MediatR/event sourcing/BPMN/brokers/microservices/plugin frameworks, no `DemoUser` table or SQL user-role mapping.
- `AGENTS.md` / `AGENTS.MD`: repo-specific instructions. Requires reading `PROJECT.md` before planning/implementation and requires completion summary format.
- `workflow-api/final-state-project.txt`: main available reference for target API conventions. The root `final-state-project.txt` path was not present when checked; use the `workflow-api/` copy.
- `workflow-api/WORKFLOW_API_BOUNDARIES.md`: durable boundary decisions for workflow API behavior.
- `TODO_WORKFLOW_API.md`: current checklist of remaining workflow API migration tasks.
- `NEXT_CODEX_PROMPT.md`: ready prompt for a fresh session.
- `workflow-api/src/WorkflowEngine.WorkflowApi`: API project being aligned.
- `src/WorkflowEngine.Domain`, `src/WorkflowEngine.Application`, `src/WorkflowEngine.Infrastructure`: reusable workflow engine projects referenced by the API.
- `src/WorkflowEngine.Console`: CLI/demo/admin host. Do not move console command parsing into the API.

## What has already been done

### Program.cs

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Program.cs`

- What changed:
  - Added Swagger/OpenAPI and Swagger UI in Development.
  - Added `AddControllers().AddJsonOptions(...)` with `PropertyNamingPolicy = null` to match target casing behavior.
  - Added `AddExceptionHandler<GlobalExceptionHandler>()` and `AddProblemDetails()`.
  - Added CORS policy `_corsOrigins`, defaulting to `http://localhost:4200`.
  - Added `AddAuthorization()` / `UseAuthorization()`.
  - Kept `TimeProvider.System` registration at composition root.
  - Kept workflow engine registration through `AddWorkflowInfrastructure(configuration)`.
  - Registered API services:
    - `IWorkflowActorResolver`
    - `IWorkflowActionNotificationDispatcher`
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
  - Match the target API host style where safe without importing target-specific log4net/JWT/target EF context packages.
  - Enable local Swagger testing.
  - Centralize request error handling.
- Status: complete for current migration API shape.
- Behavior impact:
  - Swagger UI is available at app root in Development, for example `http://localhost:5058/` with the VS `http` profile.

### Controllers

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowStartController.cs`

- What changed:
  - Added `POST /v1/WorkflowStart`.
  - Accepts `WorkflowStartRequest`.
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
- Status: complete.
- Behavior impact:
  - Request body is only `workflowKey`, `businessObjectType`, and `businessObjectId`.
  - Starting user comes from trusted headers.
  - No email is sent by start unless a future metadata-backed start notification row is added.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowContextController.cs`

- What changed:
  - Refactored to a thin controller delegating to `IWorkflowContextQueryService`.
  - Kept only target-style route `GET /v1/WorkflowContext?workflowInstanceId=...`.
  - Removed older duplicate route `GET /api/workflow-instances/{workflowInstanceId}/context`.
  - Displays trusted actor headers in Swagger.
  - Response shape is `{ "result": { "workflowContext": ... } }`.
- Why:
  - Match target route/response style and boundary decisions.
  - Avoid duplicate Swagger entries.
- Status: complete.
- Behavior impact:
  - API consumers pass query parameter `workflowInstanceId`; actor comes from headers.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs`

- What changed:
  - Added `POST /v1/WorkflowAction`.
  - Accepts `WorkflowActionRequest`, not `WorkflowActionEnvelope`.
  - Displays trusted actor headers in Swagger.
  - Delegates to `IWorkflowActionService`.
  - Successful response shape is `{ "result": { "actionResult": ..., "workflowContext": ... } }`.
  - Returns HTTP 500 with the response body when workflow-level execution returns `Success = false`.
- Why:
  - Provide a workflow-only button-click endpoint that does not accept form payload, UI echo fields, business object IDs, or caller-provided idempotency/correlation IDs.
- Status: complete.
- Behavior impact:
  - Request body contains only `workflowInstanceId` and `actionKey`.
  - If action-created notification delivery fails, response becomes `NotificationDeliveryFailed` and HTTP 500.

### Services / dependency injection

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`

- What changed:
  - Resolves starting actor through `IWorkflowActorResolver`.
  - Validates required body fields.
  - Calls application `IStartWorkflowService.Start(...)` with `Reset = false`.
  - Passes optional `InitialActionKey` and trusted actor display/roles to the application start request.
  - Reads refreshed workflow context after successful start.
  - If start executed an initial action that queued metadata notifications, dispatches only rows created by that initial action before returning success.
  - Converts initial-action notification delivery failure into `NotificationDeliveryFailed`.
  - Wraps response as `{ result: { actionResult, workflowContext } }`.
- Why:
  - Keep controller thin and keep workflow start logic in the existing application/infrastructure service.
- Status: complete, including optional start-plus-initial-action behavior.

File: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`

- What changed:
  - Added optional `InitialActionKey`, `StartedByDisplayName`, and `StartedByRoles` to `StartWorkflowRequest`.
- Why:
  - Lets API callers start a workflow and immediately execute one metadata-valid first action using the same trusted actor.
- Status: complete.

File: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`

- What changed:
  - If `InitialActionKey` is blank, existing start-only behavior is preserved.
  - If `InitialActionKey` is present, creates the workflow instance and executes the initial action through `WorkflowExecutionService` inside one SQL transaction.
  - Uses API-style execution flags: derive identity from persistence, skip section-data persistence, skip UI echo validation, and no admin override.
  - Rolls back the newly-created workflow instance when the initial action fails before commit.
  - Returns the initial action result and refreshed post-action workflow context on success.
- Why:
  - Supports one-call Maker submit for brand-new forms without requiring a prior SaveDraft.
- Status: complete.

File: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- What changed:
  - `ExecuteSideEffects` now joins an existing EF Core transaction when one is already active, otherwise it keeps owning its own transaction.
- Why:
  - Allows start-plus-initial-action to be atomic while preserving standalone workflow action behavior.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowContextQueryService.cs`

- What changed:
  - Resolves actor through `IWorkflowActorResolver`.
  - Validates blank `workflowInstanceId`.
  - Calls `IWorkflowContextReader.Read(new WorkflowContextReadRequest(...))`.
- Why:
  - Move request-level context logic out of the controller.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`

- What changed:
  - Resolves actor through `IWorkflowActorResolver`.
  - Validates body contains only required workflow fields.
  - Builds an internal `WorkflowActionEnvelope` for the existing application execution service.
  - Generates internal `ClientActionId`, `CorrelationId`, and `IdempotencyKey`.
  - Calls `IWorkflowExecutionService.Execute(...)` with:
    - `DeriveIdentityFromPersistence = true`
    - `PersistSectionData = false`
    - `ValidateActionEcho = false`
    - `AdminOverride = false`
  - If a successful action queued a metadata notification, dispatches only the notification rows created by that action before returning success.
  - Uses action result `WorkflowInstanceId` and `CorrelationId` to select action-created outbox rows.
  - Converts delivery failure into `result.success = false`, `result.resultCode = "NotificationDeliveryFailed"`, and leaves controller HTTP 500 behavior in place.
- Why:
  - API must be workflow-only and must not accept/save form payload.
  - User clarified that in real SMTP environments, successful `POST /v1/WorkflowAction` must mean action-created notification rows were delivered.
- Status: complete.
- Behavior impact:
  - API action path does not persist `MockSectionData`.
  - API derives workflow key, definition version, and business object identity from persisted workflow instance data.
  - API action path does not process unrelated pending notification outbox rows.
  - Workflow DB transaction commits before SMTP delivery. If SMTP fails, the workflow may already have moved; the notification row remains `Pending` for retry.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowActionNotificationDispatcher.cs`

- What changed:
  - Added API-facing abstraction and result type for delivering notification rows created by a workflow action.
- Why:
  - Keep SMTP/outbox delivery out of the controller and make `WorkflowActionService` testable without SMTP.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs`

- What changed:
  - Added targeted outbox dispatcher.
  - Selects pending `NotificationOutbox` rows by workflow instance ID and correlation ID.
  - Claims rows with `Status = Processing`, sends through configured `INotificationSender`, marks rows `Sent`, and releases failed rows back to `Pending` with `LastError` and incremented retry count.
- Why:
  - Deliver only the metadata-created email rows from the current action; do not drain unrelated workflow notifications.
- Status: complete.
- Behavior impact:
  - Real SMTP delivery uses existing `Notifications:Sender = "Smtp"` and `Smtp:RoleEmailMap:{RecipientRoleKey}` configuration.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/TrustedHeaderWorkflowActorResolver.cs`

- What changed:
  - Trusted header adapter reads user ID, display name, and roles from configured headers.
- Why:
  - Temporary integration adapter until target auth/claims model is known.
- Status: complete as a temporary adapter.
- Behavior impact:
  - Do not expand this into real user lookup/auth/role mapping without target identity requirements.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowApiRequestException.cs`

- What changed:
  - Added exception carrying status/title/detail for `ProblemDetails`.
- Why:
  - Service layer can signal API request failures to global exception handler.
- Status: complete.

File: `src/WorkflowEngine.Application/WorkflowExecution/IWorkflowExecutionService.cs`

- What changed:
  - Added flags to `WorkflowExecutionRequest`: `DeriveIdentityFromPersistence`, `ValidateActionEcho`, `PersistSectionData`.
- Why:
  - Support API workflow-only action execution while preserving console behavior.
- Status: complete.
- Behavior impact:
  - Defaults preserve existing console behavior.

File: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- What changed:
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

- What changed: added workflow start request body (`WorkflowKey`, `BusinessObjectType`, `BusinessObjectId`) and optional `InitialActionKey`.
- Why: workflow start must attach an instance to an upstream business object, and `InitialActionKey` supports one-call start-and-submit flows.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartApiResponse.cs`

- What changed: added nested response shape `result.actionResult` and `result.workflowContext`.
- Why: match target response convention.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`

- What changed: added workflow-only action request body (`WorkflowInstanceId`, `ActionKey`).
- Why: public API action endpoint must not accept `WorkflowActionEnvelope`, form payload, UI echo fields, or caller IDs.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionApiResponse.cs`

- What changed: action response uses nested shape `result.actionResult` and `result.workflowContext`.
- Why: match target response convention and user clarification.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowContextApiResponse.cs`

- What changed: context response uses nested shape `result.workflowContext`.
- Why: match target response convention.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowApiResponse.cs`

- What changed earlier: generic response wrapper with JSON property `result`.
- Status: still present but not primary response for current endpoints. Review later and remove only if unused and not intended for future endpoints.

### Configuration

File: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.json`

- What changed:
  - Added CORS origins (`http://localhost:4200`).
  - Existing `WorkflowApi:ActorHeaders` remains.
  - Existing `Notifications:Sender = "NoOp"` remains.
- Why:
  - Support browser/client testing and target-style CORS policy.
- Status: partial; real target API CORS origins must be confirmed.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.Development.json`

- Current value:
  - `ConnectionStrings:DefaultConnection = Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`
  - `Notifications:Sender = "NoOp"`
- Why it matters:
  - API uses this DB for local Swagger/live tests unless environment variables override it.

File: `src/WorkflowEngine.Console/appsettings.Development.json`

- Current value:
  - Same local API DB connection string.
- Why:
  - Allows console prep commands (`init-db`, `verify-seed`, `start-workflow`, `show-notifications`, `process-notification-outbox`) to target the same DB as the API.
- Status: confirmed by user on 2026-06-01. Local API and console testing should use `cdt_workflow_api_20260527`.

Files:
- `api/src/WorkflowEngine.Api/appsettings.json`
- `api/src/WorkflowEngine.Api/appsettings.Development.json`

- What changed:
  - Aligned the older tracked `WorkflowEngine.Api` configuration to `Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.
- Why:
  - Prevent either tracked API host from accidentally targeting `burrito` or `cdt_workflow` while local tests are run against `cdt_workflow_api_20260527`.
- Status: complete.

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`

- What changed:
  - Earlier normalized launch settings to project-only `http` and `https` profiles.
  - User/VS later added SMTP environment variables to the `http` profile for Swagger/manual testing.
  - Current `http` profile includes:
    - `Notifications__Sender = Smtp`
    - `Smtp__Host = localhost`
    - `Smtp__Port = 3025`
    - `Smtp__From = workflow@example.test`
    - `Smtp__RoleEmailMap__Checker = cdt-checkers@example.test`
    - `Smtp__RoleEmailMap__TradingDesk = cdt-trading@example.test`
    - `Smtp__RoleEmailMap__BDRMClientService = cdt-bdrm@example.test`
    - `Smtp__RoleEmailMap__Maker = cdt-makers@example.test`
- Why:
  - Supports Visual Studio Swagger testing against smtp4dev.
- Status: needs review before commit.
- Behavior impact:
  - This makes the VS `http` profile use SMTP instead of `NoOp`.
  - Verify `Smtp__Port` against the actual smtp4dev listener before relying on it. Current `launchSettings.json` shows `3025`.

### Dynamic Trading Desk Assignment

- User approved dynamic trading desk assignment.
- Upstream business/form API supplies `assignedRoleKey`.
- Supported assignment role keys are `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`.
- Broad `TradingDesk` remains the actor role for Trading Desk actions.

### Logging / error handling

File: `workflow-api/src/WorkflowEngine.WorkflowApi/Infrastructure/GlobalExceptionHandler.cs`

- What changed:
  - Added centralized exception handling using `IExceptionHandler` and `IProblemDetailsService`.
  - Maps `WorkflowApiRequestException`, `WorkflowActorResolutionException`, `KeyNotFoundException`, and generic exceptions.
- Why:
  - Align with centralized API error handling rather than per-controller try/catch.
- Status: complete for current request/actor/context errors.
- Behavior impact:
  - Workflow-level failed action/start results are currently returned by controllers as HTTP 500 with the workflow response body, per user clarification.

### Documentation

File: `workflow-api/WORKFLOW_API_BOUNDARIES.md`

- What changed:
  - Added durable boundary file for future `WorkflowEngine.WorkflowApi` work.
  - Records workflow-only scope and out-of-scope areas.
  - Records temporary trusted-header actor adapter decision.
  - Records exact request/response decisions for start/context/action endpoints.
  - Records that `POST /v1/WorkflowAction` delivers only action-created notification rows before returning success.
  - Records that `POST /v1/WorkflowStart` sends no email unless a future metadata-backed start notification exists.
- Why:
  - Prevent future sessions from expanding workflow API into form/user/business/admin responsibilities.
- Status: complete and should be read before future API changes.

Files:
- `CODEX_HANDOFF.md`
- `NEXT_CODEX_PROMPT.md`
- `TODO_WORKFLOW_API.md`

- What changed:
  - Updated durable handoff, next-session prompt, and task checklist.
- Status: current as of this handoff.

### Tests / build changes

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowStartControllerTests.cs`

- What changed: tests successful start response and workflow-level failure returning HTTP 500.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`

- What changed: tests trusted actor use, `Reset = false`, context read on success, no context read on failed start, missing body/field validation, initial-action request mapping, and initial-action notification dispatch/failure behavior.
- Status: complete.

File: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`

- What changed: added SQL-backed coverage for start plus `SubmitForChecker` and rollback on invalid initial action.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowActionControllerTests.cs`

- What changed: tests thin action controller response and HTTP 500 for workflow-level failures.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`

- What changed:
  - Tests workflow-only action request mapping, generated IDs, trusted actor use, internal execution flags, and missing request/field validation.
  - Tests dispatching action-created notifications by workflow instance ID/correlation ID.
  - Tests skipping dispatch when an action queued no notification.
  - Tests converting notification delivery failure into `NotificationDeliveryFailed`.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowContextControllerTests.cs`

- What changed: updated to expect `WorkflowContextApiResponse` nested shape.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowContextQueryServiceTests.cs`

- What changed: tests actor resolution, trimmed workflow instance ID, blank ID request error, actor/reader exception flow.
- Status: complete.

File: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Startup/WorkflowApiProjectStructureTests.cs`

- What changed:
  - Verifies three controllers/routes: `v1/workflowstart`, `v1/workflowcontext`, `v1/workflowaction`.
  - Verifies old REST-style context route is absent.
  - Verifies no Minimal API `MapGet`/`MapPost`, no console command terms, no controller body `WorkflowActionEnvelope`.
  - Verifies DI registration for current workflow API services, including `IWorkflowActionNotificationDispatcher`.
  - Verifies JSON naming policy remains null.
- Status: complete.

### Deleted or renamed files

- No existing source files were deleted or renamed.
- Two new source files were added:
  - `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowActionNotificationDispatcher.cs`
  - `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs`

## Live/manual testing completed

### Earlier live SQL/API check

- `dotnet run --project src\WorkflowEngine.Console -- init-db`: passed.
- `dotnet run --project src\WorkflowEngine.Console -- verify-seed`: passed.
- Live Swagger exposed `/v1/WorkflowStart`, `/v1/WorkflowContext`, `/v1/WorkflowAction`.
- Live start/context/action sequence using `PT-API-START` passed:
  - start returned workflow instance `WF-PT-API-START`
  - context returned `Draft`
  - action moved `Draft -> CheckerReview` and created `CheckerReviewTask`

### Live smtp4dev/API SMTP check completed

- Installed smtp4dev temporarily as local .NET tool (`Rnwood.Smtp4dev` 3.15.0), started SMTP on `localhost:3025` and web/API on `http://localhost:5000`.
- Started `WorkflowEngine.WorkflowApi` on `http://localhost:5069` with `Notifications:Sender = Smtp`, `Smtp:Host = localhost`, `Smtp:Port = 3025`, and role mappings for Maker/Checker/TradingDesk/BDRMClientService.
- Ran `POST /v1/WorkflowStart` for `PT-SMTP-20260528235240`; response success.
- Ran `POST /v1/WorkflowAction` with `SubmitForChecker`; response success, notification `WorkflowTaskAssigned`, recipient role `Checker`.
- Verified smtp4dev captured message `78b92fb7-5cf5-4aab-b933-89b0e807c355` containing `BusinessObjectId: PT-SMTP-20260528235240` and recipient `cdt-checkers@example.test`.
- Verified `show-notifications WF-PT-SMTP-20260528235240` showed notification `#10002 WorkflowTaskAssigned | status=Sent | recipient=Checker`.
- Stopped both background processes and removed temporary smtp4dev files/logs.

### User manual Swagger/SMTP checks completed after that

- User launched API from Visual Studio/Swagger with smtp4dev running.
- User first hit `NotificationDeliveryFailed` because API could not connect to SMTP; workflow still moved because workflow DB transaction commits before SMTP send.
- Root cause explained: smtp4dev web UI port is not SMTP port. smtp4dev output showed SMTP listening on port `25` and web UI on `http://localhost:5000`.
- User proceeded and reported:
  - Return to Maker on `WF-PT-SWAGGER-001XX` passed through Swagger using Checker role.
  - Console `process-notification-outbox` SMTP path is working as well.

### User manual start-with-initial-action API/SMTP check

- Installed `Rnwood.Smtp4dev` 3.15.0 repo-locally under `.tools\smtp4dev`.
- Started smtp4dev with SMTP on `localhost:3025` and web UI on `http://localhost:5000`.
- Verified both ports were reachable.
- User reported the manual API tests passed for the latest start-with-initial-action workflow behavior.

## Current git/worktree status

Commands run during this handoff update:

```powershell
git status --short --untracked-files=all
git diff --stat
git diff --name-only
```

Summary at the time of inspection:

```text
 M api/src/WorkflowEngine.Api/appsettings.Development.json
 M api/src/WorkflowEngine.Api/appsettings.json
?? .tools/smtp4dev/...
?? docs/superpowers/plans/2026-06-01-dynamic-trading-desk-assignment.md
?? ~/.codex/superpowers/
```

`git diff --stat` summary:

```text
 api/src/WorkflowEngine.Api/appsettings.Development.json | 2 +-
 api/src/WorkflowEngine.Api/appsettings.json             | 2 +-
 2 files changed, 2 insertions(+), 2 deletions(-)
```

Tracked files changed:

```text
api/src/WorkflowEngine.Api/appsettings.Development.json
api/src/WorkflowEngine.Api/appsettings.json
```

Notes:

- `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.Development.json` and `src/WorkflowEngine.Console/appsettings.Development.json` already target `cdt_workflow_api_20260527`.
- The older tracked `api/src/WorkflowEngine.Api` config now also targets `cdt_workflow_api_20260527`.
- Dynamic Trading Desk assignment commits are complete through `80a9d3b docs: record dynamic trading desk assignment contract`.
- `.tools/smtp4dev/...`, `docs/superpowers/plans/2026-06-01-dynamic-trading-desk-assignment.md`, and `~/.codex/superpowers/` are untracked. Do not add them unless explicitly requested.

## Current build/test status

Commands run for the DB alignment update:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -c Release -m:1 /p:UseAppHost=false
dotnet build src\WorkflowEngine.sln -c Release -m:1 /p:UseAppHost=false
dotnet build api\WorkflowEngine.Api.sln -c Release -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -c Release -m:1 /p:UseAppHost=false
dotnet test api\WorkflowEngine.Api.sln -c Release -m:1 /p:UseAppHost=false
```

Results:

- Initial Debug build of `workflow-api\WorkflowEngine.WorkflowApi.slnx` failed because a running Visual Studio/API process locked Debug DLLs.
- After the user stopped the running process, Debug build of `workflow-api\WorkflowEngine.WorkflowApi.slnx` passed with 0 warnings and 0 errors.
- Debug tests for `workflow-api\WorkflowEngine.WorkflowApi.slnx` passed: 38 passed, 0 failed, 0 skipped.
- Release build of `workflow-api\WorkflowEngine.WorkflowApi.slnx` passed with 0 warnings and 0 errors.
- Release build of `src\WorkflowEngine.sln` passed with 0 warnings and 0 errors.
- Release build of `api\WorkflowEngine.Api.sln` passed with 0 warnings and 0 errors.
- Release tests for `workflow-api\WorkflowEngine.WorkflowApi.slnx` passed: 38 passed, 0 failed, 0 skipped.
- Release tests for `api\WorkflowEngine.Api.sln` passed: 38 passed, 0 failed, 0 skipped.

Commands run for the latest start-with-initial-action work:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowStartInitialActionTests -m:1 /p:UseAppHost=false
```

Results:

- Build passed with 0 warnings and 0 errors.
- API test suite passed: 38 passed, 0 failed, 0 skipped.
- Focused SQL-backed start initial action tests passed: 2 passed, 0 failed, 0 skipped.

Broader suite verification attempted with:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
```

Result:

- The command timed out twice, once at 5 minutes and once at 10 minutes.
- Hang diagnostics on `tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj` reached 202 passed and 2 skipped before the blame collector aborted while `WorkflowEngine.Tests.FreshCloneWalkthroughTests.Demo_RunsSuccessfullyAfterInitDb` was active.
- The isolated `FreshCloneWalkthroughTests.Demo_RunsSuccessfullyAfterInitDb` test also timed out.
- Stale `dotnet`/`testhost`/MSBuild processes from the timed-out runs were stopped; no test processes remained afterward.
- The new feature's focused SQL-backed tests passed, so the remaining verification gap is isolated to the existing fresh-clone walkthrough demo shell-out test path.

Commands run for the dynamic Trading Desk assignment work:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SeedDataSmokeTests /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "WorkflowTransitionTests|WorkflowExecutionValidationTests" /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "SeedDataSmokeTests|WorkflowTransitionTests|WorkflowExecutionValidationTests" /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Results:

- Seed data smoke tests passed: 4 passed, 0 failed.
- Focused dynamic assignment engine tests passed during implementation: 28 passed, 0 failed.
- Workflow API tests passed: 39 passed, 0 failed, 0 skipped.
- Final focused workflow engine tests passed: 32 passed, 0 failed, 0 skipped.
- `workflow-api\WorkflowEngine.WorkflowApi.slnx` build passed with 0 warnings and 0 errors.
- `src\WorkflowEngine.sln` build passed with 0 warnings and 0 errors.
- Console `init-db` passed against `cdt_workflow_api_20260527`.
- Console `verify-seed` passed.
- Console sample path using `PT-DESK-SAMPLE` passed and `show-tasks` confirmed `TradingDeskReviewTask` pending with role `TradingDeskTaxable`.

## Important decisions made

- Match `workflow-api/final-state-project.txt` only where safe:
  - controller-based APIs
  - `v1/...` routes
  - nested `result` wrappers
  - Swagger UI for local testing
  - no target-specific log4net/JWT/target DbContext without full target source
- Controller routing:
  - `POST /v1/WorkflowStart`
  - `GET /v1/WorkflowContext`
  - `POST /v1/WorkflowAction`
  - removed older REST-style context route
- Response style:
  - start/action success: `{ result: { actionResult, workflowContext } }`
  - context success: `{ result: { workflowContext } }`
  - workflow-level start/action failures: HTTP 500 with same API response body
- Controllers stay thin and delegate to API-facing services.
- API services adapt HTTP/body/header inputs to existing application workflow services.
- Business logic remains in existing application/infrastructure workflow services.
- `WorkflowActionRequest` intentionally contains only `workflowInstanceId` and `actionKey`.
- API action path internally generates client action ID, correlation ID, and idempotency key.
- API action path derives workflow/business identity from persisted workflow instance data.
- API action path does not accept or persist form data.
- `POST /v1/WorkflowAction` delivers only metadata notification rows created by the current action before returning success.
- SMTP recipient resolution stays role/group-based through `Smtp:RoleEmailMap`; no individual user lookup was added.
- `POST /v1/WorkflowStart` sends no email unless start later creates metadata-backed notification rows.
- `POST /v1/WorkflowStart` can optionally accept `initialActionKey` to start and immediately execute a metadata-valid first action.
- Start plus initial action returns the initial action result and refreshed post-action workflow context.
- Start plus invalid initial action rolls back the newly-created workflow instance before returning the workflow failure result.
- `CheckerSendToTradingDesk` supports dynamic task assignment through `assignedRoleKey`; valid values are `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`.
- Broad `TradingDesk` remains the actor role authorized to execute later Trading Desk actions, even when the task is assigned to a specific desk role.
- Trusted-header actor adapter remains temporary; no real auth/JWT/claims/user-role DB mapping has been added.
- Workflow API remains workflow-only; no form validation, business object lifecycle, public console admin behavior, or deployment orchestration endpoints were added.

## Open issues / remaining work

- [ ] Review `launchSettings.json` SMTP values before commit.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
      Why it matters:
      It now contains local SMTP testing environment variables. Current file shows `Smtp__Port = 3025`.
      Suggested next step:
      Confirm the intended local smtp4dev port and decide whether these local SMTP env vars should be committed, moved to user-local settings, or documented only.
      Risk level: Medium

- [ ] Confirm unused/legacy response wrapper cleanup.
      Files involved:
      `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowApiResponse.cs`
      Why it matters:
      Current endpoints use specialized nested response DTOs. The generic wrapper may be unused.
      Suggested next step:
      Search usages and remove only if unused and not intended for future endpoints.
      Risk level: Low

- [ ] Add or verify live Swagger instructions/documentation.
      Files involved:
      README or workflow-api docs if desired.
      Why it matters:
      User is testing manually through Swagger and needs stable sample bodies, headers, and smtp4dev setup steps.
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
      Console `run-sla` and `process-notification-outbox` are operational jobs, not normal public form APIs. Pending notifications after SMTP failure need a retry runner.
      Suggested next step:
      Choose scheduled job, hosted service, Windows service, job runner, or protected internal admin endpoint.
      Risk level: Medium

- [ ] Replace trusted headers with real target API identity integration when target auth model is known.
      Files involved:
      `TrustedHeaderWorkflowActorResolver`, `IWorkflowActorResolver`, target API auth code.
      Why it matters:
      Trusted headers are local/test integration only unless secured by upstream infrastructure.
      Suggested next step:
      Map target claims/roles/profile data to `WorkflowActorSnapshot` after target auth is available.
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
- API response inner application DTOs serialize with PascalCase because JSON property naming policy is null, matching target sample behavior.
- HTTP 500 for workflow-level failures was recorded from user clarification as "custom error code 500"; confirm exact production error semantics before integrating with target exception middleware.
- SMTP send happens after workflow DB commit. If SMTP fails, workflow state may already be moved and the outbox row remains `Pending` for retry. This was explained to the user.
- `process-notification-outbox` is currently manual/console-driven unless production chooses a scheduled/background runner.
- Live API checks used local DB `cdt_workflow_api_20260527` on `Server=DOLLY`.
- Manual Swagger/smtp4dev tests created workflow/runtime rows in the local dev DB, including `WF-PT-SWAGGER-001XX`.
- Current worktree has uncommitted config/doc changes and untracked local smtp4dev tool files.
- `workflow-api/final-state-project.txt` is untracked/reference input and should remain a reference unless user wants it committed.
- Some command probes intermittently hit a sandbox `spawn setup refresh` error; rerunning or using already-approved/escalated commands has worked.

## How to continue in a new Codex session

1. Read `AGENTS.md` / `AGENTS.MD` if present.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `CODEX_HANDOFF.md`.
4. Read `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
5. Read `workflow-api/final-state-project.txt`.
6. Inspect:
   - `git status --short --untracked-files=all`
   - `git diff --stat`
   - `git diff --name-only`
7. Review changed and untracked files before editing. Do not revert user changes unless explicitly asked.
8. Continue from the highest-priority incomplete task in `TODO_WORKFLOW_API.md`.
9. Avoid redoing completed start/context/action endpoint work.
10. Preserve existing workflow behavior and generic metadata-driven engine boundaries.
11. Keep changes minimal and migration-friendly.
12. Before final handoff, run:
   - `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false`
   - `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false`
13. If touching shared workflow execution behavior, also run:
   - set `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`
   - `dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false`
14. If doing live endpoint checks, stop/restart Visual Studio API debugging or use a separate port with `ASPNETCORE_ENVIRONMENT=Development`.
15. If testing SMTP manually:
   - smtp4dev web UI is `http://localhost:5000`.
   - SMTP port is whatever smtp4dev logs as "SMTP Server is listening on port ..."; configure `Smtp__Port` to that value.
   - Use fresh business object IDs for state-changing Swagger tests.
