# 27. Scheduled catch-up closes before notifications

## Contract status

**Implemented:** the ordering inside `ISqmsScheduledService.RunAsync`. **Target implementation required:** scheduling and non-overlap controls. **Testing only:** arrange due dates only in an isolated test database.

## Endpoint

No public HTTP endpoint exists or is recommended.

## Implemented order

One run performs these stages in order:

1. Read currently open Opportunities and close every due candidate.
2. Read closed Opportunities with submitted Bids and classify their valid Bids.
3. Read open Opportunities again and queue due open/reminder year-quarter groups only for Opportunities still open.

This fresh final read prevents an overdue Opportunity closed during catch-up from contributing to an obsolete group email.

## Example

Opportunity 10000 is beyond `OpenedDate + BiddingDays + 1` and closes to `CLOSED TO BIDDING`. Its submitted Bids are then classified. It is absent from the fresh still-open set, so neither `SqmsOpportunityOpen` nor `SqmsOpportunityReminder` is queued for it during this run. Opportunity 10001, if still open and due for a group notice, may still contribute.

## Persistence ownership

Each successful scheduled transition atomically projects exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`; all other target columns remain unchanged. Notification-only queueing updates no target row.

## Failure behavior

Failures are collected per Opportunity/Bid/group and later candidates continue. A failed candidate does not reorder the stages. There is no scheduler-wide lock, so the operator must prevent overlapping runs.
