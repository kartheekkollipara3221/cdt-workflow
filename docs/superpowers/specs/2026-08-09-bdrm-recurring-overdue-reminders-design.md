# BDRM Recurring Overdue Reminder Design

**Status:** Approved

**Date:** 2026-08-09

**Scope:** Surgical extension of the existing Milestone 5 duration-triggered, calendar-recurring SLA behavior

## 1. Goal

When a `BdrmClientServiceReviewTask` remains pending, send its first overdue notification two minutes after task creation, then send another overdue reminder at 9:00 AM Eastern on the next calendar day and every calendar day afterward until that task closes.

This behavior remains generic and metadata-driven. Portfolio Trade selects the behavior through its seeded SLA policy; reusable scheduler code does not branch on the BDRM task or workflow key.

## 2. Locked Business Rules

- The BDRM task becomes overdue two minutes after creation.
- The first overdue notification is eligible at that overdue instant.
- After the first notification, the next overdue notification is eligible at 9:00 AM `America/New_York` on the next calendar day.
- Later overdue notifications are eligible at 9:00 AM Eastern every calendar day, including weekends and holidays.
- Every reminder uses the exact ordered To/Cc recipient snapshot persisted on that BDRM task.
- Repeated or concurrent SLA scans must not create the same daily occurrence twice.
- A delayed scan queues only the current applicable reminder and does not backfill every missed day.
- `BdrmTradeFulfilled` closes the BDRM task, completes its active SLA schedule, and stops future reminders.
- `BdrmTradeNotExecutedProperly` closes the BDRM task, completes its active SLA schedule, and stops future BDRM reminders before ownership moves to Trading Desk Rework.
- If Trading Desk later executes the trade again, the newly created BDRM task receives a new schedule. Its first overdue notification is eligible two minutes after that new task's creation.
- Pending or processing reminder rows for a completed BDRM schedule are cancelled before delivery. Sent reminders remain immutable history.

## 3. Metadata Design

The existing duration, local reminder time, timezone, and recurrence metadata are sufficient and remain authoritative.

The seeded `BdrmClientServiceReviewSla` policy becomes:

| Field | Value |
|---|---|
| `CalculationMode` | `DurationFromTaskCreation` |
| `DurationMinutes` | `2` |
| `SendDueDateReminder` | `false` |
| `ReminderLocalTime` | `09:00` |
| `TimeZoneId` | `America/New_York` |
| `RecurrenceIntervalDays` | `1` |
| `OverdueNotificationRouteKey` | `RouteSlaTaskOverdue` |

`null` recurrence continues to mean a one-time duration overdue notification with no local time or timezone. A positive duration recurrence requires a local reminder time and timezone: the duration determines the first overdue instant, while the calendar metadata determines later occurrences.

The public Workflow API does not accept duration or recurrence fields. The UI cannot override this policy.

## 4. Scheduler Behavior

The generic duration-SLA path must honor the schedule's snapshotted `RecurrenceIntervalDays` value:

1. Schedule creation sets `OverdueAtUtc = task.CreatedAtUtc + DurationMinutes` and initially sets `NextReminderAtUtc` to that overdue instant.
2. `run-sla` selects the schedule only while its workflow instance is active, its task is pending, its schedule is active, and `NextReminderAtUtc <= now`.
3. The first eligible scan queues one `SlaTaskOverdue` outbox row.
4. If recurrence is null, the scheduler clears `NextReminderAtUtc`, preserving existing one-time behavior for Checker and Trading Desk Rework policies.
5. If recurrence is positive, the scheduler converts the first overdue instant to the configured timezone, takes the next calendar date, and schedules that date at the configured local reminder time.
6. Each later occurrence advances by `RecurrenceIntervalDays` in the configured timezone. Every local calendar occurrence is converted independently to UTC so daylight-saving changes do not shift the 9:00 AM wall-clock time.
7. When scans are delayed across one or more expected dates, the scheduler queues at most the current eligible local-date occurrence and advances to the next future calendar occurrence. It does not emit a burst of historical reminders.

This deliberately reuses the same calendar recurrence calculation used by `ApplicationTargetDate` policies after the first occurrence. BDRM differs only in how its first overdue instant is established: task creation plus two minutes rather than a caller-supplied target date.

## 5. Occurrence Identity

Daily duration reminders can continue using the existing SLA occurrence identity:

```text
(WorkflowTaskSlaScheduleId, SlaOccurrenceDate, SlaReminderKind)
```

For recurring duration policies, `SlaOccurrenceDate` is the configured timezone's local occurrence date. The first two-minute occurrence and every later 9:00 AM occurrence fall on distinct eligible calendar dates, so the existing filtered unique constraint remains sufficient.

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

- BDRM seed metadata has a two-minute duration, 9:00 AM `America/New_York` reminder time, and one-calendar-day recurrence.
- A duration schedule with null recurrence remains one-time.
- A recurring duration schedule queues its first overdue occurrence at two minutes.
- The next occurrence is 9:00 AM Eastern on the next calendar day, not 24 hours after the first reminder.
- Later occurrences remain at 9:00 AM Eastern across daylight-saving boundaries.
- Weekends and holidays are included.
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
- Business-day or holiday exclusion calendars;
- Per-recipient delivery tracking; and
- Changes to the Workflow API envelope.
