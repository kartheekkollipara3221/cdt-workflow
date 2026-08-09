# BDRM Recurring Overdue Reminders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Send the first BDRM overdue reminder two minutes after task creation and then one reminder every 24 hours until that BDRM task closes.

**Architecture:** Reuse the existing SlaPolicy.RecurrenceIntervalDays and its schedule snapshot. Extend only the generic duration-mode branch of run-sla; Portfolio Trade opts in through seed metadata, while task completion remains the authority that disables the schedule and cancels undelivered reminders.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, existing notification outbox and TimeProvider abstractions

---

## File Map

- Modify src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs to configure BDRM daily recurrence.
- Modify src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs to accept only null or positive recurrence on duration policies.
- Modify src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs to calculate the latest elapsed interval for delayed duration scans.
- Modify src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs to queue and advance recurring duration occurrences.
- Modify src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs to reject invalid duration recurrence metadata at runtime.
- Modify tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs to lock the BDRM seed contract while preserving one-time Checker and Trading Desk Rework behavior.
- Modify tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs to lock interval alignment and no-backfill calculations.
- Modify tests/WorkflowEngine.Tests/Phase5SlaTests.cs to prove first, daily, deduplicated, and delayed duration occurrences.
- Modify tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs to prove both BDRM exit actions stop the active schedule and cancel undelivered reminders.
- Modify PROJECT.md to make the approved BDRM policy authoritative.
- Update CODEX_HANDOFF.md without discarding its existing local changes.

No public API DTO, entity property, EF model configuration, migration, or model snapshot should change.

### Task 1: Lock and Seed the BDRM Policy

**Files:**
- Modify: tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
- Modify: src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs
- Modify: src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs

- [ ] **Step 1: Replace the broad duration-policy seed assertion with explicit recurrence assertions**

Keep the shared duration assertions, then distinguish BDRM from the one-time policies:

~~~csharp
var policies = dbContext.SlaPolicies
    .Where(row => row.WorkflowDefinitionVersionId == versionId
        && row.SlaPolicyKey != "TradingDeskReviewSla")
    .ToDictionary(row => row.SlaPolicyKey, StringComparer.Ordinal);

Assert.Equal(3, policies.Count);
Assert.All(policies.Values, policy =>
{
    Assert.Equal("DurationFromTaskCreation", policy.CalculationMode);
    Assert.Equal(2, policy.DurationMinutes);
    Assert.False(policy.SendDueDateReminder);
    Assert.Null(policy.ReminderLocalTime);
    Assert.Null(policy.TimeZoneId);
    Assert.Equal(0, policy.OverdueStartOffsetDays);
    Assert.Null(policy.DueNotificationRouteKey);
    Assert.Equal("RouteSlaTaskOverdue", policy.OverdueNotificationRouteKey);
});
Assert.Null(policies["CheckerReviewSla"].RecurrenceIntervalDays);
Assert.Null(policies["TradingDeskReworkSla"].RecurrenceIntervalDays);
Assert.Equal(1, policies["BdrmClientServiceReviewSla"].RecurrenceIntervalDays);
~~~

- [ ] **Step 2: Run the seed test and verify the new assertion fails**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.OtherSeededSlaPoliciesRetainConfiguredDurationMode" -m:1
~~~

Expected: FAIL because BdrmClientServiceReviewSla.RecurrenceIntervalDays is currently null.

- [ ] **Step 3: Configure daily BDRM recurrence in seed metadata**

~~~csharp
new("BdrmClientServiceReviewSla", "DurationFromTaskCreation", 2, false, null, null, 0, 1, null, "RouteSlaTaskOverdue")
~~~

- [ ] **Step 4: Allow positive recurrence on duration policies during seed verification**

~~~csharp
if (policy.DurationMinutes is null or <= 0
    || policy.SendDueDateReminder
    || policy.ReminderLocalTime is not null
    || policy.TimeZoneId is not null
    || policy.RecurrenceIntervalDays is <= 0)
{
    errors.Add($"SLA policy '{policy.SlaPolicyKey}' has invalid duration-mode metadata.");
}
~~~

The nullable relational pattern accepts null and rejects zero or negative values.

- [ ] **Step 5: Run the focused seed test**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.OtherSeededSlaPoliciesRetainConfiguredDurationMode" -m:1
~~~

Expected: PASS.

- [ ] **Step 6: Commit the metadata contract**

~~~powershell
git add -- tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs
git commit -m "feat: configure daily BDRM overdue reminders"
~~~

### Task 2: Implement Generic Duration Recurrence

**Files:**
- Modify: tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs
- Modify: tests/WorkflowEngine.Tests/Phase5SlaTests.cs
- Modify: src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs
- Modify: src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs
- Modify: src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs

- [ ] **Step 1: Add a calculator test for a delayed daily scan**

~~~csharp
[Fact]
public void DurationRecurrenceReturnsLatestElapsedOccurrenceWithoutBackfill()
{
    var occurrence = calculator.CalculateCurrentDurationOccurrence(
        overdueAtUtc: DateTimeOffset.Parse("2030-01-15T15:58:00Z"),
        recurrenceIntervalDays: 1,
        now: DateTimeOffset.Parse("2030-01-18T16:00:00Z"));

    Assert.Equal(DateTimeOffset.Parse("2030-01-18T15:58:00Z"), occurrence);
}
~~~

- [ ] **Step 2: Add an SQL-backed monitor test for first, daily, duplicate, and delayed scans**

Extend AddTask with an optional int? recurrenceIntervalDays = null parameter and assign it to the schedule snapshot. Add:

~~~csharp
[Fact]
public async Task RunSlaRecurringDurationQueuesOneCurrentOccurrenceAndAdvancesDaily()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    var versionId = PortfolioTradeVersionId(dbContext);
    AddInstance(dbContext, "WF-PT-BDRM-RECUR", "PT-BDRM-RECUR", "Active", versionId);
    var task = AddTask(
        dbContext,
        "WF-PT-BDRM-RECUR",
        "BdrmClientServiceReviewTask",
        "BDRMClientService",
        Now.AddMinutes(-2),
        recurrenceIntervalDays: 1);
    dbContext.SaveChanges();

    var service = new RunSlaService(
        dbContext,
        new NotificationTemplateRenderer(),
        new SlaScheduleCalculator());

    var first = await service.RunAsync(new(Now), CancellationToken.None);
    var duplicate = await service.RunAsync(new(Now), CancellationToken.None);
    var delayed = await service.RunAsync(new(Now.AddDays(3)), CancellationToken.None);

    Assert.Single(first.QueuedNotifications);
    Assert.Empty(duplicate.QueuedNotifications);
    Assert.Single(delayed.QueuedNotifications);
    Assert.Equal(
        [new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 18)],
        dbContext.NotificationOutbox
            .Where(row => row.WorkflowTaskId == task.Id && row.SlaReminderKind == "Overdue")
            .OrderBy(row => row.SlaOccurrenceDate)
            .Select(row => row.SlaOccurrenceDate!.Value)
            .ToArray());
    Assert.Equal(
        DateTimeOffset.Parse("2030-01-19T15:58:00Z"),
        dbContext.WorkflowTaskSlaSchedules
            .Single(row => row.WorkflowTaskId == task.Id)
            .NextReminderAtUtc);
    Assert.All(
        dbContext.NotificationOutbox.Where(row => row.WorkflowTaskId == task.Id),
        row => Assert.Equal(RecipientSnapshot, row.NotificationRecipientsJson));
}
~~~

- [ ] **Step 3: Run the focused tests and verify they fail**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SlaScheduleCalculatorTests.DurationRecurrenceReturnsLatestElapsedOccurrenceWithoutBackfill|FullyQualifiedName~Phase5SlaTests.RunSlaRecurringDurationQueuesOneCurrentOccurrenceAndAdvancesDaily" -m:1
~~~

Expected: FAIL because the calculator method does not exist and duration schedules currently clear NextReminderAtUtc after their first occurrence.

- [ ] **Step 4: Add the generic duration interval calculation**

~~~csharp
public DateTimeOffset CalculateCurrentDurationOccurrence(
    DateTimeOffset overdueAtUtc,
    int recurrenceIntervalDays,
    DateTimeOffset now)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recurrenceIntervalDays);

    var normalizedOverdue = overdueAtUtc.ToUniversalTime();
    var normalizedNow = now.ToUniversalTime();
    if (normalizedNow <= normalizedOverdue)
    {
        return normalizedOverdue;
    }

    var intervalTicks = TimeSpan.FromDays(recurrenceIntervalDays).Ticks;
    var elapsedIntervals = (normalizedNow - normalizedOverdue).Ticks / intervalTicks;
    return normalizedOverdue.AddTicks(elapsedIntervals * intervalTicks);
}
~~~

- [ ] **Step 5: Resolve and advance duration occurrences using the snapshot**

In RunSlaService.ResolveOccurrence:

~~~csharp
if (schedule.CalculationMode == "DurationFromTaskCreation")
{
    var eligibleAtUtc = schedule.RecurrenceIntervalDays is { } recurrenceIntervalDays
        ? scheduleCalculator.CalculateCurrentDurationOccurrence(
            schedule.OverdueAtUtc,
            recurrenceIntervalDays,
            now)
        : schedule.OverdueAtUtc;
    return new SlaOccurrence(
        DateOnly.FromDateTime(eligibleAtUtc.UtcDateTime),
        "Overdue",
        eligibleAtUtc);
}
~~~

In AdvanceSchedule:

~~~csharp
if (schedule.CalculationMode == "DurationFromTaskCreation")
{
    schedule.NextReminderAtUtc = schedule.RecurrenceIntervalDays is { } recurrenceIntervalDays
        ? occurrence.EligibleAtUtc.AddDays(recurrenceIntervalDays)
        : null;
    return;
}
~~~

- [ ] **Step 6: Apply the same duration-policy validation at runtime**

~~~csharp
if (policy.DurationMinutes is null or <= 0
    || policy.RecurrenceIntervalDays is <= 0)
{
    throw new InvalidOperationException(
        $"SLA policy '{policy.SlaPolicyKey}' requires positive DurationMinutes and null or positive RecurrenceIntervalDays.");
}
~~~

- [ ] **Step 7: Run calculator, monitor, and existing one-time tests**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SlaScheduleCalculatorTests|FullyQualifiedName~Phase5SlaTests" -m:1
~~~

Expected: PASS, including existing tests that prove null recurrence remains one-time.

- [ ] **Step 8: Commit the generic scheduler behavior**

~~~powershell
git add -- tests/WorkflowEngine.Tests/SlaScheduleCalculatorTests.cs tests/WorkflowEngine.Tests/Phase5SlaTests.cs src/WorkflowEngine.Infrastructure/Sla/SlaScheduleCalculator.cs src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs
git commit -m "feat: repeat duration SLA reminders by metadata"
~~~

### Task 3: Prove Both BDRM Exit Routes Stop Recurrence

**Files:**
- Modify: tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs

- [ ] **Step 1: Add a parameterized task-close regression test**

Create a BDRM-state instance, pending BDRM task, active recurring schedule, and pending reminder, then execute both closing actions:

~~~csharp
[Theory]
[InlineData("BdrmTradeFulfilled")]
[InlineData("BdrmTradeNotExecutedProperly")]
public void BdrmExitActionCompletesRecurringScheduleAndCancelsUndeliveredReminder(string actionKey)
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
    AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
    dbContext.SaveChanges();
    var task = dbContext.WorkflowTasks.Single();
    var schedule = new WorkflowTaskSlaSchedule
    {
        WorkflowTaskId = task.Id,
        ScheduleVersion = 1,
        SlaPolicyKey = "BdrmClientServiceReviewSla",
        OverdueAtUtc = Now.AddMinutes(-1),
        NextReminderAtUtc = Now.AddDays(1),
        CalculationMode = "DurationFromTaskCreation",
        OverdueStartOffsetDays = 0,
        RecurrenceIntervalDays = 1,
        OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
        Status = "Active",
        CreatedAtUtc = Now.AddMinutes(-3),
        CreatedByUserId = "system",
        CorrelationId = "corr-bdrm-sla",
        IdempotencyKey = "idem-bdrm-sla"
    };
    dbContext.WorkflowTaskSlaSchedules.Add(schedule);
    dbContext.SaveChanges();
    dbContext.NotificationOutbox.Add(new NotificationOutbox
    {
        WorkflowInstanceId = "WF-PT-1001",
        WorkflowTaskId = task.Id,
        WorkflowTaskSlaScheduleId = schedule.Id,
        SlaOccurrenceDate = new DateOnly(2030, 1, 16),
        SlaReminderKind = "Overdue",
        BusinessObjectType = "PortfolioTrade",
        BusinessObjectId = "PT-1001",
        NotificationType = "SlaTaskOverdue",
        NotificationRecipientsJson = "{\"toEmailAddresses\":[\"bdrm@example.test\"],\"ccEmailAddresses\":[]}",
        Subject = "Overdue",
        Body = "body",
        HtmlBody = "<p>body</p>",
        PayloadJson = "{}",
        Status = "Pending",
        CorrelationId = "corr-pending-bdrm-reminder",
        CreatedAtUtc = Now
    });
    dbContext.SaveChanges();

    var envelope = Envelope(
        expectedState: "BdrmClientServiceReview",
        expectedVersion: 4,
        actionKey: actionKey,
        roles: ["BDRMClientService"]);
    envelope.Workflow!.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";

    var result = CreateService(dbContext).Execute(Request(envelope));

    Assert.True(result.Result.Success);
    Assert.Equal("Completed", schedule.Status);
    Assert.Null(schedule.NextReminderAtUtc);
    var reminder = dbContext.NotificationOutbox
        .Single(row => row.WorkflowTaskSlaScheduleId == schedule.Id);
    Assert.Equal("Cancelled", reminder.Status);
    Assert.Equal("WorkflowTaskCompleted", reminder.CancellationReason);
}
~~~

- [ ] **Step 2: Run the route-specific regression test**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~WorkflowExecutionValidationTests.BdrmExitActionCompletesRecurringScheduleAndCancelsUndeliveredReminder" -m:1
~~~

Expected: PASS using the existing generic task-completion path. If it fails, correct only the generic close/schedule interaction; do not add action-key branches.

- [ ] **Step 3: Prove Trading Desk re-execution creates a fresh BDRM recurrence cycle**

Add a second integration test that executes BdrmTradeNotExecutedProperly and then TradingTradeExecuted on the created TradingDeskReworkTask. Give each envelope a distinct client/idempotency identity:

~~~csharp
[Fact]
public void TradingReexecutionCreatesFreshBdrmRecurringSchedule()
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
    AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
    dbContext.SaveChanges();
    var oldBdrmTask = dbContext.WorkflowTasks.Single();
    dbContext.WorkflowTaskSlaSchedules.Add(new WorkflowTaskSlaSchedule
    {
        WorkflowTaskId = oldBdrmTask.Id,
        ScheduleVersion = 1,
        SlaPolicyKey = "BdrmClientServiceReviewSla",
        OverdueAtUtc = Now.AddMinutes(-1),
        NextReminderAtUtc = Now.AddDays(1),
        CalculationMode = "DurationFromTaskCreation",
        OverdueStartOffsetDays = 0,
        RecurrenceIntervalDays = 1,
        OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
        Status = "Active",
        CreatedAtUtc = Now.AddMinutes(-3),
        CreatedByUserId = "system",
        CorrelationId = "corr-old-bdrm-sla",
        IdempotencyKey = "idem-old-bdrm-sla"
    });
    dbContext.SaveChanges();
    var oldBdrmSchedule = dbContext.WorkflowTaskSlaSchedules.Single();
    var service = CreateService(dbContext);

    var reject = Envelope(
        expectedState: "BdrmClientServiceReview",
        expectedVersion: 4,
        actionKey: "BdrmTradeNotExecutedProperly",
        roles: ["BDRMClientService"]);
    reject.Request!.ClientActionId = "client-bdrm-reject";
    reject.Workflow!.IdempotencyKey = "idem-bdrm-reject";
    reject.Workflow.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";
    var rejected = service.Execute(Request(reject));

    Assert.True(rejected.Result.Success);
    Assert.Equal("Completed", oldBdrmSchedule.Status);
    Assert.Equal("TradingDeskRework", dbContext.WorkflowInstances.Single().CurrentStateKey);

    var reexecute = Envelope(
        expectedState: "TradingDeskRework",
        expectedVersion: 5,
        actionKey: "TradingTradeExecuted",
        roles: ["TradingDesk"]);
    reexecute.Request!.ClientActionId = "client-trading-reexecute";
    reexecute.Workflow!.IdempotencyKey = "idem-trading-reexecute";
    reexecute.Workflow.ExpectedCurrentTaskKey = "TradingDeskReworkTask";
    var reexecuted = service.Execute(Request(reexecute));

    Assert.True(reexecuted.Result.Success);
    var newBdrmTask = dbContext.WorkflowTasks.Single(row =>
        row.TaskKey == "BdrmClientServiceReviewTask"
        && row.Status == "Pending");
    var newBdrmSchedule = dbContext.WorkflowTaskSlaSchedules.Single(row =>
        row.WorkflowTaskId == newBdrmTask.Id
        && row.Status == "Active");
    Assert.NotEqual(oldBdrmSchedule.Id, newBdrmSchedule.Id);
    Assert.Equal(Now.AddMinutes(2), newBdrmSchedule.OverdueAtUtc);
    Assert.Equal(Now.AddMinutes(2), newBdrmSchedule.NextReminderAtUtc);
    Assert.Equal(1, newBdrmSchedule.RecurrenceIntervalDays);
}
~~~

Use the existing Envelope, Request, and CreateService helpers; do not introduce Portfolio Trade branching in production code.

- [ ] **Step 4: Run both lifecycle tests**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~WorkflowExecutionValidationTests.BdrmExitActionCompletesRecurringScheduleAndCancelsUndeliveredReminder|FullyQualifiedName~WorkflowExecutionValidationTests.TradingReexecutionCreatesFreshBdrmRecurringSchedule" -m:1
~~~

Expected: PASS. The first test proves both stopping routes; the second proves a later BDRM task has a distinct two-minute/daily schedule.

- [ ] **Step 5: Commit the lifecycle regression coverage**

~~~powershell
git add -- tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
git commit -m "test: prove BDRM exits stop SLA reminders"
~~~

### Task 4: Update Requirements and Run Quality Gates

**Files:**
- Modify: PROJECT.md
- Modify: CODEX_HANDOFF.md

- [ ] **Step 1: Update the authoritative SLA table and monitor rules**

In PROJECT.md section 13, change the BDRM row to:

~~~markdown
| BdrmClientServiceReviewTask | DurationFromTaskCreation | 2 minutes; first overdue reminder at the deadline, then repeat every 1 elapsed day while pending | Yes |
~~~

Update the duration schedule and monitor text to state that null recurrence clears NextReminderAtUtc, while positive duration recurrence advances from the original overdue instant and delayed scans queue only the latest applicable occurrence.

- [ ] **Step 2: Run formatting and negative-scope checks**

~~~powershell
git diff --check
git diff --name-only
~~~

Expected: no whitespace errors; no API DTO, entity, migration, or EF model snapshot files changed.

- [ ] **Step 3: Build both solutions sequentially with warnings as errors**

~~~powershell
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -m:1 -warnaserror
dotnet restore workflow-api/WorkflowEngine.WorkflowApi.slnx
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror
~~~

Expected: both builds pass. If the running API locks build output, stop only that known API process, build, and restart it with the same workflowengine connection and smtp4dev settings.

- [ ] **Step 4: Run the complete automated test suites sequentially**

~~~powershell
dotnet test src/WorkflowEngine.sln -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1
~~~

Expected: all default tests pass. The pre-existing transitive Microsoft.OpenApi 2.4.1 NU1903 warning may still appear during restore but must not be introduced or suppressed by this change.

- [ ] **Step 5: Apply seed metadata to the clean workflowengine database and verify it**

~~~powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
~~~

Expected: migrations are current and seed verification passes. Confirm BdrmClientServiceReviewSla has DurationMinutes = 2 and RecurrenceIntervalDays = 1.

- [ ] **Step 6: Preserve the user's clean live-test boundary**

Do not create a workflow instance or execute sample JSON until the user supplies the live scenario. Confirm the database still has zero runtime workflow instances after seed verification.

- [ ] **Step 7: Update the handoff without overwriting existing local notes**

Append the implemented behavior, commits, test/build/seed evidence, live process status, and the next prompt requesting the user's BDRM scenario to CODEX_HANDOFF.md.

- [ ] **Step 8: Commit the authoritative requirements update**

~~~powershell
git add -- PROJECT.md
git commit -m "docs: document recurring BDRM SLA"
~~~

Leave CODEX_HANDOFF.md unstaged because it contained pre-existing working-tree changes before this task. Report that preservation explicitly.

- [ ] **Step 9: Report completion using the repository-required format**

Report Changed, Why, Tests run, Seed verification run, Sample JSON executed, Files updated, and Open issues. State explicitly that live API/smtp4dev workflow execution is waiting for the user's scenario.
