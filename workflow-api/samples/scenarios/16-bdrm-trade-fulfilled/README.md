# BDRM/Client Service confirms the trade was fulfilled

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `bdrm.scenario` with role `BDRMClientService`

## Prerequisite

The workflow is in `BdrmClientServiceReview` with one pending `BdrmClientServiceReviewTask`; pack 15 or 18 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.

## Expected effects

- Closes `BdrmClientServiceReviewTask` and cancels future overdue reminders.
- Moves the workflow to terminal `Completed`, sets completion time, and leaves no pending task or SLA.
- Queues `RouteWorkflowCompleted` to persisted `originalMaker`.

## Next valid packs

None. `Completed` is terminal.
