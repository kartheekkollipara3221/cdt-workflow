# BDRM/Client Service confirms the trade was fulfilled

**Endpoint:** `POST /v1/WorkflowAction`
**Actor:** `bdrm.scenario` with role `BDRMClientService`

## Prerequisite

The workflow is in `BdrmClientServiceReview` with one pending `BdrmClientServiceReviewTask`; pack 15 or 18 creates that state.

## Request notes

The JSON is the public Workflow Action DTO only. Actor identity is supplied by the trusted headers in `request.http`.
`selectedCcUsers` is the complete cumulative Cc snapshot for this action, not a delta; it includes Scenario Manager, Scenario Checker, Scenario Trader, and the acting BDRM reviewer.

## Expected effects

- Closes `BdrmClientServiceReviewTask` and cancels future overdue reminders.
- Moves the workflow to terminal `Completed`, sets completion time, and leaves no pending task or SLA.
- Queues `RouteWorkflowCompleted` with persisted original Maker in To and Scenario Manager, Scenario Checker, Scenario Trader, and Scenario BDRM Reviewer in Cc.
- Completion closes the task and cancels its future SLA reminders; the historical task snapshot remains unchanged.

## Next valid packs

None. `Completed` is terminal.
