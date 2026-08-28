# SQMS Bid Category Column Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make scheduled SQMS Bid classification read the authoritative `BidFairValue` category while preserving `BidExpressedAsFairValue` as percentage text and retaining compatibility with existing callers.

**Architecture:** Add `BidFairValue` as a non-positional property on the existing persistence row, append the column to SQL projections without shifting existing ordinals, and make the scheduler prefer that property with a compatibility fallback. Expand the existing parser to accept the three confirmed legacy labels without changing workflow transitions or classification arithmetic.

**Tech Stack:** .NET 10, C# 14, EF Core SQL Server, xUnit, PowerShell

---

### Task 1: Accept confirmed legacy Bid category labels

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsValueParserTests.cs:35-49`
- Modify: `src/WorkflowEngine.Sqms/SqmsValueParser.cs:43-67`

- [ ] **Step 1: Add failing parser examples**

Extend `ParseBidCategoryNormalizesApprovedValues` with the confirmed target labels:

```csharp
[InlineData("Yes, Fair Value", SqmsBidCategory.FairValue)]
[InlineData("  yes, fair value  ", SqmsBidCategory.FairValue)]
[InlineData("No, Premium", SqmsBidCategory.Premium)]
[InlineData("No, Discount", SqmsBidCategory.Discount)]
```

- [ ] **Step 2: Run the focused parser test and verify RED**

Run:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsValueParserTests.ParseBidCategoryNormalizesApprovedValues"
```

Expected: FAIL for the new legacy-label rows with `Bid category '<value>' is not recognized.` Existing canonical rows remain green.

- [ ] **Step 3: Implement minimal label normalization**

Update the three branches in `ParseBidCategory` so the whitespace-normalized strings accept canonical and confirmed legacy values:

```csharp
if (normalized.Equals("FairValue", StringComparison.OrdinalIgnoreCase) ||
    normalized.Equals("Yes,FairValue", StringComparison.OrdinalIgnoreCase))
{
    return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.FairValue);
}

if (normalized.Equals("Premium", StringComparison.OrdinalIgnoreCase) ||
    normalized.Equals("No,Premium", StringComparison.OrdinalIgnoreCase))
{
    return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.Premium);
}

if (normalized.Equals("Discount", StringComparison.OrdinalIgnoreCase) ||
    normalized.Equals("No,Discount", StringComparison.OrdinalIgnoreCase))
{
    return SqmsParseResult<SqmsBidCategory>.Succeeded(SqmsBidCategory.Discount);
}
```

- [ ] **Step 4: Run parser tests and verify GREEN**

Run:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsValueParserTests"
```

Expected: all `SqmsValueParserTests` pass; malformed and missing category cases remain failures at the application level and green test assertions.

- [ ] **Step 5: Commit the parser change**

```powershell
git add -- tests/WorkflowEngine.Tests/SqmsValueParserTests.cs src/WorkflowEngine.Sqms/SqmsValueParser.cs
git commit -m "fix(sqms): accept legacy Bid category labels"
```

### Task 2: Read category and percentage from their correct SQL columns

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsDataStoreTests.cs:55-69,127-135`
- Modify: `src/WorkflowEngine.Sqms/Persistence/SqmsRows.cs:15-25`
- Modify: `src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs:18-21,313-325`

- [ ] **Step 1: Add failing SQL mapping assertions**

Change the live SQL fixture column list and values so it represents the target schema accurately:

```sql
[AccountName], [AccountNumber], [RelationshipManager], [BidFairValue], [BidExpressedAsFairValue],
[Status], [WorkflowState], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted])
VALUES
    (10000, 10001, 'Buyer Team', 'Yes', '$500.00', 'Submitted Bidder', 'BA-1', 'Buyer RM', 'No, Premium', '5 %', 'SUBMITTED', 'SUBMITTED', 'bidder', '2030-01-15', 'bidder', '2030-01-15', 0),
    (10001, 10001, 'Buyer Team', 'Yes', '$600.00', 'Deleted Bidder', 'BA-2', NULL, 'No, Discount', '5 %', 'SUBMITTED', 'SUBMITTED', 'bidder', '2030-01-15', 'bidder', '2030-01-15', 1);
```

After loading Bid `10000`, add:

```csharp
Assert.Equal("No, Premium", bid.BidFairValue);
Assert.Equal("5 %", bid.BidExpressedAsFairValue);
```

After loading `submitted`, add the same two assertions.

- [ ] **Step 2: Run the SQL data-store test and verify RED**

Load the configured test connection and run:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = ((Get-Content -Raw src\WorkflowEngine.Console\appsettings.Development.json | ConvertFrom-Json).ConnectionStrings.DefaultConnection)
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsDataStoreTests.SqlStoreSupportsScheduledFourColumnProjectionOnly"
```

Expected: compilation fails because `SqmsBidRow` does not yet expose `BidFairValue`.

- [ ] **Step 3: Add the category property without changing the positional constructor**

Change the `SqmsBidRow` declaration to:

```csharp
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
    string? WorkflowState)
{
    public string? BidFairValue { get; init; }
}
```

- [ ] **Step 4: Append and populate the SQL category column**

Append `bid.[BidFairValue]` after all current `BidColumns` fields:

```csharp
private const string BidColumns = """
    bid.[ID], bid.[OpportunityId], bid.[BuyerRmteam], bid.[AccountName], bid.[AccountNumber],
    bid.[RelationshipManager], bid.[CommitmentAmountRequested], bid.[BidExpressedAsFairValue],
    bid.[Status], bid.[WorkflowState], bid.[BidFairValue]
    """;
```

Populate ordinal 10 without altering the existing constructor arguments:

```csharp
private static SqmsBidRow ReadBid(DbDataReader reader) =>
    new(
        reader.GetInt32(0),
        reader.GetInt32(1),
        NullableString(reader, 2),
        reader.GetString(3),
        reader.GetString(4),
        NullableString(reader, 5),
        NullableString(reader, 6),
        NullableString(reader, 7),
        NullableString(reader, 8),
        NullableString(reader, 9))
    {
        BidFairValue = NullableString(reader, 10)
    };
```

- [ ] **Step 5: Run the SQL data-store test and verify GREEN**

Run the Step 2 command again.

Expected: one passing `SqmsDataStoreTests.SqlStoreSupportsScheduledFourColumnProjectionOnly` test, proving `No, Premium` and `5 %` were read from distinct target columns.

- [ ] **Step 6: Commit the persistence mapping**

```powershell
git add -- tests/WorkflowEngine.Tests/SqmsDataStoreTests.cs src/WorkflowEngine.Sqms/Persistence/SqmsRows.cs src/WorkflowEngine.Sqms/Persistence/SqlSqmsDataStore.cs
git commit -m "fix(sqms): map authoritative Bid category column"
```

### Task 3: Classify from BidFairValue while preserving existing adapters

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs:85-112`
- Modify: `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs:88-101`

- [ ] **Step 1: Add a failing target-shaped scheduler regression**

Add this test after the existing threshold theory:

```csharp
[Fact]
public async Task BidClassificationUsesCategoryInsteadOfExpressedPercentage()
{
    var store = new FakeStore();
    store.AddClosed(Opportunity(10000, "$100.00", new DateTime(2030, 3, 31)));
    store.AddBid(Bid(20000, "$60.00", "100 %") with { BidFairValue = "Yes, Fair Value" });
    store.AddBid(Bid(20001, "$50.00", "5 %") with { BidFairValue = "No, Premium" });
    store.AddBid(Bid(20002, "$25.00", "5 %") with { BidFairValue = "No, Discount" });
    var workflow = new FakeWorkflowService(store);

    var result = await CreateService(OpenedAt, store, workflow, new FakeQueueService())
        .RunAsync(CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal(2, result.BidsMovedToIpgReview);
    Assert.Equal(1, result.BidsNotAllocated);
    Assert.Equal(
        [SqmsKeys.BidActions.MoveToIpgReview, SqmsKeys.BidActions.MoveToIpgReview, SqmsKeys.BidActions.MarkNotAllocated],
        workflow.BidActions.Select(request => request.ActionKey));
}
```

The existing threshold theory continues constructing rows with canonical labels in `BidExpressedAsFairValue`; it therefore serves as the compatibility-fallback regression.

- [ ] **Step 2: Run the target-shaped scheduler test and verify RED**

Run:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsScheduledServiceTests.BidClassificationUsesCategoryInsteadOfExpressedPercentage"
```

Expected: FAIL with three category failures for `100 %` and `5 %`, because the scheduler still reads `BidExpressedAsFairValue`.

- [ ] **Step 3: Prefer the authoritative category with a narrow fallback**

Replace the category parsing line with:

```csharp
var categoryInput = string.IsNullOrWhiteSpace(bid.BidFairValue)
    ? bid.BidExpressedAsFairValue
    : bid.BidFairValue;
var category = SqmsValueParser.ParseBidCategory(categoryInput);
```

Do not parse or normalize the percentage value and do not change any action-selection or threshold code.

- [ ] **Step 4: Run all scheduled-service tests and verify GREEN**

Run:

```powershell
dotnet test src\WorkflowEngine.sln -m:1 --no-restore --filter "FullyQualifiedName~SqmsScheduledServiceTests"
```

Expected: all scheduled-service tests pass, including the new target-shaped regression and the existing canonical-label fallback cases.

- [ ] **Step 5: Commit the scheduler correction**

```powershell
git add -- tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs
git commit -m "fix(sqms): classify Bids from category column"
```

### Task 4: Run regression and handoff checks

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run warning-as-error builds**

```powershell
dotnet build src\WorkflowEngine.sln -m:1 --no-restore -warnaserror
dotnet build workflow-api\src\WorkflowEngine.WorkflowApi\WorkflowEngine.WorkflowApi.csproj -m:1 --no-restore -warnaserror
```

Expected: both builds complete with zero warnings and zero errors.

- [ ] **Step 2: Run the complete supported regression suites**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = ((Get-Content -Raw src\WorkflowEngine.Console\appsettings.Development.json | ConvertFrom-Json).ConnectionStrings.DefaultConnection)
dotnet test src\WorkflowEngine.sln -m:1 --no-build --filter "Category!=Smtp"
dotnet test workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\WorkflowEngine.WorkflowApi.Tests.csproj -m:1 --no-build
```

Expected: all non-SMTP main and API tests pass. Record exact passed, failed, and skipped counts.

- [ ] **Step 3: Run seed and deployment verification without resetting target data**

```powershell
dotnet run --project src\WorkflowEngine.Console --no-build -- verify-seed
sqlcmd -S DOLLY -d workflowengine -E -b -i sql\sqms\003_verify_sqms_deployment.sql
```

Expected: generic seed verification and SQMS deployment verification pass. Do not run `init-db`, the retained one-shot acceptance launcher, or any target-database data rewrite.

- [ ] **Step 4: Record the correction in the handoff**

Append a dated `2026-08-28 SQMS Bid Category Column Correction` section to `CODEX_HANDOFF.md` containing:

```markdown
## Current goal

- Correct scheduled Bid classification to use `BidFairValue` category labels while retaining `BidExpressedAsFairValue` percentage text.

## Completed changes

- Added the authoritative category field to the internal Bid row without changing its positional constructor.
- Mapped both target columns and retained canonical-label fallback behavior.
- Added target-shaped parser, SQL mapping, and scheduled-classification regressions.

## Remaining tasks

- Deploy the corrected build to the target host and rerun the scheduled command so still-Submitted Bids are retried.
```

Fill in exact build/test/seed evidence and changed files. Explicitly state that no target data, migration, public contract, or workflow metadata changed.

- [ ] **Step 5: Validate and commit the handoff**

```powershell
git diff --check
git add -- CODEX_HANDOFF.md
git commit -m "docs(sqms): hand off Bid category correction"
git status --short --branch
```

Expected: the commit succeeds and the worktree is clean. Do not push without separate authorization.
