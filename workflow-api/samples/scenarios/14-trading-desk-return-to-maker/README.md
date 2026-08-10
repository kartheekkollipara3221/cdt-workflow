# Trading Desk returns the workflow to Maker

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with one pending `TradingDeskReviewTask`; pack 09, 10, or 11 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta; it includes Scenario Manager, Scenario Checker, and the acting Trader.

## Expected effects

- Closes `TradingDeskReviewTask`, cancels its active SLA schedule, and moves the workflow to `ReturnedToMaker`.
- Creates one pending `MakerReworkTask` with no SLA.
- Queues `RouteReturnedToMakerFromTradingDesk` with persisted original Maker in To and Scenario Manager, Scenario Checker, and Scenario Trader in Cc.
- Saves the same snapshot on `MakerReworkTask`; that task intentionally has no SLA reminders.

## Next valid packs

Use pack 06 to save rework or pack 07 to resubmit.
