# SQMS Live Acceptance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and execute a non-production .NET 10 acceptance driver that retains three SQMS Opportunities, fifteen Bids, complete workflow/audit/outbox evidence, and nineteen smtp4dev messages in an isolated local database.

**Architecture:** A new console project under `tools/` composes the existing production workflow/SQMS services and drives a fixed, self-verifying scenario without adding a public API or production console command. A guarded PowerShell script creates the exact dedicated database, applies the existing SQL packages, starts an isolated smtp4dev instance, and runs the driver; an independent read-only SQL script proves target/workflow/email parity afterward.

**Tech Stack:** .NET 10, C# 14, SQL Server/sqlcmd, EF Core, existing workflow/SQMS services, MailKit-backed existing SMTP sender, smtp4dev 3.15.0, PowerShell.

---

## Governing boundaries

- Read `PROJECT.md`, `docs/superpowers/specs/2026-08-21-sqms-live-acceptance-design.md`, and `docs/sqms/TARGET-API-INTEGRATION.md` before implementation.
- Use only `Server=DOLLY;Database=workflowengine_sqms_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.
- Never run `--reset`, drop a database, clean retained records, or change `workflowengine`.
- Do not add an SQMS HTTP endpoint or production console command.
- Do not add packages. Reuse package versions already present in the repository.
- Do not write `TotalBid`, `LargestBid`, or `LowestBid` after fixture insertion.
- Actual acceptance execution is a compiled console run, not `dotnet test`.
- Keep smtp4dev and the acceptance database available after success or failure.

## File structure

| File | Responsibility |
|---|---|
| `tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj` | Isolated .NET 10 executable and existing project references. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/Program.cs` | Configuration, DI composition, top-level exit handling. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs` | Fixed database, marker, dates, actors, recipients, URLs, and expected counts. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceTimeProvider.cs` | Acceptance-only controllable clock. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs` | Scenario, generated-ID manifest, expected-operation, and verification records. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceFixtureWriter.cs` | Guarded target-row insertion and identity capture. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceReporter.cs` | Exact before/after service-call and summary output. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs` | Opportunity, Bid, scheduled, and outbox orchestration through production services. |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs` | Independent final database and smtp4dev API assertions. |
| `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs` | Safety, matrix, expected-count, configuration, and source-boundary tests for the driver. |
| `scripts/run-sqms-live-acceptance.ps1` | Exact safe database/package/smtp4dev/build/run orchestration. |
| `sql/sqms/testing/005_verify_sqms_live_acceptance.sql` | Read-only human verification queries. |
| `docs/sqms/SQMS-LIVE-ACCEPTANCE.md` | Operator commands, scenario/email checklist, and inspection instructions. |
| `docs/sqms/SQMS-LIVE-ACCEPTANCE-RESULTS-20260821.md` | Generated IDs and actual pass/fail evidence from the retained run. |
| `CODEX_HANDOFF.md` | Final state, commands, results, and retained-resource locations. |

---

## Task 1: Add the isolated acceptance executable and safety constants

**Files:**
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceTimeProvider.cs`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/Program.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj`
- Create: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Write RED safety and matrix-contract tests**

Add a project reference to the tool from `WorkflowEngine.Tests.csproj`:

```xml
<ProjectReference Include="..\..\tools\WorkflowEngine.Sqms.LiveAcceptance\WorkflowEngine.Sqms.LiveAcceptance.csproj" />
```

Create tests that assert the exact immutable design contract:

```csharp
using WorkflowEngine.Sqms.LiveAcceptance;

namespace WorkflowEngine.Tests;

public sealed class SqmsLiveAcceptanceContractTests
{
    [Fact]
    public void ConstantsPinTheIsolatedDatabaseAndEvidenceCounts()
    {
        Assert.Equal("workflowengine_sqms_live_acceptance", AcceptanceConstants.DatabaseName);
        Assert.Equal("SQMS-LIVE-20260821", AcceptanceConstants.RunMarker);
        Assert.Equal(3, AcceptanceConstants.ExpectedOpportunityCount);
        Assert.Equal(15, AcceptanceConstants.ExpectedBidCount);
        Assert.Equal(19, AcceptanceConstants.ExpectedEmailCount);
        Assert.Equal(new(2030, 1, 1, 15, 0, 0, TimeSpan.Zero), AcceptanceConstants.OpeningUtc);
    }

    [Fact]
    public void ScenarioMatrixContainsEveryTerminalOutcome()
    {
        var matrix = AcceptanceScenarioMatrix.Create();
        Assert.Equal(3, matrix.Opportunities.Count);
        Assert.Equal(15, matrix.Opportunities.Sum(row => row.Bids.Count));
        Assert.Equal(6, matrix.AllBids.Count(row => row.TerminalState == "ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "PARTIALLY ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "NOT ACCEPTED"));
        Assert.Single(matrix.AllBids, row => row.TerminalState == "NOT ALLOCATED");
        Assert.Single(matrix.AllBids, row => row.ReturnBeforeResubmit);
    }

    [Fact]
    public void AcceptanceClockAdvancesWithoutUsingSystemTime()
    {
        var clock = new AcceptanceTimeProvider(AcceptanceConstants.OpeningUtc);
        clock.SetUtcNow(AcceptanceConstants.OpeningUtc.AddDays(14));
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), clock.GetUtcNow());
    }
}
```

- [ ] **Step 2: Run tests and confirm RED**

Run:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
```

Expected: compilation fails because the acceptance project/types do not exist.

- [ ] **Step 3: Create the project using existing dependencies only**

Use this project shape:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\WorkflowEngine.Application\WorkflowEngine.Application.csproj" />
    <ProjectReference Include="..\..\src\WorkflowEngine.Infrastructure\WorkflowEngine.Infrastructure.csproj" />
    <ProjectReference Include="..\..\src\WorkflowEngine.Sqms\WorkflowEngine.Sqms.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.CommandLine" Version="10.0.7" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.7" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.7" />
  </ItemGroup>
</Project>
```

Define public constants and actors exactly:

```csharp
public static class AcceptanceConstants
{
    public const string DatabaseName = "workflowengine_sqms_live_acceptance";
    public const string RunMarker = "SQMS-LIVE-20260821";
    public const int ExpectedOpportunityCount = 3;
    public const int ExpectedBidCount = 15;
    public const int ExpectedEmailCount = 19;
    public static readonly DateTimeOffset OpeningUtc = new(2030, 1, 1, 15, 0, 0, TimeSpan.Zero);
    public static readonly SqmsActor Mfo = new("sqms-live-mfo", "SQMS Live MFO", [SqmsKeys.Roles.Mfo]);
    public static readonly SqmsActor Ipg = new("sqms-live-ipg", "SQMS Live IPG", [SqmsKeys.Roles.Ipg]);
    public static readonly SqmsActor Seller = new("sqms-live-seller", "SQMS Live Seller", [SqmsKeys.Roles.Seller]);
    public static readonly SqmsActor System = new("sqms-live-system", "SQMS Live Scheduler", [SqmsKeys.Roles.System]);
}
```

Implement the clock without any system-time fallback:

```csharp
public sealed class AcceptanceTimeProvider(DateTimeOffset initialUtc) : TimeProvider
{
    private DateTimeOffset utcNow = initialUtc;
    public override DateTimeOffset GetUtcNow() => utcNow;
    public void SetUtcNow(DateTimeOffset value) => utcNow = value;
}
```

Define `OpportunityScenario`, `BidScenario`, `AcceptanceManifest`, and `AcceptanceScenarioMatrix`. Use A/B/C and Bid 01-15 exactly as section 5 of the approved design, including amounts:

```text
A sale 100: 60 FAIR VALUE, 50 PREMIUM, 0 FAIR VALUE, 25 DISCOUNT, 0 FAIR VALUE
B sale 100: 20 FAIR VALUE, 20 PREMIUM, 20 DISCOUNT, 20 FAIR VALUE, 20 PREMIUM
C sale 100: 20 FAIR VALUE, 20 PREMIUM, 20 DISCOUNT, 20 FAIR VALUE, 20 PREMIUM
```

Only A/04 is marked expected `NOT ALLOCATED`; only A/01 is marked `ReturnBeforeResubmit=true`.

Make `Program.cs` validate the configured connection with `SqlConnectionStringBuilder`, reject any `InitialCatalog` other than the exact acceptance database, resolve `AcceptanceScenarioRunner`, and return its exit code. Catch exceptions only at the top level, print the full exception to stderr, and return 1.

- [ ] **Step 4: Run contract tests and warning-as-error build**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
```

Expected: 3 tests pass; build reports 0 warnings and 0 errors.

- [ ] **Step 5: Commit the safe skeleton**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "feat(sqms): add isolated live acceptance runner"
```

---

## Task 2: Insert exact retained target fixtures and capture identities

**Files:**
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceFixtureWriter.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceModels.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Add RED fixture-definition tests**

Assert all required target fields, marker placement, parent mapping, amount/category inputs, and sentinels before any database run:

```csharp
[Fact]
public void FixturesHaveExactParentsAmountsAndSentinels()
{
    var matrix = AcceptanceScenarioMatrix.Create();
    Assert.All(matrix.Opportunities, row =>
    {
        Assert.Contains(AcceptanceConstants.RunMarker, row.Account, StringComparison.Ordinal);
        Assert.Equal(777, row.TotalBidSentinel);
        Assert.Equal("L-SENTINEL", row.LargestBidSentinel);
        Assert.Equal("S-SENTINEL", row.LowestBidSentinel);
        Assert.Equal(5, row.Bids.Count);
    });
    Assert.Equal(110m, matrix.Opportunities[0].Bids
        .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
        .Sum(row => row.RequestedAmount));
    Assert.Equal(80m, matrix.Opportunities[1].Bids
        .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
        .Sum(row => row.RequestedAmount));
    Assert.Equal(80m, matrix.Opportunities[2].Bids
        .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
        .Sum(row => row.RequestedAmount));
}
```

- [ ] **Step 2: Run the focused test and confirm RED**

Expected: missing fixture properties or incorrect matrix values fail.

- [ ] **Step 3: Implement one-transaction target insertion**

`AcceptanceFixtureWriter.CreateAsync` must:

1. Open the workflow `DbConnection`.
2. Begin a transaction.
3. Execute a marker preflight against both target tables and throw if either count is nonzero.
4. Insert each Opportunity with `OUTPUT INSERTED.ID`.
5. Insert its five Bids with the captured `OpportunityId` and `OUTPUT INSERTED.ID`.
6. Commit only after exactly 3 and 15 IDs have been captured.

Use parameterized commands and explicit columns. Opportunity values include:

```text
SellerRMTeam=seller-{a|b|c}-team
Account=SQMS-LIVE-20260821-OPP-{A|B|C}
AccountName=SQMS Live Acceptance Opportunity {A|B|C}
AccountNumber=SQMS-LIVE-20260821-{A|B|C}
RelationshipManager=rm-{a|b|c}
FundConsideredSecondarySale=SQMS Live Fund {A|B|C}
Vehicle=SQMS Live Vehicle {A|B|C}
CommitmentAmountSale=$100.00
EffectiveDateForSale=2030-03-31
TotalBid=777
LargestBid=L-SENTINEL
LowestBid=S-SENTINEL
Status=Draft
WorkflowState=Draft
IsDeleted=0
```

Bid values include the run marker in `AccountNumber`, a stable `BuyerRmteam`, `BidForFullCommitment=No`, the matrix amount/category, distinct Account/RelationshipManager values, `Status=Draft`, `WorkflowState=Draft`, and `IsDeleted=0`.

Return an immutable manifest mapping scenario keys A/B/C and 01-15 to generated IDs. Print the manifest immediately; never assume identity starts at 10000.

- [ ] **Step 4: Run tests and source-boundary searches**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
rg -n "UPDATE.*TotalBid|UPDATE.*LargestBid|UPDATE.*LowestBid|DELETE FROM|DROP DATABASE|--reset" tools/WorkflowEngine.Sqms.LiveAcceptance
```

Expected: tests pass; search returns no matches.

- [ ] **Step 5: Commit fixtures**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "feat(sqms): add retained acceptance fixtures"
```

---

## Task 3: Drive all production Opportunity and Bid operations with transparent evidence

**Files:**
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceReporter.cs`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/Program.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Add RED operation-sequence tests**

Build the expected operations as data and assert exact method/action/state/version sequences. At minimum assert:

```csharp
[Fact]
public void OperationPlanHasExactReturnAndTerminalVersions()
{
    var plan = AcceptanceOperationPlan.Create(AcceptanceScenarioMatrix.Create());
    Assert.Equal(
        ["SUBMITTED", "Draft", "SUBMITTED", "PENDING", "OPEN TO BIDDING"],
        plan.OpportunityA.PreSchedule.Select(row => row.ExpectedState));
    Assert.Equal(10, plan.OpportunityA.Final.ExpectedVersion);
    Assert.Equal(8, plan.OpportunityB.Final.ExpectedVersion);
    Assert.Equal(7, plan.OpportunityC.Final.ExpectedVersion);
    Assert.Equal(7, plan.Bid01.Final.ExpectedVersion);
    Assert.Equal(3, plan.Bid04.Final.ExpectedVersion);
}
```

- [ ] **Step 2: Run and confirm RED**

Expected: compilation fails because the operation plan/runner/reporting types are absent.

- [ ] **Step 3: Implement exact before/after reporting**

For every call, `AcceptanceReporter` prints this stable block before execution:

```text
[NNN] INPUT
entityType=<Opportunity|Bid>
targetId=<generated integer>
method=<StartOpportunityAsync|StartBidAsync|ExecuteOpportunityActionAsync|ExecuteBidActionAsync>
actionKey=<key or START>
expectedVersion=<integer or NONE>
actor=<id>|<displayName>|<comma-separated roles>
recipientInputs=<canonical JSON object>
expectedState=<exact state>
```

After execution, print:

```text
[NNN] RESULT
success=<True|False>
code=<result code>
message=<result message>
workflowState=<authoritative state>
workflowVersion=<integer>
targetStatus=<value>
targetWorkflowState=<value>
parity=<PASS|FAIL>
```

Do not print internal workflow envelopes or invent `toState`.

- [ ] **Step 4: Implement pre-schedule production-service paths**

Compose services exactly like the production console, except register `AcceptanceTimeProvider` as `TimeProvider` and use `Notifications:Sender=Smtp`.

Drive Opportunity A:

```text
StartOpportunityAsync => SUBMITTED v2
ReturnOpportunityToDraft expected v2 => Draft v3
SubmitOpportunity expected v3 => SUBMITTED v4
MarkOpportunityPending expected v4 => PENDING v5
OpenOpportunityForBidding expected v5 => OPEN TO BIDDING v6
```

Drive B and C:

```text
StartOpportunityAsync => SUBMITTED v2
MarkOpportunityPending expected v2 => PENDING v3
OpenOpportunityForBidding expected v3 => OPEN TO BIDDING v4
```

Start all fifteen Bids. Bid 01 performs return/resubmit and reaches `SUBMITTED v4`; every other Bid reaches `SUBMITTED v2`.

Use recipient maps with production keys:

```csharp
relationshipManager -> one WorkflowRecipientInput with rm/bid address
sellerRmTeam -> one WorkflowRecipientInput with seller address
buyerRmTeam -> one WorkflowRecipientInput with buyer-team address
```

Require `Success=true`, exact state/version, and target parity after every operation. Stop immediately otherwise.

- [ ] **Step 5: Implement post-schedule manual paths**

After Task 4's scheduler phase, drive all non-allocated Bids:

```text
IPG REVIEW -> SendBidToSellerReview -> UNDER REVIEW BY SELLER
UNDER REVIEW BY SELLER -> AcceptBid | PartiallyAcceptBid | RejectBid -> matrix terminal state
```

Then drive Opportunity allocation paths exactly:

```text
A: CLOSED TO BIDDING v7 -> RequestSellerApproval v8 -> MarkOpportunityAwaitingAllocation v9 -> CompleteOpportunity v10
B: CLOSED TO BIDDING v5 -> RequestSellerApproval v6 -> MarkOpportunityPartiallyAllocated v7 -> CompleteOpportunity v8
C: CLOSED TO BIDDING v5 -> RequestSellerApproval v6 -> MarkOpportunityNotFulfilled v7
```

Capture each parent Opportunity state immediately before and after every Bid terminal action and fail if it changes.

- [ ] **Step 6: Run focused tests/build and commit**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "feat(sqms): drive live acceptance scenarios"
```

Expected: all focused tests pass and build has no warnings/errors.

---

## Task 4: Add controlled scheduled runs, production outbox delivery, and smtp4dev verification

**Files:**
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceScenarioRunner.cs`
- Create: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Add RED calendar, occurrence, and email-count tests**

Assert the fixed calendar and expected routes:

```csharp
[Fact]
public void CalendarAndEmailPlanAreExact()
{
    Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(1), AcceptanceConstants.OpenRunUtc);
    Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), AcceptanceConstants.ReminderRunUtc);
    Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(16), AcceptanceConstants.CloseRunUtc);
    var expected = AcceptanceEmailPlan.Create();
    Assert.Equal(19, expected.Sum(row => row.Count));
    Assert.Equal(6, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidAccepted).Count);
    Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidPartiallyAccepted).Count);
    Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidNotAccepted).Count);
}
```

- [ ] **Step 2: Run and confirm RED**

Expected: missing dates/email-plan types fail compilation.

- [ ] **Step 3: Invoke the real scheduled service at three dates**

Set the acceptance clock and call `ISqmsScheduledService.RunAsync`:

```text
2030-01-02T15:00:00Z: OpenNotificationsQueued=1, closed=0
2030-01-15T15:00:00Z: ReminderNotificationsQueued=1, closed=0
2030-01-17T15:00:00Z: OpportunitiesClosed=3, BidsMovedToIpgReview=14, BidsNotAllocated=1, reminder queued=0
```

After each call, print every count/failure and require `Success=true`. Invoke each date a second time and require occurrence replay/no duplicate queue. The close rerun must not change any terminal/classified row.

- [ ] **Step 4: Deliver through the production outbox processor**

After all request-driven actions are complete, resolve `IProcessNotificationOutboxService` and call `ProcessAsync`. Require:

```text
Failure=null
Processed.Count=19
all processed Status=Sent
all RetryCount=0
all SenderName=Smtp
```

Do not call `SmtpNotificationSender` directly.

- [ ] **Step 5: Verify smtp4dev over HTTP**

Use `HttpClient` from the BCL against `http://localhost:5000/api/Messages` and parse the response with `JsonDocument`. Accept a root array or an object whose `results` property is an array; fail with the raw JSON shape description for every other response. Deserialize the message ID, subject, From, To, Cc, text/HTML content, and received date from all returned rows. Because the smtp4dev database is newly created for this run, require exactly nineteen total messages and match each outbox record by subject plus intended recipients/content; do not weaken verification to only the total count.

- [ ] **Step 6: Verify links and recipient placement**

Require:

- two grouped emails contain all three Opportunity references and unchanged `https://sqms-acceptance.example.test/open-bidding?source=live`;
- three Seller emails contain their generated `opportunityId` while retaining `view=seller`;
- fourteen Bid disposition emails contain their generated `bidId` while retaining `source=live`;
- open To/Cc and all dynamic recipient inputs appear in the intended arrays;
- no real-domain address and no `Smtp:RecipientOverride` is used.

- [ ] **Step 7: Run tests/build and commit**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
git add tools/WorkflowEngine.Sqms.LiveAcceptance tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "feat(sqms): verify live SMTP acceptance evidence"
```

---

## Task 5: Add the independent read-only SQL verifier

**Files:**
- Create: `sql/sqms/testing/005_verify_sqms_live_acceptance.sql`
- Modify: `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs`

- [ ] **Step 1: Write a RED script-contract test**

Add a test that loads `005` and requires:

```csharp
Assert.Contains("SET NOCOUNT ON", sql, StringComparison.OrdinalIgnoreCase);
Assert.Contains("SQMS-LIVE-20260821", sql, StringComparison.Ordinal);
Assert.Contains("ExpectedOpportunityCount", sql, StringComparison.Ordinal);
Assert.Contains("ExpectedBidCount", sql, StringComparison.Ordinal);
Assert.Contains("ExpectedEmailCount", sql, StringComparison.Ordinal);
Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run and confirm RED**

Run the focused `SqmsDatabasePackageTests`; expected failure is missing `005`.

- [ ] **Step 3: Implement the ten read-only result sets**

The script declares:

```sql
DECLARE @RunMarker varchar(100) = 'SQMS-LIVE-20260821';
DECLARE @ExpectedOpportunityCount int = 3;
DECLARE @ExpectedBidCount int = 15;
DECLARE @ExpectedEmailCount int = 19;
```

Identify Opportunities through `Account LIKE @RunMarker + '%'` and Bids through their marked `AccountNumber`, never through assumed IDs. Join workflow instances using:

```sql
instance.WorkflowKey = N'SqmsOpportunity'
AND instance.BusinessObjectType = N'SqmsOpportunity'
AND instance.BusinessObjectId = CONCAT(N'SQMS-OPP-', opportunity.ID)
```

and the corresponding `SqmsBid`/`SQMS-BID-` values.

Return detailed result sets plus a final `AcceptanceChecks` result where each named check has Expected, Actual, and `PASS`/`FAIL`. Checks include 3/15 target counts, exact terminal totals, 18 instances, target parity, zero SQMS tasks/SLA rows, exact audit counts/paths, 19 Sent outbox rows, zero retries, route counts 1/1/3/6/4/4, zero parent mismatches, and unchanged `777|L-SENTINEL|S-SENTINEL` values.

Finish with:

```sql
IF EXISTS (SELECT 1 FROM @AcceptanceChecks WHERE Verdict <> 'PASS')
    THROW 51301, 'SQMS live acceptance verification failed.', 1;

PRINT 'SQMS live acceptance database verification passed.';
```

- [ ] **Step 4: Validate read-only behavior in a rollback fixture**

Extend the package test to build an isolated database, run existing setup/metadata, insert a minimal marked fixture, and execute `005` expecting its clear count failure without any mutation. Compare a SHA-256-compatible SQL checksum or row counts before/after to prove read-only behavior.

- [ ] **Step 5: Run tests and commit**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsDatabasePackageTests
git add sql/sqms/testing/005_verify_sqms_live_acceptance.sql tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs
git commit -m "test(sqms): add live acceptance SQL verifier"
```

---

## Task 6: Add guarded PowerShell orchestration and operator instructions

**Files:**
- Create: `scripts/run-sqms-live-acceptance.ps1`
- Create: `docs/sqms/SQMS-LIVE-ACCEPTANCE.md`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`

- [ ] **Step 1: Add RED script safety tests**

Read the PowerShell source and require exact literals and prohibitions:

```csharp
Assert.Contains("workflowengine_sqms_live_acceptance", script, StringComparison.Ordinal);
Assert.Contains("000_create_sqms_test_tables.sql", script, StringComparison.Ordinal);
Assert.Contains("001_sqms_notification_outbox_ddl.sql", script, StringComparison.Ordinal);
Assert.Contains("002_sqms_workflow_metadata_dml.sql", script, StringComparison.Ordinal);
Assert.Contains("003_verify_sqms_deployment.sql", script, StringComparison.Ordinal);
Assert.Contains("-WindowStyle Hidden", script, StringComparison.Ordinal);
Assert.Contains("sqms-live-acceptance-20260821", script, StringComparison.Ordinal);
Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("--reset", script, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run and confirm RED**

Expected: missing PowerShell script fails the focused contract test.

- [ ] **Step 3: Implement database preflight and package order**

The script uses fixed values, not caller-supplied database names:

```powershell
$acceptanceDatabase = 'workflowengine_sqms_live_acceptance'
$acceptanceConnection = 'Server=DOLLY;Database=workflowengine_sqms_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$existingId = sqlcmd -S DOLLY -d master -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID(N'workflowengine_sqms_live_acceptance');"
if (($existingId | Out-String).Trim() -notin @('', 'NULL')) {
    throw 'Acceptance database already exists. Refusing to overwrite retained evidence.'
}
```

Set `ConnectionStrings__DefaultConnection`, run compiled console `init-db`, then apply `000`, `001`, `002`, and `003` with `sqlcmd -b`. Supply the exact acceptance recipient JSON from the design. Run `verify-seed` and stop on every nonzero exit.

- [ ] **Step 4: Start the isolated smtp4dev process safely**

Create the exact data directory with `New-Item -ItemType Directory`. Start:

```powershell
Start-Process `
  -FilePath 'C:\projects\cdt-workflow\.tools\smtp4dev\smtp4dev.cmd' `
  -ArgumentList '--smtpport=3025','--urls=http://localhost:5000',"--baseappdatapath=$smtpDataDirectory",'--db=smtp4dev.db','--nousersettings' `
  -WorkingDirectory $smtpDataDirectory `
  -RedirectStandardOutput (Join-Path $smtpDataDirectory 'smtp4dev.out.log') `
  -RedirectStandardError (Join-Path $smtpDataDirectory 'smtp4dev.err.log') `
  -WindowStyle Hidden `
  -PassThru
```

Resolve `$smtpDataDirectory` to `C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821` before this call. Poll ports 3025 and 5000 for at most 30 seconds with short intervals, printing progress. If startup fails, print both logs and exit without deleting the database.

- [ ] **Step 5: Run the compiled driver and SQL verifier**

Export exact SQMS link, SMTP, connection, and enabled environment values. Run:

```powershell
dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build
sqlcmd -S DOLLY -d workflowengine_sqms_live_acceptance -E -C -b -i sql/sqms/testing/005_verify_sqms_live_acceptance.sql
```

Print the smtp4dev PID, UI URL, database, marker, driver exit, and verifier exit. Leave smtp4dev running and never remove its data directory.

- [ ] **Step 6: Write operator instructions**

Document:

- preconditions and the single launch command;
- database/marker safety boundary;
- all 3 Opportunity and 15 Bid scenarios;
- expected 19-message route matrix;
- smtp4dev URL and log/data paths;
- the exact `sqlcmd` verifier command;
- focused SQL queries for target states, workflow states, audits, outbox, mismatch checks, tasks/SLA absence, and sentinels;
- how to stop/restart only the smtp4dev acceptance process without deleting its database;
- that rerunning the setup is intentionally rejected while retained evidence exists.

- [ ] **Step 7: Run contract tests and PowerShell parse validation**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path 'scripts/run-sqms-live-acceptance.ps1'), [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count -ne 0) { $errors | Format-List; exit 1 }
```

Expected: tests pass and parser reports zero errors.

- [ ] **Step 8: Commit orchestration/docs**

```powershell
git add scripts/run-sqms-live-acceptance.ps1 docs/sqms/SQMS-LIVE-ACCEPTANCE.md tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs
git commit -m "docs(sqms): add live acceptance runbook"
```

---

## Task 7: Run pre-mutation gates, then execute the retained live acceptance

**Files:**
- Create after execution: `docs/sqms/SQMS-LIVE-ACCEPTANCE-RESULTS-20260821.md`

- [ ] **Step 1: Prove the worktree and destination preconditions**

Run and record:

```powershell
git status --short --branch
sqlcmd -S DOLLY -d master -E -C -b -Q "SET NOCOUNT ON; SELECT DB_ID(N'workflowengine_sqms_live_acceptance') AS AcceptanceDatabaseId;"
Test-NetConnection localhost -Port 3025
Test-NetConnection localhost -Port 5000
```

Required before setup: worktree clean, database ID null, and ports unused. Stop if any differs; do not kill an unknown process or overwrite a database.

- [ ] **Step 2: Run build and compatibility gates before mutation**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror
dotnet test src/WorkflowEngine.sln -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1
```

Required: no build warning/error, no failed default test, only the existing opt-in smtp4dev skips.

- [ ] **Step 3: Print the acceptance input manifest before execution**

Print the exact database, marker, actor identities, link values, recipient arrays, three Opportunity paths, fifteen Bid paths/amounts/categories/outcomes, expected versions, scheduled dates, expected route counts, and retained-resource policy. This is the final human-readable mutation boundary.

- [ ] **Step 4: Execute the guarded script**

```powershell
& .\scripts\run-sqms-live-acceptance.ps1
```

Required: script exits 0; runner prints overall PASS; SQL prints `SQMS live acceptance database verification passed.`; smtp4dev remains reachable.

- [ ] **Step 5: Independently rerun database and email checks**

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_live_acceptance -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
sqlcmd -S DOLLY -d workflowengine_sqms_live_acceptance -E -C -b -i sql/sqms/testing/005_verify_sqms_live_acceptance.sql
Invoke-RestMethod http://localhost:5000/api/Messages
```

Required: both SQL verifiers pass; smtp4dev contains exactly the nineteen matched acceptance messages.

- [ ] **Step 6: Write the retained results manifest**

Create the results document containing:

- branch and commit SHA;
- database/server and confirmed retained status;
- run marker;
- generated Opportunity A/B/C IDs;
- generated Bid 01-15 IDs and parent IDs;
- final target/workflow states and versions;
- audit, task/SLA, outbox, and smtp4dev totals;
- route counts 1/1/3/6/4/4;
- sentinel results;
- exact verification commands;
- smtp4dev PID/UI/data/log paths;
- every deviation or `None`.

- [ ] **Step 7: Commit results**

```powershell
git add docs/sqms/SQMS-LIVE-ACCEPTANCE-RESULTS-20260821.md
git commit -m "test(sqms): record live acceptance evidence"
```

---

## Task 8: Final regression, handoff, and stop gate

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run final regression after the live run**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror
dotnet test src/WorkflowEngine.sln -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1
git diff --check
```

Required: builds and both complete test suites pass; worktree diff is clean after handoff commit.

- [ ] **Step 2: Update the handoff**

Record:

- current goal complete and stop boundary;
- implementation/result commits;
- exact full build/test counts;
- exact live-run marker and generated IDs;
- database and smtp4dev retained locations;
- all nineteen email counts and UI URL;
- SQL verifier results and commands;
- known risk that retained acceptance data blocks intentional rerun;
- no production/shared database, push, merge, deployment, or Task Scheduler change.

- [ ] **Step 3: Commit handoff and verify repository state**

```powershell
git add CODEX_HANDOFF.md
git diff --cached --check
git commit -m "docs(sqms): record live acceptance handoff"
git status --short --branch
git log -8 --oneline
```

Expected: clean worktree; branch ahead count reported accurately.

- [ ] **Step 4: Stop and report using the required completion summary**

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

State that this was compiled application acceptance rather than sample JSON; under `Sample JSON executed`, write `Not applicable to SQMS: the target integration is in-process and the acceptance driver called the production service contracts directly.` Include retained database, smtp4dev UI/data, generated IDs, 19-message result, SQL verification command, and the fact that no production or existing `workflowengine` data was changed.

Stop. Do not push, merge, deploy, run production SQL, enable Windows Task Scheduler, delete the database, or stop/remove the retained smtp4dev evidence without a new explicit request.
