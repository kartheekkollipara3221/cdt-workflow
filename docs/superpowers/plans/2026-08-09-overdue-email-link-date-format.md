# Overdue Email Link and Date Format Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make newly queued overdue emails include the persisted request link and show due/overdue dates as `MM/dd/yyyy` after applying a fixed UTC-5 offset.

**Architecture:** Keep persisted SLA instants and the existing raw UTC payload tokens unchanged. Add two display-only tokens in `RunSlaService`, then make only the seeded `RouteSlaTaskOverdue` templates consume those tokens and the existing snapshotted `tradeLink`; normal seed upsert updates the active database metadata without a schema migration.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, existing metadata-driven notification renderer

---

## File structure

- Modify `tests/WorkflowEngine.Tests/Phase5SlaTests.cs` to prove end-to-end overdue rendering, the UTC-5 calendar boundary, absence of timestamp/timezone text, and preservation of raw UTC payload values.
- Modify `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs` to lock the overdue route's required tokens in both body formats.
- Modify `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs` to add invariant, fixed-offset display tokens without changing scheduling or raw UTC tokens.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` to add the request link and consume date-only display tokens in only the overdue templates.
- Modify `CODEX_HANDOFF.md` after verification so the next session can distinguish completed implementation from the still-pending fresh live acceptance scenario.

### Task 1: Drive overdue rendering through RED-GREEN tests

**Files:**
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs:17-78`
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs:157-174`
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs:1-16,350-390`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs:129-139`

- [ ] **Step 1: Change the focused SLA test to use a UTC instant that crosses the fixed-offset date boundary**

In `RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification`, define the boundary instant before tasks are added and use it for the `WF-PT-1` task:

```csharp
var fixedOffsetBoundary = DateTimeOffset.Parse("2030-01-15T02:00:00Z");
var returned = AddTask(dbContext, "WF-PT-7", "TradingDeskReviewTask", "TradingDesk", Now.AddMinutes(-6));
var overdue = AddTask(dbContext, "WF-PT-1", "CheckerReviewTask", "Checker", fixedOffsetBoundary);
```

Because SLA rows are returned in eligible-time order, update the queued-ID assertion for the earlier boundary instant:

```csharp
Assert.Equal([overdue.Id, returned.Id], result.QueuedNotifications.Select(row => row.WorkflowTaskId));
```

- [ ] **Step 2: Add failing assertions for the link, date-only content, forbidden timestamp text, and preserved raw payload**

Replace the current overdue body/payload assertion block with:

```csharp
const string requestUrl = "https://sharepoint.example.test/trades/1050";
Assert.Equal("Overdue: Workflow task overdue", overdueNotification.Subject);
Assert.Contains("WF-PT-1", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains("CheckerReviewTask", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains("Task due date: 01/14/2030", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains("Overdue date: 01/14/2030", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains($"Request link: {requestUrl}", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains("<strong>WF-PT-1</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
Assert.Contains("<strong>CheckerReviewTask</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
Assert.Contains("Task due date: <strong>01/14/2030</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
Assert.Contains("Overdue date: <strong>01/14/2030</strong>", overdueNotification.HtmlBody, StringComparison.Ordinal);
Assert.Contains($"<a href=\"{requestUrl}\">Open request</a>", overdueNotification.HtmlBody, StringComparison.Ordinal);
Assert.Equal(RecipientSnapshot, overdueNotification.NotificationRecipientsJson);

foreach (var forbidden in new[]
         {
             fixedOffsetBoundary.ToString("O"),
             "01/14/2030 +00:00",
             "01/14/2030 -05:00",
             "01/14/2030 UTC",
             "01/14/2030 UTC-5",
             "01/14/2030 EST",
             "01/14/2030 EDT"
         })
{
    Assert.DoesNotContain(forbidden, overdueNotification.Body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(forbidden, overdueNotification.HtmlBody, StringComparison.OrdinalIgnoreCase);
}

using var payload = JsonDocument.Parse(overdueNotification.PayloadJson);
Assert.False(payload.RootElement.TryGetProperty("ccEmailAddresses", out _));
Assert.Equal(fixedOffsetBoundary.ToString("O"), payload.RootElement.GetProperty("dueAtUtc").GetString());
Assert.Equal(fixedOffsetBoundary.ToString("O"), payload.RootElement.GetProperty("overdueAtUtc").GetString());
```

- [ ] **Step 3: Lock the overdue seed template contract with failing assertions**

Append this block to `NotificationRoutes_HaveSubjectBodyHtmlBodyAndOverduePrefix` after the returned-route assertions:

```csharp
var overdue = routes["RouteSlaTaskOverdue"];
Assert.Contains("{{dueDateDisplay}}", overdue.BodyTemplate, StringComparison.Ordinal);
Assert.Contains("{{overdueDateDisplay}}", overdue.BodyTemplate, StringComparison.Ordinal);
Assert.Contains("{{tradeLink}}", overdue.BodyTemplate, StringComparison.Ordinal);
Assert.Contains("{{dueDateDisplay}}", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
Assert.Contains("{{overdueDateDisplay}}", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
Assert.Contains("""<a href="{{tradeLink}}">Open request</a>""", overdue.HtmlBodyTemplate, StringComparison.Ordinal);
```

- [ ] **Step 4: Add a focused failure-path test for a missing saved request link**

Add this test before the existing invalid-context test in `Phase5SlaTests.cs`:

```csharp
[Fact]
public async Task RunSlaWarnsAndSkipsWhenOverdueRequestLinkIsMissing()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    var versionId = PortfolioTradeVersionId(dbContext);
    AddInstance(dbContext, "WF-PT-MISSING-LINK", "PT-MISSING-LINK", "Active", versionId);
    var task = AddTask(
        dbContext,
        "WF-PT-MISSING-LINK",
        "CheckerReviewTask",
        "Checker",
        Now.AddMinutes(-5));
    var context = NotificationContext();
    context.Remove("tradeLink");
    task.NotificationContextJson = JsonSerializer.Serialize(context);
    dbContext.SaveChanges();

    var result = await new RunSlaService(
        dbContext,
        new NotificationTemplateRenderer(),
        new SlaScheduleCalculator()).RunAsync(new(Now), CancellationToken.None);

    Assert.Contains(
        result.Warnings,
        warning => warning.StartsWith(
                $"task {task.Id} SLA notification template missing:",
                StringComparison.Ordinal)
            && warning.Contains(
                "Template token 'tradeLink' requires workflow.notificationContext.tradeLink.",
                StringComparison.Ordinal));
    Assert.Empty(result.QueuedNotifications);
    Assert.DoesNotContain(
        dbContext.NotificationOutbox,
        row => row.WorkflowTaskId == task.Id && row.NotificationType == "SlaTaskOverdue");
}
```

- [ ] **Step 5: Run the three focused tests and verify RED**

Run in PowerShell:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification|FullyQualifiedName~Phase5SlaTests.RunSlaWarnsAndSkipsWhenOverdueRequestLinkIsMissing|FullyQualifiedName~SeedDataSmokeTests.NotificationRoutes_HaveSubjectBodyHtmlBodyAndOverduePrefix"
```

Expected: all three tests fail for the intended missing contract. The rendering and seed tests fail because the current overdue templates have neither `tradeLink` nor display tokens and still render round-trip UTC timestamps; the missing-link test fails because no current overdue template consumes `tradeLink`, so the reminder is queued instead of warning and skipping.

- [ ] **Step 6: Add fixed UTC-5 display token generation while preserving raw tokens**

Add the namespace import at the top of `RunSlaService.cs`:

```csharp
using System.Globalization;
```

Add the fixed offset inside `RunSlaService` immediately after the class opening brace:

```csharp
private static readonly TimeSpan FixedUtcMinusFiveOffset = TimeSpan.FromHours(-5);
```

Keep `dueAtUtc` and `overdueAtUtc`, and add the two display values in `TryBuildNotificationContext`:

```csharp
context["dueAtUtc"] = schedule.OverdueAtUtc.ToString("O");
context["dueDateDisplay"] = FormatFixedUtcMinusFiveDate(schedule.OverdueAtUtc);
context["dueReminderAtUtc"] = schedule.DueReminderAtUtc?.ToString("O");
context["overdueAtUtc"] = schedule.OverdueAtUtc.ToString("O");
context["overdueDateDisplay"] = FormatFixedUtcMinusFiveDate(schedule.OverdueAtUtc);
```

Add this helper immediately after `TryBuildNotificationContext`:

```csharp
private static string FormatFixedUtcMinusFiveDate(DateTimeOffset value)
{
    return value
        .ToOffset(FixedUtcMinusFiveOffset)
        .ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 7: Update only the overdue plain-text and HTML templates**

Replace the two overdue template constants in `PortfolioTradeSeedData.cs` with:

```csharp
private const string SlaTaskOverdueBody = """
    Workflow instance {{workflowInstanceId}} has an overdue task {{taskKey}} assigned to {{assignedRoleKey}}.
    Task due date: {{dueDateDisplay}}
    Overdue date: {{overdueDateDisplay}}
    Request link: {{tradeLink}}
    """;

private const string SlaTaskOverdueHtmlBody = """
    <p>Workflow instance <strong>{{workflowInstanceId}}</strong> has an overdue task <strong>{{taskKey}}</strong> assigned to <strong>{{assignedRoleKey}}</strong>.</p>
    <p>Task due date: <strong>{{dueDateDisplay}}</strong></p>
    <p>Overdue date: <strong>{{overdueDateDisplay}}</strong></p>
    <p><a href="{{tradeLink}}">Open request</a></p>
    """;
```

- [ ] **Step 8: Re-run the three focused tests and verify GREEN**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification|FullyQualifiedName~Phase5SlaTests.RunSlaWarnsAndSkipsWhenOverdueRequestLinkIsMissing|FullyQualifiedName~SeedDataSmokeTests.NotificationRoutes_HaveSubjectBodyHtmlBodyAndOverduePrefix"
```

Expected: 3 tests passed, 0 failed. The runtime test proves `2030-01-15T02:00:00Z` displays as `01/14/2030`, the request link appears in both bodies, raw UTC values remain in `PayloadJson`, and a snapshot without `tradeLink` warns and queues nothing.

- [ ] **Step 9: Commit the tested implementation**

```powershell
git add tests/WorkflowEngine.Tests/Phase5SlaTests.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs
git commit -m "fix: correct overdue email link and dates"
```

### Task 2: Run repository and live-database quality gates

**Files:**
- Verify: `src/WorkflowEngine.sln`
- Verify: `workflow-api/WorkflowEngine.WorkflowApi.slnx`
- Update metadata in configured SQL Server database using the existing console seed command

- [ ] **Step 1: Stop only the API process listening on port 5058 if it would lock build outputs**

Run:

```powershell
$apiListener = Get-NetTCPConnection -LocalPort 5058 -State Listen -ErrorAction SilentlyContinue
if ($apiListener) {
    $apiProcessId = $apiListener.OwningProcess | Select-Object -Unique
    Get-Process -Id $apiProcessId
    Stop-Process -Id $apiProcessId
}
Get-NetTCPConnection -LocalPort 5058 -State Listen -ErrorAction SilentlyContinue
```

Expected: the exact listener process is shown before termination and no listener remains afterward. Do not stop smtp4dev on ports 5000/3025.

- [ ] **Step 2: Restore and build the shared engine with warnings as errors**

```powershell
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -m:1 --no-restore -warnaserror
```

Expected: build succeeds with 0 errors. Record any known transitive package warning separately rather than suppressing it.

- [ ] **Step 3: Run the default shared-engine test suite sequentially**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --no-build
```

Expected: all default tests pass and no SMTP-category test sends to an external relay.

- [ ] **Step 4: Restore, build, and test the Workflow API solution sequentially**

```powershell
dotnet restore workflow-api/WorkflowEngine.WorkflowApi.slnx
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 --no-restore -warnaserror
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 --no-build
```

Expected: API build and tests pass. The pre-existing transitive `Microsoft.OpenApi 2.4.1` NU1903 warning, if emitted during restore, is recorded as an open dependency warning and is not treated as caused by this notification change.

- [ ] **Step 5: Apply the normal seed upsert and verify active metadata**

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src/WorkflowEngine.Console --no-build -- init-db
dotnet run --project src/WorkflowEngine.Console --no-build -- verify-seed
```

Expected: both commands succeed. `init-db` updates `RouteSlaTaskOverdue` template metadata but does not reset workflow runtime data or rewrite sent outbox history.

- [ ] **Step 6: Verify the active overdue route and immutable sent history directly**

Run a read-only SQL query against `workflowengine` that selects the active `RouteSlaTaskOverdue` body fields and the existing sent overdue outbox row. Confirm the route now contains `{{tradeLink}}`, `{{dueDateDisplay}}`, and `{{overdueDateDisplay}}`; confirm the existing sent row remains sent and unchanged.

Expected: active metadata is updated while the historic smtp4dev message/outbox payload still represents the original send.

- [ ] **Step 7: Restart the API with the existing local SMTP configuration**

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://localhost:5058'
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$env:Notifications__Sender = 'Smtp'
$env:Smtp__Host = 'localhost'
$env:Smtp__Port = '3025'
$env:Smtp__From = 'workflow@example.test'
Start-Process -FilePath 'workflow-api\src\WorkflowEngine.WorkflowApi\bin\Debug\net10.0\WorkflowEngine.WorkflowApi.exe' -WorkingDirectory 'workflow-api\src\WorkflowEngine.WorkflowApi\bin\Debug\net10.0' -WindowStyle Hidden
Get-NetTCPConnection -LocalPort 5058 -State Listen
```

Expected: exactly one listener is active on port 5058. Do not create or replay a workflow occurrence yet; fresh live acceptance requires separate user approval.

### Task 3: Preserve the completion and acceptance boundary

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Prepend the verified implementation state to `CODEX_HANDOFF.md`**

Record these concrete facts with the actual test/build counts from Task 2:

```markdown
## Resume point - 2026-08-09 overdue email correction implemented

### Current goal
Run a fresh, user-approved overdue occurrence through the live API/smtp4dev stack to visually accept the corrected email.

### Completed changes
- Added fixed UTC-5, `MM/dd/yyyy` display tokens for SLA overdue rendering while preserving raw UTC payload values.
- Added the persisted request link to plain-text and HTML overdue templates; HTML uses `Open request`.
- Applied and verified the normal seed upsert against `workflowengine` without replaying sent history.

### Remaining tasks
- Obtain approval for a fresh live workflow/task occurrence.
- Inspect the new smtp4dev message for To/Cc, clickable link, date-only formatting, and absence of timezone text.

### Known risks
- Fixed UTC-5 is intentional all year and does not follow daylight-saving time.
- Existing sent outbox rows/messages remain immutable and therefore still show their original content.
```

Add the exact changed-file list, verification commands, pass counts, active API/smtp4dev listener status, and any genuine warnings observed during Task 2.

- [ ] **Step 2: Commit only the handoff update**

```powershell
git add CODEX_HANDOFF.md
git commit -m "docs: hand off overdue email live acceptance"
```

- [ ] **Step 3: Inspect final scope and history**

```powershell
git status --short --branch
git log -3 --oneline
git diff HEAD~2..HEAD --stat
```

Expected: implementation and handoff commits are present; pre-existing `.planning` changes and unrelated untracked directories remain untouched; no API contract, migration, recipient, SMTP sender, or public sample file was changed.

- [ ] **Step 4: Stop before live workflow creation**

Report implementation and automated/database verification as complete, but list fresh live-email acceptance as open. Do not alter or replay the existing sent outbox row, and do not create a fresh workflow occurrence without explicit user approval.
