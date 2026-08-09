# Reschedule a Trading Desk SLA target

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with an active `TradingDeskReviewTask` whose persisted target date differs from `2030-01-25`.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Synchronizes through the normal WorkflowAction endpoint without an `actionKey`.
- Recalculates the active application-target-date SLA schedule from the new target while suppressing stale schedule delivery.
- Leaves workflow state, version, pending task, and workflow audit unchanged and queues no immediate business notification.

## Next valid packs

Repeat pack 13 for another target change, use pack 14 to return, or pack 15 to execute.
