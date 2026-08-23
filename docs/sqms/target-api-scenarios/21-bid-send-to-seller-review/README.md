# 21. Send a Bid to seller review

## Contract status
**Implemented** in-process. **Recommended target facade** only. **Target implementation required** HTTP integration and target persistence.
## Endpoint
`POST /v1/sqms/bids/{id}/workflow/actions` is recommended and unimplemented here.
## Actor and required role
Trusted `SqmsActor` with `IPG`.
## Implemented service method
`ISqmsWorkflowService.ExecuteBidActionAsync`.
## Prerequisite
Complete [20-bid-scheduled-move-to-ipg-review](../20-bid-scheduled-move-to-ipg-review/README.md); Bid is `IPG REVIEW`, version 3.
## Public request notes
Only `actionKey` and expected version are public.
## Matching in-process call
See `in-process.cs`.
## Target persistence ownership
Request adapter never writes Bid; target persists returned state on non-replay success.
## Expected workflow effects
`SendBidToSellerReview` moves to `UNDER REVIEW BY SELLER`, version 4; no task or SLA.
## Expected target-row effects
Target-owned projection only.
## Expected audit and idempotency effects
One audit and one idempotency row; replay is side-effect-free.
## Expected notification effects
None for this transition.
## Expected available actions
A Seller receives `AcceptBid`, `PartiallyAcceptBid`, and `RejectBid`.
## Failure behavior
Any rejection or exception rolls back all request effects.
## Next valid packs
[22-bid-seller-accept](../22-bid-seller-accept/README.md), [23-bid-seller-partially-accept](../23-bid-seller-partially-accept/README.md), [24-bid-seller-reject](../24-bid-seller-reject/README.md), or [16](../16-bid-get-context/README.md).
