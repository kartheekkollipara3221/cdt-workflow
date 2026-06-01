# Workflow Start Initial Action Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional `initialActionKey` to `POST /v1/WorkflowStart` so a new workflow can be created and immediately submitted through metadata in one API call.

**Architecture:** Keep the controller thin and put orchestration in services. The API service passes actor roles/display name into the application start request; the infrastructure start service creates the workflow instance and executes the initial action in one SQL transaction by reusing `WorkflowExecutionService`.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core SQL Server, xUnit, existing workflow application/infrastructure services.

---

### Task 1: Extend Start Contracts and API Unit Tests

**Files:**
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`

- [ ] **Step 1: Add failing tests for initial action request mapping**

Add a test to `WorkflowStartServiceTests` that constructs `WorkflowStartRequest` with `InitialActionKey = " SubmitForChecker "`. Assert that the recorded `StartWorkflowRequest` receives:

```csharp
Assert.Equal("SubmitForChecker", applicationStart.LastRequest.InitialActionKey);
Assert.Equal("Maker One", applicationStart.LastRequest.StartedByDisplayName);
Assert.Equal(["Maker"], applicationStart.LastRequest.StartedByRoles);
```

Keep the existing start-only test asserting current behavior.

- [ ] **Step 2: Run the focused API service test and confirm failure**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj --filter WorkflowStartServiceTests -m:1 /p:UseAppHost=false
```

Expected: compilation fails because `WorkflowStartRequest.InitialActionKey`, `Application.WorkflowStart.StartWorkflowRequest.InitialActionKey`, `StartedByDisplayName`, and `StartedByRoles` do not exist.

- [ ] **Step 3: Extend request DTOs**

Update API request DTO:

```csharp
namespace WorkflowEngine.WorkflowApi.Models;

public sealed class WorkflowStartRequest
{
    public string? WorkflowKey { get; set; }

    public string? BusinessObjectType { get; set; }

    public string? BusinessObjectId { get; set; }

    public string? InitialActionKey { get; set; }
}
```

Update application start record:

```csharp
public sealed record StartWorkflowRequest(
    string WorkflowKey,
    string BusinessObjectType,
    string BusinessObjectId,
    string StartedByUserId,
    bool Reset,
    string? InitialActionKey = null,
    string? StartedByDisplayName = null,
    IReadOnlyList<string>? StartedByRoles = null);
```

- [ ] **Step 4: Update API start service request construction**

In `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`, trim `InitialActionKey` as optional text and pass display/roles:

```csharp
var initialActionKey = OptionalText(request.InitialActionKey);
var actor = actorResolver.Resolve(httpContext);

var startResult = startWorkflowService.Start(new ApplicationStartWorkflowRequest(
    workflowKey,
    businessObjectType,
    businessObjectId,
    actor.UserId,
    Reset: false,
    InitialActionKey: initialActionKey,
    StartedByDisplayName: actor.DisplayName,
    StartedByRoles: actor.Roles));
```

Add:

```csharp
private static string? OptionalText(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

- [ ] **Step 5: Run focused API service tests**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj --filter WorkflowStartServiceTests -m:1 /p:UseAppHost=false
```

Expected: `WorkflowStartServiceTests` pass.

### Task 2: Add Atomic Infrastructure Start-Plus-Action Behavior

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Add or modify: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`

- [ ] **Step 1: Add failing SQL-backed tests**

Create `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs` with two tests:

```csharp
[Fact]
public void StartWithInitialSubmitMovesToCheckerReviewWithoutMockSectionData()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    var service = CreateService(dbContext);

    var result = service.Start(new StartWorkflowRequest(
        "PortfolioTrade",
        "PortfolioTrade",
        "PT-START-SUBMIT",
        "maker1",
        Reset: false,
        InitialActionKey: "SubmitForChecker",
        StartedByDisplayName: "Maker One",
        StartedByRoles: ["Maker"]));

    Assert.True(result.Envelope.Result.Success);
    Assert.Equal("SubmitForChecker", result.Envelope.Result.ActionKey);
    Assert.Equal("Draft", result.Envelope.Result.OldState);
    Assert.Equal("CheckerReview", result.Envelope.Result.NewState);
    var instance = Assert.Single(dbContext.WorkflowInstances);
    Assert.Equal("CheckerReview", instance.CurrentStateKey);
    Assert.Equal(2, instance.Version);
    var task = Assert.Single(dbContext.WorkflowTasks);
    Assert.Equal("CheckerReviewTask", task.TaskKey);
    Assert.Equal("Pending", task.Status);
    var notification = Assert.Single(dbContext.NotificationOutbox);
    Assert.Equal("WorkflowTaskAssigned", notification.NotificationType);
    Assert.Equal("Checker", notification.RecipientRoleKey);
    Assert.Single(dbContext.WorkflowAudits);
    Assert.Empty(dbContext.MockSectionData);
}

[Fact]
public void StartWithInvalidInitialActionRollsBackCreatedInstance()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    var service = CreateService(dbContext);

    var result = service.Start(new StartWorkflowRequest(
        "PortfolioTrade",
        "PortfolioTrade",
        "PT-BAD-ACTION",
        "maker1",
        Reset: false,
        InitialActionKey: "CheckerReturnToMaker",
        StartedByDisplayName: "Maker One",
        StartedByRoles: ["Maker"]));

    Assert.False(result.Envelope.Result.Success);
    Assert.Equal("InvalidTransition", result.Envelope.Result.ResultCode);
    Assert.Empty(dbContext.WorkflowInstances);
    Assert.Empty(dbContext.WorkflowTasks);
    Assert.Empty(dbContext.NotificationOutbox);
    Assert.Empty(dbContext.WorkflowAudits);
    Assert.Empty(dbContext.IdempotencyRecords);
}
```

Use helper:

```csharp
private static StartWorkflowService CreateService(WorkflowDbContext dbContext)
{
    var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2030-01-15T15:30:00Z"));
    var resultBuilder = new WorkflowActionResultBuilder();
    var contextReader = new WorkflowContextReader(dbContext, timeProvider);
    var executionService = new WorkflowExecutionService(
        dbContext,
        contextReader,
        timeProvider,
        resultBuilder,
        new CanonicalWorkflowResultSerializer());
    return new StartWorkflowService(
        dbContext,
        timeProvider,
        resultBuilder,
        new WorkflowActionEnvelopeNormalizer(),
        new CanonicalRequestHasher(),
        executionService);
}
```

- [ ] **Step 2: Run focused infrastructure tests and confirm failure**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowStartInitialActionTests -m:1 /p:UseAppHost=false
```

Expected: compilation fails because `StartWorkflowService` does not have the new constructor and initial action execution is not implemented.

- [ ] **Step 3: Make `WorkflowExecutionService` join an ambient EF transaction**

In `WorkflowExecutionService.ExecuteSideEffects`, replace unconditional transaction ownership with nullable ownership:

```csharp
using var transaction = dbContext.Database.CurrentTransaction is null
    ? dbContext.Database.BeginTransaction()
    : null;
```

Replace direct rollback/commit calls with null-safe calls:

```csharp
transaction?.Rollback();
transaction?.Commit();
```

This preserves existing standalone action behavior while allowing `StartWorkflowService` to own the outer transaction.

- [ ] **Step 4: Inject execution helpers into `StartWorkflowService`**

Update constructor:

```csharp
public sealed class StartWorkflowService(
    WorkflowDbContext dbContext,
    TimeProvider timeProvider,
    WorkflowActionResultBuilder resultBuilder,
    WorkflowActionEnvelopeNormalizer normalizer,
    CanonicalRequestHasher hasher,
    WorkflowExecutionService workflowExecutionService) : IStartWorkflowService
```

Add using directives for normalization and execution contracts.

- [ ] **Step 5: Implement initial action orchestration**

In `StartWorkflowService.Start`, route non-blank `InitialActionKey` through a new helper. The helper:

1. Begins a DB transaction.
2. Calls `InsertFreshInstance`.
3. Builds an internal `WorkflowActionEnvelope`.
4. Executes `WorkflowExecutionService.Execute` with API-style flags.
5. Rolls back if the action result is unsuccessful.
6. Commits and returns the action result if successful.

The execution request must set:

```csharp
new WorkflowExecutionRequest(normalized, hasher.ComputeHash(normalized.Envelope), AdminOverride: false)
{
    DeriveIdentityFromPersistence = true,
    PersistSectionData = false,
    ValidateActionEcho = false
}
```

- [ ] **Step 6: Run focused infrastructure tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowStartInitialActionTests -m:1 /p:UseAppHost=false
```

Expected: both tests pass.

### Task 3: Deliver Initial-Action Notifications From the API Service

**Files:**
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowStartControllerTests.cs`

- [ ] **Step 1: Add failing API service tests for dispatch behavior**

Add tests mirroring `WorkflowActionServiceTests`:

- Successful start with `InitialActionKey` and queued notification dispatches by workflow instance ID and correlation ID.
- Start-only response does not dispatch.
- Dispatch failure converts result to `NotificationDeliveryFailed`.

Use a local `RecordingWorkflowActionNotificationDispatcher` test double.

- [ ] **Step 2: Run focused tests and confirm failure**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj --filter WorkflowStartServiceTests -m:1 /p:UseAppHost=false
```

Expected: compilation fails because `WorkflowStartService` does not inject or use `IWorkflowActionNotificationDispatcher`.

- [ ] **Step 3: Inject dispatcher and add delivery logic**

Update API `WorkflowStartService` constructor:

```csharp
public sealed class WorkflowStartService(
    IWorkflowActorResolver actorResolver,
    ApplicationStartWorkflowService startWorkflowService,
    IWorkflowContextReader workflowContextReader,
    IWorkflowActionNotificationDispatcher notificationDispatcher) : IWorkflowStartService
```

After start result and context read logic, call a helper only when `request.InitialActionKey` is non-blank and `actionResult.Notification?.Queued == true`. Reuse the same delivery failure mutation as `WorkflowActionService`:

```csharp
actionResult.Success = false;
actionResult.ResultCode = "NotificationDeliveryFailed";
actionResult.Message = message;
```

- [ ] **Step 4: Update tests and DI registration assumptions**

`Program.cs` already registers `IWorkflowActionNotificationDispatcher`; no new registration is needed. Update test factory methods to pass a default recording dispatcher.

- [ ] **Step 5: Run focused API tests**

Run:

```powershell
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj --filter "WorkflowStartServiceTests|WorkflowStartControllerTests" -m:1 /p:UseAppHost=false
```

Expected: start service and controller tests pass.

### Task 4: Final Verification and Documentation

**Files:**
- Modify if needed: `TODO_WORKFLOW_API.md`
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run API solution build**

Run:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Expected: build passes with 0 warnings and 0 errors.

- [ ] **Step 2: Run API tests**

Run:

```powershell
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Expected: all API tests pass.

- [ ] **Step 3: Run shared workflow tests**

Run:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
```

Expected: all default tests pass; existing SMTP category tests may remain skipped.

- [ ] **Step 4: Update handoff**

Record completed behavior, changed files, verification commands, and any remaining risks in `CODEX_HANDOFF.md`. If `TODO_WORKFLOW_API.md` tracks this new work, mark it complete or add a concise completed note.

- [ ] **Step 5: Commit implementation**

Run:

```powershell
git status --short --untracked-files=all
git add -- src workflow-api tests docs TODO_WORKFLOW_API.md CODEX_HANDOFF.md
git commit -m "feat: start workflow with initial action"
```

Expected: commit includes only relevant files; unrelated `~/.codex/superpowers/` remains untracked and unstaged.
