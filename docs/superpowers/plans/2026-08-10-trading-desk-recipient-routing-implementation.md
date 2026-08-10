# Trading Desk Recipient Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the Checker-selected Trading Desk role to one or more ordered notification addresses from versioned recipient-rule metadata without changing the public Workflow API envelope.

**Architecture:** Add an optional `AssignedRoleKey` applicability filter to the existing `NotificationRecipientRule` entity. Workflow execution passes the already-validated dynamic task assignment key into the generic resolver, which selects common rules plus matching role-specific rules before applying the existing source resolution, ordering, deduplication, snapshot, SLA, and SMTP behavior.

**Tech Stack:** .NET 10, C#, EF Core, SQL Server, xUnit, ASP.NET Core Workflow API

---

### Task 1: Add role-filtered recipient resolution

**Files:**
- Modify: `tests/WorkflowEngine.Tests/NotificationRecipientResolverTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRecipientRule.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/NotificationRecipientResolver.cs`

- [x] **Step 1: Write failing resolver tests**

Add tests proving a supplied assignment role selects common rules plus only matching role-specific rules, preserves multiple matching addresses in sort order, and produces no To recipient for an unmapped role.

- [x] **Step 2: Run the resolver tests and verify RED**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~NotificationRecipientResolverTests"
```

Expected: compilation or assertion failure because recipient rules and resolver calls do not yet support `AssignedRoleKey`.

- [x] **Step 3: Implement the minimal generic filter**

Add nullable `AssignedRoleKey` with `nvarchar(100)` model configuration. Extend `Resolve` with a nullable assignment-role argument and load only active rules where the filter is null or equals that argument. Do not change source resolution, address handling, ordering, deduplication, or To/Cc precedence.

- [x] **Step 4: Run the resolver tests and verify GREEN**

Run the same focused command and require zero failures.

### Task 2: Seed and verify multiple addresses per Trading Desk

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`

- [x] **Step 1: Write failing seed expectations**

Extend recipient-rule expectations with `AssignedRoleKey` and expect two ordered To addresses for each of `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`, followed by common Maker and selected-Cc rules.

- [x] **Step 2: Run the seed test and verify RED**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~SeedDataSmokeTests.PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded"
```

Expected: failure because current seed has one common Trading Desk address.

- [x] **Step 3: Update seed persistence and verification**

Persist and compare the optional role filter. Replace only `RouteTradingDeskAssigned` recipient rows. Verify every active assignment role for `TradingDeskReviewTask` has an applicable active To rule; leave `RouteTradingDeskReworkAssigned` unchanged.

- [x] **Step 4: Run focused seed tests and verify GREEN**

Run the focused seed test plus `SeedVerificationTests` and require zero failures.

### Task 3: Pass the selected role through workflow execution and snapshot recipients

**Files:**
- Modify: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`

- [x] **Step 1: Strengthen the existing desk-assignment theory**

For each allowed desk role, assert the exact ordered desk-specific To list, Maker/selected-Cc behavior, and identical task/outbox recipient JSON.

- [x] **Step 2: Run the transition theory and verify RED**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~WorkflowTransitionTests.CheckerSendToTradingDeskAssignsSpecificDeskTaskAndNotification"
```

Expected: failure because execution currently calls the resolver without the selected role.

- [x] **Step 3: Pass the normalized assignment key to the resolver**

Use the existing validated `workflow.assignedRoleKey`; do not add a public field, change transition selection, or derive routing from template data.

- [x] **Step 4: Run transition and SLA snapshot tests and verify GREEN**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~Phase5SlaTests"
```

### Task 4: Add the EF Core migration and schema assertions

**Files:**
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260810173812_AddNotificationRecipientAssignedRoleFilter.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260810173812_AddNotificationRecipientAssignedRoleFilter.Designer.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`
- Modify: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`

- [x] **Step 1: Write failing schema assertions**

Assert the EF model exposes nullable `AssignedRoleKey` with maximum length 100 and the generated migration adds `[AssignedRoleKey] nvarchar(100) NULL` under `dbo.NotificationRecipientRule`.

- [x] **Step 2: Run schema tests and verify RED**

```powershell
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SchemaConstraintTests"
```

- [x] **Step 3: Scaffold and review the migration**

Generate the migration from the existing design-time context. Confirm it only adds/drops the nullable column and updates the model snapshot; do not recreate tables or delete data.

- [x] **Step 4: Run schema tests and verify GREEN**

Run the same focused command and require zero failures.

### Task 5: Align source-of-truth and integration documentation

**Files:**
- Modify: `PROJECT.md`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- Modify: `workflow-api/docs/WORKFLOW_API_ENVELOPES.md`
- Modify: `README.md`

- [x] **Step 1: Update recipient metadata documentation**

Document the optional rule filter, the desk-specific sample mappings, unchanged API shape, display-only `deskName`, one-message/multiple-address behavior, and unchanged common rework route.

- [x] **Step 2: Run documentation and API contract tests**

```powershell
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore --filter "FullyQualifiedName~WorkflowRequestContractTests|FullyQualifiedName~WorkflowScenarioSampleTests"
dotnet test src/WorkflowEngine.sln --no-restore --filter "FullyQualifiedName~ReadmeValidationTests"
```

### Task 6: Run quality gates and commit implementation

**Files:**
- Update: `CODEX_HANDOFF.md` without overwriting pre-existing unrelated edits

- [x] **Step 1: Run formatting and negative searches**

Run `dotnet format --verify-no-changes`, `git diff --check`, and the recipient negative searches required by `PROJECT.md` section 21.

- [x] **Step 2: Run build and automated tests**

```powershell
dotnet restore
dotnet build -warnaserror --no-restore
dotnet test src/WorkflowEngine.sln --no-build
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build
```

- [x] **Step 3: Run SQL seed gates**

```powershell
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

- [x] **Step 4: Execute the public Checker-to-Trading-Desk sample**

Run the live/local Workflow API scenario for each desk role or, if a live API is unavailable, execute the SQL-backed application/console equivalent and report the limitation exactly. Verify the selected role's task and outbox recipient snapshot.

- [x] **Step 5: Review the final diff and commit only task-owned files**

Preserve unrelated dirty files. Commit the implementation atomically after fresh verification.
