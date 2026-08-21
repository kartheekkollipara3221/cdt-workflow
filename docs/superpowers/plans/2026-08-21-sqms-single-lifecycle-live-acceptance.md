# SQMS Single-Lifecycle Live Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute and retain a safe live acceptance run containing one complete SQMS Opportunity and three Bids ending Accepted, Partially Accepted, and Not Accepted.

**Architecture:** Extend the existing compiled acceptance tool with an environment-selected scenario definition while preserving the comprehensive profile as the default. Derive fixture, scheduled-operation, state, and email counts from the selected matrix; add an isolated launcher on ports 3026/5001 and an independent SQL verifier for the single-lifecycle evidence.

**Tech Stack:** .NET 10, C#, xUnit, EF Core, SQL Server/sqlcmd, PowerShell, MailKit SMTP path, smtp4dev.

---

### Task 1: Define the backward-compatible single-lifecycle profile

**Files:**
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs`
- Test: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write failing profile and matrix tests**

Add tests that construct profiles without changing process environment and assert both datasets:

```csharp
[Fact]
public void SingleLifecycleProfilePinsOneOpportunityThreeBidsAndSeparateSmtp4Dev()
{
    var profile = AcceptanceProfile.FromValues(
        "workflowengine_sqms_single_lifecycle_live_acceptance",
        "SQMS-SINGLE-LIVE-20260821",
        "2030-01-01T15:00:00Z",
        "single-lifecycle",
        "http://localhost:5001");

    Assert.Equal(AcceptanceScenarioKind.SingleLifecycle, profile.ScenarioKind);
    Assert.Equal(new Uri("http://localhost:5001"), profile.Smtp4DevBaseAddress);

    var matrix = AcceptanceScenarioMatrix.Create(profile.ScenarioKind);
    Assert.Single(matrix.Opportunities);
    Assert.Equal(3, matrix.AllBids.Count);
    Assert.Collection(
        matrix.AllBids,
        bid => Assert.Equal(SqmsKeys.BidStates.Accepted, bid.TerminalState),
        bid => Assert.Equal(SqmsKeys.BidStates.PartiallyAccepted, bid.TerminalState),
        bid => Assert.Equal(SqmsKeys.BidStates.NotAccepted, bid.TerminalState));
    Assert.All(matrix.AllBids, bid => Assert.True(bid.RequestedAmount > 0));
}

[Fact]
public void DefaultProfileStillSelectsTheComprehensiveRetainedDataset()
{
    var profile = AcceptanceProfile.FromValues(null, null, null, null, null);
    Assert.Equal(AcceptanceScenarioKind.Comprehensive, profile.ScenarioKind);
    Assert.Equal(new Uri("http://localhost:5000"), profile.Smtp4DevBaseAddress);
    Assert.Equal(15, AcceptanceScenarioMatrix.Create(profile.ScenarioKind).AllBids.Count);
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests.SingleLifecycleProfilePinsOneOpportunityThreeBidsAndSeparateSmtp4Dev|FullyQualifiedName~SqmsLiveAcceptanceContractTests.DefaultProfileStillSelectsTheComprehensiveRetainedDataset"
```

Expected: compilation failure because the scenario kind, two profile arguments, and matrix overload do not exist.

- [ ] **Step 3: Implement scenario selection and the compact matrix**

Add the explicit kind and profile values:

```csharp
public enum AcceptanceScenarioKind
{
    Comprehensive,
    SingleLifecycle
}

public sealed record AcceptanceProfile(
    string DatabaseName,
    string RunMarker,
    DateTimeOffset OpeningUtc,
    AcceptanceScenarioKind ScenarioKind,
    Uri Smtp4DevBaseAddress)
{
    public static AcceptanceProfile FromEnvironment() => FromValues(
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_DATABASE"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_MARKER"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_OPENING_UTC"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_SCENARIO"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL"));
}
```

Keep the two new `FromValues` parameters optional so all existing three-argument test and integration call sites remain source compatible. `FromValues` must map blank scenario to `Comprehensive`, accept only `single-lifecycle` for the new kind, default the smtp4dev address to `http://localhost:5000`, and reject other scenario values or non-HTTP(S) absolute URLs with `ArgumentException`.

Add `AcceptanceConstants.ScenarioKind` and `AcceptanceConstants.Smtp4DevBaseAddress` properties. Replace `AcceptanceScenarioMatrix.Create()` with an optional kind parameter defaulting to `AcceptanceConstants.ScenarioKind`. Preserve the existing comprehensive matrix byte-for-byte and add:

```csharp
private static AcceptanceScenarioMatrix SingleLifecycle() => new(
[
    new(
        "A",
        "AWAITING ALLOCATION -> COMPLETED",
        SqmsKeys.OpportunityStates.Completed,
        10,
        [
            new("01", 60m, "FAIR VALUE", SqmsKeys.BidStates.Accepted, true),
            new("02", 50m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
            new("03", 40m, "DISCOUNT", SqmsKeys.BidStates.NotAccepted)
        ])
]);
```

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command. Expected: 2 passed, 0 failed.

- [ ] **Step 5: Commit the profile boundary**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "test(sqms): define single lifecycle profile"
```

### Task 2: Make the compiled runner and verifier scenario-driven

**Files:**
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceFixtureWriter.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs`
- Test: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write failing derived-expectation tests**

Add tests for a scenario definition that owns counts and route/state expectations:

```csharp
[Fact]
public void SingleLifecycleDefinitionDerivesExactRuntimeAndEmailExpectations()
{
    var definition = AcceptanceScenarioDefinition.Create(AcceptanceScenarioKind.SingleLifecycle);

    Assert.Equal(1, definition.ExpectedOpportunityCount);
    Assert.Equal(3, definition.ExpectedBidCount);
    Assert.Equal(4, definition.ExpectedWorkflowInstanceCount);
    Assert.Equal(6, definition.ExpectedEmailCount);
    Assert.Equal(1, definition.ExpectedOpportunityStates[SqmsKeys.OpportunityStates.Completed]);
    Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.Accepted]);
    Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.PartiallyAccepted]);
    Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.NotAccepted]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityOpen]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityReminder]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityPendingSellerApproval]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidAccepted]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidPartiallyAccepted]);
    Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidNotAccepted]);
    Assert.Equal(3, definition.ExpectedBidsMovedToIpgReview);
    Assert.Equal(0, definition.ExpectedBidsNotAllocated);
}
```

Retain an equivalent assertion that the comprehensive definition remains 3 opportunities, 15 bids, 18 instances, and 19 emails with its existing route/state counts.

- [ ] **Step 2: Run the new definition tests and verify RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests.SingleLifecycleDefinitionDerivesExactRuntimeAndEmailExpectations"
```

Expected: compilation failure because `AcceptanceScenarioDefinition` does not exist.

- [ ] **Step 3: Implement the scenario definition**

Add a definition created from the selected matrix:

```csharp
public sealed record AcceptanceScenarioDefinition(
    AcceptanceScenarioMatrix Matrix,
    IReadOnlyDictionary<string, int> ExpectedOpportunityStates,
    IReadOnlyDictionary<string, int> ExpectedBidStates,
    IReadOnlyDictionary<string, int> ExpectedEmailRoutes)
{
    public int ExpectedOpportunityCount => Matrix.Opportunities.Count;
    public int ExpectedBidCount => Matrix.AllBids.Count;
    public int ExpectedWorkflowInstanceCount => ExpectedOpportunityCount + ExpectedBidCount;
    public int ExpectedEmailCount => ExpectedEmailRoutes.Values.Sum();
    public int ExpectedBidsNotAllocated =>
        Matrix.AllBids.Count(bid => bid.TerminalState == SqmsKeys.BidStates.NotAllocated);
    public int ExpectedBidsMovedToIpgReview => ExpectedBidCount - ExpectedBidsNotAllocated;

    public static AcceptanceScenarioDefinition Create(AcceptanceScenarioKind kind)
    {
        var matrix = AcceptanceScenarioMatrix.Create(kind);
        return new(
            matrix,
            matrix.Opportunities.GroupBy(row => row.TerminalState)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            matrix.AllBids.GroupBy(row => row.TerminalState)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            BuildEmailRoutes(matrix));
    }
}
```

`BuildEmailRoutes` returns one open and one reminder occurrence per run, one Seller-approval message per Opportunity, and one disposition message for every Bid except `NotAllocated`, grouped by terminal route.

Expose the selected definition through `AcceptanceConstants.Definition`; make the existing expected-count properties derive from it so the default values remain unchanged. Preserve `AcceptanceEmailPlan.Create()` as a compatibility wrapper over the selected definition's route dictionary because existing tests and tooling call that public helper.

- [ ] **Step 4: Replace hardcoded execution and verification counts**

In `AcceptanceFixtureWriter`, compare inserted counts to `AcceptanceConstants.Definition`.

In `AcceptanceScenarioRunner`:

- Use the selected definition's matrix for both pre- and post-schedule operations.
- Expect `ExpectedOpportunityCount`, `ExpectedBidsMovedToIpgReview`, and `ExpectedBidsNotAllocated` at close.
- Keep open/reminder/replay idempotency checks, deriving replay counts as one and two respectively.
- Replace the literal 19 in outbox validation and error text with `ExpectedEmailCount`.
- Replace unconditional A/B/C completion calls with a loop that dispatches by `AllocationPath` and only touches Opportunities present in the selected matrix.

Use this completion dispatcher:

```csharp
private Task CompleteOpportunityAsync(
    OpportunityScenario opportunity,
    int id,
    CancellationToken cancellationToken) => opportunity.AllocationPath switch
{
    "AWAITING ALLOCATION -> COMPLETED" => CompleteAwaitingAllocationAsync(id, cancellationToken),
    "PARTIALLY ALLOCATED -> COMPLETED" => CompletePartiallyAllocatedAsync(id, cancellationToken),
    "NOT FULFILLED" => CompleteNotFulfilledAsync(id, cancellationToken),
    _ => throw new InvalidOperationException(
        $"Unknown Opportunity allocation path '{opportunity.AllocationPath}'.")
};
```

Rename the current A/B/C completion methods by path without altering their action/version sequences.

In `AcceptanceEvidenceVerifier`, derive fixture, instance, route, and terminal-state counts from the selected definition. Use `AcceptanceConstants.Smtp4DevBaseAddress` instead of hardcoded port 5000. Run corrected-template content checks for the corrected-email retained marker and for `SingleLifecycle`.

- [ ] **Step 5: Run contract tests and verify GREEN**

Run:

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests"
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
```

Expected: all live-acceptance contract tests pass; build succeeds with zero warnings and errors.

- [ ] **Step 6: Commit scenario-driven execution**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "test(sqms): drive acceptance by scenario"
```

### Task 3: Add the one-shot launcher and independent SQL verifier

**Files:**
- Create: `scripts/run-sqms-single-lifecycle-live-acceptance.ps1`
- Create: `sql/sqms/testing/007_verify_sqms_single_lifecycle_live_acceptance.sql`
- Test: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write failing launcher and SQL package tests**

Add one test that reads both files and pins the safety contract:

```csharp
[Fact]
public void SingleLifecycleRunnerPinsSeparateEvidenceAndNeverDropsIt()
{
    var root = SqmsDatabasePackageTests.RepositoryRoot();
    var scriptPath = Path.Combine(root, "scripts", "run-sqms-single-lifecycle-live-acceptance.ps1");
    var verifierPath = Path.Combine(root, "sql", "sqms", "testing",
        "007_verify_sqms_single_lifecycle_live_acceptance.sql");

    Assert.True(File.Exists(scriptPath));
    Assert.True(File.Exists(verifierPath));
    var script = File.ReadAllText(scriptPath);
    var verifier = File.ReadAllText(verifierPath);

    Assert.Contains("workflowengine_sqms_single_lifecycle_live_acceptance", script, StringComparison.Ordinal);
    Assert.Contains("SQMS-SINGLE-LIVE-20260821", script, StringComparison.Ordinal);
    Assert.Contains("sqms-single-lifecycle-live-acceptance-20260821", script, StringComparison.Ordinal);
    Assert.Contains("SQMS_LIVE_ACCEPTANCE_SCENARIO = 'single-lifecycle'", script, StringComparison.Ordinal);
    Assert.Contains("SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL = 'http://localhost:5001'", script, StringComparison.Ordinal);
    Assert.Contains("Smtp__Port = '3026'", script, StringComparison.Ordinal);
    Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("DECLARE @RunMarker varchar(100) = 'SQMS-SINGLE-LIVE-20260821'", verifier, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the launcher test and verify RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests.SingleLifecycleRunnerPinsSeparateEvidenceAndNeverDropsIt"
```

Expected: failure because the launcher and verifier files do not exist.

- [ ] **Step 3: Create the launcher**

Copy the established safety structure from `run-sqms-email-template-live-acceptance.ps1`, changing only the fixed evidence and port profile:

```powershell
$acceptanceDatabase = 'workflowengine_sqms_single_lifecycle_live_acceptance'
$acceptanceConnection = 'Server=DOLLY;Database=workflowengine_sqms_single_lifecycle_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$smtpDataDirectory = 'C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821'
$env:SQMS_LIVE_ACCEPTANCE_MARKER = 'SQMS-SINGLE-LIVE-20260821'
$env:SQMS_LIVE_ACCEPTANCE_SCENARIO = 'single-lifecycle'
$env:SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL = 'http://localhost:5001'
$env:Smtp__Port = '3026'
```

Check only ports 3026 and 5001. Start smtp4dev hidden with `--smtpport=3026` and `--urls=http://localhost:5001`. Apply SQL files `000`, `001`, `002`, and `003`, run generic seed verification, execute the compiled tool once, then run `007`. Refuse an existing database/data directory and never drop or reset retained evidence.

- [ ] **Step 4: Create the independent SQL verifier**

Create `007` with read-only result sets followed by exact failures. Pin:

```sql
DECLARE @RunMarker varchar(100) = 'SQMS-SINGLE-LIVE-20260821';
DECLARE @MarkerPattern varchar(101) = @RunMarker + '%';
```

Verify exactly:

- 1 Opportunity, `COMPLETED`, workflow version 10.
- 3 Bids: one `ACCEPTED`, one `PARTIALLY ACCEPTED`, one `NOT ACCEPTED`; Bid 01 version 7 and Bids 02/03 version 5.
- 4 workflow instances.
- 23 workflow audits.
- 0 tasks and 0 SLA schedules.
- 6 `Sent` outbox rows with zero retries and no other outbox state.
- One row for each of the six notification routes.
- Target/workflow state parity and unchanged calculated-column sentinels.
- Corrected subjects, bodies, dates, recipients, static open link, `opportunityId`, and each `bidId` link.

Print `SQMS single-lifecycle live acceptance database verification passed.` only after all assertions succeed.

- [ ] **Step 5: Run parser/package tests and verify GREEN**

Run:

```powershell
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests"
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path scripts/run-sqms-single-lifecycle-live-acceptance.ps1),
    [ref]$null,
    [ref]$errors) | Out-Null
if ($errors.Count -ne 0) { throw ($errors | Out-String) }
```

Expected: all tests pass and the PowerShell parser reports zero errors.

- [ ] **Step 6: Commit the one-shot package**

```powershell
git add scripts/run-sqms-single-lifecycle-live-acceptance.ps1 sql/sqms/testing/007_verify_sqms_single_lifecycle_live_acceptance.sql tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "test(sqms): add single lifecycle live runner"
```

### Task 4: Execute once and retain user-verifiable evidence

**Files:**
- Create: `docs/sqms/SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE.md`
- Create: `docs/sqms/SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE-RESULTS-20260821.md`
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run preflight without mutation**

Confirm the exact database and data directory are absent and ports 3026/5001 are free. Confirm the earlier retained databases and mailboxes still exist. Do not execute the runner if any new target already exists.

- [ ] **Step 2: Build and run focused regression before live execution**

Run:

```powershell
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SqmsLiveAcceptanceContractTests|FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests"
```

Expected: build has zero warnings/errors and all focused tests pass.

- [ ] **Step 3: Execute the safety-pinned runner exactly once**

Run:

```powershell
& .\scripts\run-sqms-single-lifecycle-live-acceptance.ps1
```

Expected terminal markers:

```text
SQMS LIVE ACCEPTANCE PASS
opportunities=1
bids=3
emails=6
SQMS single-lifecycle live acceptance database verification passed.
```

Do not rerun the one-shot command after it creates the database or data directory.

- [ ] **Step 4: Run verification-only checks**

Set the exact environment from the launcher, then run compiled `--verify-only`, `sql/sqms/003_verify_sqms_deployment.sql`, and `sql/sqms/testing/007_verify_sqms_single_lifecycle_live_acceptance.sql`. Query and record exact IDs, versions, states, route counts, retries, audits/tasks/SLA counts, smtp4dev message count, and the smtp4dev process command line.

- [ ] **Step 5: Write the inspection runbook and results**

The runbook must provide copyable read-only SQL for:

```sql
SELECT o.ID, o.Account, o.Status, o.WorkflowState,
       w.CurrentStateKey, w.Version,
       o.TotalBid, o.LargestBid, o.LowestBid
FROM SSQMS.Opportunity AS o
JOIN dbo.WorkflowInstance AS w
  ON w.BusinessObjectId = CONCAT('SQMS-OPP-', o.ID)
WHERE o.Account LIKE 'SQMS-SINGLE-LIVE-20260821%';

SELECT b.ID, b.OpportunityId, b.Status, b.WorkflowState,
       w.CurrentStateKey, w.Version
FROM SSQMS.Bid AS b
JOIN dbo.WorkflowInstance AS w
  ON w.BusinessObjectId = CONCAT('SQMS-BID-', b.ID)
WHERE b.AccountNumber LIKE 'SQMS-SINGLE-LIVE-20260821%'
ORDER BY b.ID;

SELECT Id, NotificationType, NotificationRecipientsJson,
       Subject, Body, HtmlBody, Status, RetryCount, LastError
FROM dbo.NotificationOutbox
ORDER BY Id;
```

Record the retained database, smtp4dev URL `http://localhost:5001`, data directory, process IDs, generated Opportunity/Bid IDs, exact state/email counts, verifier output, and any verifier-only correction without hiding it.

- [ ] **Step 6: Update handoff and commit evidence documentation**

Update `CODEX_HANDOFF.md` with the current goal, completed changes, changed files, build/test status, retained evidence, remaining tasks, risks, and next-session prompt. Commit:

```powershell
git add docs/sqms/SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE.md docs/sqms/SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE-RESULTS-20260821.md CODEX_HANDOFF.md
git commit -m "test(sqms): record single lifecycle evidence"
```

### Task 5: Final compatibility and evidence gate

**Files:**
- Modify only if evidence documentation needs factual corrections.

- [ ] **Step 1: Run backward-compatibility tests**

Run sequentially:

```powershell
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror
dotnet test src/WorkflowEngine.sln --no-build
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build
```

Expected: zero warnings/errors, no failures, and only the established opt-in SMTP skips.

- [ ] **Step 2: Reverify retained single-lifecycle evidence**

Run compiled verification-only and both SQL verifiers again. Confirm smtp4dev still exposes exactly six messages on port 5001 and the new database remains available for the user.

- [ ] **Step 3: Confirm isolation and clean branch state**

Confirm both earlier retained databases still exist, the new retained database exists, no disposable database remains, `git diff --check` passes, and `git status --porcelain` is empty.

- [ ] **Step 4: Report the required completion summary**

Report:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

Include the exact smtp4dev URL, database, marker, Opportunity/Bid IDs, six email routes, and links to the runbook/results. State explicitly that no push, merge, deployment, production SQL, scheduler change, or evidence cleanup occurred.
