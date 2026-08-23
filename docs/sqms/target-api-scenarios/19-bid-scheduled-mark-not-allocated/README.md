# 19. Scheduled classification marks a Discount Bid not allocated

## Contract status
**Implemented** `ISqmsScheduledService.RunAsync`. **Target implementation required** operator host/schedule. **Testing only** controlled classification data belongs in an isolated database.
## Endpoint
No public HTTP endpoint; never expose scheduling to a browser or request controller.
## Actor and required role
Scheduler constructs trusted `system` with `System`.
## Implemented service method
`ISqmsScheduledService.RunAsync(CancellationToken)`; do not call the internal transition service from controllers.
## Prerequisite
Complete [15-bid-start-submit](../15-bid-start-submit/README.md). The parent Opportunity is `CLOSED TO BIDDING`; this valid submitted Bid is category Discount. Across all valid submitted sibling Bids, the Fair Value plus Premium requested amount is **strictly greater than** the Opportunity sale amount.
## Public request notes
There is no public request or browser-selected action.
## Matching in-process call
See `in-process.cs`. Scheduler parses persisted category/amount data and selects the metadata action.
## Target persistence ownership
Scheduled adapter atomically updates exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.
## Expected workflow effects
`MarkBidNotAllocated` moves `SUBMITTED` to terminal `NOT ALLOCATED`, version 3; no task or SLA.
## Expected target-row effects
The four owned columns are projected; every other Bid column is preserved.
## Expected audit and idempotency effects
One action audit and `SQMS:BID:20000:2:MarkBidNotAllocated`; rerun does not duplicate the transition.
## Expected notification effects
None.
## Expected available actions
None; terminal and scheduled action is never a UI action.
## Failure behavior
Invalid data/candidate failures are reported while later candidates continue. Projection failure rolls back that transition. No scheduler-wide lock exists; operators prevent overlap.
## Next valid packs
[16-bid-get-context](../16-bid-get-context/README.md) or [43](../43-complete-opportunity-and-four-bids/README.md).
