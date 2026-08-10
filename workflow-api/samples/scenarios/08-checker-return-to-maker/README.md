# Checker returns the workflow to Maker

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `checker.scenario` with role `Checker`

## Prerequisite

The workflow is in `CheckerReview` with one pending `CheckerReviewTask`; pack 03, 05, or 07 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta; it includes the acting Checker.

## Expected effects

- Closes `CheckerReviewTask` and moves the workflow to `ReturnedToMaker`.
- Creates one pending `MakerReworkTask` with no SLA.
- Queues `RouteReturnedToMakerFromChecker` with persisted original Maker in To and Scenario Manager, Scenario Checker, and the metadata operations address in Cc.
- Saves the same snapshot on `MakerReworkTask`; that task intentionally has no SLA reminders.

## Next valid packs

Use pack 06 to save rework or pack 07 to resubmit.
