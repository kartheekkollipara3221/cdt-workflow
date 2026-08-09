# Duration SLA Minute Recurrence Design

**Status:** Approved for implementation planning

**Date:** 2026-08-09

**Scope:** Generic sub-day recurrence for duration-based SLA overdue notifications

## 1. Goal

Duration-based workflow tasks must support an initial overdue deadline followed by recurring overdue notifications at a separate minute-based cadence until the task is completed.

For the Portfolio Trade Checker example:

- Demo metadata uses a two-minute initial deadline and a two-minute recurrence.
- Production metadata uses a 240-minute initial deadline and a 60-minute recurrence.
- A delayed SLA scan queues only the latest eligible occurrence and skips older missed occurrences.

The engine remains generic and metadata-driven. It must not contain Checker-, CDT-, or Portfolio Trade-specific scheduling branches.

## 2. Confirmed Current Gap

- `CheckerReviewSla` currently has `DurationMinutes = 2` and no recurrence, so its first overdue notification clears `NextReminderAtUtc`.
- Duration recurrence currently supports only calendar-day recurrence through `RecurrenceIntervalDays`, `ReminderLocalTime`, and `TimeZoneId`.
- SLA outbox uniqueness currently uses schedule ID, occurrence date, and reminder kind. That identity permits only one reminder kind per schedule per date and therefore cannot represent multiple hourly or minute-based reminders on the same date.
- SLA correlation IDs also contain only the occurrence date and reminder kind, so they are not sufficient for multiple same-day occurrences.

## 3. Locked Requirements

- Add generic minute-based recurrence for `DurationFromTaskCreation` policies.
- Keep the initial deadline and recurrence interval independent.
- Demo Checker policy: first overdue notification at task creation plus 2 minutes; subsequent overdue notifications every 2 minutes.
- Production Checker policy: first overdue notification at task creation plus 240 minutes; subsequent overdue notifications every 60 minutes.
- Production cadence comes from versioned SLA metadata. Public requests cannot supply duration or recurrence settings.
- The normal Portfolio Trade sample/demo seed remains `2/2`; production deployment metadata supplies `240/60`.
- Recurrence applies only to tasks created after the policy metadata is updated. Existing schedule snapshots remain unchanged.
- Continue reminders until the task closes and its SLA schedule is completed.
- An hourly production console job runs `run-sla` and then `process-notification-outbox`.
- A reminder may be delivered on the first hourly poll after its scheduled time.
- Delayed scans queue only one reminder: the latest scheduled slot at or before the scan time. Older missed slots are not backfilled.
- Repeated or concurrent scans cannot queue the same scheduled occurrence twice.
- Preserve existing application-date recurrence and BDRM duration-plus-calendar-day recurrence.
- Preserve existing sent notification history.

## 4. Metadata Model

Add nullable `RecurrenceIntervalMinutes` to both:

- `SlaPolicy`, where versioned workflow metadata owns the configured cadence; and
- `WorkflowTaskSlaSchedule`, where the task snapshots the policy value at schedule creation.

`DurationMinutes` continues to mean the delay from task creation to the first overdue threshold. `RecurrenceIntervalMinutes` means the elapsed-time interval between later overdue occurrences.

Validation rules:

- A non-null minute interval must be greater than zero.
- Minute recurrence is valid only for `DurationFromTaskCreation`.
- `RecurrenceIntervalMinutes` and `RecurrenceIntervalDays` cannot both be populated on one policy or schedule.
- Minute recurrence does not require `ReminderLocalTime` or `TimeZoneId` because it is an elapsed UTC cadence anchored to the first duration deadline.
- Existing day recurrence retains its current local-time and timezone requirements.

Portfolio Trade sample metadata becomes conceptually:

```text
CheckerReviewSla:
  CalculationMode = DurationFromTaskCreation
  DurationMinutes = 2
  RecurrenceIntervalMinutes = 2
  RecurrenceIntervalDays = null
```

Production metadata uses the same shape with `DurationMinutes = 240` and `RecurrenceIntervalMinutes = 60`.

## 5. Exact Occurrence Identity

Add nullable `SlaOccurrenceAtUtc` to `NotificationOutbox`.

Every newly queued SLA outbox row must populate it with the exact scheduled occurrence instant. Non-SLA outbox rows leave it null. Historical SLA outbox rows remain unchanged and may also have null because their exact scheduled instant is not reliably derivable for every existing recurrence mode.

Replace the current filtered unique index on:

```text
WorkflowTaskSlaScheduleId + SlaOccurrenceDate + SlaReminderKind
```

with a filtered unique index on:

```text
WorkflowTaskSlaScheduleId + SlaOccurrenceAtUtc + SlaReminderKind
```

The filter applies only when the schedule ID and exact occurrence instant are non-null. `SlaOccurrenceDate` remains for calendar reporting and existing inspection output, but it is no longer the durable uniqueness key.

SLA correlation IDs must also include the exact scheduled occurrence instant, preferably its UTC ticks, so multiple same-day reminders have distinct deterministic correlation IDs.

The SLA render payload adds `slaOccurrenceAtUtc` while retaining `slaOccurrenceDate` and `slaReminderKind`.

## 6. Duration Recurrence and Catch-Up Algorithm

`NextReminderAtUtc` remains the earliest scheduled occurrence that has not been processed or intentionally skipped.

For a minute-recurring duration schedule selected by `NextReminderAtUtc <= now`:

1. Start with `next = schedule.NextReminderAtUtc`.
2. Calculate how many complete recurrence intervals fit between `next` and `now`.
3. Set `occurrenceAtUtc` to `next` plus that number of intervals. This is the latest scheduled slot at or before `now`.
4. Queue exactly one outbox row for `occurrenceAtUtc`.
5. Advance `NextReminderAtUtc` to `occurrenceAtUtc + RecurrenceIntervalMinutes`.

Example with a four-hour initial deadline and hourly recurrence:

```text
Task created:       08:20
First deadline:     12:20
Scheduled slots:    12:20, 13:20, 14:20, 15:20, ...
Console resumes:    15:05
Queued occurrence:  14:20
Next reminder:      15:20
```

If the console instead resumes at 15:35, it queues only 15:20 and advances to 16:20. It does not queue 12:20, 13:20, and 14:20.

The cadence remains anchored to the first deadline rather than drifting from actual processing time.

## 7. Other Recurrence Modes

No scheduling behavior changes for:

- application-target-date due-today and overdue recurrence; or
- duration-based BDRM recurrence that switches to a configured local calendar time on later dates.

Those modes also populate `SlaOccurrenceAtUtc` for newly queued rows, using their already-calculated eligible occurrence instant. Their calendar catch-up and no-backfill behavior remains unchanged.

One-time duration schedules still clear `NextReminderAtUtc` after their first occurrence when both recurrence fields are null.

## 8. Completion, Cancellation, and Concurrency

The existing task-completion path remains authoritative:

- Completing a task marks its active SLA schedule completed.
- Future SLA scans exclude completed tasks and inactive schedules.
- A pending or claimed occurrence whose schedule becomes inactive is cancelled before sender invocation.

Concurrent scans may resolve the same exact occurrence, but the new exact-instant unique index permits only one durable row. The existing unique-violation handling rolls back the losing transaction, clears tracked state, and lets a later scan re-evaluate the schedule.

No task completion, recipient snapshot, notification template, outbox retry, or SMTP behavior changes are required.

## 9. Migration and Existing Data

The EF Core migration will:

- add nullable `RecurrenceIntervalMinutes` columns to `SlaPolicy` and `WorkflowTaskSlaSchedule`;
- add nullable `SlaOccurrenceAtUtc` to `NotificationOutbox`;
- add positive-value and recurrence-mutual-exclusion check constraints;
- drop the date-only SLA occurrence unique index; and
- create the exact-instant filtered unique index.

It will not:

- change active or completed schedule snapshots;
- populate recurrence on existing Checker schedules;
- change `NextReminderAtUtc` on existing schedules;
- rewrite sent outbox payloads or bodies; or
- replay historical reminders.

The normal seed upsert sets the Checker sample policy recurrence to two minutes. Only schedules created afterward snapshot that value.

## 10. Production Operation

The production scheduler invokes these commands in order:

```text
run-sla
process-notification-outbox
```

Running hourly with production `240/60` metadata means:

- the first reminder is eligible four hours after task creation;
- the first job after that instant queues it;
- subsequent hourly slots are anchored to the four-hour deadline; and
- a delayed or recovered job sends only the latest eligible slot.

Operational scheduling, single-job ownership, logs, and alerting belong to deployment. The engine continues to provide database-backed duplicate suppression and message-level outbox retry.

## 11. Test-Driven Verification

Implementation begins with failing tests that prove:

- the Checker sample policy and new task schedule snapshot `2/2` metadata;
- a first duration reminder queues at the initial two-minute deadline;
- another reminder can queue two minutes later on the same UTC date;
- same-slot rescans do not duplicate an occurrence;
- a delayed scan queues only the latest eligible slot and advances to the next anchored slot;
- production-style `240/60` calculations produce the correct first and subsequent instants;
- completing the task prevents later recurring reminders;
- BDRM daily recurrence and application-date recurrence retain existing behavior;
- new SLA rows populate exact occurrence instants and distinct correlation IDs;
- historical outbox rows and existing active schedule snapshots are not backfilled or replayed by migration;
- model constraints and the new filtered unique index are present; and
- `init-db` plus `verify-seed` succeed against SQL Server.

After focused tests pass, run the shared engine and Workflow API warning-as-error builds and default test suites sequentially with `-m:1`.

## 12. Expected Files

- SLA policy, schedule, and notification outbox entities
- `WorkflowDbContext` model configuration and snapshot
- A new EF Core migration
- Portfolio Trade seed records and seed upsert/verification
- SLA schedule synchronization and run/advance logic
- SLA result/console diagnostics where exact occurrence time is exposed
- Focused SLA, seed, schema, migration, completion, and regression tests
- `CODEX_HANDOFF.md` after verification

No public Workflow Start, Action, or Context DTO change is expected. No caller-owned SLA configuration, new endpoint, third-party package, background service, or Checker-specific engine branch is introduced.
