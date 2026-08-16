# SQMS End-to-End Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate SQMS Opportunity and Bid rows with the generic workflow engine, taskless notification scheduling, automatic close/classification, seller decisions, and durable email delivery.

**Architecture:** Add an application-specific `WorkflowEngine.SqmsIntegration` project that calls the existing workflow application services in-process and performs SQMS business coordination without changing public Workflow API contracts. Use caller-owned SQL transactions, metadata-only notification routes and `SqmsSaleCycle`, and the existing outbox/SMTP processor.

**Tech Stack:** .NET 10, C#, EF Core 10, SQL Server, xUnit, existing Workflow Engine application/infrastructure services, `TimeProvider`.

**Execution:** Inline execution is authorized by the user's instruction to proceed. Production recipient and SharePoint values use `example.test` placeholders until deployment values are supplied.

---

## File map

### Generic compatibility

- Modify `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`: join caller-owned transactions.
- Modify `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`: derive completion from destination metadata.
- Modify `tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs`: transaction-participation regression.
- Modify `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`: metadata-terminal regression.

### Application schema and metadata

- Modify `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`: additive SQMS business fields and fixtures.
- Modify `sql/sqms/testing/005_verify_sqms_console_mock.sql`: exact-column verification.
- Create `sql/sqms/004_seed_sqms_end_to_end_metadata.sql`: sale-cycle and notification metadata.
- Create `sql/sqms/005_verify_sqms_end_to_end_metadata.sql`: independent exact verification.
- Create `sql/sqms/006_validate_sqms_end_to_end_in_rollback.sql`: seed-twice rollback validation.

### SQMS thin layer

- Create `src/WorkflowEngine.SqmsIntegration/WorkflowEngine.SqmsIntegration.csproj`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsWorkflowIdentity.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsContracts.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsApplicationRepository.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsWorkflowCoordinator.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsNotificationInputFactory.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsBidClassificationService.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/SqmsScheduleRunner.cs`.
- Create `src/WorkflowEngine.SqmsIntegration/DependencyInjection/SqmsIntegrationServiceCollectionExtensions.cs`.
- Modify `src/WorkflowEngine.sln` and `tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj`.

### Tests, samples, and handoff

- Create `tests/WorkflowEngine.Tests/SqmsWorkflowIdentityTests.cs`.
- Create `tests/WorkflowEngine.Tests/SqmsBidClassificationServiceTests.cs`.
- Create `tests/WorkflowEngine.Tests/SqmsWorkflowCoordinatorTests.cs`.
- Create `tests/WorkflowEngine.Tests/SqmsNotificationMetadataTests.cs`.
- Create `tests/WorkflowEngine.Tests/SqmsScheduleRunnerTests.cs`.
- Create `tests/WorkflowEngine.Tests/SqmsSellerDispositionTests.cs`.
- Modify `tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs`.
- Create `samples/sqms/03-request-seller-approval.json`.
- Create `samples/sqms/04-accept-bid.json`.
- Create `samples/sqms/05-partially-accept-bid.json`.
- Create `samples/sqms/06-reject-bid.json`.
- Modify `CODEX_HANDOFF.md` and `.planning/HANDOFF.json`.

---

### Task 1: Generic transaction and terminal compatibility

**Files:**

- Modify: `tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- [ ] **Step 1: Write the caller-owned transaction failing test**

Add a SQL-backed test that begins a `WorkflowDbContext` transaction, calls `StartWorkflowService.Start`, verifies the created instance is visible inside the transaction, rolls back, clears tracking, and proves the instance is absent.

```csharp
[Fact]
public void StartJoinsCallerOwnedTransaction()
{
    using var dbContext = CreateDbContext();
    SeedMinimalWorkflow(dbContext);
    using var transaction = dbContext.Database.BeginTransaction();

    var result = CreateService(dbContext).Start(CreateRequest("TX-START-1"));

    Assert.True(result.Envelope.Result.Success);
    Assert.NotNull(dbContext.Database.CurrentTransaction);
    transaction.Rollback();
    dbContext.ChangeTracker.Clear();
    Assert.DoesNotContain(dbContext.WorkflowInstances, row => row.BusinessObjectId == "TX-START-1");
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~StartJoinsCallerOwnedTransaction
```

Expected: failure because `StartWorkflowService` attempts to begin a second transaction.

- [ ] **Step 3: Implement caller-owned transaction participation**

Replace unconditional transaction creation on all Start paths with ownership-aware logic:

```csharp
using var transaction = dbContext.Database.CurrentTransaction is null
    ? dbContext.Database.BeginTransaction()
    : null;
```

Use `transaction?.Commit()` and `transaction?.Rollback()`. When a caller owns the transaction, return failures without committing or rolling it back; the coordinator owns the outer rollback.

- [ ] **Step 4: Run the focused Start tests and verify GREEN**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~StartWorkflowServiceTests
```

Expected: all `StartWorkflowServiceTests` pass.

- [ ] **Step 5: Write the metadata-terminal failing test**

Add a test workflow whose terminal destination is `Rejected`, with `IsTerminal = true`, and assert execution produces runtime `Status = Completed`, a non-null `CompletedAtUtc`, and no available actions.

```csharp
Assert.Equal("Rejected", result.Result.NewState);
Assert.Equal("Completed", result.WorkflowContext!.Workflow.Status);
Assert.NotNull(result.WorkflowContext.Workflow.CompletedAtUtc);
Assert.Empty(result.WorkflowContext.AvailableActions);
```

- [ ] **Step 6: Run the terminal test and verify RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~TerminalStateMetadataCompletesWorkflow
```

Expected: workflow reaches `Rejected` but runtime status remains `Active`.

- [ ] **Step 7: Implement metadata-terminal completion**

Resolve the destination state in the existing execution query and use its `IsTerminal` flag:

```csharp
var completed = dbContext.WorkflowStateDefinitions
    .AsNoTracking()
    .Where(row => row.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
        && row.StateKey == transition.ToStateKey
        && row.IsActive)
    .Select(row => row.IsTerminal)
    .Single();
```

Keep every response and persistence contract unchanged.

- [ ] **Step 8: Run focused and Portfolio Trade regression tests**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~StartWorkflowServiceTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~Sqms"
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit**

```powershell
git add -- src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs
git diff --cached --check
git commit -m "fix: support SQMS transaction and terminal metadata"
```

### Task 2: Add SQMS business fields and exact verification

**Files:**

- Modify: `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`
- Modify: `sql/sqms/testing/005_verify_sqms_console_mock.sql`
- Modify: `tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs`

- [ ] **Step 1: Extend the verifier first**

Require the seven Opportunity and three Bid business columns with exact SQL types, plus `CK_SSQMS_Bid_BidPricingCategory` and JSON checks.

- [ ] **Step 2: Run the focused mock integration test and verify RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsConsoleMockIntegrationTests
```

Expected: SQL verifier reports a missing SQMS business column.

- [ ] **Step 3: Add the nullable business fields idempotently**

Use `COL_LENGTH` guards and additive `ALTER TABLE` statements. Seed deterministic values:

```sql
OpenBiddingNoticeAt = '2026-05-01T08:00:00-04:00'
OpenBiddingReminderAt = '2026-05-14T08:00:00-04:00'
BidResponseDueDate = '2026-05-15'
BiddingCloseAt = '2026-05-16T00:00:00-04:00'
SellerResponseDueDate = '2026-05-24'
OpportunityRelationshipManagerEmail = 'seller.rm@example.test'
SellerRmTeamEmailsJson = N'["seller.team1@example.test","seller.team2@example.test"]'
BidPricingCategory = 'FairValue'
BidRelationshipManagerEmail = 'buyer.rm@example.test'
BuyerRmTeamEmailsJson = N'["buyer.team1@example.test","buyer.team2@example.test"]'
```

- [ ] **Step 4: Add filtered due-query indexes and constraints**

Create indexes only when absent and use filtered predicates where SQL Server permits them. Do not rename or reinterpret existing columns.

- [ ] **Step 5: Run the focused test twice**

Expected: both executions pass, proving schema setup is rerunnable.

- [ ] **Step 6: Commit**

```powershell
git add -- sql/sqms/testing/004_create_sqms_mock_application_tables.sql sql/sqms/testing/005_verify_sqms_console_mock.sql tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs
git diff --cached --check
git commit -m "feat: add SQMS workflow business fields"
```

### Task 3: Build the thin Start, Context, and Action coordinator

**Files:**

- Create: `src/WorkflowEngine.SqmsIntegration/WorkflowEngine.SqmsIntegration.csproj`
- Create: `src/WorkflowEngine.SqmsIntegration/SqmsWorkflowIdentity.cs`
- Create: `src/WorkflowEngine.SqmsIntegration/SqmsContracts.cs`
- Create: `src/WorkflowEngine.SqmsIntegration/SqmsApplicationRepository.cs`
- Create: `src/WorkflowEngine.SqmsIntegration/SqmsWorkflowCoordinator.cs`
- Create: `src/WorkflowEngine.SqmsIntegration/DependencyInjection/SqmsIntegrationServiceCollectionExtensions.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsWorkflowIdentityTests.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsWorkflowCoordinatorTests.cs`
- Modify: `src/WorkflowEngine.sln`
- Modify: `tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj`

- [ ] **Step 1: Write identity tests and verify RED**

```csharp
[Theory]
[InlineData(10000, "SQMS-OPP-10000", "WF-SQMS-OPP-10000")]
public void OpportunityIdentityIsNamespaced(int id, string objectId, string instanceId)
{
    Assert.Equal(objectId, SqmsWorkflowIdentity.OpportunityBusinessObject(id));
    Assert.Equal(instanceId, SqmsWorkflowIdentity.OpportunityWorkflowInstance(id));
}
```

Expected: compile failure because the project/type does not exist.

- [ ] **Step 2: Create the project and identity implementation**

The project targets `net10.0`, enables nullable/implicit usings, treats warnings as errors, and references Application and Infrastructure only.

- [ ] **Step 3: Run identity tests and verify GREEN**

- [ ] **Step 4: Write SQL-backed coordinator tests**

Cover:

- start Opportunity and project `Draft`/`DRAFT`;
- start Bid and project `Draft`/`DRAFT`;
- execute `SubmitOpportunity` and project `Submitted`/`SUBMITTED`;
- return Context with metadata `availableActions`;
- rollback both SQMS and workflow changes when projection fails;
- repeated Start returns the existing workflow without duplication.

- [ ] **Step 5: Run coordinator tests and verify RED**

Expected: compile failure because the coordinator/repository contracts do not exist.

- [ ] **Step 6: Implement minimal contracts**

```csharp
public sealed record SqmsActor(string UserId, string DisplayName, IReadOnlyList<string> Roles);
public sealed record SqmsActionRequest(string ActionKey, string? Reason = null);
public sealed record SqmsWorkflowResult(int EntityId, string WorkflowState, string Status, WorkflowContext WorkflowContext);
```

- [ ] **Step 7: Implement repository and coordinator**

Use parameterized SQL against `WorkflowDbContext.Database.GetDbConnection()`. Enlist the workflow context in the caller transaction, execute core services with `PersistSectionData = false`, and update the SQMS projection with an expected-state predicate.

- [ ] **Step 8: Run coordinator tests and verify GREEN**

- [ ] **Step 9: Commit**

```powershell
git add -- src/WorkflowEngine.SqmsIntegration src/WorkflowEngine.sln tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj tests/WorkflowEngine.Tests/SqmsWorkflowIdentityTests.cs tests/WorkflowEngine.Tests/SqmsWorkflowCoordinatorTests.cs
git diff --cached --check
git commit -m "feat: add SQMS workflow thin layer"
```

### Task 4: Seed taskless sale-cycle and email metadata

**Files:**

- Create: `sql/sqms/004_seed_sqms_end_to_end_metadata.sql`
- Create: `sql/sqms/005_verify_sqms_end_to_end_metadata.sql`
- Create: `sql/sqms/006_validate_sqms_end_to_end_in_rollback.sql`
- Create: `tests/WorkflowEngine.Tests/SqmsNotificationMetadataTests.cs`
- Create: `src/WorkflowEngine.SqmsIntegration/SqmsNotificationInputFactory.cs`

- [ ] **Step 1: Write independent metadata verification first**

Require exact sale-cycle states/actions/System transitions, six route keys, exact route bindings, subject/body/HTML templates, and ordered recipient rules.

- [ ] **Step 2: Run verifier before seed and verify RED**

Expected: SQL error identifying the missing active `SqmsSaleCycle` definition.

- [ ] **Step 3: Write rerunnable seed**

Use `.example.test` static addresses:

```text
sqms-open-bidding-to1@example.test
sqms-open-bidding-to2@example.test
sqms-open-bidding-to3@example.test
sqms-static-cc1@example.test
sqms-static-cc2@example.test
```

Bind seller and Bid routes to declared `RequestInput` keys. Keep all `TaskToCloseKey`, `TaskToCreateKey`, and SLA references null.

- [ ] **Step 4: Implement exact template-data builder tests**

Assert Q3/2026, `05/15/2026`, `07/01/2026`, currency formatting, fund/vehicle/account tokens, and links under `https://example.test/sqms/`.

- [ ] **Step 5: Implement the minimal input factory**

Use invariant data extraction and `en-US` display formatting. Pass ordered address arrays without syntax lookup or rewriting.

- [ ] **Step 6: Run seed twice inside rollback and verify GREEN**

- [ ] **Step 7: Commit**

```powershell
git add -- sql/sqms/004_seed_sqms_end_to_end_metadata.sql sql/sqms/005_verify_sqms_end_to_end_metadata.sql sql/sqms/006_validate_sqms_end_to_end_in_rollback.sql src/WorkflowEngine.SqmsIntegration/SqmsNotificationInputFactory.cs tests/WorkflowEngine.Tests/SqmsNotificationMetadataTests.cs
git diff --cached --check
git commit -m "feat: add SQMS notification metadata"
```

### Task 5: Implement scheduled notices and reminders

**Files:**

- Create: `src/WorkflowEngine.SqmsIntegration/SqmsScheduleRunner.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsScheduleRunnerTests.cs`

- [ ] **Step 1: Write fake-TimeProvider schedule tests**

Cover before/at/after boundaries, grouping, repeated scans, concurrent scans, reminder blocked until initial outbox `Sent`, and late-opened Opportunity behavior.

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Implement bounded due-cycle discovery**

Read due Opportunities set-wise, group by exact cycle values, derive a deterministic cycle identity, and call Start/Action as `sqms.scheduler` with role `System`.

- [ ] **Step 4: Implement notice/reminder idempotency**

Use stable action idempotency keys based on cycle identity and action key. Check the initial cycle outbox row is `Sent` before reminder execution.

- [ ] **Step 5: Run tests and verify GREEN**

- [ ] **Step 6: Commit**

```powershell
git add -- src/WorkflowEngine.SqmsIntegration/SqmsScheduleRunner.cs tests/WorkflowEngine.Tests/SqmsScheduleRunnerTests.cs
git diff --cached --check
git commit -m "feat: schedule SQMS bidding emails"
```

### Task 6: Implement automatic close and Bid classification

**Files:**

- Create: `src/WorkflowEngine.SqmsIntegration/SqmsBidClassificationService.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsBidClassificationServiceTests.cs`
- Modify: `src/WorkflowEngine.SqmsIntegration/SqmsScheduleRunner.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsScheduleRunnerTests.cs`

- [ ] **Step 1: Write the complete pure classification matrix**

Cover below, equal, and strictly above sale amount; FairValue, Premium, and Discount; Draft exclusion; no submitted Bids; invalid amount; missing/invalid category.

```csharp
Assert.Equal(SqmsBidDisposition.NotAllocated, result[discountBidId]);
Assert.Equal(SqmsBidDisposition.IpgReview, result[premiumBidId]);
```

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Implement decimal classification**

Return an immutable Bid-ID/action decision map. Do not write SQL or workflow state from the pure service.

- [ ] **Step 4: Run classification tests and verify GREEN**

- [ ] **Step 5: Write SQL-backed close/retry tests**

Prove parent commit occurs first, child actions commit together, failed child classification leaves parent closed and every child submitted, rerun completes, and no-Bid Opportunity becomes `NotFulfilled`.

- [ ] **Step 6: Implement close and child transactions**

Use `CloseOpportunityBidding`, `MarkBidNotAllocated`, `MoveBidToIpgReview`, and `MarkOpportunityNotFulfilled`; never update destination states directly.

- [ ] **Step 7: Run schedule/classification tests and verify GREEN**

- [ ] **Step 8: Commit**

```powershell
git add -- src/WorkflowEngine.SqmsIntegration/SqmsBidClassificationService.cs src/WorkflowEngine.SqmsIntegration/SqmsScheduleRunner.cs tests/WorkflowEngine.Tests/SqmsBidClassificationServiceTests.cs tests/WorkflowEngine.Tests/SqmsScheduleRunnerTests.cs
git diff --cached --check
git commit -m "feat: close and classify SQMS bids"
```

### Task 7: Implement seller approval, disposition emails, and roll-up

**Files:**

- Modify: `src/WorkflowEngine.SqmsIntegration/SqmsWorkflowCoordinator.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsSellerDispositionTests.cs`
- Create: `samples/sqms/03-request-seller-approval.json`
- Create: `samples/sqms/04-accept-bid.json`
- Create: `samples/sqms/05-partially-accept-bid.json`
- Create: `samples/sqms/06-reject-bid.json`

- [ ] **Step 1: Write seller-approval atomicity tests**

Assert all `IpgReview` Bids move to `UnderReviewBySeller`, parent moves to `PendingSellerApproval`, one seller outbox row is queued, and any child failure rolls back the entire operation.

- [ ] **Step 2: Run tests and verify RED**

- [ ] **Step 3: Implement seller-approval orchestration**

Execute child actions first and parent notification action last inside one transaction.

- [ ] **Step 4: Write disposition and roll-up tests**

Cover exact accepted/partial/not-accepted recipients and bodies plus all-accepted, mixed/partial, and all-rejected Opportunity roll-ups.

- [ ] **Step 5: Run tests and verify RED**

- [ ] **Step 6: Implement disposition and System roll-up**

Queue the Bid email in the Bid action transaction. When no Bid remains `UnderReviewBySeller`, execute the applicable existing Opportunity System action.

- [ ] **Step 7: Run tests and samples and verify GREEN**

- [ ] **Step 8: Commit**

```powershell
git add -- src/WorkflowEngine.SqmsIntegration/SqmsWorkflowCoordinator.cs tests/WorkflowEngine.Tests/SqmsSellerDispositionTests.cs samples/sqms/03-request-seller-approval.json samples/sqms/04-accept-bid.json samples/sqms/05-partially-accept-bid.json samples/sqms/06-reject-bid.json
git diff --cached --check
git commit -m "feat: complete SQMS seller disposition flow"
```

### Task 8: Full verification, documentation, and handoff

**Files:**

- Modify: `CODEX_HANDOFF.md`
- Modify: `.planning/HANDOFF.json`

- [ ] **Step 1: Run formatting and warnings-as-errors builds**

```powershell
dotnet restore
dotnet build src/WorkflowEngine.sln -warnaserror --no-restore
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror --no-restore /p:UseAppHost=false
```

- [ ] **Step 2: Run serialized full test suites**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --no-build
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 --no-build /p:UseAppHost=false
```

- [ ] **Step 3: Run database verification**

```powershell
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

Execute SQMS transition verification and end-to-end seed-twice rollback verification against the configured SQL Server.

- [ ] **Step 4: Run SQMS console/sample flow**

Create mock rows, execute Opportunity and Bid Start/Context/Action paths, run scheduled processing with injected `--now`, process the outbox, and run independent SQL verification.

- [ ] **Step 5: Run smtp4dev acceptance when local SMTP is available**

Verify all six SQMS notification types, ordered To/Cc snapshots, subjects, plain bodies, HTML bodies, links, and zero SMTP warnings.

- [ ] **Step 6: Update handoff artifacts**

Record goal, commits, files, exact test counts, seed verification, sample execution, SMTP evidence, risks, and next-session prompt. Mark the old single-capability human gate superseded by the approved complete design.

- [ ] **Step 7: Run final repository checks**

```powershell
git diff --check
git status --short
git log --oneline --decorate -12
```

- [ ] **Step 8: Commit documentation**

```powershell
git add -- CODEX_HANDOFF.md .planning/HANDOFF.json
git diff --cached --check
git commit -m "docs: record SQMS end-to-end verification"
```

---

## Plan self-review

- Every approved design section maps to a numbered task.
- Generic changes are limited to transaction ownership and metadata terminal completion.
- Public HTTP and .NET integration contracts remain unchanged.
- Application tables contain only business inputs plus existing state projections.
- SQMS has no tasks or SLA.
- Production addresses and links use explicit `example.test` test values rather than unresolved implementation placeholders.
- Each production behavior begins with a failing test and an observed RED run.
- Portfolio Trade and public Workflow API regressions remain mandatory before completion.
