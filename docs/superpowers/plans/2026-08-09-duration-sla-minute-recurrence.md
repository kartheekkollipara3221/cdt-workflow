# Duration SLA Minute Recurrence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add metadata-driven minute recurrence for duration SLAs so a Checker task can notify at 2 minutes and every 2 minutes in the demo, while production metadata can use 240 minutes and every 60 minutes, with latest-slot-only catch-up.

**Architecture:** Snapshot a nullable minute interval from the versioned SLA policy onto each new task schedule. Persist every SLA reminder's exact scheduled UTC occurrence and use that instant for durable uniqueness, correlation, and payload identity. For delayed scans, compute the latest anchored minute slot at or before `TimeProvider.GetUtcNow()`, queue one row, and advance to the next slot; preserve the existing calendar-day and application-date paths.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, existing `TimeProvider`, SLA schedule, notification outbox, and console commands

---

## File Map

- Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/SlaPolicy.cs`, `WorkflowTaskSlaSchedule.cs`, and `NotificationOutbox.cs` for nullable minute recurrence and exact occurrence identity.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` for constraints and the exact-occurrence filtered unique index.
- Add migration `AddDurationSlaMinuteRecurrence` and update `WorkflowDbContextModelSnapshot.cs`.
- Modify Portfolio Trade seed, seed upsert, and seed verification to configure Checker `2/2` while leaving production configuration metadata-owned.
- Modify `WorkflowSlaSynchronizationService.cs` to validate and snapshot minute recurrence only on newly created schedules.
- Modify `RunSlaService.cs`, `IRunSlaService.cs`, and the console output to queue/return the exact scheduled occurrence and implement anchored latest-slot catch-up.
- Modify focused schema, migration, seed, synchronization, SLA, completion, and regression tests.
- Update `CODEX_HANDOFF.md` after verification without overwriting its pre-existing local work.

### Task 1: Model Exact Occurrence and Minute Recurrence

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`
- Modify: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/SlaPolicy.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskSlaSchedule.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Add: `src/WorkflowEngine.Infrastructure/Migrations/*_AddDurationSlaMinuteRecurrence.cs`
- Add: `src/WorkflowEngine.Infrastructure/Migrations/*_AddDurationSlaMinuteRecurrence.Designer.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`

- [ ] **Step 1: Write failing model-contract tests**

Update schema tests to require nullable `RecurrenceIntervalMinutes` properties on policy and schedule, a nullable `SlaOccurrenceAtUtc` property on the outbox, positive-value constraints, mutual exclusion between minute/day recurrence, and this exact index shape:

```csharp
var index = entityType.GetIndexes().Single(candidate =>
    candidate.Properties.Select(property => property.Name).SequenceEqual(
        new[] { "WorkflowTaskSlaScheduleId", "SlaOccurrenceAtUtc", "SlaReminderKind" }));

Assert.True(index.IsUnique);
Assert.Equal(
    "[WorkflowTaskSlaScheduleId] IS NOT NULL AND [SlaOccurrenceAtUtc] IS NOT NULL",
    index.GetFilter());
```

Update migration-script tests to require the three new nullable columns and the exact-instant index, reject the old date-only index as the final index, and prove the new migration does not update existing schedules or outbox rows.

- [ ] **Step 2: Run the focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests" -m:1
```

Expected: FAIL because the properties, constraints, and exact-occurrence index do not exist.

- [ ] **Step 3: Add nullable entity properties**

Add these properties without defaults or data backfill:

```csharp
// SlaPolicy and WorkflowTaskSlaSchedule
public int? RecurrenceIntervalMinutes { get; set; }

// NotificationOutbox
public DateTimeOffset? SlaOccurrenceAtUtc { get; set; }
```

- [ ] **Step 4: Configure constraints and exact uniqueness**

Add positive checks to both policy and schedule:

```csharp
table.HasCheckConstraint(
    "CK_SlaPolicy_RecurrenceIntervalMinutes",
    "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalMinutes] > 0");
table.HasCheckConstraint(
    "CK_SlaPolicy_RecurrenceIntervalsMutuallyExclusive",
    "[RecurrenceIntervalMinutes] IS NULL OR [RecurrenceIntervalDays] IS NULL");
```

Use equivalent `CK_WorkflowTaskSlaSchedule_*` constraints for schedules. Replace the old outbox index with:

```csharp
entity.HasIndex(row => new
    {
        row.WorkflowTaskSlaScheduleId,
        row.SlaOccurrenceAtUtc,
        row.SlaReminderKind
    })
    .IsUnique()
    .HasFilter("[WorkflowTaskSlaScheduleId] IS NOT NULL AND [SlaOccurrenceAtUtc] IS NOT NULL");
```

- [ ] **Step 5: Generate and inspect the migration**

```powershell
dotnet ef migrations add AddDurationSlaMinuteRecurrence --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.Console --output-dir Migrations
```

The generated migration must add only nullable columns, drop the date-only SLA unique index, create the filtered exact-time index, and add checks. It must contain no `UPDATE` or `INSERT` for active schedules or notification history.

- [ ] **Step 6: Run focused tests and confirm GREEN**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests" -m:1
```

- [ ] **Step 7: Commit the schema slice**

```powershell
git add tests/WorkflowEngine.Tests/SchemaConstraintTests.cs tests/WorkflowEngine.Tests/MigrationSchemaTests.cs src/WorkflowEngine.Infrastructure/Persistence/Entities/SlaPolicy.cs src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskSlaSchedule.cs src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs src/WorkflowEngine.Infrastructure/Migrations
git commit -m "feat: model duration SLA minute recurrence"
```

### Task 2: Seed, Validate, and Snapshot the Cadence

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs`

- [ ] **Step 1: Write failing seed and synchronization tests**

Lock these seed expectations:

```csharp
Assert.Equal(2, policies["CheckerReviewSla"].DurationMinutes);
Assert.Equal(2, policies["CheckerReviewSla"].RecurrenceIntervalMinutes);
Assert.Null(policies["CheckerReviewSla"].RecurrenceIntervalDays);
Assert.Null(policies["TradingDeskReworkSla"].RecurrenceIntervalMinutes);
Assert.Equal(1, policies["BdrmClientServiceReviewSla"].RecurrenceIntervalDays);
Assert.Null(policies["BdrmClientServiceReviewSla"].RecurrenceIntervalMinutes);
```

Add synchronization cases proving:

1. a new Checker task snapshots `2/2`;
2. setting policy metadata to `DurationMinutes = 240` and `RecurrenceIntervalMinutes = 60` gives a new task a first deadline four hours after creation and a 60-minute snapshot;
3. an already-active schedule remains unchanged after policy metadata changes; and
4. minute/day recurrence together and minute recurrence on `ApplicationTargetDate` are rejected.

When existing tests switch Checker metadata to `ApplicationTargetDate`, explicitly clear `RecurrenceIntervalMinutes`.

- [ ] **Step 2: Run focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests|FullyQualifiedName~WorkflowSlaSynchronizationTests|FullyQualifiedName~WorkflowStartInitialActionTests" -m:1
```

- [ ] **Step 3: Add the seed field and Checker `2/2` metadata**

Extend `SlaPolicySeed` with `int? RecurrenceIntervalMinutes`, set Checker to `2`, and set all other current policies to `null`. Copy it in `WorkflowSeedService.UpsertSlaPolicies`:

```csharp
entity.RecurrenceIntervalMinutes = seed.RecurrenceIntervalMinutes;
```

- [ ] **Step 4: Validate both recurrence modes**

For `DurationFromTaskCreation`:

```csharp
var hasMinuteRecurrence = policy.RecurrenceIntervalMinutes is > 0;
var hasDayRecurrence = policy.RecurrenceIntervalDays is > 0;

if (hasMinuteRecurrence && hasDayRecurrence)
{
    throw new InvalidOperationException("Duration SLA recurrence intervals are mutually exclusive.");
}

if (hasMinuteRecurrence && (policy.ReminderLocalTime is not null || policy.TimeZoneId is not null))
{
    throw new InvalidOperationException("Minute recurrence must not define local calendar settings.");
}
```

Keep the existing local-time/timezone requirement for day recurrence and require both recurrence fields to be null for application-date minute recurrence. Mirror the same contract in seed verification.

- [ ] **Step 5: Snapshot minute recurrence on new schedules only**

Add this to schedule creation:

```csharp
RecurrenceIntervalMinutes = policy.RecurrenceIntervalMinutes,
```

Do not update the early-return path for an existing active schedule.

- [ ] **Step 6: Run focused tests and confirm GREEN**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests|FullyQualifiedName~WorkflowSlaSynchronizationTests|FullyQualifiedName~WorkflowStartInitialActionTests" -m:1
```

- [ ] **Step 7: Commit the metadata slice**

```powershell
git add tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs
git commit -m "feat: configure Checker recurring SLA metadata"
```

### Task 3: Queue Anchored Exact-Time Occurrences

**Files:**
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- Modify: `src/WorkflowEngine.Application/Sla/IRunSlaService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- Modify: the `run-sla` output site under `src/WorkflowEngine.Console`

- [ ] **Step 1: Write the failing same-day/catch-up test**

Create a duration schedule with first occurrence `2030-01-15T12:00:00Z` and two-minute recurrence. Drive `FakeTimeProvider` through:

```text
12:00 -> queue 12:00, next 12:02
12:00 -> no duplicate
12:02 -> queue 12:02, next 12:04
12:11 -> queue only 12:10, next 12:12
```

Assert three rows on the same `SlaOccurrenceDate`, exact times `12:00`, `12:02`, `12:10`, distinct deterministic correlation IDs, and payload values matching each exact occurrence. Update any manually prequeued SLA row to populate `SlaOccurrenceAtUtc`.

- [ ] **Step 2: Write a failing completion test**

Create a minute-recurring task, queue the first reminder, complete the task through a normal workflow action, advance time past later slots, run SLA again, and assert no additional row and no active schedule.

- [ ] **Step 3: Run focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~WorkflowExecutionValidationTests" -m:1
```

- [ ] **Step 4: Compute the latest anchored minute slot**

In the duration branch, calculate from `NextReminderAtUtc`, not scan time:

```csharp
private static DateTimeOffset ResolveLatestMinuteOccurrence(
    DateTimeOffset next,
    DateTimeOffset now,
    int intervalMinutes)
{
    var intervalTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
    var elapsedTicks = (now - next).Ticks;
    var completedIntervals = elapsedTicks / intervalTicks;
    return next.AddTicks(completedIntervals * intervalTicks);
}
```

Return that instant as `EligibleAtUtc`, queue exactly one row, and advance with:

```csharp
schedule.NextReminderAtUtc = occurrence.EligibleAtUtc.AddMinutes(
    schedule.RecurrenceIntervalMinutes.Value);
```

Retain the existing one-time duration branch and day/calendar branches unchanged.

- [ ] **Step 5: Use exact occurrence identity everywhere**

Change duplicate lookup to schedule ID + exact time + kind. Set:

```csharp
SlaOccurrenceAtUtc = occurrence.EligibleAtUtc,
SlaOccurrenceDate = occurrence.Date,
CorrelationId = $"sla-{schedule.Id}-{occurrence.EligibleAtUtc.UtcTicks}-{occurrence.Kind}",
```

Add `slaOccurrenceAtUtc` to the render context. Extend `RunSlaQueuedNotification` and the console JSON projection with `OccurrenceAtUtc` so live diagnostics expose it.

- [ ] **Step 6: Run focused and regression tests**

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~WorkflowExecutionValidationTests|FullyQualifiedName~Phase5OutboxTests" -m:1
```

Expected: GREEN, including BDRM calendar recurrence and application-date reminders.

- [ ] **Step 7: Commit the runtime slice**

```powershell
git add tests/WorkflowEngine.Tests/Phase5SlaTests.cs tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs src/WorkflowEngine.Application/Sla/IRunSlaService.cs src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs src/WorkflowEngine.Console
git commit -m "feat: run recurring duration SLA reminders"
```

### Task 4: Apply, Verify, and Exercise the Live CDT Path

**Files:**
- Modify: `CODEX_HANDOFF.md` only by appending/updating the current goal section while retaining unrelated local content.

- [ ] **Step 1: Run warning-as-error quality gates sequentially**

Stop only the API process bound to port 5058 if it locks build output. Do not stop smtp4dev or reset the database.

```powershell
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror -m:1
dotnet test src/WorkflowEngine.sln --no-build -m:1
dotnet restore workflow-api/WorkflowEngine.WorkflowApi.slnx
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build -m:1
```

- [ ] **Step 2: Apply migrations and verify seed without resetting data**

Use the configured SQL connection and run:

```powershell
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

Read-only SQL verification must show:

- Checker policy is `DurationMinutes = 2`, `RecurrenceIntervalMinutes = 2`;
- pre-migration active schedules still have null minute recurrence;
- historical outbox rows retain their prior values and are not replayed; and
- the exact occurrence filtered unique index exists.

- [ ] **Step 3: Run a fresh CDT live acceptance test**

Restart the reference API with SMTP configuration. Create a new unique CDT request, submit it to Checker, and capture the initial API JSON. After the first two-minute threshold, run:

```text
run-sla
process-notification-outbox
```

Capture the command JSON showing `occurrenceAtUtc`. Wait for the next anchored two-minute slot, run the two commands again, and capture the second JSON. Read-only DB and smtp4dev checks must prove two same-day exact occurrences, two sent outbox rows, distinct correlations, and request links in both emails.

- [ ] **Step 4: Update the handoff and run final status checks**

Record current goal, completed changes, changed files, build/test status, seed verification, live JSON evidence, remaining tasks, known risks, and next-session prompt in `CODEX_HANDOFF.md`. Do not stage pre-existing `.planning`, `.tools`, `SQMS`, or `tareget-api-integration` changes.

```powershell
git status --short
git diff --check
git log -4 --oneline
```

- [ ] **Step 5: Commit only owned handoff changes if separable**

If `CODEX_HANDOFF.md` cannot be cleanly separated from pre-existing edits, leave it unstaged and report that explicitly. Otherwise:

```powershell
git add CODEX_HANDOFF.md
git commit -m "docs: record recurring SLA verification"
```

### Task 5: Completion Report

- [ ] **Step 1: Run verification-before-completion evidence checks**

Re-read the current test/build outputs, migration/seed results, live JSON, `git diff --check`, and `git status --short`. Do not claim completion from earlier or inferred results.

- [ ] **Step 2: Report using the repository-required summary**

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

Also state that production uses policy metadata `240/60`, the hourly scheduler calls `run-sla` then `process-notification-outbox`, and delivery can lag to the next poll while missed slots are not backfilled.
