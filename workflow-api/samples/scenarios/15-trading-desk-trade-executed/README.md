# Trading Desk marks the trade executed

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskReview` with one pending `TradingDeskReviewTask`; pack 09, 10, or 11 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
The calling application supplies the complete cumulative Cc snapshot under `notification.recipientInputs.selectedCcUsers`, not a delta. This pack carries Scenario Manager, Scenario Checker, and the acting Trader; it does not resend the immutable original Maker.

## Expected effects

- Closes `TradingDeskReviewTask`, cancels its active SLA schedule, and moves the workflow to `BdrmClientServiceReview`.
- Creates one pending `BdrmClientServiceReviewTask` with a two-minute first overdue threshold and daily 09:00 America/New_York recurrence until completion.
- Queues `RouteBdrmAssigned` with BDRM/Client Service in To and persisted original Maker, Scenario Manager, Scenario Checker, and Scenario Trader in Cc.
- Saves that final recipient snapshot on the BDRM task so every later BDRM overdue reminder reuses it unchanged.

## Next valid packs

Use pack 16 to complete or pack 17 to return for Trading Desk rework.
