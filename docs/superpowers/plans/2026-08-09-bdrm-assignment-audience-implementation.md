# BDRM Assignment Notification Audience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every newly-created BDRM assignment notification address BDRM/Client Service in `To` and copy the persisted original Maker plus caller-supplied Checker/other recipients in `Cc`.

**Architecture:** Extend only the versioned Portfolio Trade `RouteBdrmAssigned` recipient metadata. The public API continues accepting the existing named `notification.recipientInputs` dictionary, and the existing generic resolver produces and persists the final task/outbox snapshot that SLA reminders reuse.

**Tech Stack:** .NET 10, C#, ASP.NET Core public request samples, EF Core, SQL Server, xUnit.

---

## File map

- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`: declare the two new BDRM Cc sources.
- Modify `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`: require the exact ordered BDRM route rules.
- Modify `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`: prove action-time resolution, task/outbox snapshot equality, and SLA snapshot reuse.
- Modify `PROJECT.md`: keep the source-of-truth route table and action narrative aligned.
- Modify `workflow-api/WORKFLOW_API_BOUNDARIES.md`: document the allowed Trading execution recipient input.
- Modify `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`: require both Trading execution packs to contain recipient inputs.
- Modify `workflow-api/samples/scenarios/15-trading-desk-trade-executed/request.json`: show Checker and another caller-owned Cc recipient.
- Modify `workflow-api/samples/scenarios/15-trading-desk-trade-executed/README.md`: describe audience ownership and effects.
- Modify `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/request.json`: apply the same contract on re-execution.
- Modify `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/README.md`: describe identical re-execution behavior.
- Modify `CODEX_HANDOFF.md` only in the original checkout after feature integration: record implementation, verification, and live-snapshot limitation without staging its pre-existing user-owned changes.

### Task 1: Lock the metadata and runtime behavior with failing tests

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`

- [ ] **Step 1: Strengthen the seed test**

Change the total recipient-rule assertion from 11 to 13 and add an exact ordered assertion for `RouteBdrmAssigned`:

```csharp
var bdrmRules = rules
    .Where(row => row.RouteKey == "RouteBdrmAssigned")
    .OrderBy(row => row.SortOrder)
    .ToArray();
Assert.Collection(
    bdrmRules,
    row =>
    {
        Assert.Equal("To", row.RecipientKind);
        Assert.Equal("StaticAddress", row.SourceType);
        Assert.Equal("bdrm-client-service@example.test", row.StaticAddress);
        Assert.True(row.IsRequired);
    },
    row =>
    {
        Assert.Equal("Cc", row.RecipientKind);
        Assert.Equal("WorkflowParticipant", row.SourceType);
        Assert.Equal("originalMaker", row.SourceKey);
        Assert.True(row.IsRequired);
    },
    row =>
    {
        Assert.Equal("Cc", row.RecipientKind);
        Assert.Equal("RequestInput", row.SourceType);
        Assert.Equal("selectedCcUsers", row.SourceKey);
        Assert.False(row.IsRequired);
    });
```

- [ ] **Step 2: Add the SQL-backed action/SLA test**

Add `TradingTradeExecutedResolvesBdrmAudienceAndSlaReusesSnapshot`. Arrange a `TradingDeskReview` instance, pending Trading Desk task, and persisted `originalMaker`; send `selectedCcUsers` containing Checker and Manager through `WorkflowNotificationInput`. Assert:

```csharp
Assert.Equal(["bdrm-client-service@example.test"], recipients.ToEmailAddresses);
Assert.Equal(
    ["maker@example.test", "checker@example.test", "manager@example.test"],
    recipients.CcEmailAddresses);
Assert.Equal(task.NotificationRecipientsJson, assignment.NotificationRecipientsJson);
Assert.Equal(task.NotificationRecipientsJson, overdue.NotificationRecipientsJson);
```

Use `RunSlaService` with `Now.AddMinutes(2)` to create the first BDRM overdue occurrence from the newly-created task.

- [ ] **Step 3: Run the focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter "FullyQualifiedName~PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded|FullyQualifiedName~TradingTradeExecutedResolvesBdrmAudienceAndSlaReusesSnapshot"
```

Expected: FAIL because the seed has only the static BDRM `To` rule and rejects `selectedCcUsers` for that route.

### Task 2: Implement the metadata and source-of-truth documentation

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `PROJECT.md`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`

- [ ] **Step 1: Add the BDRM route metadata**

Immediately after the existing order-1 `RouteBdrmAssigned` rule, add:

```csharp
new("RouteBdrmAssigned", "Cc", "WorkflowParticipant", "originalMaker", null, 2, true),
new("RouteBdrmAssigned", "Cc", "RequestInput", "selectedCcUsers", null, 3, false),
```

- [ ] **Step 2: Update the authoritative route table and flow**

In `PROJECT.md`, list all three ordered `RouteBdrmAssigned` rules and state that both `TradingTradeExecuted` paths resolve BDRM as `To`, persisted `originalMaker` as required `Cc`, and optional `selectedCcUsers` as additional `Cc` recipients.

- [ ] **Step 3: Update the public boundary documentation**

In `workflow-api/WORKFLOW_API_BOUNDARIES.md`, document that `TradingTradeExecuted` accepts `selectedCcUsers`; the application places Checker and other intended participants in that list while `originalMaker` remains persistence-derived.

- [ ] **Step 4: Run the focused tests and confirm GREEN**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter "FullyQualifiedName~PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded|FullyQualifiedName~TradingTradeExecutedResolvesBdrmAudienceAndSlaReusesSnapshot"
```

Expected: PASS, 2 tests, 0 failures.

- [ ] **Step 5: Commit the metadata behavior**

```powershell
git add -- src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md
git commit -m "feat: copy workflow audience on BDRM assignment"
```

### Task 3: Update and validate the public scenario packs

**Files:**
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`
- Modify: `workflow-api/samples/scenarios/15-trading-desk-trade-executed/request.json`
- Modify: `workflow-api/samples/scenarios/15-trading-desk-trade-executed/README.md`
- Modify: `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/request.json`
- Modify: `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/README.md`

- [ ] **Step 1: Add the failing scenario contract test**

Add a theory for packs 15 and 18 that deserializes `WorkflowActionRequest`, reads `notification.recipientInputs.selectedCcUsers`, and asserts two complete caller-owned recipients:

```csharp
[Theory]
[InlineData("15-trading-desk-trade-executed")]
[InlineData("18-trading-desk-rework-trade-executed")]
public void TradingExecutionScenariosSupplySelectedCcUsers(string directoryName)
{
    var json = File.ReadAllText(Path.Combine(ScenarioRoot(), directoryName, "request.json"));
    var request = JsonSerializer.Deserialize<WorkflowActionRequest>(json);

    var recipients = request?.Notification?.RecipientInputs?["selectedCcUsers"] ?? [];
    Assert.Equal(2, recipients.Count);
    Assert.All(recipients, recipient =>
    {
        Assert.False(string.IsNullOrWhiteSpace(recipient.UserId));
        Assert.False(string.IsNullOrWhiteSpace(recipient.EmailAddress));
        Assert.False(string.IsNullOrWhiteSpace(recipient.DisplayName));
    });
}
```

- [ ] **Step 2: Run the scenario test and confirm RED**

```powershell
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1 --filter "FullyQualifiedName~TradingExecutionScenariosSupplySelectedCcUsers"
```

Expected: FAIL because packs 15 and 18 currently omit `selectedCcUsers`.

- [ ] **Step 3: Update both public request bodies**

Add this block beside `templateData` in each action's `notification` object:

```json
"recipientInputs": {
  "selectedCcUsers": [
    {
      "userId": "checker.scenario",
      "emailAddress": "checker.scenario@example.test",
      "displayName": "Scenario Checker"
    },
    {
      "userId": "manager.scenario",
      "emailAddress": "manager.scenario@example.test",
      "displayName": "Scenario Manager"
    }
  ]
}
```

- [ ] **Step 4: Update both scenario READMEs**

State that BDRM remains the metadata-owned `To`, persisted original Maker is required `Cc`, supplied `selectedCcUsers` are additional `Cc`, and later BDRM SLA reminders reuse the same snapshot.

- [ ] **Step 5: Run the scenario test and confirm GREEN**

```powershell
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1 --filter "FullyQualifiedName~TradingExecutionScenariosSupplySelectedCcUsers|FullyQualifiedName~ScenarioLibraryCoversEveryApprovedPublicRequestShape"
```

Expected: PASS, 3 test cases, 0 failures.

- [ ] **Step 6: Commit the public handoff changes**

```powershell
git add -- workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs workflow-api/samples/scenarios/15-trading-desk-trade-executed/request.json workflow-api/samples/scenarios/15-trading-desk-trade-executed/README.md workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/request.json workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/README.md
git commit -m "docs: pass BDRM assignment cc recipients"
```

### Task 4: Run quality gates and update the handoff

**Files:**
- Modify after integration: `CODEX_HANDOFF.md` in the original checkout; leave it unstaged because it already contains user-owned changes.

- [ ] **Step 1: Run warning-as-error builds sequentially**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror -m:1
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1
```

Expected: both builds exit 0 with zero warnings and zero errors.

- [ ] **Step 2: Run both complete default test suites sequentially**

```powershell
dotnet test src/WorkflowEngine.sln --no-build -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build -m:1
```

Expected: zero failed tests; smtp4dev-gated tests may remain explicitly skipped.

- [ ] **Step 3: Apply and verify seed metadata without resetting runtime rows**

```powershell
dotnet run --no-build --project src/WorkflowEngine.Console -- init-db
dotnet run --no-build --project src/WorkflowEngine.Console -- verify-seed
```

Expected: schema/seed initialization succeeds and seed verification passes. Do not use `--reset`; existing tasks and outbox snapshots remain unchanged.

- [ ] **Step 4: Run recipient regression searches**

```powershell
rg -n "RecipientRoleKey|RecipientUserId|recipientEmail|RoleEmailMap" src workflow-api/src samples workflow-api/samples tests workflow-api/tests
```

Expected: only intentional template/test/historical compatibility references; no new role-map, singular-recipient, or final-To/Cc public request path.

- [ ] **Step 5: Verify the feature diff and commit any remaining feature-only documentation**

Run:

```powershell
git diff --check
git status --short
```

Expected: only intended feature files are changed in the isolated feature worktree; pre-existing `.planning`, `.tools`, `SQMS`, `tareget-api-integration`, and `CODEX_HANDOFF.md` changes remain confined to the original checkout.

- [ ] **Step 6: Integrate the verified feature branch, then update the original handoff without staging it**

After the feature branch is integrated into `console-code-first-0518`, record commits, exact build/test counts, seed verification, the new UI payload rule, and the fact that existing task 6/outbox 14 retain their original immutable snapshots in the original checkout's `CODEX_HANDOFF.md`. Do not stage or commit that already-dirty file.
