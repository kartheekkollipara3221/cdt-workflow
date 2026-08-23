# 08. Close Opportunity bidding on schedule

## Contract status

**Implemented:** `ISqmsScheduledService.RunAsync`. **Target implementation required:** the operator-owned host and schedule. **Testing only:** controlled dates belong only in an isolated test database.

## Endpoint

No public HTTP endpoint. The scheduler must never expose this operation to a browser or request controller.

## Actor and required role

The implemented scheduler constructs the trusted `system` actor with the `System` role.

## Implemented service method

`ISqmsScheduledService.RunAsync(CancellationToken)`; callers must not invoke `ISqmsScheduledTransitionService` directly.

## Prerequisite

Complete [07-opportunity-pending-open-for-bidding](../07-opportunity-pending-open-for-bidding/README.md). The Opportunity is `OPEN TO BIDDING` version 4. Its opening audit timestamp is due under configured local calendar-day eligibility.

## Public request notes

There is no `request.http` or browser DTO. Scheduling is an operator concern.

## Matching in-process call

See `in-process.cs`. `SqmsScheduledService` reads the configured `TimeProvider`, converts it to the configured time zone, and uses calendar-day `AddDays(BiddingDays + 1)` eligibility.

## Target persistence ownership

The scheduled adapter atomically owns exactly four target columns: `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.

## Expected workflow effects

Metadata authorizes `CloseOpportunityBidding` from `OPEN TO BIDDING` for `System`, producing `CLOSED TO BIDDING` version 5. No SQMS task or SLA is created.

## Expected target-row effects

The same scheduled transaction projects `CLOSED TO BIDDING` into `Status` and `WorkflowState`, sets `ModifiedBy` to `system`, updates `ModifiedDate`, and preserves every other Opportunity column.

## Expected audit and idempotency effects

One `CloseOpportunityBidding` audit and one `SQMS:OPP:10000:4:CloseOpportunityBidding` idempotency row.

## Expected notification effects

None for the close transition. Close-first processing removes the Opportunity before open/reminder selection in the same run.

## Expected available actions

The scheduled action is not a UI action. Refresh context as an IPG actor to receive `RequestSellerApproval`.

## Failure behavior

A candidate failure is recorded in `SqmsScheduledRunResult.Failures`, makes the run unsuccessful, and processing continues to later candidates. Projection failure rolls back that candidate's workflow transition. The current scheduler has no scheduler-wide mutual-exclusion lock; operators must prevent overlapping runs.

## Next valid packs

[09-opportunity-request-seller-approval](../09-opportunity-request-seller-approval/README.md) or [02-opportunity-get-context](../02-opportunity-get-context/README.md).
