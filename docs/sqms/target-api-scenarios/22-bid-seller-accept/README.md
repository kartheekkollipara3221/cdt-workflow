# 22. Seller accepts a Bid

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP, trusted enrichment/recipients, shared transaction, and target persistence.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and unimplemented here.
## Actor and required role
Trusted server-built `SqmsActor` with `Seller`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md); Bid is `UNDER REVIEW BY SELLER`, version 4. Its trusted parent Opportunity and required Bid/Opportunity/link data exist.
## Public request notes
Public JSON contains only action and expected version; no addresses, parent data, template data, target state, actor, route, or internal identity.
## Matching in-process call
See `in-process.cs`. Target resolves `relationshipManager` and `buyerRmTeam` from trusted Bid data; the service loads the parent Opportunity for trusted template enrichment.
## Target persistence ownership
Request adapter does not update Bid. Target persists returned state only on non-replay success.
## Expected workflow effects
`AcceptBid` moves to terminal `ACCEPTED`, version 5, runtime Completed; no task or SLA.
## Expected target-row effects
Target-owned `Status`/`WorkflowState` save only.
## Expected audit and idempotency effects
One audit and one idempotency row; replay adds no effects.
## Expected notification effects
Exactly one `SqmsBidAccepted` outbox row with immutable metadata-resolved recipients from server-owned `relationshipManager` and `buyerRmTeam` inputs.
## Expected available actions
None; terminal.
## Failure behavior
Missing parent Opportunity or required trusted recipient/template/link data returns `ValidationFailed` before side effects; all failures roll back.
## Next valid packs
[16-bid-get-context](../16-bid-get-context/README.md) or composite [43](../43-complete-opportunity-and-four-bids/README.md).
