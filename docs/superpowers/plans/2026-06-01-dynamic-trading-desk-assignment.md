# Dynamic Trading Desk Assignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the upstream API to pass a resolved trading desk role so `CheckerSendToTradingDesk` creates the task and notification for `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity`.

**Architecture:** Keep the workflow transition unchanged and generic. Add SQL-backed task assignment role metadata, pass `assignedRoleKey` through the workflow action contract, validate it against metadata before side effects, and use it for created task assignment plus notification recipient fallback. Broad `TradingDesk` remains the actor role authorized to perform later trading-desk actions.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core SQL Server migrations, xUnit, existing workflow engine application/infrastructure projects.

---

## File Structure

- Modify `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
  - Add `WorkflowActionBlock.AssignedRoleKey` so console/internal execution can carry the dynamic task assignment target.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
  - Add public API `AssignedRoleKey`.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
  - Trim and map request `AssignedRoleKey` into the internal envelope.
- Create `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskAssignmentRoleDefinition.cs`
  - Generic metadata row for roles allowed to receive dynamically assigned tasks.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
  - Add DbSet and EF mapping for the new metadata table.
- Create EF migration under `src/WorkflowEngine.Infrastructure/Migrations/`
  - Add table `WorkflowTaskAssignmentRoleDefinition` and update snapshot.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
  - Add three specific trading desk roles.
  - Add assignment role seeds for `TradingDeskReviewTask`.
  - Change `RouteTradingDeskAssigned` recipient to `null` so it follows the created task assignment role.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
  - Upsert assignment role metadata.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
  - Verify assignment role metadata and route behavior.
- Modify `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
  - Validate assignment key before side effects.
  - Use the dynamic key for created task assignment.
  - Use created task assignment as notification recipient when the route recipient is null.
- Modify focused tests:
  - `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`
  - `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
  - `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
  - `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- Modify local SMTP config:
  - `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
- Modify console sample data:
  - `samples/workflow-actions/05-checker-send-to-trading-desk.json`
- Modify docs:
  - `workflow-api/WORKFLOW_API_BOUNDARIES.md`
  - `CODEX_HANDOFF.md`
  - `NEXT_CODEX_PROMPT.md`
  - `TODO_WORKFLOW_API.md`

Do not add `.tools/smtp4dev/...` files. Do not revert the existing DB alignment changes unless the user explicitly asks.

---

### Task 1: Add API Request Mapping Test

**Files:**
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`

- [ ] **Step 1: Write the failing service mapping assertion**

In `ExecuteWorkflowActionBuildsWorkflowOnlyExecutionRequest`, add `AssignedRoleKey` to the request:

```csharp
var response = service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
{
    WorkflowInstanceId = " WF-PT-1001 ",
    ActionKey = " CheckerSendToTradingDesk ",
    AssignedRoleKey = " TradingDeskTaxable "
});
```

Add this assertion after `Assert.Equal("CheckerSendToTradingDesk", envelope.Workflow?.ActionKey);`:

```csharp
Assert.Equal("TradingDeskTaxable", envelope.Workflow?.AssignedRoleKey);
```

- [ ] **Step 2: Add a blank assignment normalization test**

Append this test to `WorkflowActionServiceTests` before `CreateService`:

```csharp
[Fact]
public void ExecuteWorkflowActionTrimsBlankAssignedRoleKeyToNull()
{
    var executionService = new RecordingWorkflowExecutionService(new WorkflowActionResultEnvelope
    {
        Result = new WorkflowActionResult
        {
            Success = true,
            ResultCode = "Success",
            WorkflowInstanceId = "WF-PT-1001",
            ActionKey = "SaveDraft",
            CorrelationId = "api-action-blank-assignment",
            Notification = new WorkflowNotificationSummary { Queued = false }
        },
        WorkflowContext = new WorkflowContext()
    });
    var service = CreateService(
        new StubActorResolver(new WorkflowActorSnapshot("maker1", "Maker One", ["Maker"])),
        executionService);

    service.ExecuteWorkflowAction(new DefaultHttpContext(), new WorkflowActionRequest
    {
        WorkflowInstanceId = "WF-PT-1001",
        ActionKey = "SaveDraft",
        AssignedRoleKey = " "
    });

    Assert.NotNull(executionService.LastRequest);
    Assert.Null(executionService.LastRequest.Normalized.Envelope.Workflow?.AssignedRoleKey);
}
```

- [ ] **Step 3: Run test to verify it fails**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj -m:1 --filter WorkflowActionServiceTests /p:UseAppHost=false
```

Expected: compile failure because `WorkflowActionRequest.AssignedRoleKey` and `WorkflowActionBlock.AssignedRoleKey` do not exist.

- [ ] **Step 4: Leave the failing test uncommitted**

Do not commit after this failing-test step. Task 2 implements the request mapping and commits the passing test with the code change.

---

### Task 2: Add Assigned Role Contract And API Mapping

**Files:**
- Modify: `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`

- [ ] **Step 1: Add public API request property**

In `WorkflowActionRequest`, add:

```csharp
public string? AssignedRoleKey { get; set; }
```

The full class should be:

```csharp
namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowActionRequest
{
    public string? WorkflowInstanceId { get; set; }

    public string? ActionKey { get; set; }

    public string? AssignedRoleKey { get; set; }
}
```

- [ ] **Step 2: Add internal workflow assignment field**

In `WorkflowActionBlock`, insert this property after `ActionReason`:

```csharp
public string? AssignedRoleKey { get; set; }
```

- [ ] **Step 3: Map API request field into the envelope**

In `WorkflowActionService.ExecuteWorkflowAction`, after `actionKey` is resolved, add:

```csharp
var assignedRoleKey = OptionalText(request.AssignedRoleKey);
```

In the `Workflow = new WorkflowActionBlock` initializer, add:

```csharp
AssignedRoleKey = assignedRoleKey,
```

At the bottom of `WorkflowActionService`, after `RequiredText`, add:

```csharp
private static string? OptionalText(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 4: Run API service tests**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj -m:1 --filter WorkflowActionServiceTests /p:UseAppHost=false
```

Expected: all `WorkflowActionServiceTests` pass.

- [ ] **Step 5: Commit contract, mapping, and passing tests**

```powershell
git add src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs
git commit -m "feat: pass assigned role through workflow action api"
```

---

### Task 3: Add Assignment Metadata Schema And Seed Verification

**Files:**
- Create: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskAssignmentRoleDefinition.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- Create: EF migration under `src/WorkflowEngine.Infrastructure/Migrations/`

- [ ] **Step 1: Add failing seed smoke test**

In `SeedDataSmokeTests`, add this test after `PortfolioTradeTransitionsMatchRequiredMatrix`:

```csharp
[Fact]
public void PortfolioTradeTradingDeskAssignmentRolesAreSeeded()
{
    using var dbContext = CreateDbContext();
    dbContext.Database.EnsureDeleted();
    dbContext.Database.Migrate();

    new WorkflowSeedService(dbContext, TimeProvider.System).SeedPortfolioTrade();
    var versionId = GetPortfolioTradeVersionId(dbContext);

    var roleKeys = dbContext.WorkflowRoleDefinitions
        .Where(row => row.WorkflowDefinitionVersionId == versionId)
        .Select(row => row.RoleKey)
        .ToHashSet(StringComparer.Ordinal);
    Assert.Contains("TradingDeskTaxable", roleKeys);
    Assert.Contains("TradingDeskTaxExempt", roleKeys);
    Assert.Contains("TradingDeskEquity", roleKeys);

    var assignmentRoles = dbContext.WorkflowTaskAssignmentRoleDefinitions
        .Where(row => row.WorkflowDefinitionVersionId == versionId && row.TaskKey == "TradingDeskReviewTask")
        .Select(row => row.RoleKey)
        .OrderBy(row => row)
        .ToArray();
    Assert.Equal(
        ["TradingDeskEquity", "TradingDeskTaxExempt", "TradingDeskTaxable"],
        assignmentRoles);

    var route = Assert.Single(dbContext.NotificationRoutes.Where(row =>
        row.WorkflowDefinitionVersionId == versionId && row.RouteKey == "RouteTradingDeskAssigned"));
    Assert.Null(route.RecipientRoleKey);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SeedDataSmokeTests /p:UseAppHost=false
```

Expected: compile failure because `WorkflowTaskAssignmentRoleDefinitions` does not exist.

- [ ] **Step 3: Add assignment role entity**

Create `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskAssignmentRoleDefinition.cs`:

```csharp
namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTaskAssignmentRoleDefinition
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string TaskKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
```

- [ ] **Step 4: Register DbSet and EF mapping**

In `WorkflowDbContext`, add this DbSet after `WorkflowTaskDefinitions`:

```csharp
public DbSet<WorkflowTaskAssignmentRoleDefinition> WorkflowTaskAssignmentRoleDefinitions => Set<WorkflowTaskAssignmentRoleDefinition>();
```

In `ConfigureDefinitions`, after `WorkflowTaskDefinition` mapping, add:

```csharp
modelBuilder.Entity<WorkflowTaskAssignmentRoleDefinition>(entity =>
{
    entity.ToTable("WorkflowTaskAssignmentRoleDefinition");
    entity.HasKey(row => row.Id);
    entity.Property(row => row.TaskKey).HasMaxLength(100).IsRequired();
    entity.Property(row => row.RoleKey).HasMaxLength(100).IsRequired();
    entity.HasIndex(row => new { row.WorkflowDefinitionVersionId, row.TaskKey, row.RoleKey }).IsUnique();
});
```

- [ ] **Step 5: Generate migration**

Run:

```powershell
dotnet tool restore
dotnet ef migrations add AddWorkflowTaskAssignmentRoles --project src\WorkflowEngine.Infrastructure --startup-project src\WorkflowEngine.Console
```

Expected:

- A migration file ending in `AddWorkflowTaskAssignmentRoles.cs`.
- A matching designer file ending in `AddWorkflowTaskAssignmentRoles.Designer.cs`.
- `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs` updated with `WorkflowTaskAssignmentRoleDefinition`.

- [ ] **Step 6: Add seed data records**

In `PortfolioTradeSeedData.Roles`, add the three roles after `TradingDesk`:

```csharp
new("TradingDeskTaxable", "Taxable"),
new("TradingDeskTaxExempt", "Tax Exempt"),
new("TradingDeskEquity", "Equity"),
```

In `PortfolioTradeSeedData.NotificationRoutes`, change `RouteTradingDeskAssigned` to:

```csharp
new("RouteTradingDeskAssigned", "WorkflowTaskAssigned", null, "Portfolio Trade assigned to Trading Desk"),
```

After the `Tasks` array, add:

```csharp
public static readonly TaskAssignmentRoleSeed[] TaskAssignmentRoles =
[
    new("TradingDeskReviewTask", "TradingDeskTaxable"),
    new("TradingDeskReviewTask", "TradingDeskTaxExempt"),
    new("TradingDeskReviewTask", "TradingDeskEquity")
];
```

At the bottom of the file, after `TaskSeed`, add:

```csharp
internal sealed record TaskAssignmentRoleSeed(string TaskKey, string RoleKey);
```

- [ ] **Step 7: Upsert assignment role metadata**

In `WorkflowSeedService.SeedPortfolioTrade`, add this call after `UpsertTasks(version.Id);`:

```csharp
UpsertTaskAssignmentRoles(version.Id);
```

Add this method after `UpsertTasks`:

```csharp
private void UpsertTaskAssignmentRoles(long versionId)
{
    foreach (var seed in PortfolioTradeSeedData.TaskAssignmentRoles)
    {
        var row = dbContext.WorkflowTaskAssignmentRoleDefinitions.SingleOrDefault(item =>
            item.WorkflowDefinitionVersionId == versionId
            && item.TaskKey == seed.TaskKey
            && item.RoleKey == seed.RoleKey);
        if (row is null)
        {
            row = new WorkflowTaskAssignmentRoleDefinition
            {
                WorkflowDefinitionVersionId = versionId,
                TaskKey = seed.TaskKey,
                RoleKey = seed.RoleKey
            };
            dbContext.WorkflowTaskAssignmentRoleDefinitions.Add(row);
        }

        row.IsActive = true;
    }
}
```

- [ ] **Step 8: Verify assignment metadata**

In `SeedVerificationService.Verify`, add this call after `VerifyTaskReferences(errors, version.Id);`:

```csharp
VerifyTaskAssignmentRoles(errors, version.Id);
```

Add this method after `VerifyTaskReferences`:

```csharp
private void VerifyTaskAssignmentRoles(ICollection<string> errors, long versionId)
{
    var roleKeys = dbContext.WorkflowRoleDefinitions
        .Where(row => row.WorkflowDefinitionVersionId == versionId)
        .Select(row => row.RoleKey)
        .ToHashSet(StringComparer.Ordinal);
    var taskKeys = dbContext.WorkflowTaskDefinitions
        .Where(row => row.WorkflowDefinitionVersionId == versionId)
        .Select(row => row.TaskKey)
        .ToHashSet(StringComparer.Ordinal);

    var expected = PortfolioTradeSeedData.TaskAssignmentRoles
        .Select(row => $"{row.TaskKey}/{row.RoleKey}");
    var actual = dbContext.WorkflowTaskAssignmentRoleDefinitions
        .Where(row => row.WorkflowDefinitionVersionId == versionId && row.IsActive)
        .Select(row => row.TaskKey + "/" + row.RoleKey);
    VerifyKeySet(errors, "task assignment role", expected, actual);

    foreach (var row in dbContext.WorkflowTaskAssignmentRoleDefinitions.Where(row => row.WorkflowDefinitionVersionId == versionId))
    {
        if (!taskKeys.Contains(row.TaskKey))
        {
            errors.Add($"Task assignment role references missing TaskKey '{row.TaskKey}'.");
        }

        if (!roleKeys.Contains(row.RoleKey))
        {
            errors.Add($"Task assignment role references missing RoleKey '{row.RoleKey}'.");
        }
    }
}
```

- [ ] **Step 9: Run seed smoke tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SeedDataSmokeTests /p:UseAppHost=false
```

Expected: all `SeedDataSmokeTests` pass.

- [ ] **Step 10: Commit schema and seed metadata**

```powershell
git add src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTaskAssignmentRoleDefinition.cs src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs src/WorkflowEngine.Infrastructure/Migrations src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
git commit -m "feat: seed dynamic trading desk assignment metadata"
```

---

### Task 4: Add Execution Tests For Dynamic Assignment

**Files:**
- Modify: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`

- [ ] **Step 1: Add happy-path assignment tests**

In `WorkflowTransitionTests`, add this theory after `CheckerReturnToMakerClosesCheckerTaskAndCreatesMakerReworkTask`:

```csharp
[Theory]
[InlineData("TradingDeskTaxable")]
[InlineData("TradingDeskTaxExempt")]
[InlineData("TradingDeskEquity")]
public void CheckerSendToTradingDeskAssignsSpecificDeskTaskAndNotification(string assignedRoleKey)
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
    dbContext.WorkflowTasks.Add(new WorkflowTask
    {
        WorkflowInstanceId = "WF-PT-1001",
        TaskKey = "CheckerReviewTask",
        AssignedRoleKey = "Checker",
        Status = "Pending",
        CreatedAtUtc = Now,
        DueAtUtc = Now.AddMinutes(2)
    });
    dbContext.SaveChanges();

    var envelope = Envelope(
        actionKey: "CheckerSendToTradingDesk",
        sectionKey: "Checker",
        buttonKey: "CheckerPrimary",
        expectedState: "CheckerReview",
        expectedVersion: 2,
        expectedTaskKey: "CheckerReviewTask",
        roles: ["Checker"],
        userId: "checker1");
    envelope.Workflow!.AssignedRoleKey = assignedRoleKey;

    var result = CreateService(dbContext).Execute(Request(envelope));

    Assert.True(result.Result.Success);
    Assert.Equal("TradingDeskReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
    Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "CheckerReviewTask" && row.Status == "Completed");
    var tradingTask = Assert.Single(dbContext.WorkflowTasks.Where(row => row.TaskKey == "TradingDeskReviewTask"));
    Assert.Equal("Pending", tradingTask.Status);
    Assert.Equal(assignedRoleKey, tradingTask.AssignedRoleKey);
    var notification = Assert.Single(dbContext.NotificationOutbox);
    Assert.Equal("WorkflowTaskAssigned", notification.NotificationType);
    Assert.Equal(assignedRoleKey, notification.RecipientRoleKey);
    Assert.Equal(assignedRoleKey, result.Result.CreatedTask?.AssignedRole);
    Assert.Equal(assignedRoleKey, result.Result.Notification?.RecipientRole);
}
```

- [ ] **Step 2: Add broad role execution test**

In `WorkflowTransitionTests`, add this fact after the theory:

```csharp
[Fact]
public void BroadTradingDeskRoleCanExecuteTaskAssignedToSpecificDesk()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "TradingDeskReview", "Active", 3);
    dbContext.WorkflowTasks.Add(new WorkflowTask
    {
        WorkflowInstanceId = "WF-PT-1001",
        TaskKey = "TradingDeskReviewTask",
        AssignedRoleKey = "TradingDeskTaxable",
        Status = "Pending",
        CreatedAtUtc = Now,
        DueAtUtc = Now.AddMinutes(2)
    });
    dbContext.SaveChanges();

    var result = CreateService(dbContext).Execute(Request(Envelope(
        actionKey: "TradingTradeExecuted",
        sectionKey: "TradingDesk",
        buttonKey: "TradingPrimary",
        expectedState: "TradingDeskReview",
        expectedVersion: 3,
        expectedTaskKey: "TradingDeskReviewTask",
        roles: ["TradingDesk"],
        userId: "trader1")));

    Assert.True(result.Result.Success);
    Assert.Equal("BdrmClientServiceReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
    Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "TradingDeskReviewTask" && row.Status == "Completed");
    Assert.Contains(dbContext.WorkflowTasks, row => row.TaskKey == "BdrmClientServiceReviewTask" && row.AssignedRoleKey == "BDRMClientService" && row.Status == "Pending");
}
```

- [ ] **Step 3: Add validation tests**

In `WorkflowExecutionValidationTests`, add this fact after `ActionSectionEchoMustMatchActionMetadataBeforeSideEffects`:

```csharp
[Fact]
public void DynamicAssignmentMissingRoleReturnsValidationFailedBeforeSideEffects()
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
    AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
    dbContext.SaveChanges();
    var envelope = Envelope(
        expectedState: "CheckerReview",
        expectedVersion: 2,
        actionKey: "CheckerSendToTradingDesk",
        roles: ["Checker"]);
    envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";

    var result = CreateService(dbContext).Execute(Request(envelope));

    Assert.Equal("ValidationFailed", result.Result.ResultCode);
    Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
    Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
    Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending" && row.TaskKey == "CheckerReviewTask"));
    Assert.Empty(dbContext.NotificationOutbox);
    Assert.Empty(dbContext.WorkflowAudits);
    Assert.Empty(dbContext.MockSectionData);
}
```

Add this second fact:

```csharp
[Fact]
public void DynamicAssignmentInvalidRoleReturnsValidationFailedBeforeSideEffects()
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "CheckerReview", "Active", 2);
    AddTask(dbContext, "WF-PT-1001", "CheckerReviewTask", "Checker");
    dbContext.SaveChanges();
    var envelope = Envelope(
        expectedState: "CheckerReview",
        expectedVersion: 2,
        actionKey: "CheckerSendToTradingDesk",
        roles: ["Checker"]);
    envelope.Workflow!.ExpectedCurrentTaskKey = "CheckerReviewTask";
    envelope.Workflow.AssignedRoleKey = "TradingDeskMunicipal";

    var result = CreateService(dbContext).Execute(Request(envelope));

    Assert.Equal("ValidationFailed", result.Result.ResultCode);
    Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
    Assert.Equal("CheckerReview", dbContext.WorkflowInstances.Single().CurrentStateKey);
    Assert.Single(dbContext.WorkflowTasks.Where(row => row.Status == "Pending" && row.TaskKey == "CheckerReviewTask"));
    Assert.Empty(dbContext.NotificationOutbox);
    Assert.Empty(dbContext.WorkflowAudits);
    Assert.Empty(dbContext.MockSectionData);
}
```

Add this third fact:

```csharp
[Fact]
public void AssignmentRoleOnNonDynamicTransitionReturnsValidationFailedBeforeSideEffects()
{
    using var dbContext = CreateSeededContext();
    AddInstance(dbContext, "WF-PT-1001", "PT-1001", "Draft", "Active", 1);
    dbContext.SaveChanges();
    var envelope = Envelope();
    envelope.Workflow!.AssignedRoleKey = "TradingDeskTaxable";

    var result = CreateService(dbContext).Execute(Request(envelope));

    Assert.Equal("ValidationFailed", result.Result.ResultCode);
    Assert.Contains("workflow.assignedRoleKey", result.Result.Diagnostics.ValidationErrors!.Select(error => error.Field));
    Assert.Equal("Draft", dbContext.WorkflowInstances.Single().CurrentStateKey);
    Assert.Empty(dbContext.WorkflowTasks);
    Assert.Empty(dbContext.NotificationOutbox);
    Assert.Empty(dbContext.WorkflowAudits);
    Assert.Empty(dbContext.MockSectionData);
}
```

- [ ] **Step 4: Run tests to verify failures**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "WorkflowTransitionTests|WorkflowExecutionValidationTests" /p:UseAppHost=false
```

Expected: new tests fail because the engine still uses `WorkflowTaskDefinition.OwnerRoleKey` and route recipient directly.

- [ ] **Step 5: Leave the failing engine tests uncommitted**

Do not commit after this failing-test step. Task 5 implements workflow execution behavior and commits the passing tests with the code change.

---

### Task 5: Implement Dynamic Assignment In Workflow Execution

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- [ ] **Step 1: Validate assignment target after transition selection**

In `Execute`, after `transition` is chosen and before `BuildOptionWarnings`, add:

```csharp
var assignmentValidation = ValidateAssignedRoleKey(normalized, instance.WorkflowDefinitionVersionId, transition);
if (assignmentValidation is not null)
{
    return assignmentValidation;
}
```

Add this helper near `ValidateActionEcho`:

```csharp
private WorkflowActionResultEnvelope? ValidateAssignedRoleKey(
    NormalizedWorkflowAction normalized,
    long workflowDefinitionVersionId,
    WorkflowTransitionDefinition transition)
{
    var assignedRoleKey = normalized.Envelope.Workflow?.AssignedRoleKey?.Trim();
    var createsTask = !string.IsNullOrWhiteSpace(transition.TaskToCreateKey);
    var hasDynamicAssignment = createsTask && dbContext.WorkflowTaskAssignmentRoleDefinitions.AsNoTracking().Any(row =>
        row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
        && row.TaskKey == transition.TaskToCreateKey
        && row.IsActive);

    var errors = new List<WorkflowValidationError>();
    if (!hasDynamicAssignment)
    {
        if (!string.IsNullOrWhiteSpace(assignedRoleKey))
        {
            errors.Add(new WorkflowValidationError(
                "workflow.assignedRoleKey",
                "workflow.assignedRoleKey is only allowed when the created task supports dynamic assignment."));
        }

        return errors.Count == 0 ? null : resultBuilder.BuildValidationFailed(normalized, errors);
    }

    if (string.IsNullOrWhiteSpace(assignedRoleKey))
    {
        errors.Add(new WorkflowValidationError(
            "workflow.assignedRoleKey",
            $"workflow.assignedRoleKey is required when creating task '{transition.TaskToCreateKey}'."));
        return resultBuilder.BuildValidationFailed(normalized, errors);
    }

    var isAllowed = dbContext.WorkflowTaskAssignmentRoleDefinitions.AsNoTracking().Any(row =>
        row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
        && row.TaskKey == transition.TaskToCreateKey
        && row.RoleKey == assignedRoleKey
        && row.IsActive);
    if (!isAllowed)
    {
        errors.Add(new WorkflowValidationError(
            "workflow.assignedRoleKey",
            $"workflow.assignedRoleKey '{assignedRoleKey}' is not allowed for task '{transition.TaskToCreateKey}'."));
    }

    return errors.Count == 0 ? null : resultBuilder.BuildValidationFailed(normalized, errors);
}
```

- [ ] **Step 2: Use assignment role when creating tasks**

In the task creation block, replace:

```csharp
AssignedRoleKey = taskDefinition.OwnerRoleKey,
```

with:

```csharp
AssignedRoleKey = ResolveCreatedTaskAssignedRole(
    instance.WorkflowDefinitionVersionId,
    taskDefinition,
    workflow.AssignedRoleKey),
```

Add this helper near `ResolveDueAtUtc`:

```csharp
private string ResolveCreatedTaskAssignedRole(
    long workflowDefinitionVersionId,
    WorkflowTaskDefinition taskDefinition,
    string? assignedRoleKey)
{
    var hasDynamicAssignment = dbContext.WorkflowTaskAssignmentRoleDefinitions.AsNoTracking().Any(row =>
        row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
        && row.TaskKey == taskDefinition.TaskKey
        && row.IsActive);

    return hasDynamicAssignment
        ? assignedRoleKey!.Trim()
        : taskDefinition.OwnerRoleKey;
}
```

- [ ] **Step 3: Use created task assignment for route recipient fallback**

In the notification block, before creating `NotificationOutbox`, add:

```csharp
var recipientRoleKey = route.RecipientRoleKey ?? createdTask?.AssignedRole;
```

Replace:

```csharp
RecipientRoleKey = route.RecipientRoleKey,
```

with:

```csharp
RecipientRoleKey = recipientRoleKey,
```

Replace:

```csharp
RecipientRole = route.RecipientRoleKey
```

with:

```csharp
RecipientRole = recipientRoleKey
```

- [ ] **Step 4: Run focused engine tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "WorkflowTransitionTests|WorkflowExecutionValidationTests" /p:UseAppHost=false
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit execution implementation and passing tests**

```powershell
git add src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
git commit -m "feat: apply dynamic assigned role during workflow execution"
```

---

### Task 6: Align Console Sample JSON

**Files:**
- Modify: `samples/workflow-actions/05-checker-send-to-trading-desk.json`

- [ ] **Step 1: Add assignment target to the checker-to-trading sample**

In `samples/workflow-actions/05-checker-send-to-trading-desk.json`, change the `workflow` object from:

```json
"workflow": { "workflowKey": "PortfolioTrade", "workflowInstanceId": "WF-PT-1001", "workflowDefinitionVersion": 1, "expectedCurrentState": "CheckerReview", "expectedCurrentTaskId": null, "expectedCurrentTaskKey": "CheckerReviewTask", "expectedInstanceVersion": 2, "actionKey": "CheckerSendToTradingDesk", "actionReason": "Checker sent to Trading Desk.", "idempotencyKey": "sample-05-checker-send-to-trading-desk" }
```

to:

```json
"workflow": { "workflowKey": "PortfolioTrade", "workflowInstanceId": "WF-PT-1001", "workflowDefinitionVersion": 1, "expectedCurrentState": "CheckerReview", "expectedCurrentTaskId": null, "expectedCurrentTaskKey": "CheckerReviewTask", "expectedInstanceVersion": 2, "actionKey": "CheckerSendToTradingDesk", "actionReason": "Checker sent to Trading Desk.", "assignedRoleKey": "TradingDeskTaxable", "idempotencyKey": "sample-05-checker-send-to-trading-desk" }
```

Change the expectation in the same file from:

```json
"expectedAssignedRole": "TradingDesk"
```

to:

```json
"expectedAssignedRole": "TradingDeskTaxable"
```

- [ ] **Step 2: Run console sample path against the shared local DB**

Run:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-DESK-SAMPLE --user maker1 --reset
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-DESK-SAMPLE --workflow-instance WF-PT-DESK-SAMPLE
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\05-checker-send-to-trading-desk.json --business-object PT-DESK-SAMPLE --workflow-instance WF-PT-DESK-SAMPLE
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-DESK-SAMPLE
```

Expected: the checker-to-trading action succeeds and `TradingDeskReviewTask` is pending with role `TradingDeskTaxable`.

- [ ] **Step 3: Commit console sample alignment**

```powershell
git add samples/workflow-actions/05-checker-send-to-trading-desk.json
git commit -m "test: align console sample with desk assignment"
```

---

### Task 7: Update API Boundaries, SMTP Local Config, And Docs

**Files:**
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`
- Modify: `CODEX_HANDOFF.md`
- Modify: `NEXT_CODEX_PROMPT.md`
- Modify: `TODO_WORKFLOW_API.md`

- [ ] **Step 1: Update boundary doc request contract**

In `workflow-api/WORKFLOW_API_BOUNDARIES.md`, in the `POST /v1/WorkflowAction` bullet list, change the pure workflow fields list from:

```text
- workflow instance ID
- action key
```

to:

```text
- workflow instance ID
- action key
- optional `assignedRoleKey` for metadata-defined dynamic task assignment
```

Add this bullet in the same endpoint section:

```text
  - `assignedRoleKey` is accepted only for transitions whose created task supports dynamic assignment metadata; for `CheckerSendToTradingDesk`, the upstream form/business API supplies one of `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity`.
```

Change any sentence saying the body contains only `workflowInstanceId` and `actionKey` to:

```text
The request body contains `workflowInstanceId`, `actionKey`, and optional `assignedRoleKey`.
```

- [ ] **Step 2: Add local SMTP role mappings**

In `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`, inside the `http` profile environment variables, add:

```json
"Smtp__RoleEmailMap__TradingDeskTaxable": "cdt-trading-taxable@example.test",
"Smtp__RoleEmailMap__TradingDeskTaxExempt": "cdt-trading-tax-exempt@example.test",
"Smtp__RoleEmailMap__TradingDeskEquity": "cdt-trading-equity@example.test",
```

Keep the existing broad `Smtp__RoleEmailMap__TradingDesk` entry for compatibility.

- [ ] **Step 3: Update handoff files**

In `CODEX_HANDOFF.md`, add a short section under configuration or important decisions:

```markdown
### Dynamic Trading Desk Assignment

- User approved dynamic trading desk assignment.
- Upstream business/form API supplies `assignedRoleKey`.
- Supported assignment role keys are `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`.
- Broad `TradingDesk` remains the actor role for Trading Desk actions.
```

In `NEXT_CODEX_PROMPT.md`, add this bullet under completed/current decisions:

```markdown
- Dynamic trading desk assignment is in progress: `CheckerSendToTradingDesk` accepts optional `assignedRoleKey`; valid values are `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`; broad `TradingDesk` remains the actor role for later trading actions.
```

In `TODO_WORKFLOW_API.md`, mark the dynamic trading desk assignment work complete after Task 8 verification succeeds and leave any unrelated existing checklist items unchanged.

- [ ] **Step 4: Commit docs and local config**

```powershell
git add workflow-api/WORKFLOW_API_BOUNDARIES.md workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json CODEX_HANDOFF.md NEXT_CODEX_PROMPT.md TODO_WORKFLOW_API.md
git commit -m "docs: record dynamic trading desk assignment contract"
```

---

### Task 8: Final Verification

**Files:**
- No code edits in this task.

- [ ] **Step 1: Run workflow API tests**

Run:

```powershell
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Expected: 38 or more tests pass, 0 fail.

- [ ] **Step 2: Run focused workflow engine tests**

Run:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "SeedDataSmokeTests|WorkflowTransitionTests|WorkflowExecutionValidationTests" /p:UseAppHost=false
```

Expected: all selected tests pass.

- [ ] **Step 3: Build active solutions**

Run:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
```

Expected: both builds pass with 0 warnings and 0 errors.

- [ ] **Step 4: Run seed verification against local DB**

Run:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Expected: `verify-seed` reports success and no missing trading desk assignment roles.

- [ ] **Step 5: Run live API check with fresh business object when the API is running**

Use fresh IDs and the local DB `cdt_workflow_api_20260527`.

Start workflow:

```http
POST /v1/WorkflowStart
X-Workflow-UserId: maker1
X-Workflow-Roles: Maker
X-Workflow-DisplayName: Maker One

{
  "workflowKey": "PortfolioTrade",
  "businessObjectType": "PortfolioTrade",
  "businessObjectId": "PT-DESK-001",
  "initialActionKey": "SubmitForChecker"
}
```

Checker sends to specific desk:

```http
POST /v1/WorkflowAction
X-Workflow-UserId: checker1
X-Workflow-Roles: Checker
X-Workflow-DisplayName: Checker One

{
  "workflowInstanceId": "WF-PT-DESK-001",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable"
}
```

Inspect:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-DESK-001
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-DESK-001
```

Expected:

- `TradingDeskReviewTask` is pending with role `TradingDeskTaxable`.
- `WorkflowTaskAssigned` notification recipient is `TradingDeskTaxable`.

- [ ] **Step 6: Commit final verification notes if docs changed**

If `CODEX_HANDOFF.md` or `NEXT_CODEX_PROMPT.md` was updated with final verification output, commit only those docs:

```powershell
git add CODEX_HANDOFF.md NEXT_CODEX_PROMPT.md
git commit -m "docs: record dynamic trading desk verification"
```

---

## Self-Review

- Spec coverage: the plan covers API request shape, SQL metadata, seed data, execution validation, task assignment, notification recipient selection, broad `TradingDesk` authorization, SMTP role mappings, tests, and docs.
- Placeholder scan: no implementation step relies on unspecified names or deferred decisions.
- Type consistency: the plan uses `AssignedRoleKey` for C# properties and `assignedRoleKey` for JSON/API contract consistently.
- Scope: one subsystem, workflow task assignment and notification routing, with no user lookup, form payload parsing, or Portfolio Trade hardcoding inside the engine.
