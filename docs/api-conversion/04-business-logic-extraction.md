# Business Logic Extraction

This document classifies important code areas and explains what should be reused directly, wrapped in the API, kept for console behavior reference, or excluded from production API runtime.

## Classification Summary

| Current File / Class | Classification | Use In API? | Recommended API Role | Required Changes | Reason |
|---|---|---|---|---|---|
| `WorkflowActionEnvelope` | Reusable application contract | Yes, with actor stamping | API request body or inner body model for execute action. | Overwrite `Actor` from authenticated user before validation and hashing. Ignore `TestExpectations`. | It is the standard button-click envelope. |
| `WorkflowContextResponse` | Reusable application contract | Yes | Return from context endpoints. | None required. | It is the form-open workflow context contract. |
| `WorkflowActionResultEnvelope` | Reusable application contract | Yes | Return for workflow business outcomes. | Map result code to HTTP status. | Existing result shape carries workflow outcome and diagnostics. |
| `AvailableWorkflowAction` | Reusable application contract | Yes | Included in `WorkflowContextResponse`. | None required. | Derived from SQL transition/action metadata. |
| `WorkflowActionEnvelopeValidator` | Reusable application helper/service | Yes | Validate stamped envelopes. | Register in DI. | Enforces required blocks and MakerPrimary consistency. |
| `WorkflowActionEnvelopeNormalizer` | Reusable application helper/service | Yes | Normalize effective IDs and optional override behavior. | In API, usually call with no business-object/workflow-instance CLI overrides. | Computes effective client action/correlation/idempotency values. |
| `CanonicalRequestHasher` | Reusable application helper/service | Yes | Compute request hash after actor stamping. | Ensure actor is overwritten first. | Idempotency conflict detection depends on this hash. |
| `WorkflowActionResultBuilder` | Reusable application helper/service | Yes | Build validation and start/execution result envelopes. | Register in DI. | Used by services and controller validation path. |
| `CanonicalWorkflowResultSerializer` | Reusable application helper/service | Yes | Persist replayable result JSON and hashes. | Register in DI. | Used by `WorkflowExecutionService`. |
| `IWorkflowExecutionService` / `WorkflowExecutionService` | Reusable infrastructure service | Yes | Main execute-action service. | Register via `AddWorkflowInfrastructure`; call from API service/controller after validation/hash. | Owns metadata-driven execution, transaction, idempotency, concurrency, audit, outbox, section data save. |
| `IStartWorkflowService` / `StartWorkflowService` | Reusable infrastructure service | Yes | Start workflow endpoint service. | For normal users pass `Reset=false`. Gate reset, if exposed. | Creates deterministic `WF-{businessObjectId}` instances and handles duplicates. |
| `IWorkflowContextReader` / `WorkflowContextReader` | Reusable infrastructure service | Yes | Context endpoint service. | Use roles from authenticated actor. | Builds form-open context, available actions, SLA block. |
| `IWorkflowInspectionReader` / `WorkflowInspectionReader` | Reusable infrastructure service | Optional | Admin/inspection endpoints. | Gate with authorization. | Reads instance, tasks, history, notifications without exposing EF entities. |
| `WorkflowDbContext` | Persistence/database component | Yes | Registered DbContext. | Do not inject into controllers unless admin/internal service has clear reason. | EF Core model for workflow schema. |
| `WorkflowDatabaseInitializer` | Seed/verification component | Deployment/admin only | Deployment operation or admin-only endpoint if allowed. | Do not run on every request. | Applies migrations and seed data. |
| `WorkflowSeedService` | Seed/verification component | Deployment/admin only | Used by initializer. | Keep internal infrastructure. | Upserts Portfolio Trade metadata. |
| `SeedVerificationService` | Seed/verification component | Deployment/admin only | Deployment check or admin-only endpoint. | Gate if exposed. | Verifies seed metadata and transition matrix. |
| `IRunSlaService` / `RunSlaService` | Notification/SLA component | Yes | Hosted service, external job, or admin endpoint. | Use scoped service from a scope. | Scans overdue pending tasks and queues one notification per task. |
| `IProcessNotificationOutboxService` / `ProcessNotificationOutboxService` | Notification/SLA component | Yes | Hosted service, external job, or admin endpoint. | Use scoped service from a scope. | Claims pending rows before sending and marks sent. |
| `INotificationSender` implementations | Notification/SLA component | Yes | Sender chosen by `Notifications:Sender`. | Use `NoOp`/fake in tests; use `Smtp` only with valid config. | Abstracts notification delivery. |
| `CommandRegistry` | Console-only command layer | No | Behavior reference only. | Do not register in normal API runtime. | Dispatches CLI commands. |
| `CommandArgumentParser` | Console-only command layer | No | Behavior reference only. | Translate args to routes/body/query manually. | Parses CLI strings, not HTTP. |
| Command handlers | Console-only command layer | Normally no | Behavior reference only. | Controllers should call underlying services directly. | They read files and write console text. |
| `DemoCommandHandler`, `DemoScriptDefinitions` | Demo/sample-only component | No production use | Local demo only. | Do not expose in production API. | Uses sample files and reset behavior. |
| `samples/workflow-actions` | Demo/test fixtures | Yes, as fixtures | API regression/manual test inputs. | Strip or ignore `testExpectations` in production. | Canonical sample envelopes. |
| `tests/WorkflowEngine.Tests` | Test-only component | No runtime use | Behavior reference and library regression suite. | Add API tests separately. | Verifies reusable engine behavior. |

## Core Contracts

### `WorkflowActionEnvelope`

What it does: Models one user button click. It contains request metadata, business object identity, actor, UI action, section payload, workflow action/guards/options, and optional sample `testExpectations`.

What depends on it:

- `ExecuteJsonCommandHandler`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowExecutionService`
- sample/test code

What it depends on: `System.Text.Json` for `JsonElement` payload.

API use:

- Accept it directly or wrap it in an API-specific request.
- Before validation and hashing, overwrite `Actor.UserId`, `Actor.DisplayName`, and `Actor.Roles` from authenticated API context.
- Do not allow a request-supplied `toState`; the contract has no `toState` field.
- Do not let `workflow.options` override metadata.
- Ignore or reject `TestExpectations` outside tests.

### `WorkflowContextResponse`

What it does: Returns current workflow state, business object identity, actor access, current task, SLA status, available metadata-derived actions, and server time.

API use:

- Return directly from context endpoints.
- Build via `IWorkflowContextReader.Read(new WorkflowContextReadRequest(workflowInstanceId, userId, roles))`.
- Supply `userId` and `roles` from authentication, not query/body in production.

### `WorkflowActionResultEnvelope`

What it does: Returns action outcome, result code, idempotency details, old/new state, task changes, notification/audit summaries, and optional refreshed context.

API use:

- Return for workflow business results.
- Map `Result.ResultCode` to HTTP status through a `WorkflowHttpResultMapper`.
- Use `ProblemDetails` for malformed HTTP/model-binding/startup errors that occur before a workflow result can be built.

### `AvailableWorkflowAction`

What it does: Represents an action the current actor can perform from the current state. It includes section/button presentation hints and `WorkflowActionRequestDefaults`.

API use:

- Expose inside `WorkflowContextResponse`.
- Do not hardcode these in controllers or UI.

## Helper Services

### `WorkflowActionEnvelopeValidator`

Reusable. Register as scoped. It validates:

- `request.commandType`
- `request.clientActionId`
- `request.correlationId`
- `businessObject.objectType`
- `businessObject.objectId`
- `actor.userId`
- `actor.roles`
- `uiAction.sectionKey`
- `uiAction.buttonKey`
- `dataSave.sectionKey`
- `dataSave.payload` as JSON object
- `workflow.workflowKey`
- `workflow.workflowInstanceId`
- `workflow.actionKey`
- MakerPrimary `saveAsDraft` consistency

API order: actor stamping first, then validator.

### `WorkflowActionEnvelopeNormalizer`

Reusable. Register as scoped. It applies console override behavior and derives effective IDs. In the API, pass `null` for `businessObjectOverride` and `workflowInstanceOverride` unless you deliberately support route overrides. Prefer explicit mismatch checks instead of silently overriding route/body identities.

### `CanonicalRequestHasher`

Reusable. Register as scoped. It hashes the normalized effective request. It includes actor user ID and roles, business object, payload, action, guards, definition version, and workflow identity. It excludes request timestamps and correlation metadata.

API order: actor stamping, normalization, validation, then hashing.

### `WorkflowActionResultBuilder`

Reusable. Register as scoped. It builds standard result envelopes for validation failures, not found, invalid transition, forbidden, ambiguous role, concurrency conflict, idempotency conflict, already exists, start success, and execution success.

### `CanonicalWorkflowResultSerializer`

Reusable. Register as scoped. `WorkflowExecutionService` uses it to compute result hashes and persist replayable `ResultJson`.

## Reusable Runtime Services

### `IWorkflowExecutionService` / `WorkflowExecutionService`

Call directly from an API application service or controller after:

1. Model binding.
2. Authenticated actor resolution.
3. Actor stamping.
4. Normalization.
5. Validation.
6. Request hashing.

It handles:

- Idempotency replay and conflict.
- Persisted instance lookup.
- Workflow/business object identity validation.
- Workflow definition version mismatch.
- Completed-state rejection.
- Action metadata lookup.
- UI/data section echo validation against metadata.
- Transition lookup.
- Concurrency guards.
- Role authorization and ambiguous role detection.
- Console-only admin override if passed as `AdminOverride=true`.
- Section data save into `MockSectionData`.
- State change and task close/create.
- SLA due date assignment.
- Notification outbox queue.
- Audit write.
- Idempotency result persistence in the transaction.

Do not reproduce this logic in controllers.

### `IStartWorkflowService` / `StartWorkflowService`

Call for start workflow. It creates deterministic instance ID `WF-{businessObjectId}` and starts at `Draft` with version `1`. It returns `AlreadyExists` for existing active or completed lifetimes.

API rule: normal user requests should pass `Reset=false`. If reset is exposed at all, put it behind strict admin and environment gates.

### `IWorkflowContextReader` / `WorkflowContextReader`

Call for form-open context. It reads persisted state and pending task, derives available actions from transition/action metadata for the actor roles, and computes SLA as `NoSla`, `OnTrack`, or `Overdue`.

### `IWorkflowInspectionReader` / `WorkflowInspectionReader`

Optional. Use for admin/operations endpoints. It returns records:

- `WorkflowInspectionSnapshot`
- `WorkflowInstanceInspection`
- `WorkflowTaskInspection`
- `WorkflowAuditInspection`
- `NotificationInspection`

These are read models, not EF entities.

## Persistence And Database Components

### `WorkflowDbContext`

Use through DI. Do not manually instantiate in controllers.

Tables include:

- `WorkflowDefinition`
- `WorkflowDefinitionVersion`
- `WorkflowStateDefinition`
- `WorkflowActionDefinition`
- `WorkflowRoleDefinition`
- `WorkflowTaskDefinition`
- `WorkflowTransitionDefinition`
- `WorkflowInstance`
- `WorkflowTask`
- `MockSectionData`
- `WorkflowAudit`
- `SlaPolicy`
- `NotificationRoute`
- `NotificationOutbox`
- `IdempotencyRecord`

Important constraints include:

- Unique `WorkflowInstance.WorkflowInstanceId`.
- Unique workflow lifetime by `(WorkflowKey, BusinessObjectType, BusinessObjectId)`.
- Filtered unique pending task index on `WorkflowTask.WorkflowInstanceId` where `Status = 'Pending'`.
- Filtered unique SLA overdue notification index on `(WorkflowTaskId, NotificationType)` where `NotificationType = 'SlaTaskOverdue'`.
- Check constraints for workflow instance, task, and outbox status values.
- Foreign keys from runtime rows to `WorkflowInstance`.

### Seed And Verification

Use:

- `WorkflowDatabaseInitializer`
- `WorkflowSeedService`
- `SeedVerificationService`

for deployment/admin operations only. Do not run migrations/seeding on every request.

`PortfolioTradeSeedData` is internal infrastructure seed data. It is not a production API service and should not become hardcoded controller behavior.

## Notification And SLA Components

### `IRunSlaService` / `RunSlaService`

Use in a hosted service, external scheduled job, or admin-only endpoint. It queues `SlaTaskOverdue` notifications for overdue pending tasks with SLA due dates. It does not send notifications.

### `IProcessNotificationOutboxService` / `ProcessNotificationOutboxService`

Use in a hosted service, external scheduled job, or admin-only endpoint. It claims rows by moving them to `Processing`, sends through `INotificationSender`, marks `Sent`, and releases claims with retry data on failure.

### `INotificationSender` Implementations

- `ConsoleNotificationSender`: development/diagnostic only for API unless explicitly accepted.
- `NoOpNotificationSender`: tests and dry runs.
- `SmtpNotificationSender`: production-capable SMTP sender using MailKit, configured by `Notifications:Sender = Smtp`.

`SmtpNotificationSender` requires `Smtp:Host` and `Smtp:From`. Role recipients come from `Smtp:RoleEmailMap` unless `Smtp:RecipientOverride` is configured.

## Console-Only Components To Exclude

Exclude from normal API runtime:

- `src/WorkflowEngine.Console/Program.cs`
- `CommandRegistry`
- `CommandArgumentParser`
- `ExecuteJsonCommandHandler`
- `StartWorkflowCommandHandler`
- `ShowContextCommandHandler`
- `ShowCommandHandler`
- `ShowTasksCommandHandler`
- `ShowHistoryCommandHandler`
- `ShowNotificationsCommandHandler`
- `RunSlaCommandHandler`
- `ProcessNotificationOutboxCommandHandler`
- `DemoCommandHandler`
- `DemoScriptDefinitions`
- `WorkflowEngineDemoStepRunner`

Use them as behavior references only.

## Demo And Sample Components

Keep `samples/workflow-actions` as fixtures for manual smoke tests and API regression tests. Do not expose `testExpectations` as part of a public production request contract.

`MockSectionData` must remain clearly identified as POC/demo storage unless the real API deliberately replaces it with production form-section persistence inside the same workflow transaction boundary.

