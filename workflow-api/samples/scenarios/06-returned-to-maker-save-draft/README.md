# Save a ReturnedToMaker draft

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow is in `ReturnedToMaker` with one pending `MakerReworkTask`; pack 08 or 14 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Keeps the workflow in `ReturnedToMaker`.
- Leaves the existing `MakerReworkTask` pending; it does not create another task.
- Queues no notification and does not evaluate SLA or increment workflow version.

## Next valid packs

Repeat pack 06 for another save or use pack 07 to resubmit.
