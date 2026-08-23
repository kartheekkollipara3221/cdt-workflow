# 24. Seller rejects a Bid

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP, trusted enrichment/recipients, transaction, and persistence.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and unimplemented here.
## Actor and required role
Trusted `SqmsActor` with `Seller`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md); state `UNDER REVIEW BY SELLER`, version 4, with required trusted Bid/parent Opportunity/link data.
## Public request notes
Only action and expected version are public; notification and transition authority remain server-owned.
## Matching in-process call
See `in-process.cs`; server resolves `relationshipManager`/`buyerRmTeam`, and the service loads the trusted parent Opportunity.
## Target persistence ownership
Target saves returned state only on non-replay success; adapter never writes Bid on a request.
## Expected workflow effects
`RejectBid` produces terminal `NOT ACCEPTED`, version 5; no task or SLA.
## Expected target-row effects
Target-owned status/state projection only.
## Expected audit and idempotency effects
One audit and one idempotency row; replay adds none.
## Expected notification effects
Exactly one `SqmsBidNotAccepted` outbox row with immutable metadata-resolved recipients.
## Expected available actions
None; terminal.
## Failure behavior
Missing trusted parent/enrichment or any rejected operation rolls back before side effects.
## Next valid packs
[16-bid-get-context](../16-bid-get-context/README.md) or [43](../43-complete-opportunity-and-four-bids/README.md).
