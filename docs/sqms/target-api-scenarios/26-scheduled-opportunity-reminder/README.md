# 26. Queue the scheduled Opportunity reminder

## Contract status

**Implemented:** `ISqmsScheduledService.RunAsync` queues due reminder groups. **Target implementation required:** the operator-owned host and schedule. **Testing only:** controlled dates belong in an isolated database.

## Endpoint and authority

There is no public HTTP endpoint. The scheduler derives eligibility, route, occurrence key, recipients, and template data from trusted configuration, metadata, and persisted target data.

## Prerequisite

Opportunity 10000 remains `OPEN TO BIDDING`. With the configured 15 bidding days and an opening date of 2030-01-01, the reminder occurrence date is 2030-01-15 (`OpenedDate + BiddingDays - 1`).

## Expected notification effects

One `SqmsOpportunityReminder` outbox row is queued. The exact stable-key format and this scenario's concrete key are:

```text
SQMS:OpportunityReminder:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
SQMS:OpportunityReminder:2030:Q1:2030-01-15
```

It has `WorkflowInstanceId IS NULL` and `NotificationOccurrenceKey IS NOT NULL`. The recipient, rendered content, and payload columns are immutable queue-time snapshots. The filtered unique occurrence-key index limits the occurrence to one row; rerunning reports a replay in `AlreadyQueued`.

## Expected workflow and target effects

This is notification-only work: no transition, target update, task, SLA, audit, or workflow idempotency record.

## Catch-up behavior

Eligibility uses `localDate >= occurrenceDate`, so a missed run catches up later. A due Opportunity is closed before notification selection, so a no-longer-open Opportunity receives no obsolete reminder.

## Failure behavior

Candidate errors are collected and later candidates continue. There is no scheduler-wide mutual-exclusion lock; the operator must prevent overlapping runs.
