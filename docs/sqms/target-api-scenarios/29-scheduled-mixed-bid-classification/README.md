# 29. Classify Fair Value, Premium, and Discount Bids

## Contract status

**Implemented:** `ISqmsScheduledService.RunAsync` parses persisted amounts/categories and chooses metadata actions. **Target implementation required:** operator host/schedule. **Testing only:** the example rows belong in an isolated database.

## Endpoint

No public HTTP endpoint. The browser does not select either scheduled action or next state.

## Prerequisite data

The parent Opportunity 10000 is `CLOSED TO BIDDING` with sale amount 100.00. Three valid submitted Bids are:

| Bid | Category | Requested | Expected action | Expected state |
|---|---|---:|---|---|
| 20000 | Fair Value | 60.00 | `MoveBidToIpgReview` | `IPG REVIEW` |
| 20001 | Premium | 50.00 | `MoveBidToIpgReview` | `IPG REVIEW` |
| 20002 | Discount | 25.00 | `MarkBidNotAllocated` | `NOT ALLOCATED` |

The qualifying sum is Fair Value plus Premium: `60 + 50 = 110`. Because `110 > 100` is true, and only because it is **strictly greater**, the Discount Bid becomes `NOT ALLOCATED`. The Fair Value and Premium Bids still move to `IPG REVIEW`. At equality (`sum == sale amount`) or below, all three move to `IPG REVIEW`.

## Persistence ownership

Each scheduled Bid transaction updates exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`, preserving all other Bid columns. Each candidate commits independently; a malformed or failed Bid is recorded and does not block later valid Bids.

## Expected counts

`BidsMovedToIpgReview = 2`, `BidsNotAllocated = 1`, and no notification is queued by classification itself.
