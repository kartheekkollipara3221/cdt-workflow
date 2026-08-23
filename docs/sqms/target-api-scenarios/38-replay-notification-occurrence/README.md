# 38. Notification occurrence replay

## Contract status

- **Implemented:** notification-only occurrence idempotency returns the existing immutable outbox row.
- **Target implementation required:** the operator host invokes scheduling and treats an unsuccessful run as a nonzero exit.
- **Testing only:** use an isolated occurrence fixture when demonstrating the two calls.

## Endpoint

No public HTTP endpoint exists or is recommended.

## Actor and required role

The server-owned scheduler uses its system identity; browser actor input is not accepted.

## Implemented service method

`IQueueWorkflowNotificationService.QueueAsync`, normally reached from `ISqmsScheduledService.RunAsync`.

## Prerequisite

Use a due grouped occurrence as documented by [25-scheduled-opportunity-open-notification](../25-scheduled-opportunity-open-notification/README.md).

## Public request notes

There is no public request and no browser-controlled occurrence key, route, recipients, or template.

## Matching in-process call

See `in-process.cs`. Queue the exact same effective occurrence twice with key `SQMS:OpportunityOpen:2030:Q1:2030-01-02`.

## Target persistence ownership

Notification-only queueing writes no Opportunity or Bid row.

## Expected workflow effects

No workflow transition, task, SLA, or workflow audit occurs.

## Expected target-row effects

No target row changes.

## Expected audit and idempotency effects

The occurrence key is the server-owned idempotency identity. The second result is successful replay and returns the same `NotificationOutboxId`.

## Expected notification effects

There is exactly one outbox row with one immutable recipient/template snapshot; replay does not create a second delivery lifecycle.

## Expected available actions

Not applicable because this is notification-only scheduling.

## Failure behavior

Reusing the occurrence key with different effective route/input content is a conflict. Do not modify the existing outbox snapshot.

## Next valid packs

Process the row as documented by [30-notification-outbox-smtp-delivery](../30-notification-outbox-smtp-delivery/README.md).
