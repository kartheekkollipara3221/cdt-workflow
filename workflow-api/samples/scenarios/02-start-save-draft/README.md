# Start and save the initial draft

**Endpoint:** `POST /v1/WorkflowStart`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

Use a business object ID that has never had a Portfolio Trade workflow. The Maker is saving without submitting.

## Request notes

The JSON is the public Workflow Start DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Creates the workflow and keeps it in `Draft`.
- Captures `originalMaker`.
- Executes metadata-authorized `SaveDraft`; no task, SLA, or notification is created.

## Next valid packs

Use the returned workflow instance ID with pack 04 or 05.
