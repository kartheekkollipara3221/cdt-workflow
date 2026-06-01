# Workflow Start With Initial Action Design

Date: 2026-06-01

## Goal

Allow a caller to submit a brand-new business form into workflow with one API call, without requiring a separate prior SaveDraft workflow action.

The existing behavior remains valid:

- `POST /v1/WorkflowStart` without an initial action starts the workflow in `Draft`.
- A later `POST /v1/WorkflowAction` with `SubmitForChecker` moves `Draft` to `CheckerReview`.

The new behavior adds an optional first action:

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectType": "PortfolioTrade",
  "businessObjectId": "PT-123",
  "initialActionKey": "SubmitForChecker"
}
```

## Real Use Case Flow

Maker completes a new form and clicks Submit without saving a draft first.

1. The upstream form/business API validates and saves the form data.
2. The upstream API calls `POST /v1/WorkflowStart` with `initialActionKey`.
3. `WorkflowEngine.WorkflowApi` creates the workflow instance in the metadata-defined initial state, currently `Draft`.
4. The same service operation executes the initial action as the same actor.
5. Workflow metadata resolves whether that action is valid from the current state.
6. For Portfolio Trade, `Draft + SubmitForChecker + Maker` moves to `CheckerReview`, creates `CheckerReviewTask`, queues the Checker notification, and writes audit.
7. The API returns the same nested target-style response shape with `actionResult` and refreshed `workflowContext`.

The workflow engine still does not require SaveDraft. SaveDraft is just one valid action from `Draft`; SubmitForChecker is another valid action from `Draft`.

## API Contract

Extend `WorkflowStartRequest` with:

```csharp
public string? InitialActionKey { get; init; }
```

Rules:

- Null or blank `InitialActionKey` keeps current start-only behavior.
- Non-blank `InitialActionKey` is trimmed and executed immediately after the workflow instance is created.
- Start-only responses keep returning the start result.
- Start-with-initial-action responses return the initial action result as `result.actionResult` and the refreshed post-action context as `result.workflowContext`.
- The request must not accept `toState`, UI echo fields, form payload, business rules, caller-generated correlation IDs, or idempotency keys.
- The action must be validated through the existing metadata-driven workflow execution path.
- Do not hardcode Portfolio Trade action names in the reusable engine.

## Architecture

Keep the controller thin. `WorkflowStartController` continues delegating to `IWorkflowStartService`.

`WorkflowStartService` becomes the API-facing orchestration layer for both start-only and start-with-initial-action. It resolves the actor once from trusted headers, validates request fields, and delegates workflow behavior to application/infrastructure services.

The start-plus-action implementation should reuse the existing workflow execution service rather than duplicate transition logic. The execution request should use the same API action flags:

- `DeriveIdentityFromPersistence = true`
- `PersistSectionData = false`
- `ValidateActionEcho = false`
- `AdminOverride = false`

Notification delivery should reuse `IWorkflowActionNotificationDispatcher`, targeting only rows created by the initial action's workflow instance ID and correlation ID.

## Transaction Boundary

The database workflow changes for start plus initial action should be atomic.

If workflow creation succeeds but the initial action is invalid, forbidden, fails concurrency checks, or otherwise returns an unsuccessful workflow result, the service should roll back the new workflow instance so the caller is not left with an accidental `Draft` instance.

SMTP delivery cannot be made part of the SQL transaction. Keep the existing API convention:

- The workflow DB transaction commits before SMTP delivery.
- If SMTP delivery fails, the action response becomes `NotificationDeliveryFailed` and HTTP 500.
- The outbox row remains pending for retry.

This matches the current `POST /v1/WorkflowAction` behavior and avoids sending email for uncommitted workflow state.

## Error Handling

Start-only failures keep their current response behavior.

Start-with-initial-action failures:

- Request validation errors use `WorkflowApiRequestException` and ProblemDetails.
- Duplicate business object workflow start returns the existing `AlreadyExists` workflow failure response and does not execute the initial action.
- Invalid or unauthorized initial action returns the initial action workflow failure result and HTTP 500, consistent with existing workflow-level failure behavior.
- If the initial action fails before commit, no new workflow instance should remain.
- If notification delivery fails after commit, return `NotificationDeliveryFailed` and leave the outbox row pending.

## Testing

Add focused tests before implementation:

- `WorkflowStartService` start-only behavior remains unchanged when `InitialActionKey` is null or blank.
- `WorkflowStartService` builds an initial action execution request with the same trusted actor and workflow-only API flags.
- `WorkflowStartController` returns HTTP 500 when the initial action workflow result fails.
- SQL-backed workflow execution proves start plus `SubmitForChecker` produces final state `CheckerReview`, creates `CheckerReviewTask`, queues Checker notification, writes audit, and does not persist `MockSectionData`.
- Failure during initial action rolls back the newly-created workflow instance.
- Existing `WorkflowAction` and console execution tests continue to pass.

## Non-Goals

- Do not require SaveDraft before SubmitForChecker.
- Do not add form payload persistence to the workflow API.
- Do not add form validation or Portfolio Trade business validation.
- Do not expose `toState`.
- Do not add a new public endpoint unless the current route proves insufficient.
- Do not add user lookup, JWT/claims mapping, SQL user-role mapping, CQRS, MediatR, event sourcing, BPMN, brokers, microservices, or plugin frameworks.
