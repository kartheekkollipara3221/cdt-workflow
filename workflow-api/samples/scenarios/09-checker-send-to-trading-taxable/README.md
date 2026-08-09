# Checker sends the workflow to Taxable Trading Desk

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `checker.scenario` with role `Checker`

## Prerequisite

The workflow is in `CheckerReview` with one pending `CheckerReviewTask`. The UI forwards its application-owned target date as `slaTargetDate`.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `CheckerReviewTask` and moves the workflow to `TradingDeskReview`.
- Creates `TradingDeskReviewTask` assigned to `TradingDeskTaxable`.
- Creates the application-target-date SLA schedule: due-today cadence on the target date and overdue start at 09:00 America/New_York on target date plus one day.
- Queues `RouteTradingDeskAssigned` to the metadata Trading Desk address.

## Next valid packs

Use pack 12 or 13 to synchronize SLA, pack 14 to return, or pack 15 to execute.
