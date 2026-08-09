# BDRM Recurring Overdue Reminders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Send the first BDRM overdue reminder two minutes after task creation, then send reminders at 9:00 AM Eastern every calendar day until that BDRM task closes.

**Architecture:** Keep BDRM as a DurationFromTaskCreation policy for its initial deadline, then reuse the existing timezone-aware calendar recurrence machinery for later occurrences. The policy opts in through existing duration, local-time, timezone, and recurrence metadata; task completion remains the generic stopping authority.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, existing notification outbox and TimeProvider abstractions

---

## File Map

- Modify src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs to configure the hybrid BDRM policy.
- Modify src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs to validate one-time and calendar-recurring duration policies.
- Modify src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs to use calendar recurrence after the first duration occurrence.
- Modify src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs to enforce the same policy combinations at runtime.
- Modify tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs to lock the seed contract.
- Modify tests/WorkflowEngine.Tests/Phase5SlaTests.cs to prove the first, next-day, daily, deduplicated, delayed, and recipient-snapshot behavior.
- Modify tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs to prove both BDRM exit actions stop reminders and Trading Desk re-execution starts a fresh BDRM cycle.
- Modify PROJECT.md to make the approved policy authoritative.
- Update CODEX_HANDOFF.md without discarding or staging its pre-existing local changes.

No public API DTO, entity property, EF model configuration, migration, model snapshot, console command, or email sender should change.

### Task 1: Lock and Seed the Hybrid BDRM Policy

**Files:**
- Modify: tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
- Modify: src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs
- Modify: src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs

- [ ] **Step 1: Replace the broad duration-policy assertion with explicit one-time and recurring metadata assertions**

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
    Assert.Equal(0, policy.OverdueStartOffsetDays);
    Assert.Null(policy.DueNotificationRouteKey);
    Assert.Equal("RouteSlaTaskOverdue", policy.OverdueNotificationRouteKey);
});

foreach (var policyKey in new[] { "CheckerReviewSla", "TradingDeskReworkSla" })
{
    Assert.Null(policies[policyKey].ReminderLocalTime);
    Assert.Null(policies[policyKey].TimeZoneId);
    Assert.Null(policies[policyKey].RecurrenceIntervalDays);
}

var bdrm = policies["BdrmClientServiceReviewSla"];
Assert.Equal(new TimeOnly(9, 0), bdrm.ReminderLocalTime);
Assert.Equal("America/New_York", bdrm.TimeZoneId);
Assert.Equal(1, bdrm.RecurrenceIntervalDays);
~~~

- [ ] **Step 2: Run the seed test and verify it fails**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.OtherSeededSlaPoliciesRetainConfiguredDurationMode" -m:1
~~~

Expected: FAIL because BDRM currently has null local time, timezone, and recurrence.

- [ ] **Step 3: Configure the BDRM metadata**

Change only the BDRM seed row:

~~~csharp
new(
    "BdrmClientServiceReviewSla",
    "DurationFromTaskCreation",
    2,
    false,
    new TimeOnly(9, 0),
    "America/New_York",
    0,
    1,
    null,
    "RouteSlaTaskOverdue")
~~~

- [ ] **Step 4: Validate recurring duration metadata during seed verification**

Use the following DurationFromTaskCreation branch:

~~~csharp
if (policy.CalculationMode == "DurationFromTaskCreation")
{
    var hasRecurrence = policy.RecurrenceIntervalDays is > 0;
    if (policy.DurationMinutes is null or <= 0
        || policy.SendDueDateReminder
        || policy.OverdueStartOffsetDays != 0
        || policy.RecurrenceIntervalDays is <= 0
        || (hasRecurrence
            && (policy.ReminderLocalTime is null
                || string.IsNullOrWhiteSpace(policy.TimeZoneId)))
        || (!hasRecurrence
            && (policy.ReminderLocalTime is not null
                || policy.TimeZoneId is not null))
        || policy.DueNotificationRouteKey is not null
        || string.IsNullOrWhiteSpace(policy.OverdueNotificationRouteKey))
    {
        errors.Add($"SLA policy '{policy.SlaPolicyKey}' has invalid duration-mode metadata.");
    }
}
~~~

This accepts existing one-time duration policies and requires local calendar metadata when recurrence is positive.

- [ ] **Step 5: Run the focused seed test**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.OtherSeededSlaPoliciesRetainConfiguredDurationMode" -m:1
~~~

Expected: PASS.

- [ ] **Step 6: Commit the metadata contract**

~~~powershell
git add -- tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs
git commit -m "feat: configure BDRM calendar reminders"
~~~

### Task 2: Reuse Calendar Recurrence After the Duration Deadline

**Files:**
- Modify: tests/WorkflowEngine.Tests/Phase5SlaTests.cs
- Modify: src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs
- Modify: src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs

- [ ] **Step 1: Extend the SLA test helper with optional calendar recurrence metadata**

Add optional parameters to AddTask and copy them into the schedule:

~~~csharp
private static WorkflowTask AddTask(
    WorkflowDbContext dbContext,
    string workflowInstanceId,
    string taskKey,
    string roleKey,
    DateTimeOffset? dueAtUtc,
    string status = "Pending",
    bool storeNotificationSnapshot = true,
    string? notificationRecipientsJson = RecipientSnapshot,
    int? recurrenceIntervalDays = null,
    TimeOnly? reminderLocalTime = null,
    string? timeZoneId = null)
~~~

~~~csharp
ReminderLocalTime = reminderLocalTime,
TimeZoneId = timeZoneId,
RecurrenceIntervalDays = recurrenceIntervalDays,
~~~

- [ ] **Step 2: Add an SQL-backed monitor test for the complete recurrence timeline**

At the fixed test time, 2030-01-15T16:00:00Z is 11:00 AM Eastern and the initial deadline is 10:58 AM Eastern:

~~~csharp
[Fact]
public async Task RunSlaDurationRecurrenceStartsAtDeadlineThenRunsDailyAtNineEastern()
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
        recurrenceIntervalDays: 1,
        reminderLocalTime: new TimeOnly(9, 0),
        timeZoneId: "America/New_York");
    dbContext.SaveChanges();

    var service = new RunSlaService(
        dbContext,
        new NotificationTemplateRenderer(),
        new SlaScheduleCalculator());

    var first = await service.RunAsync(new(Now), CancellationToken.None);
    var duplicate = await service.RunAsync(new(Now), CancellationToken.None);
    var beforeNextBoundary = await service.RunAsync(
        new(DateTimeOffset.Parse("2030-01-16T13:59:00Z")),
        CancellationToken.None);
    var nextDay = await service.RunAsync(
        new(DateTimeOffset.Parse("2030-01-16T14:00:00Z")),
        CancellationToken.None);
    var delayed = await service.RunAsync(
        new(DateTimeOffset.Parse("2030-01-19T15:00:00Z")),
        CancellationToken.None);

    Assert.Single(first.QueuedNotifications);
    Assert.Empty(duplicate.QueuedNotifications);
    Assert.Empty(beforeNextBoundary.QueuedNotifications);
    Assert.Single(nextDay.QueuedNotifications);
    Assert.Single(delayed.QueuedNotifications);

    var reminders = dbContext.NotificationOutbox
        .Where(row => row.WorkflowTaskId == task.Id && row.SlaReminderKind == "Overdue")
        .OrderBy(row => row.SlaOccurrenceDate)
        .ToArray();
    Assert.Equal(
        [new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16), new DateOnly(2030, 1, 19)],
        reminders.Select(row => row.SlaOccurrenceDate!.Value).ToArray());
    Assert.All(
        reminders,
        row => Assert.Equal(RecipientSnapshot, row.NotificationRecipientsJson));
    Assert.Equal(
        DateTimeOffset.Parse("2030-01-20T14:00:00Z"),
        dbContext.WorkflowTaskSlaSchedules
            .Single(row => row.WorkflowTaskId == task.Id)
            .NextReminderAtUtc);
}
~~~

- [ ] **Step 3: Run the focused test and verify it fails**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~Phase5SlaTests.RunSlaDurationRecurrenceStartsAtDeadlineThenRunsDailyAtNineEastern" -m:1
~~~

Expected: FAIL because duration mode currently clears NextReminderAtUtc after the first occurrence.

- [ ] **Step 4: Resolve recurring duration occurrences by local calendar date**

Replace the DurationFromTaskCreation branch in RunSlaService.ResolveOccurrence:

~~~csharp
if (schedule.CalculationMode == "DurationFromTaskCreation")
{
    if (schedule.RecurrenceIntervalDays is null)
    {
        return new SlaOccurrence(
            DateOnly.FromDateTime(schedule.OverdueAtUtc.UtcDateTime),
            "Overdue",
            schedule.OverdueAtUtc);
    }

    if (schedule.ReminderLocalTime is null
        || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
    {
        throw new InvalidOperationException($"SLA schedule {schedule.Id} metadata is invalid.");
    }

    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
    var overdueLocalDate = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(schedule.OverdueAtUtc, timeZone).DateTime);
    var currentLocalDate = DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
    if (currentLocalDate <= overdueLocalDate)
    {
        return new SlaOccurrence(overdueLocalDate, "Overdue", schedule.OverdueAtUtc);
    }

    var eligibleAtUtc = scheduleCalculator.CalculateCalendarOccurrence(
        currentLocalDate,
        schedule.ReminderLocalTime.Value,
        schedule.TimeZoneId);
    return new SlaOccurrence(currentLocalDate, "Overdue", eligibleAtUtc);
}
~~~

The existing RunAsync check already handles eligibleAtUtc greater than now by advancing NextReminderAtUtc without queuing an early notification.

- [ ] **Step 5: Advance recurring duration schedules with the existing timezone-aware calculator**

Replace the DurationFromTaskCreation branch in AdvanceSchedule:

~~~csharp
if (schedule.CalculationMode == "DurationFromTaskCreation")
{
    if (schedule.RecurrenceIntervalDays is null)
    {
        schedule.NextReminderAtUtc = null;
        return;
    }

    if (schedule.ReminderLocalTime is null
        || string.IsNullOrWhiteSpace(schedule.TimeZoneId))
    {
        throw new InvalidOperationException($"SLA schedule {schedule.Id} metadata is invalid.");
    }

    schedule.NextReminderAtUtc = scheduleCalculator.CalculateNextRecurringOccurrence(
        occurrence.Date,
        schedule.ReminderLocalTime.Value,
        schedule.TimeZoneId,
        schedule.RecurrenceIntervalDays.Value);
    return;
}
~~~

This produces the next calendar date at 9:00 AM Eastern and independently converts each occurrence to UTC across daylight-saving boundaries.

- [ ] **Step 6: Apply the same policy validation at runtime**

Replace the duration validation in WorkflowSlaSynchronizationService.ValidatePolicy with:

~~~csharp
if (policy.CalculationMode == "DurationFromTaskCreation")
{
    var hasRecurrence = policy.RecurrenceIntervalDays is > 0;
    if (policy.DurationMinutes is null or <= 0
        || policy.SendDueDateReminder
        || policy.OverdueStartOffsetDays != 0
        || policy.RecurrenceIntervalDays is <= 0
        || (hasRecurrence
            && (policy.ReminderLocalTime is null
                || string.IsNullOrWhiteSpace(policy.TimeZoneId)))
        || (!hasRecurrence
            && (policy.ReminderLocalTime is not null
                || policy.TimeZoneId is not null))
        || policy.DueNotificationRouteKey is not null
        || string.IsNullOrWhiteSpace(policy.OverdueNotificationRouteKey))
    {
        throw new InvalidOperationException(
            $"SLA policy '{policy.SlaPolicyKey}' has invalid duration-mode metadata.");
    }

    return;
}
~~~

- [ ] **Step 7: Run all calculator and SLA monitor tests**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SlaScheduleCalculatorTests|FullyQualifiedName~Phase5SlaTests" -m:1
~~~

Expected: PASS. Existing null-recurrence duration tests remain one-time, and existing Trading Desk application-date tests remain unchanged.

- [ ] **Step 8: Commit the generic scheduler behavior**

~~~powershell
git add -- tests/WorkflowEngine.Tests/Phase5SlaTests.cs src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs src/WorkflowEngine.Infrastructure/Sla/WorkflowSlaSynchronizationService.cs
git commit -m "feat: schedule duration reminders by calendar"
~~~

### Task 3: Prove BDRM Closure and Re-entry

**Files:**
- Modify: tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs

- [ ] **Step 1: Add a parameterized stopping test**

Create a BDRM-state instance with a pending BDRM task, active recurring schedule, and pending reminder. Execute both actions:

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
    var schedule = AddBdrmSchedule(dbContext, task);
    AddPendingReminder(dbContext, task, schedule);
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

Add these focused private helpers:

~~~csharp
private static WorkflowTaskSlaSchedule AddBdrmSchedule(
    WorkflowDbContext dbContext,
    WorkflowTask task)
{
    var schedule = new WorkflowTaskSlaSchedule
    {
        WorkflowTaskId = task.Id,
        ScheduleVersion = 1,
        SlaPolicyKey = "BdrmClientServiceReviewSla",
        OverdueAtUtc = Now.AddMinutes(2),
        NextReminderAtUtc = Now.AddMinutes(2),
        CalculationMode = "DurationFromTaskCreation",
        ReminderLocalTime = new TimeOnly(9, 0),
        TimeZoneId = "America/New_York",
        OverdueStartOffsetDays = 0,
        RecurrenceIntervalDays = 1,
        OverdueNotificationRouteKey = "RouteSlaTaskOverdue",
        Status = "Active",
        CreatedAtUtc = Now,
        CreatedByUserId = "system",
        CorrelationId = "corr-bdrm-sla",
        IdempotencyKey = "idem-bdrm-sla"
    };
    dbContext.WorkflowTaskSlaSchedules.Add(schedule);
    dbContext.SaveChanges();
    return schedule;
}

private static void AddPendingReminder(
    WorkflowDbContext dbContext,
    WorkflowTask task,
    WorkflowTaskSlaSchedule schedule)
{
    dbContext.NotificationOutbox.Add(new NotificationOutbox
    {
        WorkflowInstanceId = task.WorkflowInstanceId,
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
}
~~~

- [ ] **Step 2: Add a fresh-cycle test after Trading Desk re-execution**

Execute BdrmTradeNotExecutedProperly, then execute TradingTradeExecuted with a distinct request identity:

~~~csharp
[Fact]
public void TradingReexecutionCreatesFreshBdrmRecurringSchedule()
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "BdrmClientServiceReview", "Active", 4);
    AddTask(dbContext, "WF-PT-1001", "BdrmClientServiceReviewTask", "BDRMClientService");
    dbContext.SaveChanges();
    var oldTask = dbContext.WorkflowTasks.Single();
    var oldSchedule = AddBdrmSchedule(dbContext, oldTask);
    dbContext.SaveChanges();
    var service = CreateService(dbContext);

    var reject = Envelope(
        expectedState: "BdrmClientServiceReview",
        expectedVersion: 4,
        actionKey: "BdrmTradeNotExecutedProperly",
        roles: ["BDRMClientService"]);
    reject.Request!.ClientActionId = "client-bdrm-reject";
    reject.Workflow!.IdempotencyKey = "idem-bdrm-reject";
    reject.Workflow.ExpectedCurrentTaskKey = "BdrmClientServiceReviewTask";
    Assert.True(service.Execute(Request(reject)).Result.Success);

    var reexecute = Envelope(
        expectedState: "TradingDeskRework",
        expectedVersion: 5,
        actionKey: "TradingTradeExecuted",
        roles: ["TradingDesk"]);
    reexecute.Request!.ClientActionId = "client-trading-reexecute";
    reexecute.Workflow!.IdempotencyKey = "idem-trading-reexecute";
    reexecute.Workflow.ExpectedCurrentTaskKey = "TradingDeskReworkTask";
    Assert.True(service.Execute(Request(reexecute)).Result.Success);

    var newTask = dbContext.WorkflowTasks.Single(row =>
        row.TaskKey == "BdrmClientServiceReviewTask"
        && row.Status == "Pending");
    var newSchedule = dbContext.WorkflowTaskSlaSchedules.Single(row =>
        row.WorkflowTaskId == newTask.Id
        && row.Status == "Active");

    Assert.Equal("Completed", oldSchedule.Status);
    Assert.NotEqual(oldSchedule.Id, newSchedule.Id);
    Assert.Equal(Now.AddMinutes(2), newSchedule.OverdueAtUtc);
    Assert.Equal(Now.AddMinutes(2), newSchedule.NextReminderAtUtc);
    Assert.Equal(new TimeOnly(9, 0), newSchedule.ReminderLocalTime);
    Assert.Equal("America/New_York", newSchedule.TimeZoneId);
    Assert.Equal(1, newSchedule.RecurrenceIntervalDays);
}
~~~

- [ ] **Step 3: Run both lifecycle tests**

~~~powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~WorkflowExecutionValidationTests.BdrmExitActionCompletesRecurringScheduleAndCancelsUndeliveredReminder|FullyQualifiedName~WorkflowExecutionValidationTests.TradingReexecutionCreatesFreshBdrmRecurringSchedule" -m:1
~~~

Expected: PASS through existing generic task completion and task creation paths. Do not add action-key branches to production code.

- [ ] **Step 4: Commit lifecycle coverage**

~~~powershell
git add -- tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
git commit -m "test: prove BDRM reminder lifecycle"
~~~

### Task 4: Update Requirements and Run Quality Gates

**Files:**
- Modify: PROJECT.md
- Modify: CODEX_HANDOFF.md

- [ ] **Step 1: Update authoritative SLA requirements**

Change the BDRM row in PROJECT.md section 13 to:

~~~markdown
| BdrmClientServiceReviewTask | DurationFromTaskCreation | 2 minutes to first overdue reminder; then 09:00 America/New_York every calendar day, including weekends and holidays, while pending | Yes |
~~~

Update schedule creation and run-sla text to define the hybrid duration/calendar behavior and retain the no-backfill rule.

- [ ] **Step 2: Run formatting and scope checks**

~~~powershell
git diff --check
git diff --name-only
~~~

Expected: no whitespace errors and no API DTO, entity, migration, or EF model snapshot changes.

- [ ] **Step 3: Build both solutions sequentially with warnings as errors**

~~~powershell
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -m:1 -warnaserror
dotnet restore workflow-api/WorkflowEngine.WorkflowApi.slnx
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror
~~~

Expected: both builds pass. If the known running API locks build output, stop only that process, build, and restart it with the same workflowengine and smtp4dev settings.

- [ ] **Step 4: Run both default test suites sequentially**

~~~powershell
dotnet test src/WorkflowEngine.sln -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1
~~~

Expected: all tests pass. Do not suppress the pre-existing transitive Microsoft.OpenApi 2.4.1 NU1903 warning.

- [ ] **Step 5: Apply and verify seed metadata on workflowengine**

~~~powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
~~~

Confirm BdrmClientServiceReviewSla has DurationMinutes = 2, ReminderLocalTime = 09:00, TimeZoneId = America/New_York, and RecurrenceIntervalDays = 1.

- [ ] **Step 6: Preserve the clean live-test boundary**

Do not create a workflow instance or execute sample JSON until the user supplies the live scenario. Confirm the database still has zero runtime workflow instances after seed verification.

- [ ] **Step 7: Update the handoff without overwriting existing notes**

Append behavior, commits, test/build/seed evidence, live process status, and the next live BDRM scenario prompt to CODEX_HANDOFF.md. Leave the file unstaged because it had pre-existing working-tree changes before this task.

- [ ] **Step 8: Commit the requirements update**

~~~powershell
git add -- PROJECT.md
git commit -m "docs: document BDRM calendar reminders"
~~~

- [ ] **Step 9: Report completion in the repository-required format**

Report Changed, Why, Tests run, Seed verification run, Sample JSON executed, Files updated, and Open issues. State that live API/smtp4dev workflow execution is waiting for the user's scenario.
