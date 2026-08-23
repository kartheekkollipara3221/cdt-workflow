# 17. Submit an existing Draft Bid

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP, trusted actor, transaction, persistence, and mapping.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and is not implemented here.
## Actor and required role
Trusted server-built `SqmsActor` with `MFO`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [18-bid-submitted-return-to-draft](../18-bid-submitted-return-to-draft/README.md); Bid 20000 is `Draft`, version 3.
## Public request notes
Only `actionKey` and `expectedWorkflowVersion` are public; no workflow or notification authority is browser-controlled.
## Matching in-process call
See `in-process.cs`.
## Target persistence ownership
The request adapter does not update Bid. Target persists returned state only for non-replay success; replay rolls back, reloads, and verifies without a write.
## Expected workflow effects
`SubmitBid` moves `Draft` to `SUBMITTED`, version 4, runtime Active; no task or SLA.
## Expected target-row effects
Target-owned `Status`/`WorkflowState` projection only.
## Expected audit and idempotency effects
One audit and one `SQMS:BID:20000:3:SubmitBid` row; replay adds neither.
## Expected notification effects
None.
## Expected available actions
IPG may receive `ReturnBidToDraft`; scheduled System actions are not UI actions.
## Failure behavior
Any unsuccessful result or exception rolls back all request effects.
## Next valid packs
[19](../19-bid-scheduled-mark-not-allocated/README.md), [20](../20-bid-scheduled-move-to-ipg-review/README.md), or [16](../16-bid-get-context/README.md).
