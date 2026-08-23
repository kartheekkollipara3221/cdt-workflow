# 25. Queue the scheduled Opportunity-open notification

## Contract status

**Implemented:** `ISqmsScheduledService.RunAsync` groups eligible still-open Opportunities and queues an outbox row. **Target implementation required:** the operator-owned host and schedule. **Testing only:** controlled dates and IDs below belong in an isolated database.

## Endpoint

There is no public HTTP endpoint. A browser never supplies the occurrence key, route, recipients, or template data.

## Prerequisite

Opportunity 10000 is `OPEN TO BIDDING`, its opening audit date is 2030-01-01 in the configured local time zone, and its effective sale date is in Q1 2030. The run date is on or after 2030-01-02 but before close eligibility.

## Matching in-process call

See `in-process.cs`. Call `ISqmsScheduledService`, not the internal transition service or notification queue.

## Expected notification effects

One `SqmsOpportunityOpen` message is queued for the due year/quarter/date group. The exact stable-key format and this scenario's concrete key are:

```text
SQMS:OpportunityOpen:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
SQMS:OpportunityOpen:2030:Q1:2030-01-02
```

The outbox row has `WorkflowInstanceId IS NULL` and a non-null `NotificationOccurrenceKey`. `NotificationRecipientsJson`, `Subject`, `Body`, `HtmlBody`, and `PayloadJson` are rendered and stored when queued; delivery later uses these immutable snapshots rather than re-resolving recipients or templates. The filtered unique index on non-null occurrence keys permits exactly one row for this occurrence. A rerun returns it as already queued instead of inserting a duplicate.

## Expected workflow and target effects

Notification-only queueing does not transition a workflow and does not update the target Opportunity row. It creates no task, SLA, transition audit, or workflow idempotency record.

## Failure behavior

Missing required effective date, dashboard-link configuration, route metadata, recipient metadata, or template data is reported in `SqmsScheduledRunResult.Failures`; later candidates continue.

## Verification

Run the read-only statements in `verify.sql`. Snapshot comparison is required to prove the target row is unchanged.
