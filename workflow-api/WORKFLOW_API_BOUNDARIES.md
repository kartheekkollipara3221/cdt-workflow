# Workflow API Boundaries

This file is the implementation boundary for future work in `WorkflowEngine.WorkflowApi`.
Use it before adding endpoints, services, validation, or integration code.

## Core Rule

`WorkflowEngine.WorkflowApi` handles workflow matters only.

It must expose workflow capabilities from the reusable workflow engine without becoming the form API, identity API, business object API, admin console, or deployment tool.

## In Scope

- Return workflow context for a workflow instance.
- Start a workflow instance for a business object identity supplied by the upstream form/business API.
- Execute a workflow action from a workflow-only API request DTO.
- Validate workflow-envelope invariants required for safe workflow execution.
- Enforce workflow metadata rules through the existing application/infrastructure workflow services.
- Persist workflow runtime effects through the workflow engine:
  - workflow state changes
  - task close/create behavior
  - SLA-related task due dates
  - notification outbox rows
  - SMTP delivery of notification outbox rows created by the current workflow action
  - workflow audit rows
  - idempotency records
- Return workflow response contracts such as `WorkflowContextResponse` and `WorkflowActionResult`.

## Out Of Scope

- Form field validation.
- Business-object validation.
- Portfolio Trade business rules outside workflow metadata.
- User lookup, authentication, authorization policy evaluation, or profile resolution.
- User-to-role database mapping.
- Business object creation or update APIs.
- UI-specific button rendering rules beyond returning workflow metadata needed by the UI.
- Console setup/admin operations as public user APIs, including:
  - `init-db`
  - `verify-seed`
  - console `start-workflow --reset`
  - `run-sla`
  - `process-notification-outbox`
  - `demo`
- Production database migration orchestration.

## Form Data Boundary

The workflow API must not validate, save, or persist form/section payload data.

The upstream form/business API owns form data persistence. `WorkflowEngine.WorkflowApi`
executes workflow state, task, notification, audit, SLA, and idempotency behavior only.

Allowed:

- Require the workflow envelope blocks needed by workflow execution.
- Accept workflow action identity such as action key, workflow instance, expected state/version/task guards, and actor evidence.
- Accept idempotency and correlation identifiers needed for safe workflow execution.

Not allowed:

- Validate Portfolio Trade field names or values.
- Add form completeness rules.
- Reject or modify payload fields based on business meaning.
- Use payload field values to decide workflow transitions.
- Persist `dataSave.payload`.
- Add API behavior that saves form sections as part of workflow action execution.
- Accept UI echo fields such as `sectionKey`, `buttonKey`, `buttonLabelShown`, or `saveAsDraft`.

Current implementation note:

- The console `execute-json` path may continue using `WorkflowActionEnvelope` and writing opaque `MockSectionData`.
- The API `POST /v1/WorkflowAction` path must use the workflow-only request DTO and must execute with section-data persistence disabled.
- Do not treat console `execute-json` data-save behavior as API scope.

## User Boundary

User identity and roles are resolved outside the workflow scope.

Allowed:

- Accept an already-resolved actor from the upstream caller or trusted integration boundary.
- Pass actor user ID and roles to workflow services so metadata authorization can determine whether the workflow action is allowed.
- Record actor user ID as workflow evidence.

Not allowed:

- Look up users.
- Map users to roles from SQL.
- Authenticate credentials or tokens.
- Add target-system-specific identity logic without an explicit integration decision.
- Treat the actor who submits an action as the assignee of newly created tasks.

## Current Temporary Adapter

`TrustedHeaderWorkflowActorResolver` currently adapts trusted upstream headers into a workflow actor for this migration API.

This is a temporary local/integration adapter, not a permanent identity model.
Do not expand it into real user resolution.
Do not add database-backed role resolution.
Do not add JWT/claims logic until the target API identity boundary is confirmed.

## Controller Boundary

Controllers stay thin.

Allowed:

- Route HTTP requests.
- Accept request bodies, route values, query values, and headers.
- Delegate to API-facing services.
- Return typed API response wrappers or `ProblemDetails`.

Not allowed:

- Direct EF Core workflow queries.
- Workflow transition decisions.
- Form validation.
- Identity resolution logic beyond passing `HttpContext` to a boundary adapter service.
- Console command parsing or file override behavior.

## Service Boundary

API-facing services may adapt HTTP-shaped inputs into application workflow service calls.

Allowed:

- Check required route/query/body presence.
- Resolve or accept actor data according to the approved user boundary.
- Call existing workflow application services.
- Convert workflow application results into API response shapes.

Not allowed:

- Duplicate workflow engine logic.
- Hardcode Portfolio Trade transitions.
- Read `workflow.options` as instructions that override metadata.
- Add console-only admin override behavior.
- Add business-object lifecycle behavior.

## Endpoint Boundary

Existing intended endpoints:

- `POST /v1/WorkflowStart`
  - Starts one workflow instance for an upstream business object.
  - Request input is `workflowKey`, `businessObjectType`, and `businessObjectId`.
  - Optional request input `initialActionKey` starts the workflow and immediately executes one metadata-valid first action from the newly-created workflow state.
  - Optional `notificationContext`, a string key/value dictionary supplied by the upstream external API for email template rendering.
  - `notificationContext` is notification data only. It is not form payload, is not persisted as form data, and does not drive workflow transitions.
  - Actor identity comes from the temporary trusted-header adapter and is recorded as the starting user.
  - The endpoint does not accept `reset`.
  - The endpoint does not require `SaveDraft`; `SaveDraft` and submit actions are ordinary metadata actions from `Draft` when seeded.
  - If `initialActionKey` is omitted, the endpoint does not send email unless a future metadata-backed start notification is added.
  - If `initialActionKey` queues a metadata notification, the endpoint delivers only notification rows created by that initial action before returning success.
  - If the initial action fails before commit, the newly-created workflow instance is rolled back.
  - If initial-action notification delivery fails after commit, the response is converted to `NotificationDeliveryFailed` and HTTP 500, and the notification row remains pending for retry.
  - The endpoint does not create, update, validate, or read form/business object data.
  - Successful responses use the target wrapper shape:
    - top-level `result`
    - nested `result.actionResult`
    - nested `result.workflowContext`
- `GET /v1/WorkflowContext`
  - Returns workflow state, current task, SLA status, actor access, and available workflow actions.
  - Request input is only `workflowInstanceId`.
  - Actor identity comes from the temporary trusted-header adapter.
  - Successful responses use the target wrapper shape:
    - top-level `result`
    - nested `result.workflowContext`
- `POST /v1/WorkflowAction`
  - Executes one workflow action using a workflow-only API request DTO.
  - The API request DTO must omit `dataSave` entirely.
  - The API request DTO must not accept form section payloads.
  - The API request DTO must not accept UI echo fields such as `sectionKey`, `buttonKey`, `buttonLabelShown`, or `saveAsDraft`.
  - The API request DTO should contain only pure workflow fields plus notification-only context:
    - workflow instance ID
    - action key
    - optional `assignedRoleKey` for metadata-defined dynamic task assignment
    - optional `notificationContext` for email template rendering data
  - The request body contains `workflowInstanceId`, `actionKey`, optional `assignedRoleKey`, and optional `notificationContext`.
  - `assignedRoleKey` is accepted only for transitions whose created task supports dynamic assignment metadata; for `CheckerSendToTradingDesk`, the upstream form/business API supplies one of `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity`.
  - Optional `notificationContext`, a string key/value dictionary supplied by the upstream external API for email template rendering.
  - `notificationContext` is notification data only. It is not form payload, is not persisted as form data, and does not drive workflow transitions.
  - `workflowInstanceId` is the lookup key.
  - Workflow key and workflow definition version are derived from persisted workflow metadata, not echoed by the request.
  - Business object identity is derived from the persisted workflow instance, not echoed by the request.
  - Current state, current task, and workflow instance version are validated from persisted workflow data and metadata, not echoed by the request.
  - Client action ID, correlation ID, and idempotency key are generated internally by the API/workflow layer.
  - Response includes the action result and refreshed workflow context.
  - Refreshed workflow context is computed for the submitting actor from trusted headers.
  - Workflow-level failures return custom error code `500` rather than HTTP 200 with `result.success = false`.
  - If a successful action queues a metadata notification, the endpoint delivers only the notification rows created by that action before returning success.
  - The action-created notification rows are selected by the action result workflow instance ID and correlation ID.
  - Delivery uses the configured `INotificationSender`; in a real SMTP environment `Notifications:Sender = "Smtp"` resolves recipients through `Smtp:RoleEmailMap:{RecipientRoleKey}`.
  - If action-created SMTP delivery fails, the workflow action response is converted to `result.success = false`, `result.resultCode = "NotificationDeliveryFailed"`, and the controller returns HTTP 500.
  - Successful responses use the target wrapper shape:
    - top-level `result`
    - nested `result.actionResult`
    - nested `result.workflowContext`

Future endpoints require a boundary check before implementation.

Examples that require explicit clarification:

- Audit/history read endpoint.
- Task or notification read endpoint.
- SLA runner or notification outbox processing endpoint.
- Any endpoint that mixes workflow action execution with business-object save logic outside the current envelope contract.

## Clarification Rule

If a future implementation touches user resolution, form validation, business-object lifecycle, deployment, SLA processing, notification delivery, or admin operations, stop before editing code and ask one clarifying question at a time.

Do not ask a batch of questions.
Do not assume the answer from previous console behavior.

## First Open Clarification

Answered: keep the temporary trusted-header actor adapter.

`WorkflowEngine.WorkflowApi` may adapt already-resolved upstream identity from trusted headers. It must not perform real user lookup, authentication, role mapping, JWT/claims integration, or profile resolution.

## Second Open Clarification

Answered: `WorkflowEngine.WorkflowApi` must stop handling section payload entirely and only execute workflow state, task, notification, audit, SLA, and idempotency behavior.

The upstream form/business API owns form data persistence.

## Third Open Clarification

Answered: create a new workflow-only API request DTO that omits `dataSave` entirely.

Do not keep `WorkflowActionEnvelope` as the public API request body for `POST /v1/WorkflowAction` once the endpoint is aligned to this boundary.

## Fourth Open Clarification

Answered: the new workflow-only request DTO should contain only pure workflow fields.

Do not include UI/action echo fields such as `sectionKey`, `buttonKey`, `buttonLabelShown`, or `saveAsDraft`.

## Fifth Open Clarification

Answered: `workflowInstanceId` alone is the lookup key.

The API derives workflow key and workflow definition version from persisted workflow metadata. Do not require the caller to echo `workflowKey` or `workflowDefinitionVersion` in the workflow action request DTO.

## Sixth Open Clarification

Answered: derive business object identity entirely from the persisted workflow instance.

Do not require or accept `objectType` or `objectId` in the workflow action request DTO.

## Seventh Open Clarification

Answered: the workflow action request DTO should only accept `workflowInstanceId`, `actionKey`, and optional `assignedRoleKey`.

Do not require or accept `expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, or `expectedInstanceVersion` in the public API request DTO. The engine validates current state and task from persisted workflow data and metadata.

## Eighth Open Clarification

Answered: the API generates IDs and correlation internally.

Do not accept `actionReason`, `clientActionId`, `correlationId`, `idempotencyKey`, or source-system/command metadata in the public workflow action request DTO. The request body should contain only `workflowInstanceId`, `actionKey`, and optional `assignedRoleKey`.

## Ninth Open Clarification

Answered: `GET /v1/WorkflowContext` stays minimal.

The request input is only `workflowInstanceId`; actor identity continues to come from trusted headers through the temporary adapter.

## Tenth Open Clarification

Answered: `POST /v1/WorkflowAction` should continue returning refreshed workflow context.

The refreshed context is returned with the action result and is computed for the submitting actor.

## Eleventh Open Clarification

Answered: workflow-level failures should return custom error code `500`.

Do not return HTTP 200 with `result.success = false` for workflow-level failures such as invalid transition, forbidden role, or completed workflow once the API is aligned to this boundary.

## Twelfth Open Clarification

Answered: successful workflow action responses should use the target nested result shape.

Expected shape:

```json
{
  "result": {
    "actionResult": {},
    "workflowContext": {}
  }
}
```

## Thirteenth Open Clarification

Answered: successful workflow context responses should use the target nested result shape.

Expected shape:

```json
{
  "result": {
    "workflowContext": {}
  }
}
```
