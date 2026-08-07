# Application-Anchored Recurring Task SLA Design

**Status:** Approved conversational design, pending written-spec review

**Date:** 2026-08-06

**Scope:** Extension of the existing Milestone 5 SLA monitor and notification behavior

## 1. Goal

Extend the generic task SLA engine so a task deadline can be anchored either to:

1. A duration configured in versioned workflow metadata; or
2. A business date supplied by the calling application on its normal workflow save/action request.

For the CDT example, assigning `TradingDeskReviewTask` sends the existing assignment notification immediately. The Maker-selected Target Completion Date is forwarded as the generic SLA target date. The engine sends a due-today reminder at 9:00 AM Eastern on that date, considers the task overdue at 9:00 AM Eastern on the following calendar day, and sends one overdue reminder every calendar day until task completion.

The design must remain generic. The engine never knows the CDT field name and never parses application fields from `dataSave.payload`.

## 2. Locked Business Rules

- The assignment notification is queued immediately when the task is created.
- The target-date reminder is eligible at 9:00 AM Eastern on the application target date.
- The SLA becomes overdue at 9:00 AM Eastern on the next calendar date.
- One overdue reminder is eligible every calendar day afterward, including weekends and holidays.
- Reminder time, timezone, overdue offset, and cadence are SLA policy metadata.
- A changed application target date reschedules the currently pending application-date SLA task.
- The calling application forwards its current target date but does not compare it with a previous value or decide whether to reschedule.
- Sent reminders remain immutable history after rescheduling.
- Pending, undelivered reminders for a superseded schedule are cancelled or suppressed.
- A revised schedule can send a new due-today reminder on its revised target date.
- If task creation, rescheduling, or a delayed monitor run occurs after a scheduled occurrence, the next scan queues one reminder for the current state. It never backfills every missed day.
- Completing the task stops all future reminders.
- Duration is application/workflow configuration. It is never task input and cannot be overridden by a request.
- Local tests fast-forward injected time and complete within minutes. SIT/UAT use the configured wall-clock schedule.
- Persisted legacy SLA tasks and notification rows do not require backward-compatible migration or backfill.

## 3. Ownership Boundaries

### Calling application

The calling application owns:

- Its business field and field name, such as `targetCompletionDate`;
- Permission to edit that field;
- Business validation of the selected date;
- Persisting the application record; and
- Mapping the current field value to the generic workflow `slaInput.targetDate` value on every normal save/action call.

The application does not:

- Compare old and new SLA dates;
- Calculate UTC reminder timestamps;
- Choose SLA cadence, timezone, or reminder time;
- Track an SLA schedule version;
- Call a separate rescheduling API; or
- Override metadata-defined duration or schedule rules.

### Workflow API boundary

The Workflow API:

- Uses the existing trusted actor-identity boundary;
- Accepts the typed generic SLA input on existing workflow start/action entry points;
- Supports SLA synchronization through the same workflow action endpoint when a form save has no state transition;
- Passes the input to the engine without reading application-specific databases; and
- Returns the refreshed workflow/SLA projection.

### Generic workflow engine

The engine owns:

- Resolving the task definition and SLA policy from versioned metadata;
- Selecting duration or application-date calculation mode;
- Comparing the current application target date with the active schedule;
- Creating, retaining, superseding, completing, or cancelling task schedules;
- Timezone-aware calendar calculations;
- Reminder occurrence deduplication;
- Notification outbox creation and cancellation; and
- SLA schedule history and diagnostic evidence.

The engine can observe an external application date change only when the application's existing save/action call reaches it. It does not poll or query application-specific storage.

## 4. Metadata Model

`SlaPolicy` remains scoped by `WorkflowDefinitionVersionId` and `SlaPolicyKey`. It gains the following metadata:

| Field | Meaning |
|---|---|
| `CalculationMode` | `DurationFromTaskCreation` or `ApplicationTargetDate` |
| `DurationMinutes` | Configured duration; required only for duration mode |
| `SendDueDateReminder` | Whether to send a due-today occurrence |
| `ReminderLocalTime` | Configured wall-clock reminder time |
| `TimeZoneId` | Timezone used for calendar occurrences |
| `OverdueStartOffsetDays` | Calendar-day offset from target date to overdue state |
| `RecurrenceIntervalDays` | Null for a one-time overdue reminder; `1` for daily recurrence |
| `DueNotificationRouteKey` | Metadata route for the due-today notification |
| `OverdueNotificationRouteKey` | Metadata route for overdue notifications |
| `IsActive` | Existing active-policy flag |

Required policy combinations are validated by seed verification and again before runtime use.

### Duration mode

- `DurationMinutes` is configured once in the policy row.
- `WorkflowTaskDefinition.SlaPolicyKey` selects the policy.
- `DueAtUtc = task.CreatedAtUtc + DurationMinutes`.
- The request cannot supply or change duration.
- Existing clean-seed duration behavior remains: no due-today reminder and one overdue notification unless policy metadata explicitly configures otherwise.

### Application-target-date mode

For CDT `TradingDeskReviewTask`, policy metadata is:

- `CalculationMode = ApplicationTargetDate`
- `SendDueDateReminder = true`
- `ReminderLocalTime = 09:00`
- `TimeZoneId = America/New_York`
- `OverdueStartOffsetDays = 1`
- `RecurrenceIntervalDays = 1`

The application supplies only `targetDate`. The request cannot supply timing, timezone, offset, cadence, route keys, or `DueAtUtc`.

Each calendar occurrence is converted independently through `TimeZoneInfo`; recurrence must not be implemented as `previousUtc + 24 hours`, because daylight-saving offsets can change.

## 5. Same-API SLA Input Contract

The API-level request gains an optional typed block:

```json
{
  "workflowInstanceId": "WF-CDT-1001",
  "actionKey": "CheckerSendToTradingDesk",
  "slaInput": {
    "targetDate": "2026-08-10"
  }
}
```

The same typed input is available on workflow start when an initial action creates an SLA task.

The request carries the application's current value every time. Supplying it does not make it authoritative over metadata. The engine uses it only when the active or newly created task resolves to an `ApplicationTargetDate` policy. Duration and no-SLA task behavior cannot be changed by this input.

### Synchronization without a state transition

The existing workflow action endpoint supports a synchronization-only path for a normal form save that has no workflow transition. In that path:

- `actionKey` is absent;
- The engine compares `slaInput.targetDate` with the current pending task schedule;
- Workflow state, workflow-instance version, and task ownership do not change;
- No synthetic `WorkflowAudit` action is written; and
- Schedule-version evidence records any SLA change.

This is the same API endpoint and application integration path, not a second SLA rescheduling endpoint.

### Validation matrix

| Effective task effect | SLA input rule |
|---|---|
| Pending application-date task remains active | Current target date is required and synchronized |
| Transition creates an application-date task | Current target date is required and creates schedule version 1 |
| Transition closes the application-date task and creates no new one | Task completion wins; no target date is required for the closing task |
| Transition closes one task and creates another application-date task | Current target date initializes the new task |
| Duration or no-SLA task | Supplied target date has no authority and does not alter policy behavior |

The canonical idempotency hash includes the effective SLA input. A repeated identical request produces one effective schedule result.

## 6. Task Creation and Synchronization Flow

Task creation remains in the existing action transaction:

```text
Resolve transition and task definition
-> resolve SLA policy
-> validate current SLA input against metadata
-> calculate effective schedule
-> move workflow state
-> close current task
-> create next task and schedule version 1
-> queue assignment notification
-> write existing workflow audit/idempotency evidence
-> commit
```

For an existing pending application-date task, the engine compares the received target date with the current schedule:

- No active schedule: create version 1.
- Same target date: successful no-op; do not increment schedule version.
- Different target date: supersede the current schedule, cancel pending reminder occurrences, and create the next schedule version.

If an action closes the pending task, completion takes precedence over rescheduling that task. If the action creates another application-date task, the current input is evaluated for the new task.

Schedule synchronization has its own optimistic concurrency boundary. It does not increment `WorkflowInstance.Version`, because it does not change workflow state. Concurrent identical updates converge through idempotency and unique constraints. For concurrent different inputs, one update commits and the other receives a retryable concurrency result; retrying the same normal save/action request synchronizes the latest application value without a separate SLA call.

## 7. Persistence Model

### `WorkflowTaskSlaSchedule`

A versioned schedule table records task SLA state and history:

| Field group | Contents |
|---|---|
| Identity | `Id`, `WorkflowTaskId`, `ScheduleVersion`, `SlaPolicyKey` |
| Input | Nullable application `TargetDate` |
| Resolved schedule | Nullable `DueReminderAtUtc`, `OverdueAtUtc`, `NextReminderAtUtc` |
| Effective snapshot | Calculation mode, timezone, local reminder time, overdue offset, recurrence interval, notification route keys |
| Lifecycle | `Active`, `Superseded`, or `Completed` status |
| Evidence | Created/completed/superseded timestamps, actor, correlation, and idempotency evidence |

Constraints enforce:

- One active schedule per workflow task;
- Unique `(WorkflowTaskId, ScheduleVersion)`; and
- Positive, monotonically increasing versions.

Schedule history is not overwritten. Operational fields such as `NextReminderAtUtc` advance on the active version.

### `WorkflowTask.DueAtUtc`

`WorkflowTask.DueAtUtc` remains the stable task-deadline projection consumed by the existing context model:

- Duration policy: task creation plus configured duration.
- Application-date policy: target date plus `OverdueStartOffsetDays` at the configured local reminder time, converted to UTC.

For CDT, this is target date plus one calendar day at 9:00 AM Eastern. The task remains `OnTrack` during the target date even though a due-today notification is sent.

### `NotificationOutbox`

The outbox is also the durable reminder-occurrence ledger. SLA rows gain:

- `WorkflowTaskSlaScheduleId`;
- `SlaOccurrenceDate` in the schedule timezone;
- `SlaReminderKind` (`DueToday` or `Overdue`);
- `CancelledAtUtc`; and
- `CancellationReason`.

`NotificationOutbox.Status` adds `Cancelled`.

A unique constraint on `(WorkflowTaskSlaScheduleId, SlaOccurrenceDate, SlaReminderKind)` prevents duplicate occurrences while allowing one reminder on every calendar date and allowing a revised schedule to generate its own occurrences.

Non-SLA assignment, return, completion, and other workflow notification rows leave these SLA fields null.

## 8. SLA Monitor Algorithm

`run-sla` selects active schedules for pending tasks on active workflow instances where `NextReminderAtUtc <= now`.

Each candidate is processed in its own transaction:

1. Re-read the workflow instance, task, and current active schedule.
2. Confirm the instance is active and the task is pending.
3. Convert `now` to the policy timezone and determine the current local calendar date.
4. Determine the current reminder kind:
   - Target date before overdue start: `DueToday`.
   - Overdue start date or later: `Overdue`.
5. Insert at most one occurrence for the current local date.
6. Advance `NextReminderAtUtc` to the next configured calendar occurrence.
7. For one-time duration policies, set `NextReminderAtUtc = null` after the overdue occurrence.

If multiple occurrences were missed, the monitor queues only the current applicable occurrence. It never inserts historical rows for missed dates.

The configured time is an eligibility boundary. An external scheduler invokes the existing monitor regularly; the occurrence is queued on the first scan at or after the boundary. No broker, hosted scheduling framework, or third-party scheduling package is added.

## 9. Notification Behavior

- Task assignment notification behavior remains immediate and unchanged.
- Due-today and overdue reminders use separate metadata notification routes.
- Both reuse the task's stored ordered To/Cc recipient snapshot.
- Reminder template context includes workflow identity, task key, assigned role, target date, occurrence date, reminder kind, due reminder instant, and overdue-start instant.
- The existing outbox sender abstraction, MailKit SMTP sender, recipient override, and message-level retry lifecycle remain in use.

Rescheduling:

- Leaves sent rows unchanged;
- Marks pending old-schedule rows `Cancelled`;
- Prevents cancelled rows from being claimed for delivery; and
- Requires the dispatcher to recheck that the referenced schedule is still active immediately before SMTP delivery. An obsolete claimed row is cancelled rather than sent.

Task completion marks the active schedule `Completed`, cancels pending SLA reminder rows, and removes the task from future scans.

## 10. Workflow Context

The existing SLA projection keeps:

- `status`;
- `dueAtUtc`;
- `overdue`; and
- `overdueByMinutes`.

It gains:

- `calculationMode`;
- `targetDate` when applicable;
- `scheduleVersion`; and
- `nextReminderAtUtc`.

The application does not use `scheduleVersion` to decide whether a reschedule is necessary. It is operational evidence returned by the engine.

## 11. Errors and Concurrency

- A missing or malformed required target date returns validation failure before workflow/task side effects.
- Invalid or inconsistent policy metadata fails seed verification and runtime validation.
- A past target date is valid and produces one current catch-up occurrence at the next scan.
- An unchanged target date is a successful no-op.
- Duplicate and concurrent monitor scans are safe through occurrence uniqueness and per-task transactions.
- Template or recipient failures retain the existing message-level retry behavior.
- Cancelled outbox rows are terminal and are never retried.
- No request may specify `toState` or override task/SLA metadata through `workflow.options` or SLA input.
- All current-time reads use injected `TimeProvider`; no new direct `DateTime.UtcNow` calls are permitted.

## 12. CDT Seed Configuration

On a clean database:

- `TradingDeskReviewTask` references a new application-target-date SLA policy.
- Its policy uses 9:00 AM `America/New_York`, due-today notification enabled, overdue offset one calendar day, and daily recurrence.
- Checker, Trading Desk Rework, and BDRM tasks retain duration-mode policies unless separately configured later.
- The generic mode can be assigned to any future task definition without engine changes.

The sample that sends Checker work to Trading Desk includes `slaInput.targetDate`. The engine remains unaware that CDT calls the application field Target Completion Date.

## 13. Clean-Schema Deployment

Persisted legacy SLA tasks and outbox rows are not supported by this change.

- No legacy schedule backfill is implemented.
- No legacy deduplication compatibility path is implemented.
- Local, SIT, and UAT verification starts from a database initialized from the latest migrations and seed metadata.
- Deployment must ensure no legacy in-flight workflow/SLA data requires preservation.
- The implementation must not silently delete an existing database; recreation or cleanup is an explicit operator/deployment step.

Newly seeded duration-mode policies continue to provide the current duration-based behavior. “No backward compatibility” applies to persisted legacy data, not to removal of duration calculation mode.

## 14. Verification Strategy

### Unit coverage

- Duration deadline calculation and request non-override.
- Application target-date calculation.
- Eastern timezone conversion across daylight-saving boundaries.
- Due-today, overdue-start, and next-calendar-day calculations.
- Late and past-date catch-up with no backfill.
- Same-date no-op and changed-date rescheduling.

### SQL-backed integration coverage

- Task and schedule creation in one transaction.
- Same-API synchronization with and without a state transition.
- One active schedule and monotonically increasing versions.
- Superseded and completed schedule cancellation.
- Occurrence-level duplicate prevention under repeated and concurrent scans.
- Concurrent SLA input handling and idempotent replay.
- Clean migration and seed verification.

### API and contract coverage

- Workflow start/action SLA input mapping.
- Synchronization-only requests on the same endpoint.
- Required-target validation based on effective task metadata.
- Metadata authority over duration and schedule behavior.
- Refreshed workflow context SLA projection.

### Notification and SMTP coverage

- Immediate assignment notification.
- Due-today and recurring overdue templates.
- Stored To/Cc snapshot reuse.
- Superseded reminder suppression.
- Completed-task reminder suppression.
- Message-level retry and smtp4dev delivery.

## 15. Fast Local Acceptance Scenario

Local testing does not wait for real 9:00 AM or calendar days. It uses the same seeded policy as SIT/UAT; it does not replace calendar rules with a minute-based policy. The actual API and smtp4dev run normally while the existing monitor time override fast-forwards the SLA clock:

1. Initialize and verify a clean database.
2. Start the Workflow API and smtp4dev.
3. Start CDT through the actual HTTP API.
4. Move the workflow to Trading Desk with a target date.
5. Confirm the assignment email arrives immediately.
6. Run `run-sla --now` at the target date's 9:00 AM Eastern instant.
7. Process the outbox and confirm one due-today email in smtp4dev.
8. Repeat the same scan and confirm no duplicate.
9. Run at the following date's 9:00 AM Eastern instant and confirm one overdue email.
10. Run later simulated calendar dates and confirm one occurrence per date.
11. Send a changed target date through the same Workflow API and confirm automatic engine rescheduling.
12. Prove old pending reminders are cancelled and the revised schedule can send its own due-today occurrence.
13. Complete the task and prove future simulated scans queue nothing.
14. Run a duration-mode task scenario to confirm configured duration behavior remains intact.

This complete multi-day scenario finishes within minutes. SIT/UAT use the actual configured 9:00 AM Eastern schedule rather than a request-level time override.

## 16. Quality Gates

Before implementation is declared complete:

- Restore and build both solutions with warnings as errors.
- Run default automated tests.
- Run required SQL-backed integration tests.
- Apply latest migrations to a clean database.
- Run `verify-seed`.
- Execute updated sample JSON.
- Run opt-in SMTP tests against smtp4dev.
- Execute the full fast-forwarded live HTTP/SLA/SMTP scenario.
- Leave the Workflow API and smtp4dev running only when the user requests interactive review.

The feature is an approved extension of the existing SLA/notification milestone. It does not replace the milestone mapping or introduce a new project roadmap.

## 17. Explicit Non-Goals

- Business-day or holiday calendars;
- Parallel workflow tasks;
- Per-task duration overrides;
- Application database polling;
- A separate SLA rescheduling API;
- Request-authoritative schedule configuration;
- Message brokers, CQRS, MediatR, BPMN, or a scheduling framework;
- New external packages; and
- Legacy persisted SLA data migration or backfill.
