# Trading Desk returns the workflow to Maker

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with one pending `TradingDeskReviewTask`; pack 09, 10, or 11 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `TradingDeskReviewTask`, cancels its active SLA schedule, and moves the workflow to `ReturnedToMaker`.
- Creates one pending `MakerReworkTask` with no SLA.
- Queues `RouteReturnedToMakerFromTradingDesk` to persisted `originalMaker`.

## Next valid packs

Use pack 06 to save rework or pack 07 to resubmit.
