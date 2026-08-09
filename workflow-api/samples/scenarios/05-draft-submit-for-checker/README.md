# Submit an existing Draft for Checker review

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow exists in `Draft`; pack 01 or 02 can create it. Its required `originalMaker` participant is already persisted.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Moves the workflow from `Draft` to `CheckerReview`.
- Creates a pending `CheckerReviewTask` with the two-minute test SLA.
- Queues `RouteCheckerAssignedOnSubmit` to the metadata Checker address and optional selected Cc user.

## Next valid packs

Continue with pack 08, 09, 10, or 11.
