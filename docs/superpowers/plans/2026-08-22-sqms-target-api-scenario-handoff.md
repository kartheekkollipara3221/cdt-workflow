# SQMS Target API Scenario Handoff Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a source-verified 43-pack SQMS target API scenario catalog containing both implemented in-process examples and clearly labeled recommended target-owned HTTP contracts.

**Architecture:** Add a documentation-only catalog under `docs/sqms/target-api-scenarios/`, mirroring the established `workflow-api/samples/scenarios` convention. Keep every pack atomic and self-describing, add read-only SQL evidence where applicable, and enforce structure, JSON boundaries, route conventions, source-key coverage, and link integrity through a new xUnit contract-test class.

**Tech Stack:** Markdown, REST Client `.http` files, JSON, C# example snippets, SQL Server read-only verification SQL, .NET 10, xUnit.

---

## Governing sources

- `PROJECT.md`
- `AGENTS.MD`
- `docs/superpowers/specs/2026-08-22-sqms-target-api-scenario-handoff-design.md`
- `docs/sqms/TARGET-API-INTEGRATION.md`
- `workflow-api/samples/scenarios/README.md`
- `src/WorkflowEngine.Sqms/Contracts/ISqmsWorkflowService.cs`
- `src/WorkflowEngine.Sqms/Contracts/ISqmsScheduledService.cs`
- `src/WorkflowEngine.Sqms/Contracts/SqmsModels.cs`
- `src/WorkflowEngine.Sqms/SqmsKeys.cs`
- `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs`
- `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs`
- `sql/sqms/002_sqms_workflow_metadata_dml.sql`
- `sql/sqms/003_verify_sqms_deployment.sql`

## File structure

### Create

- `docs/sqms/target-api-scenarios/README.md` — contract legend, quick start, shared HTTP and in-process rules, ownership, full catalog, and valid sequences.
- `docs/sqms/target-api-scenarios/01-opportunity-start-submit/` through `14-opportunity-complete-partially-allocated/` — every Opportunity request/context/scheduled path.
- `docs/sqms/target-api-scenarios/15-bid-start-submit/` through `24-bid-seller-reject/` — every Bid request/context/scheduled path.
- `docs/sqms/target-api-scenarios/25-scheduled-opportunity-open-notification/` through `30-notification-outbox-smtp-delivery/` — notification and scheduler paths.
- `docs/sqms/target-api-scenarios/31-failure-target-row-not-found/` through `42-request-versus-scheduled-projection-ownership/` — failure, replay, rollback, continuation, and ownership paths.
- `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/` — composite target API/in-process/SQL/SMTP acceptance handoff.
- `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs` — static documentation contract and drift coverage.

### Modify

- `docs/sqms/TARGET-API-INTEGRATION.md` — link the concise engineering contract to the scenario catalog and preserve its current ownership statements.
- `CODEX_HANDOFF.md` — record documentation progress only at the final task if the work spans sessions.

### Do not modify

- Production workflow or SQMS source.
- Existing `workflow-api/samples/scenarios` packs.
- SQL deployment scripts.
- Retained live-acceptance scripts, database, or smtp4dev data.
- `restore-repo.ps1`.

## Shared scenario content rules

Every pack must use the exact status banner:

```markdown
## Contract status

- **Implemented:** The in-process service behavior shown here exists in the current workflow/SQMS assemblies.
- **Recommended target facade:** The HTTP route and DTO are proposed for the external target API and are not implemented in this repository.
- **Target implementation required:** The target API must supply authentication, actor/recipient resolution, target persistence, transaction enlistment, HTTP mapping, and response DTOs.
```

Every request-driven `request.http` uses:

```http
@baseUrl = https://localhost:7001
@accessToken = REPLACE_WITH_ACCESS_TOKEN

Authorization: Bearer {{accessToken}}
Content-Type: application/json
```

Every Action `request.json` contains exactly:

```json
{
  "actionKey": "MarkOpportunityPending",
  "expectedWorkflowVersion": 2
}
```

Each pack uses its exact action and prerequisite version from the approved matrix. No public request may contain `toState`, `status`, `workflowState`, actor, roles, recipients, notification route/template values, workflow-instance ID, correlation ID, or idempotency key.

Every request-driven `in-process.cs` follows this concrete implemented call shape:

```csharp
var actor = actorFactory.CreateFromAuthenticatedUser();
var recipients = await recipientResolver.ResolveForSqmsAsync(cancellationToken);

var result = await sqmsWorkflowService.ExecuteOpportunityActionAsync(
    new SqmsExecuteRequest(
        opportunityId,
        "MarkOpportunityPending",
        expectedWorkflowVersion,
        actor,
        recipients),
    cancellationToken);

if (!result.Success)
{
    throw targetErrorMapper.Map(result);
}

var state = result.WorkflowResult?.Result.NewState
    ?? result.WorkflowResult?.Result.CurrentState
    ?? throw new InvalidOperationException(
        "Successful workflow operation returned no authoritative state.");

opportunity.WorkflowState = state;
opportunity.Status = state;
opportunity.ModifiedBy = actor.UserId;
opportunity.ModifiedDate = timeProvider.GetUtcNow().UtcDateTime;

await targetDbContext.SaveChangesAsync(cancellationToken);
```

Each file must state that `actorFactory`, `recipientResolver`, `targetErrorMapper`, target entity/repository, and shared-transaction coordinator are target-owned integration placeholders. Do not claim these class names exist.

Every `verify.sql` begins with:

```sql
SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
```

It performs read-only `SELECT`/`IF ... THROW` verification only. It must not perform `INSERT`, `UPDATE`, `DELETE`, `MERGE`, `DROP`, `TRUNCATE`, or execute deployment scripts.

---

### Task 1: Add the contract-test harness and root catalog

**Files:**
- Create: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: `docs/sqms/target-api-scenarios/README.md`

- [ ] **Step 1: Write the failing root-catalog contract test**

Create `SqmsTargetApiScenarioContractTests.cs` with this initial structure:

```csharp
using System.Text.Json;
using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsTargetApiScenarioContractTests
{
    private static readonly string Root = Path.Combine(
        SqmsDatabasePackageTests.RepositoryRoot(),
        "docs",
        "sqms",
        "target-api-scenarios");

    [Fact]
    public void RootCatalogDeclaresTheDualContractAndOwnershipBoundary()
    {
        var readme = Read("README.md");

        Assert.Contains("Implemented in-process contract", readme, StringComparison.Ordinal);
        Assert.Contains("Recommended target facade", readme, StringComparison.Ordinal);
        Assert.Contains("not implemented in this repository", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request-driven", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scheduled", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target API persists", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four columns", readme, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments]));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter FullyQualifiedName~SqmsTargetApiScenarioContractTests --verbosity minimal
```

Expected: FAIL because `docs/sqms/target-api-scenarios/README.md` does not exist.

- [ ] **Step 3: Create the root README**

Write the root README with these exact top-level sections:

```markdown
# SQMS Target API scenario catalog

## Contract legend
## How to use the HTTP packs
## How to use the in-process examples
## Target API transaction sequence
## Trusted actor and recipient boundary
## Request versus scheduled persistence ownership
## Recommended HTTP routes
## Recommended response
## Recommended error mapping
## Scenario catalog
## Valid Opportunity sequences
## Valid Bid sequences
## Scheduled and notification sequences
## Failure, replay, rollback, and ownership sequences
## Complete acceptance sequence
```

Include the six approved routes, bearer-token convention, exact Action body, recommended response, error table, implemented/recommended labels, and a 43-row catalog copied exactly from the approved design. State that scheduled operations have no public UI route.

- [ ] **Step 4: Run the focused test and verify GREEN**

Expected: the root-catalog test passes.

- [ ] **Step 5: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/README.md
git commit -m "test(sqms): define target API scenario catalog contract"
```

---

### Task 2: Author all Opportunity packs

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: `docs/sqms/target-api-scenarios/01-opportunity-start-submit/*`
- Create: `docs/sqms/target-api-scenarios/02-opportunity-get-context/*`
- Create: `docs/sqms/target-api-scenarios/03-opportunity-submit-existing-draft/*`
- Create: `docs/sqms/target-api-scenarios/04-opportunity-submitted-return-to-draft/*`
- Create: `docs/sqms/target-api-scenarios/05-opportunity-submitted-mark-pending/*`
- Create: `docs/sqms/target-api-scenarios/06-opportunity-pending-return-to-draft/*`
- Create: `docs/sqms/target-api-scenarios/07-opportunity-pending-open-for-bidding/*`
- Create: `docs/sqms/target-api-scenarios/08-opportunity-scheduled-close-bidding/*`
- Create: `docs/sqms/target-api-scenarios/09-opportunity-request-seller-approval/*`
- Create: `docs/sqms/target-api-scenarios/10-opportunity-mark-awaiting-allocation/*`
- Create: `docs/sqms/target-api-scenarios/11-opportunity-mark-partially-allocated/*`
- Create: `docs/sqms/target-api-scenarios/12-opportunity-mark-not-fulfilled/*`
- Create: `docs/sqms/target-api-scenarios/13-opportunity-complete-awaiting-allocation/*`
- Create: `docs/sqms/target-api-scenarios/14-opportunity-complete-partially-allocated/*`

- [ ] **Step 1: Add failing Opportunity manifest coverage**

Add this manifest and test to the contract class:

```csharp
private static readonly string[] OpportunityPacks =
[
    "01-opportunity-start-submit",
    "02-opportunity-get-context",
    "03-opportunity-submit-existing-draft",
    "04-opportunity-submitted-return-to-draft",
    "05-opportunity-submitted-mark-pending",
    "06-opportunity-pending-return-to-draft",
    "07-opportunity-pending-open-for-bidding",
    "08-opportunity-scheduled-close-bidding",
    "09-opportunity-request-seller-approval",
    "10-opportunity-mark-awaiting-allocation",
    "11-opportunity-mark-partially-allocated",
    "12-opportunity-mark-not-fulfilled",
    "13-opportunity-complete-awaiting-allocation",
    "14-opportunity-complete-partially-allocated"
];

[Fact]
public void OpportunityCatalogContainsEveryApprovedPath()
{
    Assert.All(OpportunityPacks, pack =>
    {
        Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
        Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
    });
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Expected: FAIL listing missing Opportunity directories.

- [ ] **Step 3: Create request-driven Opportunity packs 01, 03 through 07, and 09 through 14**

Use `README.md`, `request.http`, `request.json`, `in-process.cs`, `expected-response.json`, and `verify.sql`. Use this exact behavior matrix:

| Pack | HTTP operation | Service call | Actor role | Expected version/state |
|---|---|---|---|---|
| 01 | POST start | `StartOpportunityAsync` | MFO | version 2, `SUBMITTED` |
| 03 | POST action `SubmitOpportunity` | `ExecuteOpportunityActionAsync` | MFO | `Draft` to `SUBMITTED` |
| 04 | `ReturnOpportunityToDraft` | Execute | IPG | `SUBMITTED` to `Draft` |
| 05 | `MarkOpportunityPending` | Execute | IPG | `SUBMITTED` to `PENDING` |
| 06 | `ReturnOpportunityToDraft` | Execute | IPG | `PENDING` to `Draft` |
| 07 | `OpenOpportunityForBidding` | Execute | IPG | `PENDING` to `OPEN TO BIDDING` |
| 09 | `RequestSellerApproval` | Execute | IPG | `CLOSED TO BIDDING` to `PENDING SELLER APPROVAL` |
| 10 | `MarkOpportunityAwaitingAllocation` | Execute | IPG | `PENDING SELLER APPROVAL` to `AWAITING ALLOCATION` |
| 11 | `MarkOpportunityPartiallyAllocated` | Execute | IPG | `PENDING SELLER APPROVAL` to `PARTIALLY ALLOCATED` |
| 12 | `MarkOpportunityNotFulfilled` | Execute | IPG | terminal `NOT FULFILLED` |
| 13 | `CompleteOpportunity` | Execute | IPG | `AWAITING ALLOCATION` to terminal `COMPLETED` |
| 14 | `CompleteOpportunity` | Execute | IPG | `PARTIALLY ALLOCATED` to terminal `COMPLETED` |

For pack 09, document server-owned `relationshipManager` and `sellerRmTeam` resolution plus the `SqmsOpportunityPendingSellerApproval` route. No recipient data appears in `request.json`.

- [ ] **Step 4: Create context pack 02**

Use `README.md`, `request.http`, `expected-response.json`, and `verify.sql`. The HTTP route is `GET /v1/sqms/opportunities/{id}/workflow`; the implemented call is `GetOpportunityContextAsync`. Explain that it never lazily starts and never writes target/workflow data.

- [ ] **Step 5: Create scheduled close pack 08**

Use `README.md`, `in-process.cs`, and `verify.sql`. Call `ISqmsScheduledService.RunAsync`, not `ISqmsScheduledTransitionService` directly. Document configured local calendar-day eligibility, System role, and scheduled four-column projection.

- [ ] **Step 6: Run the focused test and verify GREEN**

Expected: all Opportunity directories/readmes exist and the prior root test still passes.

- [ ] **Step 7: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/0*-opportunity-* docs/sqms/target-api-scenarios/1[0-4]-opportunity-*
git commit -m "docs(sqms): add Opportunity target API scenarios"
```

---

### Task 3: Author all Bid packs

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: `docs/sqms/target-api-scenarios/15-bid-start-submit/*` through `24-bid-seller-reject/*`

- [ ] **Step 1: Add failing Bid manifest coverage**

Add:

```csharp
private static readonly string[] BidPacks =
[
    "15-bid-start-submit",
    "16-bid-get-context",
    "17-bid-submit-existing-draft",
    "18-bid-submitted-return-to-draft",
    "19-bid-scheduled-mark-not-allocated",
    "20-bid-scheduled-move-to-ipg-review",
    "21-bid-send-to-seller-review",
    "22-bid-seller-accept",
    "23-bid-seller-partially-accept",
    "24-bid-seller-reject"
];
```

Add a test identical in structure to `OpportunityCatalogContainsEveryApprovedPath`, applied to `BidPacks`.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: FAIL listing missing Bid directories.

- [ ] **Step 3: Create request-driven Bid packs 15, 17, 18, and 21 through 24**

| Pack | Operation | Role | Expected result |
|---|---|---|---|
| 15 | `StartBidAsync` | MFO | version 2, `SUBMITTED` |
| 17 | `SubmitBid` | MFO | `Draft` to `SUBMITTED` |
| 18 | `ReturnBidToDraft` | IPG | `SUBMITTED` to `Draft` |
| 21 | `SendBidToSellerReview` | IPG | `IPG REVIEW` to `UNDER REVIEW BY SELLER` |
| 22 | `AcceptBid` | Seller | terminal `ACCEPTED`, route `SqmsBidAccepted` |
| 23 | `PartiallyAcceptBid` | Seller | terminal `PARTIALLY ACCEPTED`, route `SqmsBidPartiallyAccepted` |
| 24 | `RejectBid` | Seller | terminal `NOT ACCEPTED`, route `SqmsBidNotAccepted` |

Disposition packs document trusted parent Opportunity lookup, `relationshipManager`/`buyerRmTeam` recipient resolution, and target-owned request persistence. Public JSON contains neither addresses nor template data.

- [ ] **Step 4: Create context pack 16**

Use the Bid GET route and `GetBidContextAsync`; prove read-only/no-lazy-start semantics.

- [ ] **Step 5: Create scheduled classification packs 19 and 20**

Call `ISqmsScheduledService.RunAsync`. Pack 19 documents the strict `FairValue + Premium > sale amount` condition that sends a Discount Bid to `NOT ALLOCATED`. Pack 20 documents every other valid submitted Bid moving to `IPG REVIEW`. Both document scheduled four-column target projection.

- [ ] **Step 6: Run focused tests and verify GREEN**

- [ ] **Step 7: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/1[5-9]-bid-* docs/sqms/target-api-scenarios/2[0-4]-bid-*
git commit -m "docs(sqms): add Bid target API scenarios"
```

---

### Task 4: Author notification and scheduler packs

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: packs 25 through 30.

- [ ] **Step 1: Add failing scheduler/notification manifest coverage**

```csharp
private static readonly string[] SchedulerPacks =
[
    "25-scheduled-opportunity-open-notification",
    "26-scheduled-opportunity-reminder",
    "27-scheduled-catch-up-close-first",
    "28-scheduled-disabled",
    "29-scheduled-mixed-bid-classification",
    "30-notification-outbox-smtp-delivery"
];
```

Add the corresponding directory/readme existence test.

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Create packs 25 and 26**

Document notification-only queueing with stable keys:

```text
SQMS:OpportunityOpen:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
SQMS:OpportunityReminder:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
```

Prove `WorkflowInstanceId IS NULL`, `NotificationOccurrenceKey IS NOT NULL`, immutable recipient/payload snapshots, and one filtered-unique occurrence row.

- [ ] **Step 4: Create packs 27 through 29**

- 27: close due Opportunities first, then classify, then queue groups only for Opportunities still open.
- 28: `Sqms.Enabled=false` returns successful zero counts before reading target tables.
- 29: use one Fair Value, one Premium, and one Discount Bid to demonstrate both sides of the strict threshold and per-Bid expected outcomes.

- [ ] **Step 5: Create SMTP pack 30**

Document `process-notification-outbox`, immutable To/Cc snapshots, six SQMS routes, `Sent` state, retry zero, and smtp4dev inspection. Do not start smtp4dev or promise a production relay in this task.

- [ ] **Step 6: Run focused tests and verify GREEN**

- [ ] **Step 7: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/2[5-9]-* docs/sqms/target-api-scenarios/30-*
git commit -m "docs(sqms): add scheduled notification scenarios"
```

---

### Task 5: Author validation and concurrency failure packs

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: packs 31 through 36.

- [ ] **Step 1: Add failing manifest coverage for packs 31 through 36**

```csharp
private static readonly string[] FailurePacks =
[
    "31-failure-target-row-not-found",
    "32-failure-workflow-not-started",
    "33-failure-invalid-action-or-role",
    "34-failure-stale-workflow-version",
    "35-failure-seller-approval-enrichment",
    "36-failure-bid-disposition-enrichment"
];
```

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Create packs 31 through 34**

Document exact results and recommended HTTP mappings:

- 31: `TargetNotFound` -> 404; no workflow/audit/idempotency/outbox.
- 32: `NotWorkflowManaged` -> 409; Context/Action do not start a workflow.
- 33: `Forbidden` -> 403 or `InvalidTransition` -> 422 depending whether role or state/action fails; no target-owned state save.
- 34: `ConcurrencyConflict` -> 409 with refreshed current context; UI must retry only after user refresh/reselection.

- [ ] **Step 4: Create enrichment packs 35 and 36**

Pack 35 removes one required trusted Opportunity value or link configuration and proves seller approval produces no transition/email. Pack 36 removes one required Bid/parent value and proves all Bid-disposition side effects roll back. Mark any data preparation as testing-only.

- [ ] **Step 5: Run focused tests and verify GREEN**

- [ ] **Step 6: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/3[1-6]-*
git commit -m "docs(sqms): add target integration failure scenarios"
```

---

### Task 6: Author replay, rollback, continuation, and ownership packs

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: packs 37 through 42.

- [ ] **Step 1: Add failing manifest coverage**

```csharp
private static readonly string[] OwnershipPacks =
[
    "37-replay-request-action",
    "38-replay-notification-occurrence",
    "39-rollback-target-persistence-after-workflow",
    "40-rollback-scheduled-target-projection",
    "41-scheduled-per-record-continuation",
    "42-request-versus-scheduled-projection-ownership"
];
```

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Create replay packs 37 and 38**

Pack 37 sends the exact Action request twice and proves one transition audit/outbox plus `replay=true`. It must also prove request-driven replay leaves the target row untouched until the target-owned save. Pack 38 runs the same occurrence twice and proves one outbox row with the second result marked replay.

- [ ] **Step 4: Create rollback packs 39 and 40**

Pack 39 shows the target throwing after a successful workflow service call but before commit; verifier proves target/workflow/audit/idempotency/outbox all rolled back. Pack 40 injects a scheduled target projection failure and proves the workflow transition is rolled back. Do not invent public failure controls.

- [ ] **Step 5: Create continuation and ownership packs 41 and 42**

Pack 41 uses three scheduled candidates where the middle candidate fails and the first/last succeed. Pack 42 reproduces the approved ownership proof: complete target row unchanged after request call, target-owned persistence afterward, and scheduled four-column projection with all other target columns preserved.

- [ ] **Step 6: Run focused tests and verify GREEN**

- [ ] **Step 7: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/3[7-9]-* docs/sqms/target-api-scenarios/4[0-2]-*
git commit -m "docs(sqms): add replay rollback and ownership scenarios"
```

---

### Task 7: Author the complete Opportunity/four-Bid acceptance pack

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Create: `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/README.md`
- Create: `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/sequence.http`
- Create: `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/in-process.cs`
- Create: `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/expected-results.sql`
- Create: `docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids/expected-emails.md`

- [ ] **Step 1: Write failing composite-pack assertions**

```csharp
[Fact]
public void CompositePackCoversFourBidOutcomesAndSixRoutes()
{
    var readme = Read("43-complete-opportunity-and-four-bids", "README.md");
    foreach (var state in new[] { "ACCEPTED", "PARTIALLY ACCEPTED", "NOT ACCEPTED", "NOT ALLOCATED" })
    {
        Assert.Contains(state, readme, StringComparison.Ordinal);
    }

    foreach (var route in new[]
    {
        SqmsKeys.Routes.OpportunityOpen,
        SqmsKeys.Routes.OpportunityReminder,
        SqmsKeys.Routes.OpportunityPendingSellerApproval,
        SqmsKeys.Routes.BidAccepted,
        SqmsKeys.Routes.BidPartiallyAccepted,
        SqmsKeys.Routes.BidNotAccepted
    })
    {
        Assert.Contains(route, readme, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: Run focused tests and verify RED**

- [ ] **Step 3: Create the composite files**

Base the expected lifecycle on the retained projection-ownership acceptance profile without rerunning it:

- Opportunity reaches `COMPLETED` version 10.
- Bids finish `ACCEPTED` version 7, `PARTIALLY ACCEPTED` version 5, `NOT ACCEPTED` version 5, and `NOT ALLOCATED` version 3.
- Six messages are expected: open, reminder, seller approval, accepted, partially accepted, and not accepted.
- Twenty request-unchanged proofs, twenty target-owner-persisted proofs, five scheduled-projected proofs, twenty-five audit rows, twenty-five idempotency rows, zero tasks, and zero SLA schedules are documented as the retained reference result.

Clearly distinguish retained evidence from steps a target team must execute in its own isolated environment.

- [ ] **Step 4: Run focused tests and verify GREEN**

- [ ] **Step 5: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios/43-complete-opportunity-and-four-bids
git commit -m "docs(sqms): add complete target API acceptance scenario"
```

---

### Task 8: Enforce global public-boundary, coverage, and link contracts

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs`
- Modify: any scenario files failing the new global checks.

- [ ] **Step 1: Add failing JSON authority tests**

Enumerate every `request.json`, parse with `JsonDocument`, recursively collect property names, and reject these case-insensitive names:

```csharp
private static readonly HashSet<string> ForbiddenPublicProperties = new(StringComparer.OrdinalIgnoreCase)
{
    "toState",
    "status",
    "workflowState",
    "actor",
    "roles",
    "recipientInputs",
    "emailAddress",
    "notificationRouteKey",
    "templateData",
    "workflowInstanceId",
    "idempotencyKey",
    "correlationId"
};
```

Add a recursive helper that walks objects and arrays and asserts no collected property is forbidden.

- [ ] **Step 2: Add route and authorization tests**

For every `request.http`, assert it contains `Authorization: Bearer {{accessToken}}`, does not contain `X-Workflow-UserId`, and uses only the approved Opportunity/Bid route patterns. Exempt no scheduled pack because scheduled packs must not contain `request.http`.

- [ ] **Step 3: Add exact scenario-count and link tests**

Combine the four manifest arrays plus pack 43 and assert 43 unique directories. Parse root Markdown relative links matching `./NN-.../` and assert every manifest directory appears exactly once. Resolve every relative Markdown link in every README and assert the target exists.

Also declare exact pack categories and verify their required files:

```csharp
private static readonly string[] RequestFiles =
[
    "README.md",
    "request.http",
    "request.json",
    "in-process.cs",
    "expected-response.json",
    "verify.sql"
];

private static readonly string[] ContextFiles =
[
    "README.md",
    "request.http",
    "expected-response.json",
    "verify.sql"
];

private static readonly string[] ScheduledOrFailureFiles =
[
    "README.md",
    "in-process.cs",
    "verify.sql"
];
```

Apply `RequestFiles` to request-driven transition and replay packs, `ContextFiles` to packs 02 and 16, and `ScheduledOrFailureFiles` to scheduler/rollback packs. Assert pack 43 has `README.md`, `sequence.http`, `in-process.cs`, `expected-results.sql`, and `expected-emails.md`. Permit explicitly documented additions such as `setup.testing.sql`; do not require empty non-applicable files.

- [ ] **Step 4: Add source-key coverage tests**

Read all pack content and assert it contains every stable action key from `SqmsKeys.OpportunityActions` and `SqmsKeys.BidActions`, plus all six route keys and all six terminal states. Assert request packs reference `ISqmsWorkflowService`; scheduled packs reference `ISqmsScheduledService` and do not reference public scheduled routes.

- [ ] **Step 5: Run focused tests and fix documentation until GREEN**

Run the focused contract suite. Correct documentation; do not weaken the boundary tests to accept forbidden public authority fields.

- [ ] **Step 6: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsTargetApiScenarioContractTests.cs docs/sqms/target-api-scenarios
git commit -m "test(sqms): enforce scenario handoff boundaries"
```

---

### Task 9: Link the engineering contract and run full verification

**Files:**
- Modify: `docs/sqms/TARGET-API-INTEGRATION.md`
- Modify: `CODEX_HANDOFF.md` only if session continuity is needed.

- [ ] **Step 1: Add the catalog link without duplicating contracts**

Immediately after the opening paragraph in `TARGET-API-INTEGRATION.md`, add:

```markdown
## Executable target-team scenarios

The complete Opportunity, Bid, scheduling, notification, failure, replay, rollback, and persistence-ownership handoff is in [SQMS Target API scenario catalog](./target-api-scenarios/README.md). The catalog pairs the implemented in-process contracts with clearly labeled recommended target-owned HTTP routes and DTOs.
```

- [ ] **Step 2: Run Markdown/JSON/source contract verification**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter FullyQualifiedName~SqmsTargetApiScenarioContractTests --verbosity minimal
```

Expected: all scenario contract tests pass.

- [ ] **Step 3: Run the complete current solution regression**

```powershell
$config = Get-Content -Raw 'src/WorkflowEngine.Console/appsettings.Development.json' | ConvertFrom-Json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = $config.ConnectionStrings.DefaultConnection
dotnet test src/WorkflowEngine.sln -m:1 --no-restore --verbosity minimal
```

Expected baseline: 453 passed, 0 failed, 4 opt-in SMTP tests skipped, plus the newly added documentation tests.

- [ ] **Step 4: Run Workflow API regression**

```powershell
$config = Get-Content -Raw 'src/WorkflowEngine.Console/appsettings.Development.json' | ConvertFrom-Json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = $config.ConnectionStrings.DefaultConnection
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 --no-restore --verbosity minimal
```

Expected: 68 passed, 0 failed, 0 skipped.

- [ ] **Step 5: Verify repository integrity**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intended documentation/test/handoff changes remain.

- [ ] **Step 6: Record AGENTS completion evidence**

Report:

```text
Changed: 43-pack SQMS target API scenario catalog, documentation contract tests, and engineering-contract link.
Why: Provide the target API team executable-style HTTP/in-process integration documentation for every approved path.
Tests run: Focused scenario contract tests, full core/SQMS suite, and full Workflow API suite.
Seed verification run: Not run unless explicitly required; documentation verifiers are read-only.
Sample JSON executed: Parsed and boundary-validated; proposed target HTTP requests are not sent because the target facade is not implemented here.
Files updated: Catalog, test class, TARGET-API-INTEGRATION.md, and optional session handoff.
Open issues: Actual target controller/DTO/transaction implementation and final route confirmation require the external target API source.
```

- [ ] **Step 7: Commit final integration link and evidence**

```powershell
git add docs/sqms/TARGET-API-INTEGRATION.md CODEX_HANDOFF.md
git commit -m "docs(sqms): complete target API scenario handoff"
```

If `CODEX_HANDOFF.md` was not changed, omit it from `git add`.

## Execution stop conditions

Stop and ask before continuing if:

- current source/SQL metadata contradicts the approved 20-transition or six-route catalog;
- a proposed public request appears to require actor, recipient, `toState`, route, template, workflow-instance, or idempotency authority from the browser;
- exact target API controller/DTO/transaction code is needed but the external target source remains unavailable;
- a verifier would need to mutate a non-isolated database;
- retained live acceptance would need to be rerun or destroyed;
- implementation would require production code changes rather than documentation and contract tests.
