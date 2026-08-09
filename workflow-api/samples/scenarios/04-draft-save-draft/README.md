# Save an existing Draft

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `maker.scenario` with role `Maker`

## Prerequisite

The workflow exists in `Draft`; pack 01 or 02 can create it. Set `workflowInstanceId` to that persisted instance.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Keeps the workflow in `Draft`.
- Creates or closes no task and does not evaluate SLA.
- Queues no notification; the workflow instance version is not incremented.

## Next valid packs

Repeat pack 04 for another save or use pack 05 to submit.
