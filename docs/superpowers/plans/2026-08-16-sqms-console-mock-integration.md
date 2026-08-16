# SQMS Console Mock Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create exact-shape SQMS mock application tables and prove existing Console Start, Get Context, and Execute behavior for one Opportunity and one related Bid.

**Architecture:** Application-owned mock tables live under `workflowengine.SSQMS` and are created by explicit test SQL, not EF migrations. Existing generic console commands operate only on workflow metadata/runtime tables using namespaced business IDs derived from the mock row IDs. Automated SQL-backed coverage runs the same boundary in an isolated database before the scripts and commands are exercised against the local `workflowengine` database.

**Tech Stack:** .NET 10, xUnit, EF Core/SQL Server, `Microsoft.Data.SqlClient`, PowerShell, `sqlcmd`.

**Execution status:** Completed and verified on 2026-08-16.

---

## File structure

- Create `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`: idempotent exact-schema creation and deterministic fixture inserts.
- Create `sql/sqms/testing/005_verify_sqms_console_mock.sql`: independent schema, constraint, fixture, workflow-runtime, and application-row immutability verification.
- Create `samples/sqms/01-submit-opportunity.json`: internal Console envelope for `SubmitOpportunity`.
- Create `samples/sqms/02-submit-bid.json`: internal Console envelope for `SubmitBid`.
- Create `tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs`: isolated SQL-backed Start/Get/Execute regression.
- Modify `.planning/SQMS-CONTEXT.md`, `.planning/.continue-here.md`, `.planning/HANDOFF.json`, and `CODEX_HANDOFF.md`: record execution evidence and remaining boundary.

### Task 1: Add the failing SQL-backed Console test

**Files:**
- Create: `tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs`

- [x] **Step 1: Write the failing test**

Create a test that:

1. Creates an isolated database with `TestConsoleHost`.
2. Runs `init-db` for the generic schema and Portfolio Trade baseline.
3. Executes `sql/sqms/001_seed_sqms_transition_metadata.sql` in batches split on standalone `GO` lines.
4. Attempts to execute the not-yet-created mock-table setup script.
5. Starts Opportunity/Bid workflows, reads context, executes the two not-yet-created JSON samples, and asserts final runtime/application state.

Required assertions:

```csharp
Assert.Equal("Draft", opportunityDraft.WorkflowContext.Workflow.CurrentState);
Assert.Equal(["SubmitOpportunity"], opportunityDraft.WorkflowContext.AvailableActions.Select(x => x.ActionKey));
Assert.Equal("Submitted", opportunitySubmitted.WorkflowContext.Workflow.CurrentState);
Assert.Equal(["CancelOpportunity", "MarkOpportunityPending", "ReturnOpportunityToDraft"],
    opportunitySubmitted.WorkflowContext.AvailableActions.Select(x => x.ActionKey).OrderBy(x => x));

Assert.Equal("Draft", bidDraft.WorkflowContext.Workflow.CurrentState);
Assert.Equal(["SubmitBid"], bidDraft.WorkflowContext.AvailableActions.Select(x => x.ActionKey));
Assert.Equal("Submitted", bidSubmitted.WorkflowContext.Workflow.CurrentState);
Assert.Equal(["MarkBidNotAllocated", "MoveBidToIpgReview"],
    bidSubmitted.WorkflowContext.AvailableActions.Select(x => x.ActionKey).OrderBy(x => x));
```

Query SQL directly to assert both application rows remain `Status = 'DRAFT'`, `WorkflowState = 'Draft'`, `ModifiedBy IS NULL`, and `ModifiedDate IS NULL`. Assert each workflow has one audit and no tasks, SLA schedules, or outbox rows.

- [x] **Step 2: Run the test and verify RED**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter FullyQualifiedName~SqmsConsoleMockIntegrationTests
```

Expected: FAIL because `sql/sqms/testing/004_create_sqms_mock_application_tables.sql` or the SQMS sample files do not exist.

- [x] **Step 3: Commit the red test**

```powershell
git add tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs
git commit -m "test: define SQMS console mock integration"
```

### Task 2: Add exact mock schema and independent verification

**Files:**
- Create: `sql/sqms/testing/004_create_sqms_mock_application_tables.sql`
- Create: `sql/sqms/testing/005_verify_sqms_console_mock.sql`

- [x] **Step 1: Implement the transactional setup script**

Use:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'SSQMS') IS NULL
    EXEC(N'CREATE SCHEMA SSQMS AUTHORIZATION dbo;');

IF OBJECT_ID(N'SSQMS.Opportunity', N'U') IS NULL
BEGIN
    CREATE TABLE SSQMS.Opportunity
    (
        ID int IDENTITY(10000,1) NOT NULL,
        SellerRMTeam varchar(max) NOT NULL,
        Account varchar(max) NOT NULL,
        AccountName varchar(1000) NOT NULL,
        AccountNumber varchar(1000) NOT NULL,
        RelationshipManager varchar(1000) NULL,
        FundConsideredSecondarySale varchar(1000) NULL,
        Vehicle varchar(1000) NULL,
        ClientCommitmentToVehicle varchar(100) NULL,
        CommitmentAmountSale varchar(100) NULL,
        ClientRelationshipLeavingBBH varchar(100) NULL,
        RationaleForSecondarySale varchar(max) NULL,
        EffectiveDateForSale datetime NULL,
        Status varchar(200) NULL,
        FundManagementFee varchar(100) NULL,
        MinimumBid varchar(100) NULL,
        RecentDateOfValuation datetime NULL,
        ValuationAsRecentDate varchar(100) NULL,
        ContributionSinceValuation varchar(1000) NULL,
        DistributionSinceValuation varchar(1000) NULL,
        EstimatedFairValue varchar(1000) NULL,
        RemainingCapitalCommitment varchar(1000) NULL,
        IsValuationForPrivateFundVehicle bit NULL,
        TotalBid int NULL,
        LargestBid varchar(100) NULL,
        LowestBid varchar(100) NULL,
        WorkflowState nvarchar(100) NULL,
        CreatedBy nvarchar(50) NULL,
        CreatedDate datetime NULL,
        ModifiedBy nvarchar(50) NULL,
        ModifiedDate datetime NULL,
        IsDeleted bit NULL,
        CONSTRAINT PK_SSQMS_Opportunity PRIMARY KEY CLUSTERED (ID ASC) WITH (FILLFACTOR = 100)
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
END;
```

Create `SSQMS.Bid` with the exact approved columns/types and add checked constraint `FK_SSQMS_Opportunity_Bid`. Insert ID `10000` fixtures under `IDENTITY_INSERT` only when absent; never update existing rows. Commit the transaction and print the two IDs.

- [x] **Step 2: Implement the independent verifier**

The verifier must throw distinct 512xx errors when:

- either table/column/type/nullability/identity property is missing or wrong;
- PK or FK is absent or disabled;
- fixture relationship is wrong;
- either application row differs from its original status/workflow/modification values;
- either workflow instance is not `Submitted` version 2 after execution;
- audit counts differ from one or any task/SLA/outbox row exists.

It must finish with:

```sql
PRINT 'SQMS console mock verification passed.';
```

- [x] **Step 3: Run setup twice and schema-only verification**

Run:

```powershell
sqlcmd -S DOLLY -d workflowengine -E -b -i sql\sqms\testing\004_create_sqms_mock_application_tables.sql
sqlcmd -S DOLLY -d workflowengine -E -b -i sql\sqms\testing\004_create_sqms_mock_application_tables.sql
```

Expected: both runs succeed; each fixture ID remains unique. Do not run the final runtime verifier until console execution completes.

- [x] **Step 4: Commit the SQL scripts**

```powershell
git add sql/sqms/testing/004_create_sqms_mock_application_tables.sql sql/sqms/testing/005_verify_sqms_console_mock.sql
git commit -m "test: add SQMS mock application tables"
```

### Task 3: Add the SQMS Console action fixtures

**Files:**
- Create: `samples/sqms/01-submit-opportunity.json`
- Create: `samples/sqms/02-submit-bid.json`

- [x] **Step 1: Create the Opportunity envelope**

Use `objectType`/`workflowKey` `SqmsOpportunity`, object ID `SQMS-OPP-10000`, workflow instance `WF-SQMS-OPP-10000`, MFO actor, expected `Draft` version 1, and action `SubmitOpportunity`. Include opaque mock `uiAction`/`dataSave` blocks required only by the existing internal Console envelope and expectations `Draft -> Submitted` with audit `SubmitOpportunity`.

- [x] **Step 2: Create the Bid envelope**

Use `objectType`/`workflowKey` `SqmsBid`, object ID `SQMS-BID-10000`, workflow instance `WF-SQMS-BID-10000`, MFO actor, expected `Draft` version 1, and action `SubmitBid`. Include expectations `Draft -> Submitted` with audit `SubmitBid`.

- [x] **Step 3: Run the focused test and verify GREEN**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter FullyQualifiedName~SqmsConsoleMockIntegrationTests
```

Expected: PASS with the isolated database deleted during test cleanup.

- [x] **Step 4: Commit fixtures and green test state**

```powershell
git add samples/sqms tests/WorkflowEngine.Tests/SqmsConsoleMockIntegrationTests.cs
git commit -m "test: prove SQMS console start context and execute"
```

### Task 4: Execute the live local Console proof

**Files:**
- No source modifications.

- [x] **Step 1: Verify exact SQMS metadata**

```powershell
sqlcmd -S DOLLY -d workflowengine -E -b -i sql\sqms\002_verify_sqms_transition_metadata.sql
```

Expected: Opportunity 31 transitions, Bid 9 transitions.

- [x] **Step 2: Start and query Opportunity**

Run `start-workflow SqmsOpportunity SQMS-OPP-10000 --user sqms.mfo --reset --format json`, then `show-context WF-SQMS-OPP-10000 --user sqms.mfo --roles MFO --format json`. Capture and verify `Draft` plus `SubmitOpportunity`.

- [x] **Step 3: Execute and re-query Opportunity**

Run `execute-json samples/sqms/01-submit-opportunity.json`, then context as IPG. Capture and verify `Submitted` plus `CancelOpportunity`, `MarkOpportunityPending`, and `ReturnOpportunityToDraft`.

- [x] **Step 4: Start, execute, and query Bid**

Repeat with `SqmsBid`, `SQMS-BID-10000`, and `samples/sqms/02-submit-bid.json`. Verify `Submitted` plus `MarkBidNotAllocated` and `MoveBidToIpgReview`.

- [x] **Step 5: Run the independent verifier**

```powershell
sqlcmd -S DOLLY -d workflowengine -E -b -i sql\sqms\testing\005_verify_sqms_console_mock.sql
```

Expected: `SQMS console mock verification passed.`

### Task 5: Run regression gates and record evidence

**Files:**
- Modify: `.planning/SQMS-CONTEXT.md`
- Modify: `.planning/.continue-here.md`
- Modify: `.planning/HANDOFF.json`
- Modify: `CODEX_HANDOFF.md`

- [x] **Step 1: Run warning-as-error builds**

```powershell
dotnet build src\WorkflowEngine.sln --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx --no-restore
```

Expected: zero warnings and zero errors.

- [x] **Step 2: Run core and API regression suites**

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "Category!=Smtp" --blame-hang-timeout 2m
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx --no-restore --no-build
```

Expected: all non-SMTP tests pass.

- [x] **Step 3: Re-run Portfolio Trade and SQMS seed verification**

Run Console `verify-seed` against `workflowengine`, then run `002_verify_sqms_transition_metadata.sql` again.

- [x] **Step 4: Record exact evidence and remaining scope**

Update the four handoff/context files with commands, counts, final states, audit/task/SLA/outbox results, and the unchanged application-row proof. Keep UI, application-table synchronization, scheduling, notifications, tasks, SLA, aggregation, and generic terminal synchronization deferred.

- [x] **Step 5: Commit documentation and final checks**

```powershell
git add .planning/SQMS-CONTEXT.md .planning/.continue-here.md .planning/HANDOFF.json CODEX_HANDOFF.md
git diff --cached --check
git commit -m "docs: record SQMS console mock verification"
git status --short --branch
```
