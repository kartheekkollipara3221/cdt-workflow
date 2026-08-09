# Start without an initial action

**Endpoint:** `POST /v1/WorkflowStart`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

Use a business object ID that has never had a Portfolio Trade workflow. The database must be initialized and seeded.

## Request notes

The JSON is the public Workflow Start DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Creates one active workflow instance in `Draft`.
- Captures `originalMaker` as a required immutable workflow participant.
- Creates no task or SLA schedule and queues no notification.

## Next valid packs

Use the returned workflow instance ID with pack 04 or 05.
