# SQMS Request Projection Ownership Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop request-driven SQMS Start/Action calls from updating Opportunity/Bid rows, retain atomic target projection for scheduled transitions, and prove the corrected ownership with a complete isolated Opportunity/Bid smtp4dev acceptance run.

**Architecture:** Keep `ISqmsWorkflowService` unchanged for target API callers and make its successful operations return authoritative workflow state/context without target mutation. Add a dedicated scheduled-transition interface implemented by the same scoped service; only that interface enables the existing four-column projection inside the workflow transaction. Adapt the live acceptance driver to prove request calls leave target rows unchanged before a separate target-owned persistence step, while scheduled close/classification project directly.

**Tech Stack:** .NET 10, C# 14, EF Core/SQL Server, xUnit, MailKit, smtp4dev, PowerShell, sqlcmd.

---

## File map

**Production ownership boundary**

- Create `src/WorkflowEngine.Sqms/Contracts/ISqmsScheduledTransitionService.cs` — scheduled-only execution surface.
- Modify `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs` — choose projection by call path, not caller data.
- Modify `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs` — use scheduled transition execution for close/classification.
- Modify `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs` — map both interfaces to one scoped implementation.
- Retain `src/WorkflowEngine.Sqms/Persistence/ISqmsDataStore.cs` and `SqlSqmsDataStore.cs` projection methods for scheduled use only.

**Automated proof**

- Modify `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs` — request-driven unchanged-row and replay proof.
- Modify `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs` — scheduled interface routing/projection behavior.
- Modify `tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs` — separate request result from simulated target-owned persistence.
- Modify `tests/WorkflowEngine.Tests/SqmsDataStoreTests.cs` — rename projection test to scheduled ownership and preserve four-column limit.
- Modify `tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs` — DI/interface assertions.
- Modify `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs` — new isolated profile/script/verifier contracts.

**Live acceptance**

- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs` — add projection-ownership scenario with one Opportunity/four Bids.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs` — parse `projection-ownership` profile.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs` — snapshot, assert unchanged, then explicitly persist returned state for request calls.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs` — verify request/scheduled ownership evidence and six SMTP messages.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceReporter.cs` — print ownership evidence.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceFixtureWriter.cs` — insert the fourth Bid fixture and persist testing-only ownership evidence rows.
- Modify `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs` — define the fourth Bid and typed ownership evidence records.
- Create `scripts/run-sqms-projection-ownership-live-acceptance.ps1` — one-shot isolated orchestration.
- Create `sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql` — independent SQL proof.
- Create `docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE.md` — operator inspection guide.
- Create `docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE-RESULTS-20260822.md` after the run.

**Active documentation and continuity**

- Modify `docs/sqms/TARGET-API-INTEGRATION.md` — request versus scheduled ownership.
- Modify `.planning/HANDOFF.json`, `.planning/.continue-here.md`, and `CODEX_HANDOFF.md` — corrected current state and retained evidence.
- Add correction notices to active documentation that currently presents request projection as current behavior; do not rewrite historical executed results.

---

### Task 1: Establish isolated execution baseline

**Files:**
- Read: `PROJECT.md`
- Read: `docs/superpowers/specs/2026-08-22-sqms-request-projection-ownership-correction-design.md`
- Read: `src/WorkflowEngine.Console/appsettings.Development.json`

- [ ] **Step 1: Create an isolated worktree from the approved design commit**

Use the `using-git-worktrees` skill. Create branch `sqms_projection_ownership` from `327919f` under the repository's ignored worktree directory. Do not reset, delete, or rewrite `console-code-first-0518`.

- [ ] **Step 2: Verify branch and clean state**

Run:

```powershell
git branch --show-current
git status --porcelain=v1
git rev-parse HEAD
```

Expected: branch `sqms_projection_ownership`, empty porcelain output, HEAD descended from `327919f`.

- [ ] **Step 3: Run focused baseline tests**

Load `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` from the development connection and run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~SqmsEndToEndTests|FullyQualifiedName~SqmsDataStoreTests" --verbosity minimal
```

Expected: current focused tests pass before behavior changes. If they fail, stop and report the baseline failure.

---

### Task 2: Prove request-driven target rows must remain unchanged

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs`
- Test: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`

- [ ] **Step 1: Add a complete target-row snapshot helper**

Add a fixture helper that reads every target column in deterministic order and serializes database nulls explicitly. Use SQL metadata/explicit columns already defined by the test table script; do not use only `Status`/`WorkflowState`.

```csharp
public Task<string> TargetRowSnapshotAsync(string tableName, int id) => tableName switch
{
    "Opportunity" => DbContext.Database.SqlQueryRaw<string>(
        "SELECT (SELECT * FROM [SSQMS].[Opportunity] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
        id).SingleAsync(),
    "Bid" => DbContext.Database.SqlQueryRaw<string>(
        "SELECT (SELECT * FROM [SSQMS].[Bid] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
        id).SingleAsync(),
    _ => throw new ArgumentOutOfRangeException(nameof(tableName))
};
```

Use the fixture's parameterized EF/SQL helpers; do not concatenate IDs or accept arbitrary table names beyond the closed switch.

- [ ] **Step 2: Rewrite Start assertions to require no target mutation**

Capture each row before Start, call the existing method, assert returned `SUBMITTED`/version 2 and workflow runtime/audit/idempotency, then compare the full target snapshot:

```csharp
var opportunityBefore = await fixture.TargetRowSnapshotAsync("Opportunity", 10000);
var bidBefore = await fixture.TargetRowSnapshotAsync("Bid", 10000);

var opportunity = await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None);
var bid = await fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None);

Assert.Equal("SUBMITTED", opportunity.Context?.WorkflowContext.Workflow.CurrentState);
Assert.Equal("SUBMITTED", bid.Context?.WorkflowContext.Workflow.CurrentState);
Assert.Equal(opportunityBefore, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
Assert.Equal(bidBefore, await fixture.TargetRowSnapshotAsync("Bid", 10000));
```

- [ ] **Step 3: Rewrite successful manual Action assertions**

After the test fixture explicitly simulates target-owned persistence for the initial Start result, capture the row, execute Return to Draft, assert returned state/version, and require the full row to remain unchanged. Repeat for Opportunity and Bid.

- [ ] **Step 4: Add idempotent replay unchanged-row proof**

Execute a successful manual action, snapshot the target row, replay the identical request, and assert:

```csharp
Assert.True(replay.Success);
Assert.Equal(first.WorkflowResult?.Result.NewState, replay.WorkflowResult?.Result.NewState);
Assert.Equal(beforeReplay, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
```

Also assert no duplicate workflow audit, idempotency, or outbox side effect.

- [ ] **Step 5: Run RED tests**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsWorkflowServiceTests" --verbosity normal
```

Expected: FAIL because current `CompleteWorkflowOperationAsync` changes `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.

- [ ] **Step 6: Record the expected RED evidence in the eventual summary and commit tests**

```powershell
git add tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs
git commit -m "test(sqms): require request operations to preserve target rows"
```

---

### Task 3: Remove request projection and add atomic scheduled projection

**Files:**
- Create: `src/WorkflowEngine.Sqms/Contracts/ISqmsScheduledTransitionService.cs`
- Modify: `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs`
- Modify: `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs`
- Modify: `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs`

- [ ] **Step 1: Add RED scheduled routing tests**

Replace the scheduler fake's single execution surface with separate request/scheduled fakes. Assert close and classification call only the scheduled interface and that notification-only work calls neither transition method.

```csharp
Assert.Equal(1, scheduledTransitions.OpportunityExecutions);
Assert.Equal(expectedBidCount, scheduledTransitions.BidExecutions);
Assert.Equal(0, requestWorkflow.ManualExecutions);
```

Add DI assertions that both interfaces resolve to the same scoped concrete instance.

- [ ] **Step 2: Run scheduled RED tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~SqmsProjectStructureTests" --verbosity normal
```

Expected: compile/test failure because `ISqmsScheduledTransitionService` does not exist and the scheduler still uses `ISqmsWorkflowService` for transitions.

- [ ] **Step 3: Add the dedicated scheduled interface**

Create:

```csharp
namespace WorkflowEngine.Sqms.Contracts;

public interface ISqmsScheduledTransitionService
{
    Task<SqmsOperationResult> ExecuteScheduledOpportunityActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken);

    Task<SqmsOperationResult> ExecuteScheduledBidActionAsync(
        SqmsExecuteRequest request,
        CancellationToken cancellationToken);
}
```

Do not add a projection flag to `SqmsExecuteRequest` or any target API-facing contract.

- [ ] **Step 4: Make projection an internal call-path decision**

Have `SqmsWorkflowService` implement both interfaces. Change the private execution/completion path to accept a private projection enum or boolean that cannot come from caller JSON:

```csharp
private enum TargetProjectionMode
{
    None,
    Scheduled
}
```

All existing Start and Action methods pass `None`. Scheduled interface methods validate the same request and call the same execution path with `Scheduled`.

In `CompleteWorkflowOperationAsync`, retain authoritative state validation, but execute `ProjectOpportunityStateAsync`/`ProjectBidStateAsync` only when mode is `Scheduled`. Always read and return refreshed workflow context.

- [ ] **Step 5: Route scheduler transitions through the scheduled interface**

Inject both:

```csharp
ISqmsWorkflowService workflowService,
ISqmsScheduledTransitionService scheduledTransitions
```

Use `workflowService` for read-only Context and `scheduledTransitions` for close/classification Execute calls.

- [ ] **Step 6: Preserve one scoped implementation in DI**

Register:

```csharp
services.AddScoped<SqmsWorkflowService>();
services.AddScoped<ISqmsWorkflowService>(provider =>
    provider.GetRequiredService<SqmsWorkflowService>());
services.AddScoped<ISqmsScheduledTransitionService>(provider =>
    provider.GetRequiredService<SqmsWorkflowService>());
```

- [ ] **Step 7: Run focused GREEN tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~SqmsProjectStructureTests" --verbosity normal
```

Expected: PASS with manual unchanged-row and scheduled-interface routing proof.

- [ ] **Step 8: Run negative source searches**

```powershell
rg -n "ProjectOpportunityStateAsync|ProjectBidStateAsync" src/WorkflowEngine.Sqms/SqmsWorkflowService.cs
rg -n "TargetProjectionMode\.Scheduled" src/WorkflowEngine.Sqms
```

Expected: projection calls exist only behind the scheduled mode branch; manual public methods never pass scheduled mode.

- [ ] **Step 9: Commit production boundary**

```powershell
git add src/WorkflowEngine.Sqms tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs
git commit -m "fix(sqms): restrict target projection to scheduled transitions"
```

---

### Task 4: Restore end-to-end coverage with explicit target-owned persistence

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsDataStoreTests.cs`

- [ ] **Step 1: Add a test-only target API persistence helper**

Add a clearly named helper such as:

```csharp
public Task PersistReturnedStateAsTargetOwnerAsync(
    string entityType,
    int id,
    SqmsOperationResult result,
    string modifiedBy)
```

It extracts state from returned Context/WorkflowResult, then performs the target update through explicit test SQL. It must not call the request-driven adapter's projection methods.

- [ ] **Step 2: Wrap every manual lifecycle operation**

For Start and manual Actions:

1. snapshot target row;
2. invoke adapter;
3. assert full target row unchanged;
4. assert returned state/version;
5. call `PersistReturnedStateAsTargetOwnerAsync`;
6. assert target/workflow parity.

Scheduled calls continue asserting direct target projection without the helper.

- [ ] **Step 3: Rename datastore projection test**

Rename `SqlStoreReadsApprovedRowsFiltersDeletedRecordsAndProjectsOnlyWorkflowState` to make scheduled ownership explicit, for example `SqlStoreSupportsScheduledFourColumnProjectionOnly`, and retain assertions that business/calculated fields are unchanged.

- [ ] **Step 4: Run end-to-end tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsEndToEndTests|FullyQualifiedName~SqmsDataStoreTests" --verbosity normal
```

Expected: PASS for complete Opportunity/Bid branches, scheduled classification, outbox, and unchanged sentinels.

- [ ] **Step 5: Commit end-to-end proof**

```powershell
git add tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs tests/WorkflowEngine.Tests/SqmsDataStoreTests.cs
git commit -m "test(sqms): prove target-owned manual state persistence"
```

---

### Task 5: Add the projection-ownership live acceptance profile

**Files:**
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceReporter.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceFixtureWriter.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write RED profile and contract tests**

Require `AcceptanceProfile.FromValues(..., "projection-ownership", ...)` to select a new scenario kind containing exactly one Opportunity and four Bids with terminal states Accepted, Partially Accepted, Not Accepted, and Not Allocated. Require expected email routes to total six.

Require the scenario runner source to contain explicit ownership-proof operations and forbid direct use of request adapter projection methods.

- [ ] **Step 2: Run RED contract tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests" --verbosity normal
```

Expected: FAIL because the profile and ownership evidence do not exist.

- [ ] **Step 3: Add the scenario kind and matrix**

Add `AcceptanceScenarioKind.ProjectionOwnership` and parse exact text `projection-ownership`. Its matrix contains one Opportunity with:

```csharp
[
    new("01", 40m, "FAIR VALUE", SqmsKeys.BidStates.Accepted),
    new("02", 30m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
    new("03", 0m, "FAIR VALUE", SqmsKeys.BidStates.NotAccepted),
    new("04", 20m, "DISCOUNT", SqmsKeys.BidStates.NotAllocated)
]
```

Ensure qualifying Fair Value + Premium total is strictly greater than the Opportunity sale amount so the Discount Bid is scheduled to Not Allocated.

- [ ] **Step 4: Add request ownership proof records**

Before each manual Start/Action, snapshot the target row. After the call, assert exact equality and report an evidence record containing entity, ID, operation, returned state/version, before hash/JSON, and unchanged verdict. Then call a separately named target-owner writer and report that persistence as a second evidence record.

Scheduled close/classification must not call that writer. Verify their target rows changed to the returned scheduled states.

- [ ] **Step 5: Extend the verifier and reporter**

Verify:

- every manual operation has an unchanged-row proof;
- every manual returned state has a later target-owner persistence proof;
- scheduled close and Not Allocated/IPG Review classifications have direct projection proof;
- final workflow and target states match;
- six route/message counts match;
- all outbox rows are `Sent` with retry zero;
- zero tasks and zero SLA schedules;
- calculated sentinels remain unchanged.

- [ ] **Step 6: Run GREEN contract and focused tool tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests|FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests" --verbosity normal
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror --no-restore
```

Expected: all selected tests pass and build has zero warnings/errors.

- [ ] **Step 7: Commit acceptance-tool changes**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "test(sqms): add projection ownership acceptance profile"
```

---

### Task 6: Add isolated launcher, SQL verifier, and documentation corrections

**Files:**
- Create: `scripts/run-sqms-projection-ownership-live-acceptance.ps1`
- Create: `sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql`
- Create: `docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE.md`
- Modify: `docs/sqms/TARGET-API-INTEGRATION.md`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write RED artifact-contract tests**

Require the launcher to use unique constants:

```text
Database: workflowengine_sqms_projection_ownership_live_acceptance
Marker: SQMS-PROJECTION-OWNERSHIP-LIVE-20260822
SMTP: localhost:3027
UI: http://localhost:5002
Data: .tools/smtp4dev/data/sqms-projection-ownership-live-acceptance-20260822
Scenario: projection-ownership
```

Require one-shot refusal when database/data/ports already exist. Require IMAP/POP disabled and hidden smtp4dev launch. Require the independent verifier and operator guide.

- [ ] **Step 2: Run RED artifact tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests" --verbosity normal
```

Expected: FAIL because launcher/verifier/guide do not exist.

- [ ] **Step 3: Implement the one-shot launcher**

Copy the safety structure of `run-sqms-single-lifecycle-live-acceptance.ps1`, changing only the isolated constants/profile and verifier path. It must build, initialize the fresh database, seed metadata, start smtp4dev hidden, run the compiled profile once, run independent SQL verification, and retain evidence. It must never stop or reuse an unknown process.

- [ ] **Step 4: Implement independent SQL proof**

Verify exact counts and final states for one Opportunity/four Bids/six outbox rows. Verify workflow/audit/idempotency state, zero task/SLA rows, sent/retry-zero outbox, sentinels, and marker identity. Include proof rows that distinguish manual target-owner updates from scheduled projection; if evidence requires a new testing-only evidence table, create it only in the acceptance database setup path, never production DDL.

- [ ] **Step 5: Correct active target integration documentation**

Change `TARGET-API-INTEGRATION.md` to state:

```text
Request Start/Action returns authoritative state/context and does not update SSQMS target rows.
The target API persists returned state under its application transaction policy.
Scheduled close/classification is the only thin-layer path that projects target state.
```

Remove statements that request transactions commit adapter-owned state projection. Add the target API migration action explicitly.

- [ ] **Step 6: Run GREEN artifact tests and source checks**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests" --verbosity normal
rg -n "request.*project|adapter writes|state projection" docs/sqms/TARGET-API-INTEGRATION.md docs/sqms/DBA-DEPLOYMENT.md
```

Expected: tests pass; active docs contain only the corrected ownership language.

- [ ] **Step 7: Commit launcher/verifier/docs**

```powershell
git add scripts/run-sqms-projection-ownership-live-acceptance.ps1 sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql docs/sqms tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "docs(sqms): add projection ownership live acceptance"
```

---

### Task 7: Run regression gates and fresh live smtp4dev acceptance

**Files:**
- Create after successful run: `docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE-RESULTS-20260822.md`

- [ ] **Step 1: Run warning-as-error builds**

```powershell
dotnet build src/WorkflowEngine.sln -m:1 -warnaserror --no-restore
dotnet build workflow-api/WorkflowEngine.Api.sln -m:1 -warnaserror --no-restore
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -m:1 -warnaserror --no-restore
```

Expected: zero warnings and errors.

- [ ] **Step 2: Run complete regressions serially**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --verbosity minimal
dotnet test workflow-api/WorkflowEngine.Api.sln -m:1 --no-restore --verbosity minimal
```

Expected: all non-opt-in tests pass; only documented opt-in SMTP tests may skip. Do not launch concurrent SQL-backed test runs.

- [ ] **Step 3: Run seed/package verification**

Run the relevant `PROJECT.md` section 21 commands against a disposable verification database: generic seed verification, production SQMS deployment verifier, and SQMS package rollback verification. Do not run `init-db` against a retained or shared database.

- [ ] **Step 4: Execute the one-shot live acceptance exactly once**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/run-sqms-projection-ownership-live-acceptance.ps1
```

Expected: orchestration PASS, database retained, smtp4dev retained at UI port 5002, six messages, and independent SQL verifier PASS.

- [ ] **Step 5: Run verification-only mode without replay**

Set the exact retained profile environment variables and run:

```powershell
dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build -- --verify-only
sqlcmd -S DOLLY -d workflowengine_sqms_projection_ownership_live_acceptance -E -C -b -i sql/sqms/testing/008_verify_sqms_projection_ownership_live_acceptance.sql
```

Expected: both verifiers pass without inserting fixtures, executing transitions, or sending mail.

- [ ] **Step 6: Write the retained results report**

Record exact IDs/counts/states, manual unchanged-row evidence count, target-owner persistence count, scheduled projection count, audit/idempotency/outbox counts, six smtp4dev subjects/routes, ports, PID resolved at report time, database, marker, mailbox path, tests/builds, and the no-replay warning.

- [ ] **Step 7: Commit verified results**

```powershell
git add docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE-RESULTS-20260822.md
git commit -m "test(sqms): record projection ownership live acceptance"
```

---

### Task 8: Update continuity and perform final verification

**Files:**
- Modify: `.planning/HANDOFF.json`
- Modify: `.planning/.continue-here.md`
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Replace the stale projection ownership handoff**

Record that request-driven projection was removed, scheduled projection remains, the target API must persist returned state, and the full isolated acceptance evidence is retained. Include exact current SHAs and service/database paths verified at that moment.

- [ ] **Step 2: Run final clean verification**

```powershell
git diff --check
git status --short
git log -8 --oneline
```

Re-run the focused ownership tests after documentation changes:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~SqmsEndToEndTests|FullyQualifiedName~SqmsLiveAcceptanceContractTests" --verbosity minimal
```

Expected: no whitespace errors; focused suite passes.

- [ ] **Step 3: Commit handoff**

```powershell
git add .planning/HANDOFF.json .planning/.continue-here.md CODEX_HANDOFF.md
git commit -m "docs(sqms): hand off corrected projection ownership"
```

- [ ] **Step 4: Provide required completion summary**

Use exactly:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

Also state that no push, merge, deployment, production SQL, scheduler registration, service shutdown, or retained-evidence cleanup occurred unless separately authorized.
