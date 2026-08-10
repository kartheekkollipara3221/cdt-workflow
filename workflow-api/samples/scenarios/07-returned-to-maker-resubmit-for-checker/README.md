# Resubmit Maker rework for Checker review

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow is in `ReturnedToMaker` with one pending `MakerReworkTask`; pack 08 or 14 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta; it carries Scenario Manager and Scenario Checker forward.

## Expected effects

- Closes `MakerReworkTask` and moves the workflow to `CheckerReview`.
- Creates one pending `CheckerReviewTask` with the two-minute test SLA.
- Queues `RouteCheckerAssignedOnResubmit` with the metadata Checker address in To and persisted original Maker, Scenario Manager, and Scenario Checker in Cc.
- Saves that snapshot on `CheckerReviewTask`; Checker overdue reminders reuse it unchanged.

## Next valid packs

Continue with pack 08, 09, 10, or 11.
