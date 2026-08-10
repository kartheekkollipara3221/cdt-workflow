# Checker sends the workflow to Taxable Trading Desk

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `checker.scenario` with role `Checker`

## Prerequisite

The workflow is in `CheckerReview` with one pending `CheckerReviewTask`. The UI forwards its application-owned target date as `slaTargetDate`.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta; it includes Scenario Manager and the acting Checker.

## Expected effects

- Closes `CheckerReviewTask` and moves the workflow to `TradingDeskReview`.
- Creates `TradingDeskReviewTask` assigned to `TradingDeskTaxable`.
- Creates the application-target-date SLA schedule: due-today cadence on the target date and overdue start at 09:00 America/New_York on target date plus one day.
- Queues `RouteTradingDeskAssigned` with the metadata Trading Desk address in To and persisted original Maker, Scenario Manager, and Scenario Checker in Cc.
- Saves that snapshot on `TradingDeskReviewTask`; due-today and overdue reminders reuse it unchanged.

## Next valid packs

Use pack 12 or 13 to synchronize SLA, pack 14 to return, or pack 15 to execute.
