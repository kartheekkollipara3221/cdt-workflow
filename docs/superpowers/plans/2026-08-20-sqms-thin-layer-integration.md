# SQMS Thin-Layer Workflow Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the approved SQMS Opportunity/Bid adapter, DBA SQL package, grouped notifications, and scheduled processing while proving that existing CDT production behavior remains compatible.

**Architecture:** A new `WorkflowEngine.Sqms` outer-layer project owns all `SSQMS` knowledge and calls the existing generic application services in process. Generic Infrastructure receives only two reusable corrections: metadata-driven terminal completion and notification-only outbox support. SQL Server DDL/DML files are the production deployment contract; local EF migration parity and exhaustive CDT upgrade tests prevent drift.

**Tech Stack:** .NET 10, C# 14, EF Core 10, SQL Server, xUnit, Microsoft.Extensions.Configuration/DependencyInjection, existing MailKit notification sender, `TimeProvider`.

---

## Governing references

- Design: `docs/superpowers/specs/2026-08-20-sqms-thin-layer-integration-design.md`
- Project contract: `PROJECT.md`
- Clean-slate evidence: `docs/sqms/CLEAN-SLATE-CONTEXT.md`
- Target API integration evidence only: `tareget-api-integration/`
- Raw legacy evidence only: `SQMS/`

Do not modify evidence files. Do not revive the reverted SQMS implementation. Do not run production SQL or `init-db` against a production-selected database.

## Milestone mapping and stop gates

This plan extends the existing `PROJECT.md` milestones; it does not replace them.

| Plan tasks | Existing milestone | Stop gate |
|---|---|---|
| 1 | Milestone 1: Solution skeleton | New project and DI skeleton build with warnings as errors. |
| 2-4 | Milestone 2: SQL schema and seed data | DBA DDL/DML rerun tests, exact metadata verification, and CDT upgrade preservation pass. |
| 5-6 | Milestone 3: execution path | SQMS Start/Context/Action tests pass with same-transaction state projection. |
| 7 | Milestone 4: workflow engine | Metadata terminal handling passes SQMS tests and existing CDT transition tests. |
| 8-11 | Milestone 5: notifications and scheduled runner | Notification-only, email, scheduler, retry, and command tests pass. |
| 12-14 | Milestone 6: samples, tests, and demo | Full CDT/SQMS SQL-backed regression and all section 21 gates pass. |

Do not start tasks in a later row until the preceding stop gate passes and its commit exists.

## File structure

### New SQMS project

```text
src/WorkflowEngine.Sqms/
  WorkflowEngine.Sqms.csproj
  Contracts/ISqmsWorkflowService.cs
  Contracts/ISqmsScheduledService.cs
  Contracts/SqmsModels.cs
  Configuration/SqmsOptions.cs
  DependencyInjection/SqmsServiceCollectionExtensions.cs
  Persistence/ISqmsDataStore.cs
  Persistence/SqlSqmsDataStore.cs
  Persistence/SqmsRows.cs
  SqmsKeys.cs
  SqmsLinkBuilder.cs
  SqmsValueParser.cs
  SqmsWorkflowService.cs
  Scheduling/SqmsScheduledService.cs
```

Each file has one responsibility: contracts, configuration, SQL access, identity/constants, links, value conversion, request orchestration, or scheduled orchestration.

### Generic additive files

```text
src/WorkflowEngine.Application/Notifications/IQueueWorkflowNotificationService.cs
src/WorkflowEngine.Infrastructure/Notifications/QueueWorkflowNotificationService.cs
src/WorkflowEngine.Infrastructure/Migrations/20260820200000_AddNotificationOccurrenceKey.cs
src/WorkflowEngine.Infrastructure/Migrations/20260820200000_AddNotificationOccurrenceKey.Designer.cs
```

Normalize the generated migration identifier to `20260820200000_AddNotificationOccurrenceKey` in both filenames and the designer migration attribute so the plan, DBA comparison, and review evidence use one stable identifier.

### DBA SQL package

```text
sql/sqms/001_sqms_notification_outbox_ddl.sql
sql/sqms/002_sqms_workflow_metadata_dml.sql
sql/sqms/003_verify_sqms_deployment.sql
sql/sqms/testing/000_create_sqms_test_tables.sql
sql/sqms/testing/004_validate_sqms_package_in_rollback.sql
```

Only `001`-`003` are production handoff files. `testing/000` creates the user-supplied tables only in isolated test databases. `testing/004` wraps production scripts in an outer rollback for rerun validation.

### Tests and documentation

```text
tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs
tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs
tests/WorkflowEngine.Tests/NotificationOnlyQueueTests.cs
tests/WorkflowEngine.Tests/WorkflowTerminalStateTests.cs
tests/WorkflowEngine.Tests/SqmsValueParserTests.cs
tests/WorkflowEngine.Tests/SqmsLinkBuilderTests.cs
tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs
tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs
tests/WorkflowEngine.Tests/SqmsCommandTests.cs
tests/WorkflowEngine.Tests/CdtSqmsCompatibilityTests.cs
docs/sqms/TARGET-API-INTEGRATION.md
docs/sqms/DBA-DEPLOYMENT.md
```

## Task 1: Milestone 1 — add the isolated SQMS project skeleton

**Files:**
- Create: `src/WorkflowEngine.Sqms/WorkflowEngine.Sqms.csproj`
- Create: `src/WorkflowEngine.Sqms/SqmsKeys.cs`
- Create: `src/WorkflowEngine.Sqms/Configuration/SqmsOptions.cs`
- Create: `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs`
- Modify: `src/WorkflowEngine.sln`
- Modify: `src/WorkflowEngine.Console/WorkflowEngine.Console.csproj`
- Modify: `tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj`

- [ ] **Step 1: Write the failing structure test**

```csharp
public sealed class SqmsProjectStructureTests
{
    [Fact]
    public void SqmsKeysUseApprovedStableIdentifiers()
    {
        Assert.Equal("SqmsOpportunity", SqmsKeys.OpportunityWorkflow);
        Assert.Equal("SqmsBid", SqmsKeys.BidWorkflow);
        Assert.Equal("SQMS-OPP-42", SqmsKeys.OpportunityBusinessObjectId(42));
        Assert.Equal("WF-SQMS-OPP-42", SqmsKeys.OpportunityWorkflowInstanceId(42));
        Assert.Equal("SQMS-BID-84", SqmsKeys.BidBusinessObjectId(84));
        Assert.Equal("WF-SQMS-BID-84", SqmsKeys.BidWorkflowInstanceId(84));
    }
}
```

- [ ] **Step 2: Run the test and confirm RED**

Run:

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsProjectStructureTests
```

Expected: compilation fails because `WorkflowEngine.Sqms` and `SqmsKeys` do not exist.

- [ ] **Step 3: Create the project and stable keys**

Use a project with references to Application and Infrastructure and no new package references:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\WorkflowEngine.Application\WorkflowEngine.Application.csproj" />
    <ProjectReference Include="..\WorkflowEngine.Infrastructure\WorkflowEngine.Infrastructure.csproj" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Implement `SqmsKeys` with constants for every approved workflow, state, action, role, and route. Identity methods must use `InvariantCulture` and reject IDs less than 1.

- [ ] **Step 4: Add options and a DI stub**

```csharp
public sealed class SqmsOptions
{
    public const string SectionName = "Sqms";
    public bool Enabled { get; set; }
    public SqmsLinkOptions Links { get; set; } = new();
    public SqmsSchedulingOptions Scheduling { get; set; } = new();
}

public sealed class SqmsLinkOptions
{
    public string? OpenBiddingDashboardUrl { get; set; }
    public string? SellerApprovalBidsDashboardUrl { get; set; }
    public string? BidFormUrl { get; set; }
}

public sealed class SqmsSchedulingOptions
{
    public string TimeZoneId { get; set; } = "Eastern Standard Time";
    public int BiddingDays { get; set; } = 15;
    public int SellerResponseDays { get; set; } = 5;
}
```

`AddSqmsWorkflowIntegration` initially binds and validates options only. Validation requires positive day counts and a resolvable timezone when `Enabled=true`; disabled SQMS must not require URLs.

- [ ] **Step 5: Add project references and run the Milestone 1 gate**

Run:

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsProjectStructureTests
```

Expected: build succeeds with zero warnings; focused test passes.

- [ ] **Step 6: Commit**

```powershell
git add src/WorkflowEngine.sln src/WorkflowEngine.Sqms src/WorkflowEngine.Console/WorkflowEngine.Console.csproj tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj tests/WorkflowEngine.Tests/SqmsProjectStructureTests.cs
git commit -m "feat(sqms): add isolated integration project"
```

Stop and report the Milestone 1 gate before Task 2.

## Task 2: Milestone 2 — add the backward-compatible outbox DDL

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Create: generated migration and designer files under `src/WorkflowEngine.Infrastructure/Migrations/`
- Modify: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`
- Create: `sql/sqms/001_sqms_notification_outbox_ddl.sql`
- Modify: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`
- Modify: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs`

- [ ] **Step 1: Write RED model and SQL-contract tests**

Tests must assert:

```csharp
Assert.True(outbox.FindProperty(nameof(NotificationOutbox.WorkflowInstanceId))!.IsNullable);
Assert.Equal(300, outbox.FindProperty(nameof(NotificationOutbox.NotificationOccurrenceKey))!.GetMaxLength());
Assert.Contains(outbox.GetIndexes(), index =>
    index.IsUnique
    && index.GetFilter() == "[NotificationOccurrenceKey] IS NOT NULL"
    && index.Properties.Select(property => property.Name)
        .SequenceEqual(["NotificationOccurrenceKey"]));
Assert.Contains(outbox.GetCheckConstraints(), constraint =>
    constraint.Name == "CK_NotificationOutbox_Owner"
    && constraint.Sql == "[WorkflowInstanceId] IS NOT NULL OR [NotificationOccurrenceKey] IS NOT NULL");
```

Add a file-content test requiring `SET XACT_ABORT ON`, `BEGIN TRANSACTION`, `NotificationOccurrenceKey`, the filtered unique index, owner check constraint, and no `SSQMS` DDL in `001`.

- [ ] **Step 2: Run focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SqmsDatabasePackageTests"
```

Expected: failures report required `WorkflowInstanceId`, missing occurrence property/index/check, and missing SQL file.

- [ ] **Step 3: Change the entity and EF model**

```csharp
public string? WorkflowInstanceId { get; set; }
public string? NotificationOccurrenceKey { get; set; }
```

Map max lengths, keep the FK, add the filtered unique index, and add `CK_NotificationOutbox_Owner`. Do not change any existing status, SLA-occurrence, task, or recipient-snapshot mapping.

- [ ] **Step 4: Generate the local/test migration**

```powershell
dotnet ef migrations add AddNotificationOccurrenceKey --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.Console
```

Review the generated migration. It must preserve data, contain no `DELETE`, and only alter `WorkflowInstanceId`, add `NotificationOccurrenceKey`, and create/recreate required constraints/indexes.

- [ ] **Step 5: Write the authoritative DBA DDL**

`001` must:

1. verify `dbo.NotificationOutbox` and current column shape;
2. capture/validate the expected FK and `IX_NotificationOutbox_WorkflowInstanceId`;
3. drop only dependencies that block `ALTER COLUMN`;
4. alter `WorkflowInstanceId nvarchar(150) NULL`;
5. add/validate `NotificationOccurrenceKey nvarchar(300) NULL`;
6. recreate the FK/index with their existing delete semantics;
7. create `UX_NotificationOutbox_NotificationOccurrenceKey` with `WHERE NotificationOccurrenceKey IS NOT NULL`;
8. create `CK_NotificationOutbox_Owner`;
9. commit or roll back on error with `THROW`.

Use catalog checks against `sys.columns`, `sys.indexes`, `sys.foreign_keys`, and `sys.check_constraints`; never silently accept an incompatible object.

- [ ] **Step 6: Run model/script tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SqmsDatabasePackageTests"
```

Expected: all focused tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/WorkflowEngine.Infrastructure/Persistence src/WorkflowEngine.Infrastructure/Migrations sql/sqms/001_sqms_notification_outbox_ddl.sql tests/WorkflowEngine.Tests/SchemaConstraintTests.cs tests/WorkflowEngine.Tests/MigrationSchemaTests.cs tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs
git commit -m "feat(db): add notification occurrence ownership"
```

## Task 3: Milestone 2 — seed exact SQMS metadata through DBA DML

**Files:**
- Create: `sql/sqms/002_sqms_workflow_metadata_dml.sql`
- Create: `sql/sqms/003_verify_sqms_deployment.sql`
- Create: `sql/sqms/testing/000_create_sqms_test_tables.sql`
- Create: `sql/sqms/testing/004_validate_sqms_package_in_rollback.sql`
- Modify: `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs`

- [ ] **Step 1: Write RED exact-matrix tests**

Create an isolated SQL database, apply current migrations, apply testing table DDL, execute `002`, and execute `003`. Assert these exact active counts:

```text
SqmsOpportunity: 10 states, 10 actions, 3 roles, 13 transitions, 3 routes
SqmsBid:          8 states,  7 actions, 4 roles,  9 transitions, 3 routes
Combined:         0 task definitions, 0 SLA policies
```

Run `002` twice in the same database and require identical counts.

- [ ] **Step 2: Run focused tests and confirm RED**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsDatabasePackageTests
```

Expected: missing DML/verifier scripts fail the test.

- [ ] **Step 3: Implement exact state/action/role/transition DML**

Use the matrices from design section 7 verbatim. Seed `IsTerminal=1` only for:

```text
Opportunity: NOT FULFILLED, COMPLETED
Bid: NOT ALLOCATED, ACCEPTED, PARTIALLY ACCEPTED, NOT ACCEPTED
```

Every SQMS transition has null task close/create keys. Attach routes only to `RequestSellerApproval`, `AcceptBid`, `PartiallyAcceptBid`, and `RejectBid`.

- [ ] **Step 4: Seed six routes with complete templates and ordered recipient rules**

Use these required SQLCMD JSON-array inputs:

```text
SqmsOpenToRecipientsJson
SqmsOpenCcRecipientsJson
SqmsSellerApprovalCcRecipientsJson
SqmsBidDispositionCcRecipientsJson
```

Parse them with `OPENJSON`, retain array order as `SortOrder`, require at least one open To address, and insert one `StaticAddress` rule per value. Do not parse delimited address strings.

Dynamic required To rules are:

```text
SqmsOpportunityPendingSellerApproval: relationshipManager, sellerRmTeam
SqmsBidAccepted: relationshipManager, buyerRmTeam
SqmsBidPartiallyAccepted: relationshipManager, buyerRmTeam
SqmsBidNotAccepted: relationshipManager, buyerRmTeam
```

Use the exact token sets from design section 9. Bid outcome bodies must preserve the distinct accepted, partially accepted, and not-accepted legacy wording. Subjects must distinguish all six routes.

Use these exact plain-text templates; `quarter` is rendered as `Q1`, `Q2`, `Q3`, or `Q4`:

| Route | Subject | Plain-text body |
|---|---|---|
| `SqmsOpportunityOpen` | `SQMS Opportunities Open to Bidding - {{quarter}} {{year}}` | `Opportunities for {{quarter}} {{year}} are open to bidding. Responses are due by {{responseDate}}. View the Open to Bidding dashboard: {{dashboardUrl}}` |
| `SqmsOpportunityReminder` | `SQMS Reminder: Opportunities Open to Bidding - {{quarter}} {{year}}` | `This is a reminder that opportunities for {{quarter}} {{year}} remain open to bidding. Responses are due by {{responseDate}}. View the Open to Bidding dashboard: {{dashboardUrl}}` |
| `SqmsOpportunityPendingSellerApproval` | `SQMS Opportunity Pending Seller Approval - {{accountName}}` | `Seller approval is requested for {{accountName}} ({{accountNumber}}), {{fund}} / {{vehicle}}, sale amount {{saleAmount}}, effective-sale period {{quarter}} {{year}}. Please respond by {{sellerResponseDate}}. Review the related bids: {{opportunityUrl}}` |
| `SqmsBidAccepted` | `SQMS Bid Accepted - {{buyerAccountName}}` | `The bid placed for {{buyerAccountName}} ({{buyerAccountNumber}}) has been accepted by the seller for {{fund}} / {{vehicle}}, requested amount {{requestedAmount}}, effective date {{effectiveDate}} ({{quarter}} {{year}}). Please confirm the buyer will proceed on the effective date. Open the bid: {{bidUrl}}` |
| `SqmsBidPartiallyAccepted` | `SQMS Bid Partially Accepted - {{buyerAccountName}}` | `The bid placed for {{buyerAccountName}} ({{buyerAccountNumber}}) has been partially accepted by the seller for {{fund}} / {{vehicle}}, requested amount {{requestedAmount}}, effective date {{effectiveDate}} ({{quarter}} {{year}}). Please confirm the buyer will proceed with the accepted Private Fund interest. Open the bid: {{bidUrl}}` |
| `SqmsBidNotAccepted` | `SQMS Bid Not Accepted - {{buyerAccountName}}` | `The bid placed for {{buyerAccountName}} ({{buyerAccountNumber}}) has not been accepted by the seller for {{fund}} / {{vehicle}}, requested amount {{requestedAmount}}, effective date {{effectiveDate}} ({{quarter}} {{year}}). Open the bid: {{bidUrl}}` |

HTML templates contain the same sentences and tokens in the same order, wrap paragraphs in `<p>`, and render only the final URL as an encoded `<a href="{{token}}">` link. Do not introduce additional facts or recipient data in HTML.

- [ ] **Step 5: Implement independent verification**

`003` must throw unique error numbers for missing/wrong schema, workflow, state, action, role, transition, route, token, recipient rule, terminal flag, task/SLA absence, and unexpected `SSQMS` column shape. It must not insert, update, or delete data.

- [ ] **Step 6: Add rollback validation**

`testing/004` begins an outer transaction, runs `001`, runs `002` twice with example.test JSON arrays, runs `003`, and rolls back. It must print `SQMS package rollback validation passed.` only after every verification succeeds.

- [ ] **Step 7: Run the Milestone 2 metadata tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsDatabasePackageTests
```

Expected: all package, rerun, exact-matrix, and read-only verifier tests pass.

- [ ] **Step 8: Commit**

```powershell
git add sql/sqms tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs
git commit -m "feat(sqms): add DBA metadata package"
```

## Task 4: Milestone 2 — prove CDT rows survive the exact DDL twice

**Files:**
- Create: `tests/WorkflowEngine.Tests/CdtSqmsCompatibilityTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs`

- [ ] **Step 1: Write the pre-upgrade CDT fixture**

Use existing Portfolio Trade services to create active and completed instances, pending/completed tasks, active/completed SLA schedules, audit/idempotency rows, and outbox rows in `Pending`, `Processing`, `Sent`, and `Cancelled` states. Compute a SHA-256 hash over every pre-existing outbox column ordered by `Id`.

- [ ] **Step 2: Write RED upgrade assertions**

Apply the exact `001` file twice, then assert:

```csharp
Assert.Equal(beforeCount, await db.NotificationOutbox.CountAsync());
Assert.Equal(beforeHash, await HashExistingOutboxRows(db));
Assert.All(await db.NotificationOutbox.ToListAsync(), row => Assert.NotNull(row.WorkflowInstanceId));
```

Also assert old-style inserts succeed, ownerless inserts fail with `CK_NotificationOutbox_Owner`, notification-only inserts succeed, and duplicate occurrence keys fail with the unique index.

- [ ] **Step 3: Run the compatibility test**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~CdtSqmsCompatibilityTests
```

Expected: pass against isolated SQL Server; no production database is touched.

- [ ] **Step 4: Run the complete Milestone 2 stop gate**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SqmsDatabasePackageTests|FullyQualifiedName~CdtSqmsCompatibilityTests|FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests"
```

Expected: zero warnings, zero failures.

- [ ] **Step 5: Commit**

```powershell
git add tests/WorkflowEngine.Tests/CdtSqmsCompatibilityTests.cs tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs
git commit -m "test(sqms): prove CDT database compatibility"
```

Stop and report the Milestone 2 gate before Task 5.

## Task 5: Milestone 3 — implement SQMS data access, parsing, and links

**Files:**
- Create: `src/WorkflowEngine.Sqms/Persistence/SqmsRows.cs`
- Create: `src/WorkflowEngine.Sqms/Persistence/ISqmsDataStore.cs`
- Create: `src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs`
- Create: `src/WorkflowEngine.Sqms/SqmsValueParser.cs`
- Create: `src/WorkflowEngine.Sqms/SqmsLinkBuilder.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsValueParserTests.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsLinkBuilderTests.cs`

- [ ] **Step 1: Write RED parser tests**

Cover invariant inputs `1000`, `1,000.00`, `$1,000.00`, surrounding whitespace, null, empty, malformed text, and negative values. Cover category normalization for `Fair Value`, `FAIRVALUE`, `Premium`, and `Discount`; unknown categories must fail rather than default.

- [ ] **Step 2: Write RED link tests**

Require:

```text
Open link: configured URL unchanged
Seller link: ?opportunityId=10000
Seller link with existing query: &opportunityId=10000
Bid link: ?bidId=10001
Fragment remains last
Relative, ftp, blank, and malformed URLs fail validation
```

- [ ] **Step 3: Implement pure parser and link builder**

Use `decimal.TryParse` with explicit invariant `NumberStyles.Currency`; never use current culture. Use `UriBuilder` plus parsed query pairs, not string concatenation. Return structured success/error records so scheduled processing can report a candidate failure without exceptions for expected bad data.

- [ ] **Step 4: Define minimal row projections and data-store interface**

```csharp
public sealed record SqmsOpportunityRow(
    int Id,
    string? SellerRmTeam,
    string AccountName,
    string AccountNumber,
    string? RelationshipManager,
    string? FundConsideredSecondarySale,
    string? Vehicle,
    string? CommitmentAmountSale,
    DateTime? EffectiveDateForSale,
    string? Status,
    string? WorkflowState);

public sealed record SqmsBidRow(
    int Id,
    int OpportunityId,
    string? BuyerRmTeam,
    string AccountName,
    string AccountNumber,
    string? RelationshipManager,
    string? CommitmentAmountRequested,
    string? BidExpressedAsFairValue,
    string? Status,
    string? WorkflowState);
```

`ISqmsDataStore` exposes only approved reads, state projections, open/reminder candidates, and submitted Bids. It has no members for `TotalBid`, `LargestBid`, or `LowestBid`.

Use this exact asynchronous surface:

```csharp
public interface ISqmsDataStore
{
    Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken);
    Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(int opportunityId, CancellationToken cancellationToken);
    Task ProjectOpportunityStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
    Task ProjectBidStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken);
}

public sealed record SqmsOpenOpportunityRow(
    SqmsOpportunityRow Opportunity,
    DateTimeOffset OpenedAtUtc);
```

- [ ] **Step 5: Implement SQL access through the workflow connection/transaction**

`SqlSqmsDataStore` uses parameterized commands on `WorkflowDbContext.Database.GetDbConnection()` and `CurrentTransaction?.GetDbTransaction()`. Select explicit columns; never use `SELECT *`. Every query includes `(IsDeleted IS NULL OR IsDeleted = 0)`. Projection updates set only `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` and require exactly one affected row.

- [ ] **Step 6: Run focused tests and negative source search**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SqmsValueParserTests|FullyQualifiedName~SqmsLinkBuilderTests"
rg -n "TotalBid|LargestBid|LowestBid" src/WorkflowEngine.Sqms
```

Expected: tests pass; `rg` returns no matches.

- [ ] **Step 7: Commit**

```powershell
git add src/WorkflowEngine.Sqms tests/WorkflowEngine.Tests/SqmsValueParserTests.cs tests/WorkflowEngine.Tests/SqmsLinkBuilderTests.cs
git commit -m "feat(sqms): add trusted data mapping"
```

## Task 6: Milestone 3 — implement Start, Context, and Action orchestration

**Files:**
- Create: `src/WorkflowEngine.Sqms/Contracts/SqmsModels.cs`
- Create: `src/WorkflowEngine.Sqms/Contracts/ISqmsWorkflowService.cs`
- Create: `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs`
- Modify: `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`
- Create: `docs/sqms/TARGET-API-INTEGRATION.md`

- [ ] **Step 1: Write RED identity, missing-workflow, and no-lazy-start tests**

Test both entities. `Get*Context` must return `NotWorkflowManaged` when the deterministic instance is absent and must leave `WorkflowInstance` count unchanged.

- [ ] **Step 2: Write RED start-plus-submit transaction tests**

Within one SQL transaction, prove new Opportunity and Bid workflows commit directly to `SUBMITTED`, version 2, one audit row, zero tasks/SLA, and exact SQMS `Status`/`WorkflowState`. Force projection failure and assert workflow instance/audit/idempotency rows roll back.

- [ ] **Step 3: Write RED action tests**

Test `actionKey` plus expected version only, role enforcement, stale version, return to visible `Draft`, successful state projection, and no client next-state input. For notification actions, pass resolved address inputs under the exact metadata source keys.

- [ ] **Step 4: Implement additive contracts**

```csharp
public sealed record SqmsActor(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record SqmsExecuteRequest(
    int EntityId,
    string ActionKey,
    int ExpectedWorkflowVersion,
    SqmsActor Actor,
    IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInputs);

public sealed record SqmsOperationResult(
    bool Success,
    string ResultCode,
    string? Message,
    WorkflowActionResultEnvelope? WorkflowResult,
    WorkflowContextResponse? Context);
```

`ISqmsWorkflowService` exposes the six approved methods and no target HTTP DTOs.

```csharp
public interface ISqmsWorkflowService
{
    Task<SqmsOperationResult> StartOpportunityAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> StartBidAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> GetOpportunityContextAsync(int opportunityId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> GetBidContextAsync(int bidId, SqmsActor actor, CancellationToken cancellationToken);
    Task<SqmsOperationResult> ExecuteOpportunityActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken);
    Task<SqmsOperationResult> ExecuteBidActionAsync(SqmsExecuteRequest request, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement orchestration using existing services**

Build internal envelopes with `DeriveIdentityFromPersistence=true`, `PersistSectionData=false`, and `ValidateActionEcho=false`. Start idempotency keys are `SQMS:OPP:{id}:START:SUBMIT` and `SQMS:BID:{id}:START:SUBMIT`. Action keys are `SQMS:OPP:{id}:{expectedVersion}:{actionKey}` and `SQMS:BID:{id}:{expectedVersion}:{actionKey}`. Start calls `IStartWorkflowService.Start` with `InitialActionKey`. Execute calls `IWorkflowExecutionService.Execute`. Context calls `IWorkflowContextReader.Read`.

Begin one workflow database transaction when none exists; `StartWorkflowService` must be changed additively to join `Database.CurrentTransaction` exactly as `WorkflowExecutionService` does. Commit state projection only after a successful workflow result.

- [ ] **Step 6: Document target API transaction enlistment**

`TARGET-API-INTEGRATION.md` must show the target API:

1. opens its transaction;
2. saves the target row and obtains `ID`;
3. enlists workflow/SQMS access on the same scoped SQL connection and transaction;
4. calls the appropriate SQMS service;
5. commits once;
6. maps `SqmsOperationResult` into target-owned DTOs.

State explicitly that the browser never sends actor identity, addresses, route/template keys, links, or `toState`.

- [ ] **Step 7: Run the Milestone 3 stop gate**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsWorkflowServiceTests
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --filter "FullyQualifiedName~WorkflowActionServiceTests|FullyQualifiedName~WorkflowStartServiceTests"
```

Expected: zero warnings and all focused SQMS/CDT integration tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src/WorkflowEngine.Application src/WorkflowEngine.Infrastructure/WorkflowStart src/WorkflowEngine.Sqms tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs docs/sqms/TARGET-API-INTEGRATION.md
git commit -m "feat(sqms): add in-process workflow adapter"
```

Stop and report the Milestone 3 gate before Task 7.

## Task 7: Milestone 4 — derive runtime completion from terminal metadata

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Create: `tests/WorkflowEngine.Tests/WorkflowTerminalStateTests.cs`
- Modify: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`

- [ ] **Step 1: Write RED nonliteral-terminal tests**

Seed an isolated workflow whose terminal state key is `NOT ACCEPTED` with `IsTerminal=true`. Execute its transition and require `WorkflowInstance.Status == "Completed"`, non-null `CompletedAtUtc`, no pending task, and no available actions.

- [ ] **Step 2: Write the CDT preservation assertion**

Extend the existing Portfolio Trade completion test to assert the same status/time values for literal `Completed` before and after the change.

- [ ] **Step 3: Run tests and confirm only the nonliteral case is RED**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowTerminalStateTests|FullyQualifiedName~WorkflowTransitionTests"
```

Expected: nonliteral terminal test fails because runtime remains Active; existing CDT completion passes.

- [ ] **Step 4: Replace the literal comparison**

Load the active destination `WorkflowStateDefinition` by definition-version ID and `ToStateKey`. Use `IsTerminal` for `WorkflowInstance.Status` and `CompletedAtUtc`. Fail before mutation if the destination metadata is missing or inactive. Do not change action, task, notification, audit, idempotency, or context contracts.

- [ ] **Step 5: Run the Milestone 4 stop gate**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowTerminalStateTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~WorkflowExecutionValidationTests"
```

Expected: all pass with zero warnings.

- [ ] **Step 6: Commit**

```powershell
git add src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs tests/WorkflowEngine.Tests/WorkflowTerminalStateTests.cs tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs
git commit -m "fix(engine): honor terminal state metadata"
```

Stop and report the Milestone 4 gate before Task 8.

## Task 8: Milestone 5 — add the generic notification-only queue

**Files:**
- Create: `src/WorkflowEngine.Application/Notifications/IQueueWorkflowNotificationService.cs`
- Create: `src/WorkflowEngine.Infrastructure/Notifications/QueueWorkflowNotificationService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/NotificationRecipientResolver.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- Modify: `src/WorkflowEngine.Application/Notifications/INotificationSender.cs`
- Modify: `src/WorkflowEngine.Application/Notifications/IProcessNotificationOutboxService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- Create: `tests/WorkflowEngine.Tests/NotificationOnlyQueueTests.cs`
- Modify: `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`

- [ ] **Step 1: Write RED queue tests**

Cover static/request-input recipient resolution without an instance, template rendering, HTML encoding, required-token failure, required-recipient failure, unexpected recipient input, one pending row, exact occurrence replay, and concurrent duplicate insert handling.

- [ ] **Step 2: Define the generic contract**

```csharp
public sealed record QueueWorkflowNotificationRequest(
    string WorkflowKey,
    int WorkflowDefinitionVersion,
    string NotificationRouteKey,
    string NotificationOccurrenceKey,
    string BusinessObjectType,
    string BusinessObjectId,
    WorkflowNotificationInput NotificationInput,
    string CorrelationId);

public sealed record QueueWorkflowNotificationResult(
    bool Success,
    string ResultCode,
    long? NotificationOutboxId,
    bool Replay,
    IReadOnlyList<string> Errors);

public interface IQueueWorkflowNotificationService
{
    Task<QueueWorkflowNotificationResult> QueueAsync(
        QueueWorkflowNotificationRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Make recipient resolution nullable-instance aware**

Change `Resolve(..., string? workflowInstanceId, ...)`. Query participants only when the instance ID is nonblank. A route that declares `WorkflowParticipant` with no instance must produce the existing required-source error; static and request-input semantics remain byte-for-byte unchanged.

- [ ] **Step 4: Implement queue/render/idempotency**

Load the exact workflow version and active route, resolve recipients, render subject/text/HTML using `NotificationTemplateRenderer`, serialize the same payload/recipient shapes as transition notifications, and insert with null `WorkflowInstanceId`. Catch only SQL unique violations for `UX_NotificationOutbox_NotificationOccurrenceKey` and return `Replay=true` after loading the existing row.

- [ ] **Step 5: Preserve sender compatibility**

Make `NotificationDelivery.WorkflowInstanceId`, `ProcessedNotification.WorkflowInstanceId`, and `NotificationOutboxFailure.WorkflowInstanceId` nullable annotations; this does not change their CLR constructor signatures. Add `NotificationOccurrenceKey` as a non-positional init property in each record body so existing source and compiled constructor call sites remain valid:

```csharp
public string? NotificationOccurrenceKey { get; init; }

public string DisplayReference =>
    WorkflowInstanceId ?? NotificationOccurrenceKey ?? "Unowned";
```

Set the property with an object initializer when mapping an outbox row. Existing producers continue supplying non-null workflow IDs. Console and SMTP diagnostics use `DisplayReference`.

- [ ] **Step 6: Run generic and CDT outbox tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~NotificationOnlyQueueTests|FullyQualifiedName~Phase5OutboxTests|FullyQualifiedName~SmtpNotificationSenderTests|FullyQualifiedName~ConsoleNotificationSenderTests"
```

Expected: all pass; existing CDT delivery tests retain their previous assertions.

- [ ] **Step 7: Commit**

```powershell
git add src/WorkflowEngine.Application/Notifications src/WorkflowEngine.Infrastructure/Notifications src/WorkflowEngine.Infrastructure/DependencyInjection tests/WorkflowEngine.Tests/NotificationOnlyQueueTests.cs tests/WorkflowEngine.Tests/Phase5OutboxTests.cs
git commit -m "feat(notifications): queue notification-only occurrences"
```

## Task 9: Milestone 5 — enrich request-driven SQMS emails

**Files:**
- Modify: `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs`
- Modify: `src/WorkflowEngine.Sqms/Persistence/ISqmsDataStore.cs`
- Modify: `src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`

- [ ] **Step 1: Write RED Seller-approval email tests**

Require the route to receive resolved `relationshipManager` and `sellerRmTeam` address arrays and exact tokens: quarter, year, vehicle, account number/name, fund, sale amount, transition-date-plus-five, and the Bids dashboard URL with URL-encoded `opportunityId`.

- [ ] **Step 2: Write RED Bid-disposition email tests**

For all three terminal actions, require separate route/notification types, parent fund/vehicle/effective date, requested amount, buyer account details, resolved `relationshipManager`/`buyerRmTeam`, and Bid form URL with URL-encoded `bidId`.

- [ ] **Step 3: Write RED atomic-failure tests**

For missing effective date, amount, parent Opportunity, unresolved required addresses, and missing URL, assert no SQMS state update, workflow transition, audit, idempotency, or outbox row commits.

- [ ] **Step 4: Implement trusted enrichment**

The service loads data by ID, derives template values using the configured timezone and `TimeProvider`, constructs links via `SqmsLinkBuilder`, and passes only normalized resolved addresses through `WorkflowNotificationInput`. It never accepts template values, links, routes, or addresses from the UI.

- [ ] **Step 5: Run request-email tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsWorkflowServiceTests
```

Expected: all request-driven workflow and email tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/WorkflowEngine.Sqms tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs
git commit -m "feat(sqms): queue request-driven emails"
```

## Task 10: Milestone 5 — implement close-first scheduled processing

**Files:**
- Create: `src/WorkflowEngine.Sqms/Contracts/ISqmsScheduledService.cs`
- Create: `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs`
- Modify: `src/WorkflowEngine.Sqms/Persistence/ISqmsDataStore.cs`
- Modify: `src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs`
- Modify: `src/WorkflowEngine.Sqms/DependencyInjection/SqmsServiceCollectionExtensions.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs`

- [ ] **Step 1: Write RED calendar/catch-up tests**

With a fixed `TimeProvider`, test open day +1, reminder day +14, response deadline +15, close day +16, missed-run catch-up, no reminder after close, grouping by quarter/year/occurrence date, and no duplicate group email on rerun.

- [ ] **Step 2: Write RED Bid-classification tests**

Cover greater-than, equal, and less-than thresholds; Fair Value/Premium sum; Discount rejection only when strictly greater; remaining submitted Bids to IPG review; already-classified Bids untouched; invalid amount/category candidate failure; per-Bid commits surviving later failure.

- [ ] **Step 3: Define the scheduled result**

```csharp
public sealed record SqmsScheduledRunResult(
    DateTimeOffset Now,
    int OpportunitiesClosed,
    int BidsMovedToIpgReview,
    int BidsNotAllocated,
    int OpenNotificationsQueued,
    int ReminderNotificationsQueued,
    int AlreadyQueued,
    IReadOnlyList<SqmsScheduledFailure> Failures)
{
    public bool Success => Failures.Count == 0;
}
```

- [ ] **Step 4: Implement the exact order**

Use three explicit phases in one invocation: close Opportunities, classify Bids, then queue open/reminder groups still open. Each Opportunity close and each Bid action uses its own transaction through `ISqmsWorkflowService`. Notification groups call `IQueueWorkflowNotificationService`; no direct sender call is allowed.

- [ ] **Step 5: Implement stable occurrence keys**

```text
SQMS:OpportunityOpen:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
SQMS:OpportunityReminder:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}
```

Use the configured static open dashboard URL unchanged in both routes.

- [ ] **Step 6: Run scheduler tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter FullyQualifiedName~SqmsScheduledServiceTests
```

Expected: all timing, ordering, classification, continuation, and idempotency tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/WorkflowEngine.Sqms tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs
git commit -m "feat(sqms): add scheduled close and notifications"
```

## Task 11: Milestone 5 — wire `run-sqms-scheduled`

**Files:**
- Create: `src/WorkflowEngine.Application/Commands/IRunSqmsScheduledCommandHandler.cs`
- Create: `src/WorkflowEngine.Sqms/Commands/RunSqmsScheduledCommandHandler.cs`
- Modify: `src/WorkflowEngine.Application/Commands/CommandArgumentParser.cs`
- Modify: `src/WorkflowEngine.Application/Commands/CommandRegistry.cs`
- Modify: `src/WorkflowEngine.Console/Program.cs`
- Modify: `src/WorkflowEngine.Console/appsettings.json`
- Modify: `src/WorkflowEngine.Console/appsettings.Development.json`
- Create: `tests/WorkflowEngine.Tests/SqmsCommandTests.cs`
- Modify: `tests/WorkflowEngine.Tests/CommandRegistryTests.cs`
- Modify: `tests/WorkflowEngine.Tests/ReadmeValidationTests.cs`

- [ ] **Step 1: Write RED parser/registry/exit tests**

Require `run-sqms-scheduled` in `KnownCommands`, no arguments, successful summary on zero failures, and nonzero exit with every failure listed. Existing command names and constructor call sites must continue compiling through a trailing optional handler parameter.

- [ ] **Step 2: Implement command routing additively**

Add `IRunSqmsScheduledCommandHandler? runSqmsScheduledCommandHandler = null` as the final `CommandRegistry` constructor parameter. Dispatch the command asynchronously in the same style as `run-sla`. Do not reorder or remove existing parameters/commands.

- [ ] **Step 3: Implement output**

Print the fixed now, each aggregate count, one line per failure, and final success/failure summary. Do not send SMTP from this command; it queues outbox rows only.

- [ ] **Step 4: Wire DI and configuration**

Register `AddSqmsWorkflowIntegration(configuration)`, the scheduled handler, and the SQMS project reference. Default checked-in configuration sets `Sqms:Enabled=false`, the approved day counts/timezone, and illustrative example.test URLs. Production enablement requires environment overrides.

- [ ] **Step 5: Run the Milestone 5 stop gate**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SqmsCommandTests|FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~NotificationOnlyQueueTests|FullyQualifiedName~Phase5OutboxTests|FullyQualifiedName~Phase5SlaTests"
```

Expected: zero warnings and zero failures.

- [ ] **Step 6: Commit**

```powershell
git add src/WorkflowEngine.Application/Commands src/WorkflowEngine.Sqms src/WorkflowEngine.Console tests/WorkflowEngine.Tests/SqmsCommandTests.cs tests/WorkflowEngine.Tests/CommandRegistryTests.cs tests/WorkflowEngine.Tests/ReadmeValidationTests.cs
git commit -m "feat(console): add SQMS scheduled command"
```

Stop and report the Milestone 5 gate before Task 12.

## Task 12: Milestone 6 — add end-to-end SQMS SQL-backed coverage

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/CdtSqmsCompatibilityTests.cs`
- Create: `tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs`

- [ ] **Step 1: Add the complete Opportunity path test**

Execute Start/Submit, return, resubmit, pending, open, scheduled close, pending Seller approval, each manual allocation branch in separate instances, and terminal completion. Assert exact state strings, versions, audits, no tasks/SLA, email routes, links, recipients, and terminal runtime status.

- [ ] **Step 2: Add the complete Bid path tests**

Execute Start/Submit, return/resubmit, scheduled IPG/Not Allocated classification, individual Seller review, and each terminal disposition in separate Bids. Assert parent Opportunity state never changes from Bid disposition.

- [ ] **Step 3: Add catch-up and dispatcher test**

Run the scheduler twice after an outage date, require close-first behavior and one occurrence per group, then process all pending rows with `RecordingNotificationSender`. Assert rendered subject/text/HTML, recipient snapshots, link tokens, Sent status, and zero retries.

- [ ] **Step 4: Add explicit excluded-column proof**

Set sentinel values in `TotalBid`, `LargestBid`, and `LowestBid` before the full scenario. Assert the exact sentinel values remain after submission, actions, closing, classification, notification queueing, and dispatch.

- [ ] **Step 5: Run end-to-end tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SqmsEndToEndTests|FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests"
```

Expected: all SQMS paths pass against isolated SQL Server.

- [ ] **Step 6: Commit**

```powershell
git add tests/WorkflowEngine.Tests/SqmsEndToEndTests.cs tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs tests/WorkflowEngine.Tests/CdtSqmsCompatibilityTests.cs
git commit -m "test(sqms): cover complete workflow paths"
```

## Task 13: Milestone 6 — run the full CDT compatibility gate

**Files:**
- Modify only if a regression test exposes an approved compatibility defect.
- Record results in: `CODEX_HANDOFF.md`

- [ ] **Step 1: Restore and build with warnings as errors**

```powershell
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror
```

Expected: zero warnings and zero errors.

- [ ] **Step 2: Run all default tests**

```powershell
dotnet test src/WorkflowEngine.sln -m:1
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx
```

Expected: zero failures; only existing opt-in SMTP tests may be skipped.

- [ ] **Step 3: Run database initialization and seed verification in an isolated database**

Use the fixed isolated acceptance database name below after verifying with `DB_ID` that it is not a pre-existing user database. If it already exists, stop and obtain a new explicitly approved isolated name rather than deleting it. Run:

```powershell
$sqmsAcceptanceDatabase = "workflowengine_sqms_acceptance_20260820"
$env:ConnectionStrings__DefaultConnection = "Server=DOLLY;Database=$sqmsAcceptanceDatabase;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
dotnet run --project src/WorkflowEngine.Console -- init-db
sqlcmd -b -S DOLLY -d $sqmsAcceptanceDatabase -i sql/sqms/001_sqms_notification_outbox_ddl.sql
sqlcmd -b -S DOLLY -d $sqmsAcceptanceDatabase -i sql/sqms/002_sqms_workflow_metadata_dml.sql -v 'SqmsOpenToRecipientsJson=["sqms-open@example.test"]' 'SqmsOpenCcRecipientsJson=["sqms-open-cc@example.test"]' 'SqmsSellerApprovalCcRecipientsJson=["sqms-seller-cc@example.test"]' 'SqmsBidDispositionCcRecipientsJson=["sqms-bid-cc@example.test"]'
sqlcmd -b -S DOLLY -d $sqmsAcceptanceDatabase -i sql/sqms/003_verify_sqms_deployment.sql
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

Record `workflowengine_sqms_acceptance_20260820` in `CODEX_HANDOFF.md`. Never point these commands at the production database.

- [ ] **Step 4: Execute the required CDT sample branches**

Run every command from `PROJECT.md` section 21 for Checker Return, Trading Desk Return, and `demo`. Then run the typed-recipient focused gates from section 21.

Expected: every command exits 0; existing state, task, SLA, notification, audit, and response semantics remain unchanged.

- [ ] **Step 5: Run negative compatibility searches**

```powershell
rg -n "DateTime\.UtcNow" src workflow-api
rg -n '"toState"|\bToState\b' src workflow-api samples
rg -n "RecipientRoleKey|RecipientUserId" src workflow-api samples docs tests
rg -n "TotalBid|LargestBid|LowestBid" src/WorkflowEngine.Sqms sql/sqms/001_sqms_notification_outbox_ddl.sql sql/sqms/002_sqms_workflow_metadata_dml.sql
```

Expected: no forbidden active-code use. Historical migrations may contain approved old recipient column names. The excluded calculated columns may appear only in testing table DDL, verifier schema checks, sentinel tests, design, and plan.

- [ ] **Step 6: Stop on any CDT regression**

Do not weaken or update an existing CDT assertion merely to make it pass. Diagnose the cause, apply the smallest backward-compatible fix, rerun the focused failing test, and rerun Steps 1-5.

- [ ] **Step 7: Commit verified compatibility fixes/results**

```powershell
git add CODEX_HANDOFF.md
git commit -m "test(sqms): verify CDT compatibility"
```

If source fixes were required, include only the approved focused files and describe them in the commit body.

## Task 14: Milestone 6 — deployment documentation and final verification

**Files:**
- Create: `docs/sqms/DBA-DEPLOYMENT.md`
- Modify: `README.md`
- Modify: `CODEX_HANDOFF.md`
- Modify: `docs/superpowers/specs/2026-08-20-sqms-thin-layer-integration-design.md` only to mark implementation status; do not rewrite approved requirements.

- [ ] **Step 1: Write DBA deployment instructions**

Document exact preflight, DDL, DML variable inputs, verifier, application deployment, smoke test, scheduler enablement, and rollback order. State the schema-modification lock risk and require the DBA to inspect table size/workload. State that production never runs `init-db`, `Database.Migrate()`, or `EnsureCreated()`.

- [ ] **Step 2: Document Windows Task Scheduler**

Document three triggers invoking the same `run-sqms-scheduled` command, **Do not start a new instance**, environment configuration, working directory, service identity, log capture, nonzero alerting, and the rule that scheduling remains disabled until CDT/SQMS gates pass.

- [ ] **Step 3: Update README additively**

Add the SQMS adapter/command/configuration overview without removing or changing existing CDT commands, samples, restore guidance, or notification documentation.

- [ ] **Step 4: Run documentation and final quality checks**

```powershell
dotnet test src/WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~ReadmeValidationTests|FullyQualifiedName~Section21QualityGateTests|FullyQualifiedName~SqmsEndToEndTests|FullyQualifiedName~CdtSqmsCompatibilityTests"
git diff --check
```

Expected: all tests pass and `git diff --check` prints nothing.

- [ ] **Step 5: Update handoff and produce the mandatory completion summary**

Record the exact database name, build/test counts, SQL verifier output, sample commands, changed files, open risks, and next-session prompt. Use:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

- [ ] **Step 6: Commit documentation**

```powershell
git add README.md CODEX_HANDOFF.md docs/sqms/DBA-DEPLOYMENT.md docs/superpowers/specs/2026-08-20-sqms-thin-layer-integration-design.md
git commit -m "docs(sqms): add production deployment handoff"
```

Stop after Milestone 6. Do not push, merge, deploy, run production SQL, or enable a production scheduled task without explicit user authorization.

## Final execution checklist

- [ ] Every task commit exists in order.
- [ ] Every milestone stop gate was reported before the next milestone began.
- [ ] No public HTTP/request/response contract changed.
- [ ] Existing DI and console call sites remain source-compatible.
- [ ] Production DDL/DML is entirely represented in `sql/sqms/001` and `002`.
- [ ] `SSQMS` tables were not modified by production scripts.
- [ ] CDT upgrade hashes and behavioral regression passed.
- [ ] SQMS scheduled processing stayed disabled until all gates passed.
- [ ] No excluded calculated-column behavior was introduced.
- [ ] Worktree is clean and local/remote status is reported accurately.
