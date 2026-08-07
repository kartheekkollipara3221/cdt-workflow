# Application-Anchored Recurring SLA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add metadata-driven application-date SLA schedules, automatic same-API rescheduling, due-today and recurring overdue notifications, and fast-forwarded local verification while retaining clean-seed duration-mode behavior.

**Architecture:** `SlaPolicy` selects duration or application-date calculation. A focused calculator resolves calendar dates into UTC, a versioned `WorkflowTaskSlaSchedule` row records the effective schedule, normal workflow calls synchronize the current application target date, and `run-sla` creates occurrence-keyed outbox rows. Existing notification senders deliver those rows after rejecting cancelled or superseded occurrences.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core, SQL Server, xUnit, `TimeProvider`, MailKit, smtp4dev.

---

## File structure

New focused units:

- `src/WorkflowEngine.Application/Contracts/WorkflowSlaInput.cs` — typed application date input.
- `src/WorkflowEngine.Application/Sla/IWorkflowSlaSynchronizationService.cs` — same-API synchronization contract/result.
- `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskSlaSchedule.cs` — versioned runtime schedule.
- `src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs` — pure timezone/calendar calculation.
- `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs` — metadata resolution, create/no-op/reschedule/complete behavior.
- `tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs` — deterministic calculator tests.
- `tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs` — SQL-backed schedule synchronization tests.
- `tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs` — due-today, recurrence, catch-up, completion, and deduplication tests.

Existing files remain responsible for their current boundaries: workflow execution orchestrates transition side effects, the context reader projects persisted state, `RunSlaService` scans, and the outbox processor claims/delivers messages.

## Task 1: Typed SLA input and deterministic schedule calculator

**Files:**

- Create: `src/WorkflowEngine.Application/Contracts/WorkflowSlaInput.cs`
- Modify: `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs`
- Create: `src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionEnvelopeSerializationTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs`
- Create: `tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs`

- [ ] **Step 1: Add failing contract and hash tests**

Assert this exact block round-trips and changes the canonical request hash when the date changes:

```json
"slaInput": {
  "targetDate": "2030-01-20"
}
```

```csharp
envelope.Workflow!.SlaInput = new WorkflowSlaInput
{
    TargetDate = new DateOnly(2030, 1, 20)
};
var first = hasher.ComputeHash(envelope);
envelope.Workflow.SlaInput.TargetDate = new DateOnly(2030, 1, 21);
Assert.NotEqual(first, hasher.ComputeHash(envelope));
```

- [ ] **Step 2: Add failing calculator tests**

Cover target-date 9:00 Eastern, next-calendar-day overdue, spring/fall DST offsets, duration calculation, due-today catch-up, overdue catch-up, and next-day recurrence:

```csharp
var result = calculator.CalculateApplicationDate(
    new DateOnly(2030, 3, 10),
    new TimeOnly(9, 0),
    "America/New_York",
    overdueStartOffsetDays: 1,
    recurrenceIntervalDays: 1,
    now: DateTimeOffset.Parse("2030-03-10T15:00:00Z"));

Assert.Equal(DateTimeOffset.Parse("2030-03-10T13:00:00Z"), result.DueReminderAtUtc);
Assert.Equal(DateTimeOffset.Parse("2030-03-11T13:00:00Z"), result.OverdueAtUtc);
Assert.Equal(DateTimeOffset.Parse("2030-03-10T15:00:00Z"), result.NextReminderAtUtc);
Assert.Equal("DueToday", result.NextReminderKind);
```

- [ ] **Step 3: Run focused tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowActionEnvelopeSerializationTests|FullyQualifiedName~WorkflowActionEnvelopeNormalizationTests|FullyQualifiedName~SlaScheduleCalculatorTests"
```

Expected: FAIL because `WorkflowSlaInput` and `SlaScheduleCalculator` do not exist.

- [ ] **Step 4: Implement the minimal contract and calculator**

```csharp
public sealed class WorkflowSlaInput
{
    public DateOnly? TargetDate { get; set; }
}
```

Add `WorkflowSlaInput? SlaInput` to `WorkflowActionBlock` and `StartWorkflowRequest`; include `workflow.slaInput.targetDate` in canonical hashing.

Calculator output:

```csharp
public sealed record CalculatedSlaSchedule(
    DateOnly? TargetDate,
    DateTimeOffset? DueReminderAtUtc,
    DateTimeOffset OverdueAtUtc,
    DateTimeOffset? NextReminderAtUtc,
    string? NextReminderKind);
```

Use `TimeZoneInfo.FindSystemTimeZoneById`, `DateOnly.ToDateTime(TimeOnly, DateTimeKind.Unspecified)`, and `TimeZoneInfo.ConvertTimeToUtc`. Calculate each local calendar date independently; never add 24 hours to a UTC occurrence.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 3 command. Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -- src/WorkflowEngine.Application/Contracts src/WorkflowEngine.Application/WorkflowStart src/WorkflowEngine.Application/Normalization src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs tests/WorkflowEngine.Tests/WorkflowActionEnvelopeSerializationTests.cs tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs
git commit -m "feat: add SLA target-date calculation"
```

## Task 2: Metadata, schedule schema, clean migration, and seed

**Files:**

- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/SlaPolicy.cs`
- Create: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskSlaSchedule.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260807030000_AddApplicationAnchoredRecurringSla.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260807030000_AddApplicationAnchoredRecurringSla.Designer.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- Test: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`
- Test: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`
- Test: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [ ] **Step 1: Write failing EF model and seed tests**

Assert:

```csharp
Assert.Contains(dbContext.Model.FindEntityType(typeof(WorkflowTaskSlaSchedule))!.GetIndexes(),
    index => index.IsUnique
        && index.Properties.Select(p => p.Name).SequenceEqual(["WorkflowTaskId", "ScheduleVersion"]));

Assert.Equal("ApplicationTargetDate", tradingPolicy.CalculationMode);
Assert.Null(tradingPolicy.DurationMinutes);
Assert.Equal(new TimeOnly(9, 0), tradingPolicy.ReminderLocalTime);
Assert.Equal("America/New_York", tradingPolicy.TimeZoneId);
Assert.Equal(1, tradingPolicy.OverdueStartOffsetDays);
Assert.Equal(1, tradingPolicy.RecurrenceIntervalDays);
```

Assert the outbox occurrence index is unique on `WorkflowTaskSlaScheduleId`, `SlaOccurrenceDate`, and `SlaReminderKind`, and the status check permits `Cancelled`.

- [ ] **Step 2: Run model/seed tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SeedDataSmokeTests"
```

Expected: FAIL for missing metadata columns, entity/table, occurrence index, and seed policy.

- [ ] **Step 3: Add entity and EF mappings**

Add nullable `DurationMinutes` and the locked metadata fields to `SlaPolicy`. Add `WorkflowTaskSlaSchedule` with identity, task FK, schedule version, policy key, target date, due/overdue/next instants, effective metadata snapshot, status, and actor/correlation timestamps.

Add these nullable SLA occurrence fields to `NotificationOutbox`:

```csharp
public long? WorkflowTaskSlaScheduleId { get; set; }
public DateOnly? SlaOccurrenceDate { get; set; }
public string? SlaReminderKind { get; set; }
public DateTimeOffset? CancelledAtUtc { get; set; }
public string? CancellationReason { get; set; }
```

Replace the old `(WorkflowTaskId, NotificationType)` SLA index with the occurrence index. Map one active schedule per task with filtered unique index `[Status] = 'Active'` and status checks for `Active`, `Superseded`, and `Completed`.

- [ ] **Step 4: Generate a clean-schema migration**

```powershell
dotnet ef migrations add AddApplicationAnchoredRecurringSla --project src\WorkflowEngine.Infrastructure --startup-project src\WorkflowEngine.Console
```

The migration adds the new model directly and drops the old SLA-overdue unique index. It contains no legacy task/outbox backfill and no automatic database deletion.

- [ ] **Step 5: Update seed and verification**

Seed duration policies with `CalculationMode = "DurationFromTaskCreation"`, their existing two-minute duration, no due reminder, and one-time overdue behavior. Seed `TradingDeskReviewSla` as application-date mode with null duration, 09:00 `America/New_York`, offset 1, recurrence 1, and explicit due/overdue routes. Add `SlaTaskDueToday` and recurring `SlaTaskOverdue` templates. Verify mode-specific required values and route references.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add -- src/WorkflowEngine.Infrastructure/Persistence src/WorkflowEngine.Infrastructure/Migrations tests/WorkflowEngine.Tests/SchemaConstraintTests.cs tests/WorkflowEngine.Tests/MigrationSchemaTests.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
git commit -m "feat: add versioned SLA schedule schema"
```

## Task 3: Schedule synchronization service and workflow task integration

**Files:**

- Create: `src/WorkflowEngine.Application/Sla/IWorkflowSlaSynchronizationService.cs`
- Create: `src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- Modify: `src/WorkflowEngine.Application/Results/WorkflowActionResultBuilder.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`

- [ ] **Step 1: Write failing SQL-backed synchronization tests**

Prove:

```csharp
Assert.Equal("Active", first.Status);
Assert.Equal(1, first.ScheduleVersion);
Assert.Equal(new DateOnly(2030, 1, 20), first.TargetDate);

var same = service.Synchronize(request with { TargetDate = first.TargetDate });
Assert.False(same.Changed);
Assert.Single(dbContext.WorkflowTaskSlaSchedules);

var revised = service.Synchronize(request with { TargetDate = new DateOnly(2030, 1, 25) });
Assert.True(revised.Changed);
Assert.Equal(2, revised.ScheduleVersion);
Assert.Equal("Superseded", dbContext.WorkflowTaskSlaSchedules.Single(x => x.ScheduleVersion == 1).Status);
```

Also assert duration tasks ignore target-date authority, missing target date rejects creation of an application-date task before side effects, closing a task completes its schedule, and pending old-schedule outbox rows become `Cancelled`.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowSlaSynchronizationTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~WorkflowStartInitialActionTests"
```

Expected: FAIL because synchronization and schedule persistence are absent.

- [ ] **Step 3: Implement synchronization service**

Expose requests/results similar to:

```csharp
public sealed record WorkflowSlaSynchronizationRequest(
    string WorkflowInstanceId,
    long? WorkflowTaskId,
    DateOnly? TargetDate,
    string ActorUserId,
    string CorrelationId,
    string IdempotencyKey);

public sealed record WorkflowSlaSynchronizationResult(
    bool Changed,
    int ScheduleVersion,
    DateOnly? TargetDate,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? NextReminderAtUtc);
```

Resolve the instance definition version, task definition, and active policy from SQL. Implement create/no-op/reschedule/complete operations. A reschedule marks the active schedule `Superseded`, cancels pending referenced outbox rows, and inserts the next version. Do not write synthetic `WorkflowAudit` rows or increment `WorkflowInstance.Version`.

- [ ] **Step 4: Integrate with workflow execution transaction**

In `WorkflowExecutionService`, resolve the effective SLA task after transition effects:

- Closing task with no new app-date task: complete the closing schedule.
- Creating task: require/use `workflow.SlaInput.TargetDate` only when metadata selects application-date mode; create schedule version 1.
- Keeping current app-date task pending: compare and synchronize.
- Duration task: create its schedule from metadata duration; never read a request duration.

Set `WorkflowTask.DueAtUtc` from the schedule result before commit. Include the input in the existing idempotency hash.

- [ ] **Step 5: Integrate workflow start/reset**

Copy start `SlaInput` into the initial action envelope. Delete schedule rows before tasks in demo reset counts/cleanup so foreign keys remain valid.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add -- src/WorkflowEngine.Application/Sla src/WorkflowEngine.Application/Results src/WorkflowEngine.Infrastructure/Sla src/WorkflowEngine.Infrastructure/WorkflowExecution src/WorkflowEngine.Infrastructure/WorkflowStart src/WorkflowEngine.Infrastructure/DependencyInjection tests/WorkflowEngine.Tests/WorkflowSlaSynchronizationTests.cs tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs
git commit -m "feat: synchronize task SLA schedules"
```

## Task 4: Same Workflow API synchronization and context projection

**Files:**

- Modify: `src/WorkflowEngine.Application/Contracts/WorkflowContextResponse.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Program.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowContextReaderTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowContextSerializationTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`

- [ ] **Step 1: Write failing context and API tests**

Assert API action/start mapping preserves `targetDate`. Add action-service tests for:

- An action creates schedule version 1.
- An actionless request to the same `POST /v1/WorkflowAction` endpoint synchronizes an existing pending task.
- Unchanged date returns success/no-op.
- Changed date reschedules without changing workflow state/version.

Assert context JSON contains:

```json
"sla": {
  "status": "OnTrack",
  "dueAtUtc": "2030-01-21T14:00:00Z",
  "overdue": false,
  "overdueByMinutes": 0,
  "calculationMode": "ApplicationTargetDate",
  "targetDate": "2030-01-20",
  "scheduleVersion": 2,
  "nextReminderAtUtc": "2030-01-20T14:00:00Z"
}
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowContextReaderTests|FullyQualifiedName~WorkflowContextSerializationTests"
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "FullyQualifiedName~WorkflowActionServiceTests|FullyQualifiedName~WorkflowStartServiceTests"
```

Expected: FAIL because API mapping, actionless synchronization, and schedule projection are absent.

- [ ] **Step 3: Extend context projection**

Add nullable `CalculationMode`, `TargetDate`, `ScheduleVersion`, and `NextReminderAtUtc` to `WorkflowContextSla`. Query the active task schedule with the pending task. Continue to derive `Overdue` from `WorkflowTask.DueAtUtc`; a due-today reminder does not make the task overdue.

- [ ] **Step 4: Extend the same API endpoint**

Add `WorkflowSlaInput? SlaInput` and optional `string? IdempotencyKey` to API start/action models. When `ActionKey` is present, map input into the normal envelope. When it is absent, call `IWorkflowSlaSynchronizationService` for the current pending task and return a `WorkflowActionResult` with `ResultCode = "SlaSynchronized"`, empty `ActionKey`, no audit/notification side effects, and refreshed context. Do not add another controller or endpoint.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 commands. Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -- src/WorkflowEngine.Application/Contracts/WorkflowContextResponse.cs src/WorkflowEngine.Infrastructure/WorkflowContext workflow-api/src/WorkflowEngine.WorkflowApi workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services tests/WorkflowEngine.Tests/WorkflowContextReaderTests.cs tests/WorkflowEngine.Tests/WorkflowContextSerializationTests.cs
git commit -m "feat: synchronize SLA through workflow API"
```

## Task 5: Recurring monitor occurrences and stale-delivery suppression

**Files:**

- Modify: `src/WorkflowEngine.Application/Sla/IRunSlaService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- Create: `tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs`
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- Modify: `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs`

- [ ] **Step 1: Write failing recurring-monitor tests**

Cover:

- Due-today occurrence on target date.
- One overdue occurrence on each following calendar date.
- Weekend recurrence.
- Repeated scans on one date produce no duplicate.
- A scan delayed by several dates queues only the current occurrence.
- Completing the task produces no later occurrence.
- Superseding the schedule cancels pending old rows.
- A claimed row whose schedule is no longer active becomes `Cancelled` before SMTP send.

Occurrence identity assertion:

```csharp
Assert.Equal(
    1,
    dbContext.NotificationOutbox.Count(row =>
        row.WorkflowTaskSlaScheduleId == schedule.Id
        && row.SlaOccurrenceDate == new DateOnly(2030, 1, 21)
        && row.SlaReminderKind == "Overdue"));
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~RecurringSlaMonitorTests|FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~Phase5OutboxTests"
```

Expected: FAIL because the scanner still enforces one overdue row per task.

- [ ] **Step 3: Replace task-level scan with schedule-level occurrences**

Query active schedules joined to pending tasks and active instances where `NextReminderAtUtc <= request.Now`. Re-read each row in a short transaction, calculate the current local occurrence, render the configured due/overdue route, insert one occurrence row, and advance `NextReminderAtUtc` by calendar date. Set it null after a one-time duration reminder.

Update `RunSlaQueuedNotification` to include schedule id, schedule version, occurrence date, and reminder kind while retaining task/workflow/outbox identifiers.

- [ ] **Step 4: Suppress cancelled and stale schedule rows in the outbox processor**

Exclude `Cancelled` from candidate selection. After claim and before `SendAsync`, reload the SLA schedule when `WorkflowTaskSlaScheduleId` is non-null. If it is not active, atomically change the claimed row to `Cancelled`, clear the claim, record the reason, and continue without invoking the sender.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 6: Run opt-in SMTP integration tests**

With smtp4dev on `localhost:3025`:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter Category=Smtp
```

Expected: SMTP tests pass and cancelled rows are never delivered.

- [ ] **Step 7: Commit**

```powershell
git add -- src/WorkflowEngine.Application/Sla src/WorkflowEngine.Infrastructure/Sla src/WorkflowEngine.Infrastructure/Notifications tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs tests/WorkflowEngine.Tests/Phase5SlaTests.cs tests/WorkflowEngine.Tests/Phase5OutboxTests.cs tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs
git commit -m "feat: queue recurring SLA reminders"
```

## Task 6: Samples, authoritative documentation, and handoff

**Files:**

- Modify: `samples/workflow-actions/05-checker-send-to-trading-desk.json`
- Modify: `PROJECT.md`
- Modify: `README.md`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- Modify: `docs/api-json-bodies-and-console-real-usage.html`
- Modify: `docs/beginners-api-console-handoff.html`
- Modify: `docs/frontend-workflow-api-handoff.html`
- Modify: `tests/COVERAGE_AUDIT.md`
- Modify: `CODEX_HANDOFF.md`
- Test: `tests/WorkflowEngine.Tests/WorkflowSamplesTests.cs`
- Test: `tests/WorkflowEngine.Tests/SampleExpectationTests.cs`

- [ ] **Step 1: Write failing sample-contract tests**

Require sample 05 to contain:

```json
"slaInput": {
  "targetDate": "2030-01-20"
}
```

Assert callers cannot send duration, timezone, recurrence, offset, or `dueAtUtc` fields.

- [ ] **Step 2: Run sample tests and verify RED**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowSamplesTests|FullyQualifiedName~SampleExpectationTests"
```

Expected: FAIL because sample 05 lacks the required target date.

- [ ] **Step 3: Update sample and documentation**

Update the authoritative requirements without changing section 24 milestone mapping. Document:

- Duration as versioned application/workflow metadata.
- Same-API current-date forwarding and engine-owned comparison.
- Due-today plus recurring overdue behavior.
- Calendar days and configurable 09:00 Eastern policy.
- Clean-schema/no-legacy-backfill deployment.
- Local `run-sla --now` fast-forward versus SIT/UAT wall-clock execution.
- No second SLA API and no application payload parsing.

Apply only current feature additions to `CODEX_HANDOFF.md`; preserve the existing untracked `SQMS/`, `.tools/`, and `tareget-api-integration/` content.

- [ ] **Step 4: Run sample tests and verify GREEN**

Run the Step 2 command. Expected: all selected tests pass.

- [ ] **Step 5: Commit intended files only**

```powershell
git add -- samples/workflow-actions/05-checker-send-to-trading-desk.json PROJECT.md README.md workflow-api/WORKFLOW_API_BOUNDARIES.md docs/api-json-bodies-and-console-real-usage.html docs/beginners-api-console-handoff.html docs/frontend-workflow-api-handoff.html tests/COVERAGE_AUDIT.md tests/WorkflowEngine.Tests/WorkflowSamplesTests.cs tests/WorkflowEngine.Tests/SampleExpectationTests.cs
git commit -m "docs: document application-anchored SLA"
```

Do not stage `.tools/`, `SQMS/`, `tareget-api-integration/`, or the pre-existing combined `CODEX_HANDOFF.md` modifications.

## Task 7: Full quality gates and fast local HTTP/SMTP acceptance

**Files:**

- Update evidence only: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run clean restore/build/test gates**

```powershell
dotnet restore src\WorkflowEngine.sln
dotnet build src\WorkflowEngine.sln -m:1 --no-restore -warnaserror
dotnet test src\WorkflowEngine.sln -m:1 --no-build
dotnet restore workflow-api\WorkflowEngine.WorkflowApi.slnx
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --no-restore -warnaserror
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --no-build
```

Expected: zero build warnings/errors and all default tests pass; SMTP-category tests remain opt-in.

- [ ] **Step 2: Initialize and verify a clean SQL database**

```powershell
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Expected: latest migration applies and seed verification passes. If an existing database is not clean, stop and obtain explicit approval before destructive recreation.

- [ ] **Step 3: Start smtp4dev and Workflow API**

Start the repo-local smtp4dev on HTTP `5000`/SMTP `3025` and the Workflow API on `5058`. Verify `/api/Server`, Swagger, and all three listeners before executing the scenario.

- [ ] **Step 4: Execute actual HTTP workflow calls**

Use a unique business object/workflow instance. Start, submit to Checker, then call `CheckerSendToTradingDesk` with current target date and recipient/context data. Verify the assignment message is sent immediately and SQL contains schedule version 1.

- [ ] **Step 5: Fast-forward the real SLA monitor**

```powershell
dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-20T14:00:00Z"
dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox
dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-21T14:00:00Z"
dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox
```

Use concrete ISO instants selected for the scenario when executing; record them in `CODEX_HANDOFF.md`. Repeat same-date scans to prove no duplicates and jump several dates to prove one catch-up only.

- [ ] **Step 6: Prove same-API rescheduling and completion**

Send the revised current target date through `POST /v1/WorkflowAction` without a state transition. Verify schedule version 2, old pending occurrence cancellation, a new due-today occurrence under fast-forward, and unchanged workflow state/version. Complete the task and prove future scans queue nothing.

- [ ] **Step 7: Review smtp4dev and SQL evidence**

Verify assignment, due-today, recurring overdue, rescheduled, and completion behavior; intended To/Cc lists; HTML/plain parts; zero retries; no stale cancelled delivery; and no smtp4dev server-log errors.

- [ ] **Step 8: Final source checks**

```powershell
rg -n "DateTime\.UtcNow|toState|DurationMinutes" src workflow-api
git diff --check
git status --short
```

`DateTime.UtcNow` must not appear outside approved composition-root context, requests must not gain `toState`, and `DurationMinutes` must not appear in public API request DTOs.

- [ ] **Step 9: Update handoff and completion evidence**

Record exact build/test counts, seed verification, HTTP requests, simulated SLA instants, SQL/outbox evidence, smtp4dev message results, running process IDs, remaining risks, and the next-session prompt in `CODEX_HANDOFF.md`.

Use the required completion summary labels: `Changed`, `Why`, `Tests run`, `Seed verification run`, `Sample JSON executed`, `Files updated`, and `Open issues`.
