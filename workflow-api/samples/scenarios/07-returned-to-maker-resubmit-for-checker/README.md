# Resubmit Maker rework for Checker review

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow is in `ReturnedToMaker` with one pending `MakerReworkTask`; pack 08 or 14 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `MakerReworkTask` and moves the workflow to `CheckerReview`.
- Creates one pending `CheckerReviewTask` with the two-minute test SLA.
- Queues `RouteCheckerAssignedOnResubmit` to the metadata Checker address; this route accepts no selected Cc input.

## Next valid packs

Continue with pack 08, 09, 10, or 11.
