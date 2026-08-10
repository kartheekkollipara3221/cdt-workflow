# SLA Email Inherits Task Assignment Design

**Date:** 2026-08-10
**Status:** Approved for implementation
**Scope:** Shared workflow engine SLA notification content

## Goal

Make every SLA due-today and overdue email preserve the exact language of the original task-assignment email.

Only the subject prefix changes:

- Due today: `Due today: <original assignment subject>`
- Overdue: `Overdue: <original assignment subject>`

The plain-text and HTML bodies are copied unchanged from the original task-assignment outbox row. The existing generic SLA bodies are no longer delivered.

## Current Behavior

`RunSlaService` renders the selected SLA notification route's own subject, plain-text body, and HTML body. Portfolio Trade currently seeds generic SLA subjects such as `Workflow task due today` and `Workflow task overdue`. The overdue path prepends `Overdue: `, producing subjects such as `Overdue: Workflow task overdue`.

Task creation already persists two immutable pieces of evidence:

1. `WorkflowTask.NotificationRecipientsJson`, reused by later SLA notifications.
2. The original rendered assignment `NotificationOutbox` row, linked by `WorkflowTaskId`, containing the exact subject, plain-text body, and HTML body delivered for the task assignment.

## Chosen Approach

Use the durable original task-assignment outbox row as the SLA email-content source.

For each eligible SLA occurrence, `RunSlaService` will load exactly one primary outbox row satisfying all of the following:

- `WorkflowTaskId` equals the active task ID.
- `SlaReminderKind` is null, distinguishing the primary assignment from SLA occurrences.

The primary row's metadata-defined `NotificationType` is not constrained to a Portfolio Trade value. The task linkage and absence of an SLA occurrence marker define the generic relationship.

The primary row's delivery status does not affect content inheritance. A pending or failed original delivery still contains the committed original rendered content and remains the historical source.

This approach is preferred because it:

- reproduces the exact historical wording rather than re-rendering mutable metadata;
- reuses an existing immutable record instead of duplicating content on `WorkflowTask`;
- supports already-persisted tasks whose primary outbox rows exist;
- requires no public-contract or SQL-schema change.

## SLA Outbox Construction

The new SLA outbox row keeps its existing SLA identity and scheduling data:

- `NotificationType` remains `SlaTaskDueToday` or `SlaTaskOverdue` from the selected SLA route.
- `SlaReminderKind`, occurrence timestamp/date, schedule ID, correlation ID, and SLA payload remain unchanged.
- `NotificationRecipientsJson` continues to copy the task recipient snapshot exactly.
- `Body` copies the primary assignment row's `Body` exactly.
- `HtmlBody` copies the primary assignment row's `HtmlBody` exactly.
- `Subject` is the selected SLA route's metadata-owned prefix followed by the primary assignment row's `Subject`.

Portfolio Trade SLA prefix metadata becomes:

- `RouteSlaTaskDueToday`: `Due today: `
- `RouteSlaTaskOverdue`: `Overdue: `

The existing persisted `OverdueSubjectPrefix` route column will act as the route-owned SLA prefix for both SLA route types. This avoids a schema migration while keeping the wording metadata-driven. Its legacy name does not change the public or database contract.

The SLA route's current generic subject/body templates remain persisted because the existing schema requires them, but `RunSlaService` will not use those templates as delivered email content for due-today or overdue occurrences.

## Failure Handling

Content inheritance fails closed.

If zero or more than one primary assignment outbox row is found for the task:

- do not queue the SLA notification;
- do not advance the SLA schedule;
- roll back the occurrence transaction;
- emit a warning identifying the task and the missing or ambiguous primary assignment email;
- do not fall back to generic SLA subject or body text.

If the prefixed subject exceeds the persisted subject-length limit:

- do not truncate or rewrite the original subject;
- skip and warn using the same rollback behavior.

Existing malformed-recipient and inactive-schedule safeguards remain unchanged.

## Generic Engine Boundary

The implementation remains workflow-generic:

- It identifies the primary email by generic task/outbox linkage and the absence of an SLA occurrence marker.
- It does not hardcode `WorkflowTaskAssigned` or any Portfolio Trade notification type.
- It does not inspect Portfolio Trade fields.
- Prefix wording remains versioned notification-route metadata.
- Request JSON does not supply subjects, bodies, prefixes, routes, or SLA configuration.
- Public Workflow Start and Action DTOs do not change.
- Recipient routing and task snapshot semantics do not change.

## Testing Strategy

Implementation follows red-green-refactor TDD.

Focused SQL-backed tests will prove:

1. Due-today copies the original assignment plain/HTML bodies and produces `Due today: <original subject>`.
2. Overdue copies the original assignment plain/HTML bodies and produces `Overdue: <original subject>`.
3. Recurrent SLA occurrences continue using the same original assignment content.
4. SLA recipient JSON remains byte-for-byte equal to the task snapshot.
5. Missing primary assignment content queues nothing, advances nothing, and reports a warning.
6. Ambiguous primary assignment content queues nothing, advances nothing, and reports a warning.
7. An over-length prefixed subject queues nothing and reports a warning.
8. A primary task email with a notification type other than `WorkflowTaskAssigned` is still used, proving the lookup remains workflow-generic.
9. Seed verification expects the due-today and overdue prefixes.
10. Existing task completion, cancellation, deduplication, concurrency, recurrence, and no-SLA behavior remains green.

After focused tests pass, run the warnings-as-errors build, the full shared SQL-backed suite, Workflow API build/tests, isolated `init-db`/`verify-seed`, and a live API plus Console SLA scenario. The live scenario must verify smtp4dev receives inherited assignment content with the correct due-today and overdue prefixes.

## Non-Goals

- No change to the original task-assignment email.
- No change to SLA timing, recurrence, or occurrence identity.
- No change to recipient resolution or snapshots.
- No change to API request or response DTOs.
- No database migration.
- No fallback to the old generic SLA email content.
