# 20. Scheduled classification moves a Bid to IPG review

## Contract status
**Implemented** `ISqmsScheduledService.RunAsync`. **Target implementation required** operator host/schedule. **Testing only** controlled data belongs in an isolated database.
## Endpoint
No public HTTP endpoint; never expose scheduling through a UI controller.
## Actor and required role
Scheduler constructs trusted `system` with `System`.
## Implemented service method
`ISqmsScheduledService.RunAsync(CancellationToken)`.
## Prerequisite
Complete [15-bid-start-submit](../15-bid-start-submit/README.md); parent Opportunity is `CLOSED TO BIDDING`. This is every other valid submitted Bid: a Fair Value or Premium Bid, or a Discount Bid when the combined valid Fair Value plus Premium requested amount is not strictly greater than the Opportunity sale amount.
## Public request notes
No public request exists; target/UI cannot choose the action or next state.
## Matching in-process call
See `in-process.cs`.
## Target persistence ownership
Scheduled adapter atomically updates exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.
## Expected workflow effects
`MoveBidToIpgReview` moves `SUBMITTED` to `IPG REVIEW`, version 3; no task or SLA.
## Expected target-row effects
Only the four scheduled projection columns change; all others are preserved.
## Expected audit and idempotency effects
One audit and `SQMS:BID:20000:2:MoveBidToIpgReview` row.
## Expected notification effects
None.
## Expected available actions
After refresh, IPG receives `SendBidToSellerReview`; scheduled actions are not UI actions.
## Failure behavior
Malformed candidate data is reported and processing continues. Projection failure rolls back that candidate. Operators prevent overlapping runs because no scheduler-wide lock exists.
## Next valid packs
[21-bid-send-to-seller-review](../21-bid-send-to-seller-review/README.md) or [16-bid-get-context](../16-bid-get-context/README.md).
