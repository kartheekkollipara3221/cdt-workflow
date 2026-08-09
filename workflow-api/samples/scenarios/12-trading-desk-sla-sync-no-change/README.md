# Synchronize an unchanged Trading Desk SLA target

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with an active `TradingDeskReviewTask` whose current target date is already `2030-01-20`.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Synchronizes through the normal WorkflowAction endpoint without an `actionKey`.
- Leaves workflow state, version, pending task, active SLA schedule, and workflow audit unchanged.
- Queues no business notification; later due/overdue notifications remain scheduler-owned.

## Next valid packs

Use pack 13 if the application target date changes, pack 14 to return, or pack 15 to execute.
