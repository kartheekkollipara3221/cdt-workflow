# Start and submit for Checker review

**Endpoint:** `POST /v1/WorkflowStart`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

Use a business object ID that has never had a Portfolio Trade workflow. The Maker has completed the UI validation needed to submit.

## Request notes

The JSON is the public Workflow Start DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`originalMaker` is captured once, while `selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta.

## Expected effects

- Creates the workflow and moves it from `Draft` to `CheckerReview`.
- Captures `originalMaker`.
- Creates a pending `CheckerReviewTask` with the two-minute test SLA.
- Queues `RouteCheckerAssignedOnSubmit` with the metadata Checker address in To and persisted original Maker plus Scenario Manager in Cc.
- Saves that snapshot on `CheckerReviewTask`; Checker overdue reminders reuse it unchanged.

## Next valid packs

Continue with pack 08, 09, 10, or 11.
