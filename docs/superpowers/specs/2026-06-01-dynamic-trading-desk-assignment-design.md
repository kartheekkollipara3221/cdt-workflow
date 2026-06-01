# Dynamic Trading Desk Assignment Design

## Summary

When Checker approves a Portfolio Trade request, the workflow should assign the next task and notification to one of three specific trading desks instead of the broad `TradingDesk` recipient.

The upstream business/form API owns the business decision for which desk applies. It passes the resolved desk role key into the workflow action request. The reusable workflow engine validates and applies that role key as metadata-driven assignment data; it does not inspect Portfolio Trade form fields or compute desk selection itself.

Desk role keys:

- `TradingDeskTaxable` - display name `Taxable`
- `TradingDeskTaxExempt` - display name `Tax Exempt`
- `TradingDeskEquity` - display name `Equity`

The broad `TradingDesk` role remains the role allowed to perform trading-desk workflow actions after assignment.

## Goals

- Keep the Checker action as `CheckerSendToTradingDesk`.
- Allow the API caller to provide the resolved target desk role for that action.
- Create the trading desk task with the specific assigned role.
- Queue the assignment notification to the same specific role.
- Keep later Trading Desk actions authorized by broad `TradingDesk`.
- Keep workflow logic generic and metadata-driven.
- Avoid user lookup, SQL user-to-role mapping, and Portfolio Trade field parsing inside the workflow engine.

## Non-Goals

- Do not add a `DemoUser` table or SQL user-to-role mapping.
- Do not make workflow inspect Portfolio Trade payload fields.
- Do not add three separate Checker approve actions unless a later requirement asks for them.
- Do not change the single-active-task workflow model.
- Do not change request JSON to specify `toState`.
- Do not let `workflow.options` override metadata.

## Public API Shape

`POST /v1/WorkflowAction` remains workflow-only. It currently accepts:

```json
{
  "workflowInstanceId": "WF-PT-123",
  "actionKey": "CheckerSendToTradingDesk"
}
```

For dynamic task assignment, it will accept an additional workflow-only field:

```json
{
  "workflowInstanceId": "WF-PT-123",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable"
}
```

`assignedRoleKey` is required only for transitions configured as dynamic assignment transitions. It is rejected for transitions that do not allow dynamic assignment.

The console `execute-json` path can continue using the existing envelope and may add the same assignment value in the workflow block if needed for samples/tests.

## Metadata Model

Add metadata to mark a task creation as assignment-overridable and constrain the allowed target roles.

Recommended shape:

- Add a workflow role row for each specific trading desk role.
- Add a generic SQL lookup table, tentatively `WorkflowTaskAssignmentRoleDefinition`, keyed by workflow definition version, task key, and role key.
- Seed assignment-role metadata that says `TradingDeskReviewTask` can be dynamically assigned to:
  - `TradingDeskTaxable`
  - `TradingDeskTaxExempt`
  - `TradingDeskEquity`
- Keep the default owner role for `TradingDeskReviewTask` as `TradingDesk` only as a fallback/default, not as the actual assignment when a target desk is supplied.
- For `RouteTradingDeskAssigned`, set `RecipientRoleKey = null` and have workflow execution use the created task's assigned role when a task-created notification route has no configured recipient role.

The engine must validate the supplied role against persisted metadata instead of hardcoded Portfolio Trade strings.

## Execution Flow

1. Upstream business/form API determines the correct trading desk from business data.
2. Checker submits `POST /v1/WorkflowAction` with `actionKey = CheckerSendToTradingDesk` and `assignedRoleKey`.
3. API service builds the internal workflow execution request, including the assignment target.
4. Workflow execution resolves the metadata transition from `CheckerReview` using the existing `(fromState, actionKey, allowedRole)` rule.
5. When creating `TradingDeskReviewTask`, the engine validates `assignedRoleKey` against allowed assignment metadata.
6. Created task stores `AssignedRoleKey = assignedRoleKey`.
7. Notification outbox row stores `RecipientRoleKey = assignedRoleKey`.
8. SMTP delivery resolves the address from `Smtp:RoleEmailMap:{assignedRoleKey}`.
9. Later Trading Desk actions continue to require actor role `TradingDesk`.

## Validation And Errors

- Missing `assignedRoleKey` for `CheckerSendToTradingDesk` returns validation failure before side effects.
- Unknown or inactive `assignedRoleKey` returns validation failure before side effects.
- A role not allowed for the task's dynamic assignment metadata returns validation failure before side effects.
- `assignedRoleKey` must not affect next-state selection. The transition still comes only from workflow metadata.
- A supplied assignment key must not override metadata for task creation, SLA, notification route, or audit behavior.

## Notification Configuration

SMTP config needs role mappings for the three specific desks:

```text
Smtp:RoleEmailMap:TradingDeskTaxable
Smtp:RoleEmailMap:TradingDeskTaxExempt
Smtp:RoleEmailMap:TradingDeskEquity
```

Existing `Smtp:RoleEmailMap:TradingDesk` can remain for compatibility or fallback, but the Checker approval path should send to the specific desk key.

## Testing

Add focused coverage for:

- `CheckerSendToTradingDesk` with `TradingDeskTaxable` creates a task assigned to `TradingDeskTaxable` and notification to `TradingDeskTaxable`.
- Same for `TradingDeskTaxExempt` and `TradingDeskEquity`.
- Missing `assignedRoleKey` for the dynamic path fails before state/task/notification changes.
- Invalid `assignedRoleKey` fails before state/task/notification changes.
- A user with broad `TradingDesk` role can still perform `TradingTradeExecuted` on a task assigned to a specific desk.
- Existing return-to-maker, BDRM rework, SLA, and idempotency tests continue to pass.

## Implementation Notes

- `assignedRoleKey` on non-dynamic transitions is invalid input.
- Using `RecipientRoleKey = null` to mean "recipient follows created task assigned role" matches the existing SLA overdue route pattern, where the overdue notification follows the task assignee role.
