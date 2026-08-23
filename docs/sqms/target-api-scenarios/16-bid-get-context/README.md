# 16. Read Bid workflow context

## Contract status
**Implemented** read-only in-process behavior. **Recommended target facade** only. **Target implementation required** controller, authentication, and mapping.
## Endpoint
`GET /v1/sqms/bids/{id}/workflow` is recommended and unimplemented here.
## Actor and required role
The target builds the trusted actor; roles filter `availableActions` but do not alter state.
## Implemented service method
`ISqmsWorkflowService.GetBidContextAsync`.
## Prerequisite
Complete any successful Bid Start or Action pack, such as [15-bid-start-submit](../15-bid-start-submit/README.md).
## Public request notes
GET has no body. Route ID and authenticated principal determine the read.
## Matching in-process call
See `in-process.cs`.
## Target persistence ownership
Read-only: nobody writes target or workflow data. Context never lazily starts a missing workflow.
## Expected workflow effects
Current state, runtime status, version, terminal flag, and role-filtered actions are returned unchanged.
## Expected target-row effects
None.
## Expected audit and idempotency effects
No rows created or changed.
## Expected notification effects
None.
## Expected available actions
Derived from persisted metadata and trusted roles, never computed by the UI.
## Failure behavior
Missing Bid returns `TargetNotFound`; a saved Bid without a workflow returns `NotWorkflowManaged` and remains unstarted.
## Next valid packs
Any action offered by returned `availableActions`.
