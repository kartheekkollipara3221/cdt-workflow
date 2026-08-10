# Submit an existing Draft for Checker review

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow exists in `Draft`; pack 01 or 02 can create it. Its required `originalMaker` participant is already persisted.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta.

## Expected effects

- Moves the workflow from `Draft` to `CheckerReview`.
- Creates a pending `CheckerReviewTask` with the two-minute test SLA.
- Queues `RouteCheckerAssignedOnSubmit` with the metadata Checker address in To and persisted original Maker plus Scenario Manager in Cc.
- Saves that snapshot on `CheckerReviewTask`; Checker overdue reminders reuse it unchanged.

## Next valid packs

Continue with pack 08, 09, 10, or 11.
