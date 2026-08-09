# Checker returns the workflow to Maker

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `checker.scenario` with role `Checker`

## Prerequisite

The workflow is in `CheckerReview` with one pending `CheckerReviewTask`; pack 03, 05, or 07 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `CheckerReviewTask` and moves the workflow to `ReturnedToMaker`.
- Creates one pending `MakerReworkTask` with no SLA.
- Queues `RouteReturnedToMakerFromChecker` to persisted `originalMaker`, optional selected Cc users, and the metadata operations Cc address.

## Next valid packs

Use pack 06 to save rework or pack 07 to resubmit.
