# 18. Return a submitted Bid to Draft

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP integration and target persistence.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and unimplemented here.
## Actor and required role
Trusted `SqmsActor` with `IPG`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [15-bid-start-submit](../15-bid-start-submit/README.md); Bid is `SUBMITTED`, version 2.
## Public request notes
Only action intent and expected version are public. The browser supplies no actor, recipient, target state, or internal identity.
## Matching in-process call
See `in-process.cs`.
## Target persistence ownership
Target API persists returned state on non-replay success; the adapter never updates Bid on a request.
## Expected workflow effects
`ReturnBidToDraft` moves `SUBMITTED` to `Draft`, version 3; no task or SLA.
## Expected target-row effects
Target-owned `Status`/`WorkflowState` save only.
## Expected audit and idempotency effects
One audit and one version-derived idempotency row; replay adds neither.
## Expected notification effects
None.
## Expected available actions
MFO receives `SubmitBid` after refresh.
## Failure behavior
Forbidden, stale, invalid, or failed persistence rolls back without partial effects.
## Next valid packs
[17-bid-submit-existing-draft](../17-bid-submit-existing-draft/README.md) or [16-bid-get-context](../16-bid-get-context/README.md).
