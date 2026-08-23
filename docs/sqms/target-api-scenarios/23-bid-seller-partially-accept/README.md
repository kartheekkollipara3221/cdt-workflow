# 23. Seller partially accepts a Bid

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP, trusted enrichment/recipients, transaction, and persistence.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and unimplemented here.
## Actor and required role
Trusted `SqmsActor` with `Seller`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md); state `UNDER REVIEW BY SELLER`, version 4, with required Bid, parent Opportunity, and configured link data.
## Public request notes
No recipients, parent fields, template fields, actor, `toState`, route, workflow-instance, or idempotency authority are public.
## Matching in-process call
See `in-process.cs`; server resolves `relationshipManager` and `buyerRmTeam`, while the service loads the trusted parent Opportunity.
## Target persistence ownership
Target saves returned state on non-replay success; request adapter never writes Bid.
## Expected workflow effects
`PartiallyAcceptBid` produces terminal `PARTIALLY ACCEPTED`, version 5; no task or SLA.
## Expected target-row effects
Target-owned status/state projection only.
## Expected audit and idempotency effects
One audit and one idempotency row; replay adds none.
## Expected notification effects
Exactly one `SqmsBidPartiallyAccepted` outbox row with immutable metadata-resolved recipient snapshot.
## Expected available actions
None; terminal.
## Failure behavior
Missing trusted parent/enrichment prevents runtime, audit, idempotency, target, or outbox effects.
## Next valid packs
[16-bid-get-context](../16-bid-get-context/README.md) or [43](../43-complete-opportunity-and-four-bids/README.md).
