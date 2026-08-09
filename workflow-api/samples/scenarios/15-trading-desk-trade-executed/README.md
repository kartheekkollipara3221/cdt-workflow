# Trading Desk marks the trade executed

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with one pending `TradingDeskReviewTask`; pack 09, 10, or 11 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `TradingDeskReviewTask`, cancels its active SLA schedule, and moves the workflow to `BdrmClientServiceReview`.
- Creates one pending `BdrmClientServiceReviewTask` with a two-minute first overdue threshold and daily 09:00 America/New_York recurrence until completion.
- Queues `RouteBdrmAssigned` to the metadata BDRM/Client Service address.

## Next valid packs

Use pack 16 to complete or pack 17 to return for Trading Desk rework.
