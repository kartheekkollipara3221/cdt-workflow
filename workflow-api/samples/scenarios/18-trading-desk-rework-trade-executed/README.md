# Trading Desk executes the reworked trade

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `trader.scenario` with role `TradingDesk`

## Prerequisite

The workflow is in `TradingDeskRework` with one pending `TradingDeskReworkTask`; pack 17 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
The calling application supplies the complete cumulative Cc snapshot under `notification.recipientInputs.selectedCcUsers`, not a delta. This pack carries Scenario Manager, Scenario Checker, Scenario Trader, and Scenario BDRM Reviewer; it does not resend the immutable original Maker.

## Expected effects

- Closes `TradingDeskReworkTask`, cancels its SLA schedule, and moves the workflow back to `BdrmClientServiceReview`.
- Creates a new pending `BdrmClientServiceReviewTask` with the configured recurring overdue-reminder schedule.
- Queues `RouteBdrmAssigned` with BDRM/Client Service in To and persisted original Maker, Scenario Manager, Scenario Checker, Scenario Trader, and Scenario BDRM Reviewer in Cc.
- Saves that final recipient snapshot on the new BDRM task so every later BDRM overdue reminder reuses it unchanged.

## Next valid packs

Use pack 16 to complete or pack 17 to request another Trading Desk rework.
