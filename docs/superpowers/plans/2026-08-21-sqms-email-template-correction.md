# SQMS Email Template Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the six inferred SQMS email templates with the approved SharePoint-compatible subjects, bodies, field layouts, links, and zero-padded dates while preserving workflow, recipient, scheduling, and public contracts.

**Architecture:** Keep notification content in existing versioned `NotificationRoute` metadata and continue rendering through the generic outbox pipeline. Make the smallest SQMS-specific code change required for `MM/dd/yyyy` template values, strengthen package/runtime tests, and add an optional live-acceptance profile so the corrected messages can be exercised in a new retained database without breaking the prior retained run.

**Tech Stack:** .NET 10, C#, xUnit, EF Core, SQL Server, SQLCMD DML/verifiers, MailKit sender, smtp4dev, PowerShell.

---

## Execution constraints

- Read `PROJECT.md` before execution and stop for every section 22 trigger.
- Work only in the existing `sqms_clean_slate` worktree.
- Use test-driven development: observe each focused RED before implementation.
- Do not rename route, notification, workflow, state, action, or role keys.
- Do not change recipient sources/order, scheduler offsets, URL ownership, public DTOs, or target-table mappings.
- Do not rewrite existing `NotificationOutbox` rows.
- Do not rerun or alter retained evidence in `workflowengine_sqms_live_acceptance` or `sqms-live-acceptance-20260821`.
- Use a new database and smtp4dev data directory for the corrected live run.
- Do not push, merge, deploy, execute production SQL, or enable Windows Task Scheduler.

## File responsibility map

| File | Responsibility |
|---|---|
| `sql/sqms/002_sqms_workflow_metadata_dml.sql` | Authoritative six route templates and existing recipient metadata |
| `sql/sqms/003_verify_sqms_deployment.sql` | Independent deployment-time verification of exact active route contracts |
| `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs` | Seller/Bid template token enrichment and zero-padded action-driven dates |
| `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs` | Open/reminder token enrichment and zero-padded response date |
| `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs` | Rerunnable DML and exact persisted-template contract |
| `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs` | Exact Seller/Bid subject, body, HTML, recipient, and link rendering |
| `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs` | Existing opened-date schedule plus exact response-date token |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs` | Backward-compatible default and optional isolated acceptance profile values |
| `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs` | SQL/outbox/smtp4dev proof including corrected content |
| `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs` | Default-profile compatibility and new-profile/script contracts |
| `scripts/run-sqms-email-template-live-acceptance.ps1` | One-shot corrected-template run in a new retained database/mailbox |
| `sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql` | Independent read-only verification for the new marker |
| `docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE.md` | Operator commands and inspection queries |
| `docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE-RESULTS-20260821.md` | Actual retained IDs, states, routes, and smtp4dev results |
| `CODEX_HANDOFF.md` | Durable completion and retained-evidence handoff |

### Task 1: Persist the exact six metadata templates

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs`
- Modify: `sql/sqms/002_sqms_workflow_metadata_dml.sql`
- Modify: `sql/sqms/003_verify_sqms_deployment.sql`

- [ ] **Step 1: Add a failing exact persisted-template test**

Extend `SqmsMetadataPackageIsRerunnableAndHasExactActiveCounts` after the count assertions. Add a helper that reads the six active routes and compare exact subjects plus distinguishing body/HTML content:

```csharp
var routes = await ReadSqmsRoutesAsync(connection);
Assert.Equal(6, routes.Count);

Assert.Equal(
    "{{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}",
    routes["SqmsOpportunityOpen"].Subject);
Assert.Equal(
    "REMINDER {{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}",
    routes["SqmsOpportunityReminder"].Subject);
Assert.Equal(
    "{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{accountNumber}} - {{accountName}}: Response Required by {{sellerResponseDate}}",
    routes["SqmsOpportunityPendingSellerApproval"].Subject);

foreach (var routeKey in new[] { "SqmsBidAccepted", "SqmsBidPartiallyAccepted", "SqmsBidNotAccepted" })
{
    Assert.Equal(
        "{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{buyerAccountNumber}} - {{buyerAccountName}}",
        routes[routeKey].Subject);
}

Assert.Contains("View all Opportunities open for Bidding", routes["SqmsOpportunityOpen"].HtmlBody, StringComparison.Ordinal);
Assert.Contains("Link to approve or reject bids received", routes["SqmsOpportunityPendingSellerApproval"].Body, StringComparison.Ordinal);
Assert.Contains("documentation required to effect this sale", routes["SqmsBidAccepted"].Body, StringComparison.Ordinal);
Assert.Contains("documentation required to effect this sale", routes["SqmsBidPartiallyAccepted"].Body, StringComparison.Ordinal);
Assert.DoesNotContain("Please confirm your buyer", routes["SqmsBidNotAccepted"].Body, StringComparison.Ordinal);
```

Add these test-only types/helpers:

```csharp
private sealed record RouteText(string Subject, string Body, string HtmlBody);

private static async Task<IReadOnlyDictionary<string, RouteText>> ReadSqmsRoutesAsync(SqlConnection connection)
{
    const string sql = """
        SELECT route.[RouteKey], route.[SubjectTemplate], route.[BodyTemplate], route.[HtmlBodyTemplate]
        FROM [dbo].[NotificationRoute] AS route
        INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
            ON version.[Id] = route.[WorkflowDefinitionVersionId]
        INNER JOIN [dbo].[WorkflowDefinition] AS definition
            ON definition.[Id] = version.[WorkflowDefinitionId]
        WHERE definition.[WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid')
          AND version.[VersionNumber] = 1
          AND route.[IsActive] = 1;
        """;
    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    var result = new Dictionary<string, RouteText>(StringComparer.Ordinal);
    while (await reader.ReadAsync())
    {
        result.Add(reader.GetString(0), new(reader.GetString(1), reader.GetString(2), reader.GetString(3)));
    }
    return result;
}
```

- [ ] **Step 2: Run the package test and confirm RED**

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --filter FullyQualifiedName~SqmsMetadataPackageIsRerunnableAndHasExactActiveCounts
```

Expected: FAIL because the persisted subjects and bodies still contain the inferred SQMS wording.

- [ ] **Step 3: Replace only the six route templates in the DML**

In `002_sqms_workflow_metadata_dml.sql`, declare a deterministic newline before `@Routes`:

```sql
DECLARE @CrLf nchar(2) = NCHAR(13) + NCHAR(10);
```

Replace the six `INSERT @Routes` values with the approved subjects. Build plain bodies with `CONCAT(..., @CrLf, ...)` so SQLCMD checkout line endings cannot change rendered content. Use equivalent HTML `<p>` blocks and `<a href="{{token}}">approved label</a>` links. The exact Accepted body expression is:

```sql
CONCAT(
 N'The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
 N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', @CrLf, @CrLf,
 N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.', @CrLf, @CrLf,
 N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
 N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
 N'Name of Fund: {{fund}}', @CrLf,
 N'Name of Vehicle: {{vehicle}}', @CrLf,
 N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
 N'Link to View: {{bidUrl}}')
```

For HTML, make only `Click Here` clickable:

```sql
N'<p>The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.</p><p>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>'
```

Use these exact remaining plain-body expressions:

```sql
-- Open and Reminder
CONCAT(
 N'Please click the following link(s) to review secondary sales opportunities for this quarter.', @CrLf, @CrLf,
 N'View all Opportunities open for Bidding: {{dashboardUrl}}')

-- Pending Seller Approval
CONCAT(
 N'Please click the following link to review secondary sale bids for the following opportunity.', @CrLf, @CrLf,
 N'Account Number: {{accountNumber}}', @CrLf,
 N'Account Name: {{accountName}}', @CrLf,
 N'Name of Fund: {{fund}}', @CrLf,
 N'Name of Vehicle: {{vehicle}}', @CrLf,
 N'Total Amount Requested to Be Sold: {{saleAmount}}', @CrLf, @CrLf,
 N'Link to approve or reject bids received: {{opportunityUrl}}')

-- Partially Accepted
CONCAT(
 N'The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
 N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', @CrLf, @CrLf,
 N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.', @CrLf, @CrLf,
 N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
 N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
 N'Name of Fund: {{fund}}', @CrLf,
 N'Name of Vehicle: {{vehicle}}', @CrLf,
 N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
 N'Link to View: {{bidUrl}}')

-- Not Accepted
CONCAT(
 N'The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
 N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
 N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
 N'Name of Fund: {{fund}}', @CrLf,
 N'Name of Vehicle: {{vehicle}}', @CrLf,
 N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
 N'Link to View: {{bidUrl}}')
```

Use these exact HTML structures:

```sql
-- Open and Reminder
N'<p>Please click the following link(s) to review secondary sales opportunities for this quarter.</p><p><a href="{{dashboardUrl}}">View all Opportunities open for Bidding</a></p>'

-- Pending Seller Approval
N'<p>Please click the following link to review secondary sale bids for the following opportunity.</p><p>Account Number: {{accountNumber}}<br>Account Name: {{accountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Sold: {{saleAmount}}</p><p>Link to approve or reject bids received: <a href="{{opportunityUrl}}">Click Here</a></p>'

-- Partially Accepted
N'<p>The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.</p><p>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>'

-- Not Accepted
N'<p>The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>'
```

Do not alter recipient-rule SQL or transition rows.

- [ ] **Step 4: Strengthen the independent SQL verifier**

In `003_verify_sqms_deployment.sql`, add an `@ExpectedRoutes` table with `RouteKey`, `SubjectTemplate`, and the required/forbidden body fragments. Throw if an active route is missing or differs. The key assertion pattern is:

```sql
IF EXISTS
(
    SELECT 1
    FROM @ExpectedRoutes AS expected
    LEFT JOIN [dbo].[NotificationRoute] AS route ON route.[RouteKey] = expected.[RouteKey]
    LEFT JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.[Id] = route.[WorkflowDefinitionVersionId]
    LEFT JOIN [dbo].[WorkflowDefinition] AS definition ON definition.[Id] = version.[WorkflowDefinitionId]
    WHERE route.[Id] IS NULL
       OR definition.[WorkflowKey] <> expected.[WorkflowKey]
       OR version.[VersionNumber] <> 1
       OR route.[IsActive] <> 1
       OR route.[SubjectTemplate] <> expected.[SubjectTemplate]
       OR route.[BodyTemplate] NOT LIKE N'%' + expected.[RequiredBodyFragment] + N'%'
       OR (expected.[ForbiddenBodyFragment] IS NOT NULL
           AND route.[BodyTemplate] LIKE N'%' + expected.[ForbiddenBodyFragment] + N'%')
)
    THROW 51220, 'Missing or incorrect SQMS notification template contract.', 1;
```

- [ ] **Step 5: Run focused package verification**

Run the test command from Step 2 again.

Expected: PASS; `003_verify_sqms_deployment.sql` also executes inside the test and prints the existing deployment-verification success message.

- [ ] **Step 6: Commit Task 1**

```powershell
git add tests/WorkflowEngine.Tests/SqmsDatabasePackageTests.cs sql/sqms/002_sqms_workflow_metadata_dml.sql sql/sqms/003_verify_sqms_deployment.sql
git diff --cached --check
git commit -m "fix(sqms): correct notification templates"
```

### Task 2: Render zero-padded dates without changing scheduling

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`
- Modify: `src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs`
- Modify: `src/WorkflowEngine.Sqms/SqmsWorkflowService.cs`

- [ ] **Step 1: Add failing date-token assertions**

In `GroupNotificationsUseStableKeysAndReplayWithoutDuplicates`, add:

```csharp
Assert.All(queue.Requests, request =>
    Assert.Equal("01/16/2030", request.NotificationInput.TemplateData["responseDate"]));
```

Update the existing action-driven assertions:

```csharp
Assert.Contains("01/21/2030", row.Body, StringComparison.Ordinal);
Assert.Contains("03/31/2030", row.Body, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the focused tests and confirm RED**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~SqmsScheduledServiceTests.GroupNotificationsUseStableKeysAndReplayWithoutDuplicates|FullyQualifiedName~SqmsWorkflowServiceTests.SellerApprovalEmailUsesTrustedOpportunityDataResolvedRecipientsAndConfiguredLink|FullyQualifiedName~SqmsWorkflowServiceTests.BidDispositionEmailsUseDistinctRoutesAndTrustedBidAndParentData"
```

Expected: FAIL with actual values `1/16/2030`, `1/21/2030`, or `3/31/2030`.

- [ ] **Step 3: Make the minimal formatting changes**

In `SqmsScheduledService.QueueGroupsAsync`, change only:

```csharp
["responseDate"] = responseDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture),
```

In `SqmsWorkflowService.FormatDate`, change only the format string:

```csharp
private static string FormatDate(DateOnly value) =>
    value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
```

Do not change `openedDate`, offsets, grouping, `sellerResponseDate`, or effective-date selection.

- [ ] **Step 4: Run the focused tests and confirm GREEN**

Run the command from Step 2.

Expected: all selected tests pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add tests/WorkflowEngine.Tests/SqmsScheduledServiceTests.cs tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs src/WorkflowEngine.Sqms/Scheduling/SqmsScheduledService.cs src/WorkflowEngine.Sqms/SqmsWorkflowService.cs
git diff --cached --check
git commit -m "fix(sqms): zero-pad notification dates"
```

### Task 3: Prove exact rendered Seller and Bid messages

**Files:**
- Modify: `tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs`

- [ ] **Step 1: Replace loose body checks with exact message contracts**

For Seller approval, assert:

```csharp
Assert.Equal(
    "Q1 2030 Secondary Sale Bids - Vehicle One/OA-1 - Opportunity One: Response Required by 01/21/2030",
    row.Subject);
Assert.Equal(
    string.Join("\r\n",
        "Please click the following link to review secondary sale bids for the following opportunity.",
        "",
        "Account Number: OA-1",
        "Account Name: Opportunity One",
        "Name of Fund: Fund One",
        "Name of Vehicle: Vehicle One",
        "Total Amount Requested to Be Sold: $1,250.00",
        "",
        "Link to approve or reject bids received: https://dashboard.example.test/bids?view=seller&opportunityId=10000"),
    row.Body);
Assert.Contains(">Click Here</a>", row.HtmlBody, StringComparison.Ordinal);
```

For the Bid theory, add expected opening/follow-up parameters to each `InlineData`, then assert the shared subject and exact structural rules:

```csharp
Assert.Equal("Q1 2030 Secondary Sale Bids - Vehicle One/BA-1 - Bid One", row.Subject);
Assert.StartsWith(expectedOpening + "\r\n", row.Body, StringComparison.Ordinal);
Assert.Contains("Buyer Account Number: BA-1", row.Body, StringComparison.Ordinal);
Assert.Contains("Total Amount Requested to Be Purchased: $500.00", row.Body, StringComparison.Ordinal);
Assert.EndsWith(
    "Link to View: https://forms.example.test/bid?mode=edit&bidId=10000",
    row.Body,
    StringComparison.Ordinal);
Assert.Equal(expectFollowUp, row.Body.Contains("Please confirm your buyer", StringComparison.Ordinal));
Assert.Equal(expectFollowUp, row.Body.Contains("documentation required to effect this sale", StringComparison.Ordinal));
Assert.Contains(">Click Here</a>", row.HtmlBody, StringComparison.Ordinal);
```

Use these opening sentences:

```text
The bid placed for the following opportunity has been accepted by the Seller for an effective date of 03/31/2030.
The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of 03/31/2030.
The bid placed for the following opportunity has not been accepted by the Seller for an effective date of 03/31/2030.
```

- [ ] **Step 2: Run the tests and confirm RED if any template detail is incomplete**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~SellerApprovalEmailUsesTrustedOpportunityDataResolvedRecipientsAndConfiguredLink|FullyQualifiedName~BidDispositionEmailsUseDistinctRoutesAndTrustedBidAndParentData"
```

Expected before all Task 1 template details are correct: at least one exact subject/body/HTML assertion fails. If Task 1 already makes every assertion pass, record that the new assertions are GREEN against the completed metadata change; do not introduce a production change solely to manufacture another RED.

- [ ] **Step 3: Correct only mismatched metadata text**

If Step 2 exposes punctuation, paragraph, label, or HTML-link differences, adjust only the corresponding route literal in `002_sqms_workflow_metadata_dml.sql`. Do not change renderer code.

- [ ] **Step 4: Run Seller/Bid and renderer regressions**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter "FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~NotificationTemplateRendererTests"
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit Task 3**

```powershell
git add tests/WorkflowEngine.Tests/SqmsWorkflowServiceTests.cs sql/sqms/002_sqms_workflow_metadata_dml.sql
git diff --cached --check
git commit -m "test(sqms): pin rendered email contracts"
```

### Task 4: Add a backward-compatible corrected-email live profile

**Files:**
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs`
- Modify: `tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs`
- Modify: `tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs`
- Create: `scripts/run-sqms-email-template-live-acceptance.ps1`
- Create: `sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql`

- [ ] **Step 1: Add failing profile and script contract tests**

Add pure profile parsing to the contract test without mutating process-global environment:

```csharp
[Fact]
public void AcceptanceProfileDefaultsPreserveThePriorRetainedRun()
{
    var profile = AcceptanceProfile.FromValues(null, null, null);
    Assert.Equal("workflowengine_sqms_live_acceptance", profile.DatabaseName);
    Assert.Equal("SQMS-LIVE-20260821", profile.RunMarker);
    Assert.Equal(new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero), profile.OpeningUtc);
}

[Fact]
public void AcceptanceProfileAcceptsAnExplicitIsolatedRun()
{
    var profile = AcceptanceProfile.FromValues(
        "workflowengine_sqms_email_live_acceptance",
        "SQMS-EMAIL-LIVE-20260821",
        "2030-01-01T15:00:00Z");
    Assert.Equal("workflowengine_sqms_email_live_acceptance", profile.DatabaseName);
    Assert.Equal("SQMS-EMAIL-LIVE-20260821", profile.RunMarker);
}
```

Add script assertions:

```csharp
Assert.Contains("workflowengine_sqms_email_live_acceptance", script, StringComparison.Ordinal);
Assert.Contains("SQMS-EMAIL-LIVE-20260821", script, StringComparison.Ordinal);
Assert.Contains("sqms-email-live-acceptance-20260821", script, StringComparison.Ordinal);
Assert.Contains("006_verify_sqms_email_template_live_acceptance.sql", script, StringComparison.Ordinal);
Assert.DoesNotContain("workflowengine_sqms_live_acceptance;", script, StringComparison.Ordinal);
```

- [ ] **Step 2: Run the contract suite and confirm RED**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
```

Expected: compile failure because `AcceptanceProfile` and the new script do not exist.

- [ ] **Step 3: Add the optional profile while preserving defaults**

In `AcceptanceConstants.cs`, add `using System.Globalization;` and:

```csharp
public sealed record AcceptanceProfile(string DatabaseName, string RunMarker, DateTimeOffset OpeningUtc)
{
    public static AcceptanceProfile FromEnvironment() => FromValues(
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_DATABASE"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_MARKER"),
        Environment.GetEnvironmentVariable("SQMS_LIVE_ACCEPTANCE_OPENING_UTC"));

    public static AcceptanceProfile FromValues(string? database, string? marker, string? openingUtc) => new(
        string.IsNullOrWhiteSpace(database) ? "workflowengine_sqms_live_acceptance" : database,
        string.IsNullOrWhiteSpace(marker) ? "SQMS-LIVE-20260821" : marker,
        string.IsNullOrWhiteSpace(openingUtc)
            ? new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.Parse(openingUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal));
}
```

Initialize one immutable process profile and keep existing call sites source-compatible:

```csharp
private static readonly AcceptanceProfile Profile = AcceptanceProfile.FromEnvironment();
public static string DatabaseName => Profile.DatabaseName;
public static string RunMarker => Profile.RunMarker;
public static DateTimeOffset OpeningUtc => Profile.OpeningUtc;
```

Keep count constants and actors unchanged. Existing no-variable runs and `--verify-only` continue targeting the prior retained evidence.

- [ ] **Step 4: Add corrected-content checks to the evidence verifier**

Only when `AcceptanceConstants.RunMarker` equals `SQMS-EMAIL-LIVE-20260821`, require zero rows from a SQL mismatch query after the route-count checks. This condition preserves `--verify-only` against the prior retained mailbox, whose immutable messages intentionally contain the earlier wording. Use:

```sql
SELECT COUNT(*)
FROM [dbo].[NotificationOutbox]
WHERE ([NotificationType] = N'SqmsOpportunityOpen'
       AND ([Subject] NOT LIKE N'Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030'
            OR [Body] NOT LIKE N'%View all Opportunities open for Bidding%'))
   OR ([NotificationType] = N'SqmsOpportunityReminder'
       AND [Subject] NOT LIKE N'REMINDER Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030')
   OR ([NotificationType] = N'SqmsOpportunityPendingSellerApproval'
       AND ([Subject] NOT LIKE N'Q1 2030 Secondary Sale Bids - %: Response Required by 01/22/2030'
            OR [Body] NOT LIKE N'%Link to approve or reject bids received:%'))
   OR ([NotificationType] = N'SqmsBidAccepted'
       AND [Body] NOT LIKE N'%has been accepted by the Seller for an effective date of 03/31/2030.%')
   OR ([NotificationType] = N'SqmsBidPartiallyAccepted'
       AND [Body] NOT LIKE N'%has been partially accepted by the Seller for an effective date of 03/31/2030.%')
   OR ([NotificationType] = N'SqmsBidNotAccepted'
       AND ([Body] NOT LIKE N'%has not been accepted by the Seller for an effective date of 03/31/2030.%'
            OR [Body] LIKE N'%Please confirm your buyer%'));
```

The existing RFC822 comparison continues proving smtp4dev received the same subject, recipients, bodies, and links stored in the outbox.

- [ ] **Step 5: Create the new one-shot runner**

Copy the safety structure of `run-sqms-live-acceptance.ps1`, but pin only these new values:

```powershell
$acceptanceDatabase = 'workflowengine_sqms_email_live_acceptance'
$acceptanceConnection = 'Server=DOLLY;Database=workflowengine_sqms_email_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$smtpDataDirectory = 'C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821'
$env:SQMS_LIVE_ACCEPTANCE_DATABASE = $acceptanceDatabase
$env:SQMS_LIVE_ACCEPTANCE_MARKER = 'SQMS-EMAIL-LIVE-20260821'
$env:SQMS_LIVE_ACCEPTANCE_OPENING_UTC = '2030-01-01T15:00:00Z'
```

Retain the absent-database, absent-data-directory, free-port, hidden-window, no-overwrite, build, DDL, DML, seed, smtp4dev, compiled-run, and SQL-verifier guards. Invoke `006_verify_sqms_email_template_live_acceptance.sql` at the end.

- [ ] **Step 6: Create the independent read-only SQL verifier**

Copy the structural checks from `005_verify_sqms_live_acceptance.sql`, replace only the marker with `SQMS-EMAIL-LIVE-20260821`, and add the corrected-content mismatch query from Step 4. Preserve the read-only prohibition: no `INSERT`, `UPDATE`, `DELETE`, `DROP`, or DDL.

- [ ] **Step 7: Run focused contracts and build**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore --filter FullyQualifiedName~SqmsLiveAcceptanceContractTests
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-restore -warnaserror
```

Expected: all contract tests pass; build has 0 warnings and 0 errors.

- [ ] **Step 8: Commit Task 4**

```powershell
git add tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceConstants.cs tools/WorkflowEngine.Sqms.LiveAcceptance/AcceptanceEvidenceVerifier.cs tests/WorkflowEngine.Tests/SqmsLiveAcceptanceContractTests.cs scripts/run-sqms-email-template-live-acceptance.ps1 sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql
git diff --cached --check
git commit -m "test(sqms): add corrected email live profile"
```

### Task 5: Execute and retain the corrected live email evidence

**Files:**
- Create: `docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE.md`
- Create: `docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE-RESULTS-20260821.md`

- [ ] **Step 1: Document operator and inspection commands**

Document the new database, marker, smtp ports/UI, data directory, one-shot command, `--verify-only` environment variables, independent SQL verifier, focused target/workflow/outbox queries, and process-management safety. Explicitly state that the old retained run remains untouched.

- [ ] **Step 2: Run safe preflight checks**

```powershell
sqlcmd -S DOLLY -d master -E -C -b -h -1 -W -Q "SET NOCOUNT ON; SELECT COALESCE(CONVERT(varchar(20), DB_ID(N'workflowengine_sqms_email_live_acceptance')), 'NULL');"
Test-Path 'C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821'
Get-NetTCPConnection -LocalPort 3025,5000 -ErrorAction SilentlyContinue
git status --short
```

Expected: database ID `NULL`, data path `False`, ports free, and clean worktree. If the existing retained smtp4dev process is still using the ports, stop only its verified PID without deleting its data, then continue.

- [ ] **Step 3: Execute the compiled one-shot runner**

```powershell
& .\scripts\run-sqms-email-template-live-acceptance.ps1
```

Expected final lines:

```text
SQMS LIVE ACCEPTANCE PASS
SQMS email-template live acceptance database verification passed.
```

Do not rerun the script after it creates evidence. Diagnose retained state and use verification-only mode if a verifier-only defect appears.

- [ ] **Step 4: Independently re-run database and compiled verification**

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql
```

Set the three profile variables plus the same SQMS/SMTP configuration used by the runner, then execute:

```powershell
dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build -- --verify-only
```

Expected: all three checks pass without inserting or replaying data.

- [ ] **Step 5: Record exact retained evidence**

In the results document record generated Opportunity/Bid IDs, final target/workflow states and versions, route counts, 19 Sent/zero-retry outbox rows, 19 smtp4dev messages, corrected subject examples, link parameters, 82 audits, zero tasks/SLA schedules, sentinel preservation, database name, smtp4dev PID, and data directory. Record any verifier-only corrections transparently.

- [ ] **Step 6: Commit Task 5**

```powershell
git add docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE.md docs/sqms/SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE-RESULTS-20260821.md
git diff --cached --check
git commit -m "test(sqms): record corrected email evidence"
```

### Task 6: Run the final backward-compatibility gate and hand off

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run warning-as-error builds**

```powershell
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror
dotnet build tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -warnaserror
```

Expected: every build succeeds with 0 warnings and 0 errors. Do not use bare `dotnet build` at repository root because no solution file exists there.

- [ ] **Step 2: Run full regression suites once, not concurrently**

Set `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` to the approved local SQL Server source connection, then run sequentially:

```powershell
dotnet test src/WorkflowEngine.sln --no-build
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build
```

Expected: zero failures. The four opt-in core SMTP tests may remain skipped; the corrected SMTP path is covered by the retained live run.

- [ ] **Step 3: Run the exact PROJECT section 21 compatibility subset**

Against a new disposable database, run `init-db`, `verify-seed`, Checker Return to Maker, Trading Return to Maker, the 14-step `demo`, typed public API tests, and sample/inspection tests exactly as listed in `PROJECT.md` section 21. Remove only that verified disposable database afterward; retain both SQMS live-evidence databases.

- [ ] **Step 4: Re-run final SQMS verification**

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-build --filter "FullyQualifiedName~SqmsDatabasePackageTests|FullyQualifiedName~SqmsWorkflowServiceTests|FullyQualifiedName~SqmsScheduledServiceTests|FullyQualifiedName~SqmsLiveAcceptanceContractTests"
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql
git diff --check
```

Expected: every test and SQL verifier passes; Git whitespace check is clean.

- [ ] **Step 5: Update the durable handoff**

Add a top `2026-08-21 SQMS Email Template Correction` section to `CODEX_HANDOFF.md` containing:

- current goal and approval boundary;
- commits and changed files;
- exact template/date/recipient/link behavior;
- builds, full regressions, section 21 samples, SQL, and smtp4dev results;
- retained database/mailbox locations;
- known risks and the instruction not to replay the one-shot runner;
- no push/merge/deployment/production SQL/scheduler authorization.

- [ ] **Step 6: Commit Task 6**

```powershell
git add CODEX_HANDOFF.md
git diff --cached --check
git commit -m "docs(sqms): hand off corrected email templates"
```

- [ ] **Step 7: Confirm final branch/evidence state**

```powershell
git status --porcelain=v1
git log -8 --oneline
sqlcmd -S DOLLY -d master -E -C -b -W -s '|' -Q "SET NOCOUNT ON; SELECT name,state_desc FROM sys.databases WHERE name IN (N'workflowengine_sqms_live_acceptance',N'workflowengine_sqms_email_live_acceptance') ORDER BY name;"
```

Expected: clean worktree, both retained evidence databases online, corrected smtp4dev mailbox available, and no push or merge performed.
