# SLA Email Inherits Task Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make due-today and overdue SLA messages reuse the original task email bodies and prefix its subject with metadata-owned `Due today: ` or `Overdue: ` wording.

**Architecture:** `RunSlaService` resolves the task's one non-SLA `NotificationOutbox` row as immutable source content, keeps the existing task recipient snapshot and SLA occurrence metadata, and fails closed when that source is missing, ambiguous, or would produce an oversized subject. Portfolio Trade seed metadata supplies both prefixes through the existing `OverdueSubjectPrefix` column, avoiding contract and schema changes.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, ASP.NET Core Workflow API, Console processors, smtp4dev

---

### Task 1: Prove inherited content and generic primary-email lookup

**Files:**
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- Modify: `tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs`

- [x] **Step 1: Add primary-email setup to the SLA test fixtures**

Persist one non-SLA outbox row after the task ID exists. Use distinctive historical content and an arbitrary notification type so lookup cannot depend on Portfolio Trade:

```csharp
dbContext.NotificationOutbox.Add(new NotificationOutbox
{
    WorkflowInstanceId = task.WorkflowInstanceId,
    WorkflowTaskId = task.Id,
    BusinessObjectType = "PortfolioTrade",
    BusinessObjectId = businessObjectId,
    NotificationType = "ArbitraryPrimaryTaskEmail",
    NotificationRecipientsJson = task.NotificationRecipientsJson,
    Subject = "Original assignment subject",
    Body = "Original assignment plain body",
    HtmlBody = "<p>Original assignment HTML body</p>",
    PayloadJson = "{}",
    Status = "Pending",
    CorrelationId = $"primary-{task.Id}",
    CreatedAtUtc = task.CreatedAtUtc
});
```

- [x] **Step 2: Change focused expectations to exact historical content**

```csharp
Assert.Equal("Overdue: Original assignment subject", overdueNotification.Subject);
Assert.Equal("Original assignment plain body", overdueNotification.Body);
Assert.Equal("<p>Original assignment HTML body</p>", overdueNotification.HtmlBody);
Assert.Equal(RecipientSnapshot, overdueNotification.NotificationRecipientsJson);
```

For application-date recurrence, assert:

```csharp
Assert.Equal("Due today: Original assignment subject", dueOccurrence.Subject);
var slaOccurrences = dbContext.NotificationOutbox
    .Where(row => row.WorkflowTaskSlaScheduleId == schedule.Id)
    .OrderBy(row => row.SlaOccurrenceAtUtc)
    .ToArray();
Assert.Equal("Overdue: Original assignment subject", slaOccurrences[1].Subject);
Assert.All(slaOccurrences, row => Assert.Equal("Original assignment plain body", row.Body));
Assert.All(slaOccurrences, row => Assert.Equal("<p>Original assignment HTML body</p>", row.HtmlBody));
```

- [x] **Step 3: Run focused tests and observe RED**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~RecurringSlaMonitorTests"
```

Expected: FAIL because the service still renders generic SLA templates and does not prefix due-today subjects.

### Task 2: Implement primary-content inheritance and prefix seed metadata

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [x] **Step 1: Resolve exactly one primary task email**

```csharp
var primaryEmails = await dbContext.NotificationOutbox
    .AsNoTracking()
    .Where(outbox => outbox.WorkflowTaskId == task.Id && outbox.SlaReminderKind == null)
    .Take(2)
    .ToListAsync(cancellationToken);
if (primaryEmails.Count != 1)
{
    await transaction.RollbackAsync(CancellationToken.None);
    warnings.Add($"task {task.Id} SLA primary task email {(primaryEmails.Count == 0 ? "missing" : "ambiguous")}");
    continue;
}

var primaryEmail = primaryEmails[0];
var subject = route.OverdueSubjectPrefix + primaryEmail.Subject;
if (subject.Length > 300)
{
    await transaction.RollbackAsync(CancellationToken.None);
    warnings.Add($"task {task.Id} SLA notification subject exceeds 300 characters");
    continue;
}
```

Keep `TryBuildNotificationContext` for the SLA occurrence payload, but remove route-template rendering from this delivery path.

- [x] **Step 2: Copy historical content into the SLA outbox**

```csharp
NotificationRecipientsJson = task.NotificationRecipientsJson,
Subject = subject,
Body = primaryEmail.Body,
HtmlBody = primaryEmail.HtmlBody,
PayloadJson = JsonSerializer.Serialize(context),
```

- [x] **Step 3: Seed and test exact prefixes**

```csharp
new("RouteSlaTaskDueToday", "SlaTaskDueToday", "Workflow task due today", SlaTaskDueTodayBody, SlaTaskDueTodayHtmlBody, "Due today: "),
new("RouteSlaTaskOverdue", "SlaTaskOverdue", "Workflow task overdue", SlaTaskOverdueBody, SlaTaskOverdueHtmlBody, "Overdue: ")
```

Update `SeedDataSmokeTests.NotificationRoutes_HaveSubjectBodyHtmlBodyAndOverduePrefix` to assert both exact SLA prefixes.

- [x] **Step 4: Run focused tests and observe GREEN**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~RecurringSlaMonitorTests|FullyQualifiedName~SeedDataSmokeTests"
```

Expected: PASS with zero failures.

- [x] **Step 5: Commit core behavior**

```powershell
git add -- src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs tests/WorkflowEngine.Tests/Phase5SlaTests.cs tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
git commit -m "feat: inherit task email content in SLA reminders"
```

### Task 3: Prove fail-closed edge cases

**Files:**
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`

- [x] **Step 1: Add missing, ambiguous, and oversized-primary tests**

Each test captures the original schedule boundary, then proves no SLA row and no advance. The missing-primary assertions are:

```csharp
Assert.Empty(result.QueuedNotifications);
Assert.Contains(result.Warnings, warning =>
    warning == $"task {task.Id} SLA primary task email missing");
Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
Assert.DoesNotContain(dbContext.NotificationOutbox, row =>
    row.WorkflowTaskId == task.Id && row.SlaReminderKind != null);
```

For ambiguity, add two non-SLA task outbox rows and assert:

```csharp
Assert.Contains(result.Warnings, warning =>
    warning == $"task {task.Id} SLA primary task email ambiguous");
Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
```

For length, use `new string('x', 300)` as the original subject and assert:

```csharp
Assert.Contains(result.Warnings, warning =>
    warning == $"task {task.Id} SLA notification subject exceeds 300 characters");
Assert.Equal(originalNextReminderAtUtc, schedule.NextReminderAtUtc);
```

- [x] **Step 2: Run focused edge tests**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~Phase5SlaTests"
```

Expected: PASS with zero failures.

- [x] **Step 3: Commit edge coverage**

```powershell
git add -- tests/WorkflowEngine.Tests/Phase5SlaTests.cs
git commit -m "test: cover SLA primary email failures"
```

### Task 4: Run quality gates and isolated seed verification

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [x] **Step 1: Run warnings-as-errors builds**

```powershell
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror
```

Expected: both exit 0 with zero warnings and zero errors.

- [x] **Step 2: Run complete default test suites**

```powershell
dotnet test src/WorkflowEngine.sln --no-build --filter "Category!=Smtp"
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build
```

Expected: zero failures; opt-in SMTP tests stay excluded from the shared default suite.

- [x] **Step 3: Initialize and verify a fresh isolated SQL database**

Set `ConnectionStrings__DefaultConnection` to a fresh database and run:

```powershell
dotnet run --project src/WorkflowEngine.Console --no-build -- init-db
dotnet run --project src/WorkflowEngine.Console --no-build -- verify-seed
```

Expected: migration/seed and prefix verification pass.

### Task 5: Execute the live return/resubmit path and generate smtp4dev UAT evidence

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [x] **Step 1: Start isolated smtp4dev and rebuilt Workflow API**

Resolve listener PIDs before stopping only processes on ports 5058, 5000, and 3025. Launch smtp4dev with a fresh data directory. Launch the API at `http://localhost:5058` against the isolated SQL database with explicit SMTP sender, host `localhost`, port `3025`, and sender address.

- [x] **Step 2: Run the public API action path**

Use a fresh business object and execute Maker SaveDraft, Maker SubmitForChecker, Checker ReturnToMaker, Maker SaveDraft, Maker ResubmitForChecker, Checker SendToTradingDesk, TradingDesk TradeExecuted, and BDRMClientService TradeFulfilled.

Before every action, print endpoint, trusted actor headers, and exact public JSON. Include `selectedCcUsers` on every notification-producing request and retain the immutable original Maker.

- [x] **Step 3: Exercise every active task SLA**

Run Console `run-sla` and `process-notification-outbox` at controlled occurrence times for both Checker tasks, Trading Desk due-today and overdue, and BDRM overdue. Process every assignment before its SLA occurrence so smtp4dev presents paired evidence.

- [x] **Step 4: Verify SQL, context, outbox, and smtp4dev**

Prove final `Completed` state/status; all tasks and schedules completed; every scenario outbox row sent with no retry/error; SLA recipient JSON equals its task snapshot; exact prefixed subjects; byte-equal plain/HTML bodies; and zero smtp4dev warnings. Leave smtp4dev and API running for user UAT.

- [x] **Step 5: Update the handoff while preserving pre-existing handoff changes**

Append scenario identifiers, listener PIDs, database, message IDs, verification results, and next-session prompt to `CODEX_HANDOFF.md`. Because that file already contained substantial uncommitted session history, leave it in place without staging unrelated content; commit only the completed plan tracker.
