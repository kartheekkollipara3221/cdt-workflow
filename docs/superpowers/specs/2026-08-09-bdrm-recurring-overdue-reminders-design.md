# BDRM Recurring Overdue Reminder Design

**Status:** Approved

**Date:** 2026-08-09

**Scope:** Surgical extension of the existing Milestone 5 duration-based SLA behavior

## 1. Goal

When a `BdrmClientServiceReviewTask` remains pending, send its first overdue notification two minutes after task creation and then send one overdue reminder every day until that task closes.

This behavior remains generic and metadata-driven. Portfolio Trade selects the behavior through its seeded SLA policy; reusable scheduler code does not branch on the BDRM task or workflow key.

## 2. Locked Business Rules

- The BDRM task becomes overdue two minutes after creation.
- The first overdue notification is eligible at that overdue instant.
- After the first notification, another overdue notification is eligible every 24 hours.
- Every reminder uses the exact ordered To/Cc recipient snapshot persisted on that BDRM task.
- Repeated or concurrent SLA scans must not create the same daily occurrence twice.
- A delayed scan queues only the current applicable reminder and does not backfill every missed day.
- `BdrmTradeFulfilled` closes the BDRM task, completes its active SLA schedule, and stops future reminders.
- `BdrmTradeNotExecutedProperly` closes the BDRM task, completes its active SLA schedule, and stops future BDRM reminders before ownership moves to Trading Desk Rework.
- If Trading Desk later executes the trade again, the newly created BDRM task receives a new schedule. Its first overdue notification is eligible two minutes after that new task's creation.
- Pending or processing reminder rows for a completed BDRM schedule are cancelled before delivery. Sent reminders remain immutable history.

## 3. Metadata Design

The existing `RecurrenceIntervalDays` metadata is sufficient and remains authoritative.

The seeded `BdrmClientServiceReviewSla` policy becomes:

| Field | Value |
|---|---|
| `CalculationMode` | `DurationFromTaskCreation` |
| `DurationMinutes` | `2` |
| `SendDueDateReminder` | `false` |
| `RecurrenceIntervalDays` | `1` |
| `OverdueNotificationRouteKey` | `RouteSlaTaskOverdue` |

`null` recurrence continues to mean a one-time duration overdue notification. A positive recurrence interval means the overdue notification repeats at that interval while the task and schedule remain active.

The public Workflow API does not accept duration or recurrence fields. The UI cannot override this policy.

## 4. Scheduler Behavior

The generic duration-SLA path must honor the schedule's snapshotted `RecurrenceIntervalDays` value:

1. Schedule creation sets `OverdueAtUtc = task.CreatedAtUtc + DurationMinutes` and initially sets `NextReminderAtUtc` to the overdue instant.
2. `run-sla` selects the schedule only while its workflow instance is active, its task is pending, its schedule is active, and `NextReminderAtUtc <= now`.
3. The first eligible scan queues one `SlaTaskOverdue` outbox row.
4. If recurrence is null, the scheduler clears `NextReminderAtUtc`, preserving existing one-time behavior for Checker and Trading Desk Rework policies.
5. If recurrence is positive, the scheduler advances `NextReminderAtUtc` to the next interval-aligned instant after the processed scan time.
6. When scans are delayed across one or more expected instants, the scheduler queues one current occurrence and advances to the next future interval. It does not emit a burst of historical reminders.

Duration recurrence uses exact elapsed-day intervals from the original overdue instant. It does not use application-date timezone/calendar calculations; those remain unchanged for `ApplicationTargetDate` policies such as Trading Desk review.

## 5. Occurrence Identity

Daily duration reminders can continue using the existing SLA occurrence identity:

```text
(WorkflowTaskSlaScheduleId, SlaOccurrenceDate, SlaReminderKind)
```

For duration recurrence, `SlaOccurrenceDate` is the UTC date of the current applicable interval occurrence. Because this requirement permits only one reminder per day, the existing filtered unique constraint remains sufficient.

No database-schema migration is required.

## 6. Task Closure and Delivery Suppression

The existing task-close path remains the stopping authority:

```text
Close BDRM task
-> mark active SLA schedule Completed
-> clear NextReminderAtUtc
-> cancel Pending or Processing SLA outbox rows
-> commit transition
```

The notification dispatcher rechecks the referenced SLA schedule immediately after claiming a reminder. If the schedule is no longer active, it marks the reminder `Cancelled` and does not invoke SMTP.

This prevents a race where fulfillment or return-to-rework happens after a reminder was queued but before it was delivered.

## 7. API and Recipient Impact

There is no public API-envelope change:

- No new Start or Action request field;
- No caller-supplied cadence;
- No caller-supplied deadline;
- No workflow response shape change; and
- No form/UI behavior change.

The BDRM assignment transition still stores its resolved recipient snapshot on the created task and assignment-notification outbox row. Every recurring SLA notification copies that stored task snapshot unchanged.

## 8. Verification

Automated coverage must prove:

- BDRM seed metadata has a two-minute duration and one-day recurrence.
- A duration schedule with null recurrence remains one-time.
- A recurring duration schedule queues its first overdue occurrence at two minutes.
- A later eligible scan queues the next daily occurrence.
- Repeating a scan for the same occurrence does not duplicate it.
- A delayed scan queues only one current occurrence and advances to a future instant.
- `BdrmTradeFulfilled` stops future reminders.
- `BdrmTradeNotExecutedProperly` stops reminders for the closed BDRM task.
- A new BDRM task created after Trading Desk rework receives a new two-minute schedule and independent daily recurrence.
- Reminder recipients exactly match the BDRM task's stored ordered To/Cc snapshot.

SQL-backed seed verification must confirm the updated policy. Live acceptance against the fresh `workflowengine` database and smtp4dev should exercise the actual API path, first overdue reminder, one simulated next-day recurrence, and both stopping routes without waiting a real day; `run-sla --now` supplies the later monitor time.

## 9. Files Expected to Change

- Portfolio Trade SLA seed metadata;
- Generic duration recurrence handling in the SLA monitor;
- Seed verification and SLA monitor tests;
- Project documentation describing the BDRM policy; and
- An EF model snapshot or migration only if implementation discovers an actual model mismatch. The approved design itself requires no schema change.

## 10. Explicit Non-Goals

- A two-minute recurring test override;
- Environment-specific SLA cadence configuration;
- Caller-controlled SLA configuration;
- Hardcoded BDRM scheduler logic;
- Changes to Trading Desk application-date recurrence;
- Business-day or holiday calendars;
- Per-recipient delivery tracking; and
- Changes to the Workflow API envelope.
