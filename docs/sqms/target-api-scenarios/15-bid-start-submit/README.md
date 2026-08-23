# 15. Start and submit a saved Bid

## Contract status
**Implemented** in-process. **Recommended target facade** HTTP only. **Target implementation required** controller, trusted actor, shared transaction, target persistence, and response mapping.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/start` is recommended and is not implemented here.
## Actor and required role
The server builds a trusted `SqmsActor` with `MFO`; public JSON supplies no actor or role.
## Implemented service method
`ISqmsWorkflowService.StartBidAsync`.
## Prerequisite
A saved, non-deleted Bid 20000 exists with a valid parent Opportunity and no Bid workflow.
## Public request notes
The route ID is authoritative and the body is `{}`. No actor, recipient, `toState`, workflow-instance, idempotency, route, or template authority is accepted.
## Matching in-process call
See `in-process.cs`; enlist `WorkflowDbContext` in the target-owned SQL transaction first.
## Target persistence ownership
Request SQMS code never updates Bid. On non-replay success the target saves returned `Status`/`WorkflowState` and commits once. Duplicate Start returns `AlreadyExists`.
## Expected workflow effects
The initial `SubmitBid` action moves `Draft` to `SUBMITTED`, version 2, runtime Active. No SQMS task or SLA.
## Expected target-row effects
Only the target-owned projection changes; all other Bid columns remain unchanged.
## Expected audit and idempotency effects
One `SubmitBid` audit and one Start idempotency row.
## Expected notification effects
None.
## Expected available actions
Role-filtered metadata; an IPG actor can receive `ReturnBidToDraft` after refresh.
## Failure behavior
Missing target, duplicate Start, invalid role, or exception rolls the outer transaction back without partial effects.
## Next valid packs
[16-bid-get-context](../16-bid-get-context/README.md), [18-bid-submitted-return-to-draft](../18-bid-submitted-return-to-draft/README.md), or scheduled classification [19](../19-bid-scheduled-mark-not-allocated/README.md)/[20](../20-bid-scheduled-move-to-ipg-review/README.md).
