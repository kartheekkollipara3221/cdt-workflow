# Trading Desk executes the reworked trade

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskRework` with one pending `TradingDeskReworkTask`; pack 17 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `TradingDeskReworkTask`, cancels its SLA schedule, and moves the workflow back to `BdrmClientServiceReview`.
- Creates a new pending `BdrmClientServiceReviewTask` with the configured recurring overdue-reminder schedule.
- Queues `RouteBdrmAssigned` to the metadata BDRM/Client Service address.

## Next valid packs

Use pack 16 to complete or pack 17 to request another Trading Desk rework.
