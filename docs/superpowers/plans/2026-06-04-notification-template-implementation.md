# Notification Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add generic route-specific email templates rendered from external API notification context, with SLA overdue notifications reusing the stored task context and prefixing the subject.

**Architecture:** Store templates in SQL workflow metadata, pass business notification values as a generic dictionary on the workflow action request, render subject/body before outbox persistence, and store the task's notification route/context so the SLA hosted process can reuse it without calling the external API. Keep Directed Trade wording in Portfolio Trade seed data only; reusable engine code stays metadata-driven.

**Tech Stack:** .NET 10, EF Core, SQL Server, xUnit, MailKit, System.Text.Json.

---

## File Map

**Contracts and API adapters**
- Modify `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs` to add `WorkflowActionBlock.NotificationContext`.
- Modify `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs` to add `StartWorkflowRequest.NotificationContext`.
- Modify `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs` so context changes affect idempotency request hash.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs` to accept optional `NotificationContext`.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs` to accept optional `NotificationContext`.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs` to map API context into the internal envelope.
- Modify `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs` to map start API context into the application start request.
- Modify `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs` to verify the mapping.
- Modify `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs` to verify initial action context mapping.

**Template rendering**
- Create `src/WorkflowEngine.Application/Notifications/NotificationTemplateRenderer.cs`.
- Create `tests/WorkflowEngine.Tests/NotificationTemplateRendererTests.cs`.

**Persistence and metadata**
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRoute.cs` with `BodyTemplate` and `OverdueSubjectPrefix`.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs` with `Body`.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs` with `NotificationRouteKey` and `NotificationContextJson`.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` mappings.
- Create migration `src/WorkflowEngine.Infrastructure/Migrations/20260604120000_AddNotificationTemplates.cs`.
- Update EF model snapshot/designer files after the model changes.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` with approved route-specific templates.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`.
- Modify `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`.
- Modify `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`.

**Execution and SLA**
- Modify `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` to validate/render templates, store task context, and persist outbox body.
- Modify `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs` to pass `StartWorkflowRequest.NotificationContext` into internally executed initial actions.
- Modify `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs` to render overdue notifications from stored task context.
- Modify `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`.
- Modify `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs` constructor helpers.
- Modify `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`.

**SMTP and outbox delivery**
- Modify `src/WorkflowEngine.Application/Notifications/INotificationSender.cs` to include body in `NotificationDelivery`.
- Modify `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`.
- Modify `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs` to send persisted body when present.
- Modify `tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs`.

**Samples and docs**
- Modify all notification-producing sample JSON files under `samples/workflow-actions/`.
- Modify `workflow-api/WORKFLOW_API_BOUNDARIES.md` to record that `notificationContext` is allowed as workflow notification data and is not form payload.

---

### Task 1: Add Pure Template Renderer

**Files:**
- Create: `src/WorkflowEngine.Application/Notifications/NotificationTemplateRenderer.cs`
- Create: `tests/WorkflowEngine.Tests/NotificationTemplateRendererTests.cs`

- [ ] **Step 1: Write failing renderer tests**

Create `tests/WorkflowEngine.Tests/NotificationTemplateRendererTests.cs`:

```csharp
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Tests;

public sealed class NotificationTemplateRendererTests
{
    [Fact]
    public void RenderReplacesTokensWithContextValues()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Hello {{submitterDisplayName}}, account {{accountShortName}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["submitterDisplayName"] = "Kollipara, Kartheek",
                ["accountShortName"] = "EFG"
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.True(result.Success);
        Assert.Equal("Hello Kollipara, Kartheek, account EFG", result.Rendered);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RenderReportsMissingTokens()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Account {{accountShortName}} for {{deskName}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["accountShortName"] = "EFG"
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.False(result.Success);
        Assert.Equal("Account EFG for {{deskName}}", result.Rendered);
        var error = Assert.Single(result.Errors);
        Assert.Equal("workflow.notificationContext.deskName", error.Field);
        Assert.Equal("Template token 'deskName' requires workflow.notificationContext.deskName.", error.Message);
    }

    [Fact]
    public void RenderTreatsWhitespaceValueAsMissing()
    {
        var renderer = new NotificationTemplateRenderer();

        var result = renderer.Render(
            "Sharepoint link = {{tradeLink}}",
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["tradeLink"] = " "
            },
            fieldPrefix: "workflow.notificationContext");

        Assert.False(result.Success);
        Assert.Equal("workflow.notificationContext.tradeLink", Assert.Single(result.Errors).Field);
    }
}
```

- [ ] **Step 2: Run renderer tests to verify they fail**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter NotificationTemplateRendererTests /p:UseAppHost=false
```

Expected: build fails because `NotificationTemplateRenderer` does not exist.

- [ ] **Step 3: Implement renderer**

Create `src/WorkflowEngine.Application/Notifications/NotificationTemplateRenderer.cs`:

```csharp
using System.Text.RegularExpressions;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Application.Notifications;

public sealed partial class NotificationTemplateRenderer
{
    public NotificationTemplateRenderResult Render(
        string template,
        IReadOnlyDictionary<string, string?> values,
        string fieldPrefix)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPrefix);

        var errors = new List<WorkflowValidationError>();
        var rendered = TokenRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new WorkflowValidationError(
                    $"{fieldPrefix}.{key}",
                    $"Template token '{key}' requires {fieldPrefix}.{key}."));
                return match.Value;
            }

            return value.Trim();
        });

        return new NotificationTemplateRenderResult(errors.Count == 0, rendered, errors);
    }

    [GeneratedRegex(@"\{\{(?<key>[A-Za-z0-9_]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}

public sealed record NotificationTemplateRenderResult(
    bool Success,
    string Rendered,
    IReadOnlyList<WorkflowValidationError> Errors);
```

- [ ] **Step 4: Run renderer tests to verify they pass**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter NotificationTemplateRendererTests /p:UseAppHost=false
```

Expected: `3 passed`.

- [ ] **Step 5: Commit**

```powershell
git add src\WorkflowEngine.Application\Notifications\NotificationTemplateRenderer.cs tests\WorkflowEngine.Tests\NotificationTemplateRendererTests.cs
git commit -m "feat: add notification template renderer"
```

---

### Task 2: Add Notification Template Schema and Seed Metadata

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRoute.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260604120000_AddNotificationTemplates.cs`
- Modify: EF migration designer and snapshot files under `src/WorkflowEngine.Infrastructure/Migrations/`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- Test: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [ ] **Step 1: Add failing seed metadata assertions**

Add this test to `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`:

```csharp
[Fact]
public void NotificationRoutes_HaveSubjectBodyAndOverduePrefix()
{
    using var dbContext = WorkflowExecutionValidationTests.CreateSeededContext();
    var routes = dbContext.NotificationRoutes.ToDictionary(row => row.RouteKey, StringComparer.Ordinal);

    var submit = routes["RouteCheckerAssignedOnSubmit"];
    Assert.Equal("Directed Trade Pending Checker Approval", submit.SubjectTemplate);
    Assert.Contains("{{submitterDisplayName}}", submit.BodyTemplate, StringComparison.Ordinal);
    Assert.Equal("Overdue: ", submit.OverdueSubjectPrefix);

    var returned = routes["RouteReturnedToMakerFromChecker"];
    Assert.Equal("Updates Required to Directed Trade Request", returned.SubjectTemplate);
    Assert.DoesNotContain("returnReason", returned.BodyTemplate, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run focused seed test to verify it fails**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter NotificationRoutes_HaveSubjectBodyAndOverduePrefix /p:UseAppHost=false
```

Expected: build fails because `BodyTemplate` and `OverdueSubjectPrefix` do not exist.

- [ ] **Step 3: Add entity properties**

Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRoute.cs`:

```csharp
namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationRoute
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string RouteKey { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? RecipientRoleKey { get; set; }
    public string? RecipientUserId { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string OverdueSubjectPrefix { get; set; } = "Overdue: ";
    public bool IsActive { get; set; }
}
```

Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`:

```csharp
namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationOutbox
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public long? WorkflowTaskId { get; set; }
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? RecipientRoleKey { get; set; }
    public string? RecipientUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? ProcessingToken { get; set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
}
```

Modify `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs`:

```csharp
namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTask
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string TaskKey { get; set; } = string.Empty;
    public string AssignedRoleKey { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public string? NotificationRouteKey { get; set; }
    public string? NotificationContextJson { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public string? ClosedByActionKey { get; set; }
}
```

- [ ] **Step 4: Add EF mappings**

In `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`, update `NotificationRoute` mapping:

```csharp
entity.Property(row => row.SubjectTemplate).HasMaxLength(300).IsRequired();
entity.Property(row => row.BodyTemplate).IsRequired();
entity.Property(row => row.OverdueSubjectPrefix).HasMaxLength(100).IsRequired();
```

In `NotificationOutbox` mapping, add:

```csharp
entity.Property(row => row.Body).IsRequired();
```

In `WorkflowTask` mapping, add:

```csharp
entity.Property(row => row.NotificationRouteKey).HasMaxLength(100);
entity.Property(row => row.NotificationContextJson);
```

- [ ] **Step 5: Add migration**

Create `src/WorkflowEngine.Infrastructure/Migrations/20260604120000_AddNotificationTemplates.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowEngine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyTemplate",
                table: "NotificationRoute",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OverdueSubjectPrefix",
                table: "NotificationRoute",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Overdue: ");

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "NotificationOutbox",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NotificationRouteKey",
                table: "WorkflowTask",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationContextJson",
                table: "WorkflowTask",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyTemplate",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "OverdueSubjectPrefix",
                table: "NotificationRoute");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "NotificationOutbox");

            migrationBuilder.DropColumn(
                name: "NotificationRouteKey",
                table: "WorkflowTask");

            migrationBuilder.DropColumn(
                name: "NotificationContextJson",
                table: "WorkflowTask");
        }
    }
}
```

Update the migration designer and `WorkflowDbContextModelSnapshot.cs` to include the same five properties. Use the existing migration designer style in `20260601175951_AddWorkflowTaskAssignmentRoles.Designer.cs`.

- [ ] **Step 6: Update seed record and route seed data**

In `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`, replace the notification route array with:

```csharp
public static readonly NotificationRouteSeed[] NotificationRoutes =
[
    new(
        "RouteCheckerAssignedOnSubmit",
        "WorkflowTaskAssigned",
        "Checker",
        "Directed Trade Pending Checker Approval",
        """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following
        Sharepoint link = {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteCheckerAssignedOnResubmit",
        "WorkflowTaskAssigned",
        "Checker",
        "Directed Trade Pending Checker Approval",
        """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following
        Sharepoint link = {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteReturnedToMakerFromChecker",
        "WorkflowReturnedToMaker",
        "Maker",
        "Updates Required to Directed Trade Request",
        """
        Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
        This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
        The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteTradingDeskAssigned",
        "WorkflowTaskAssigned",
        null,
        "Directed Trade Request Pending Execution",
        """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteReturnedToMakerFromTradingDesk",
        "WorkflowReturnedToMaker",
        "Maker",
        "Updates Required to Directed Trade Request",
        """
        Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
        This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
        The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteBdrmAssigned",
        "WorkflowTaskAssigned",
        "BDRMClientService",
        "Trade Execution Pending Confirmation",
        """
        This is to confirm that the trade request for account {{accountShortName}}, {{accountDisplayName}} has been completed by trader {{traderDisplayName}} on the {{deskName}} desk for security type {{securityType}}.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteWorkflowCompleted",
        "WorkflowCompleted",
        "Maker",
        "Confirmation that Trade settled in account",
        """
        This is to confirm that {{confirmerDisplayName}} has verified that the trade was executed and settled in account {{accountShortName}}, {{accountDisplayName}}. The trade request on Sharepoint is now closed.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteTradingDeskReworkAssigned",
        "WorkflowTaskAssigned",
        "TradingDesk",
        "Directed Trade Request Pending Rework",
        """
        This is to advise that the trade request for account {{accountShortName}}, {{accountDisplayName}} was returned to the {{deskName}} desk for rework.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """,
        "Overdue: "),
    new(
        "RouteSlaTaskOverdue",
        "SlaTaskOverdue",
        null,
        "Portfolio Trade workflow task is overdue",
        """
        Workflow task {{taskKey}} for workflow {{workflowInstanceId}} is overdue.
        """,
        "Overdue: ")
];
```

Replace the `NotificationRouteSeed` record with:

```csharp
internal sealed record NotificationRouteSeed(
    string Key,
    string NotificationType,
    string? RecipientRoleKey,
    string SubjectTemplate,
    string BodyTemplate,
    string OverdueSubjectPrefix);
```

- [ ] **Step 7: Update seed upsert and verification**

In `WorkflowSeedService.UpsertNotificationRoutes`, after `row.SubjectTemplate = seed.SubjectTemplate;`, add:

```csharp
row.BodyTemplate = seed.BodyTemplate;
row.OverdueSubjectPrefix = seed.OverdueSubjectPrefix;
```

In `SeedVerificationService.VerifyNotificationRoutes`, extend the comparison:

```csharp
if (!string.Equals(row.NotificationType, seed.NotificationType, StringComparison.Ordinal)
    || !string.Equals(row.RecipientRoleKey, seed.RecipientRoleKey, StringComparison.Ordinal)
    || !string.Equals(row.SubjectTemplate, seed.SubjectTemplate, StringComparison.Ordinal)
    || !string.Equals(row.BodyTemplate, seed.BodyTemplate, StringComparison.Ordinal)
    || !string.Equals(row.OverdueSubjectPrefix, seed.OverdueSubjectPrefix, StringComparison.Ordinal))
{
    errors.Add($"Notification route '{seed.Key}' does not match required template metadata.");
}
```

- [ ] **Step 8: Run focused seed test**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "SeedDataSmokeTests|NotificationRoutes_HaveSubjectBodyAndOverduePrefix" /p:UseAppHost=false
```

Expected: seed tests pass.

- [ ] **Step 9: Commit**

```powershell
git add src\WorkflowEngine.Infrastructure\Persistence\Entities\NotificationRoute.cs src\WorkflowEngine.Infrastructure\Persistence\Entities\NotificationOutbox.cs src\WorkflowEngine.Infrastructure\Persistence\Entities\WorkflowTask.cs src\WorkflowEngine.Infrastructure\Persistence\WorkflowDbContext.cs src\WorkflowEngine.Infrastructure\Migrations src\WorkflowEngine.Infrastructure\Persistence\Seed\PortfolioTradeSeedData.cs src\WorkflowEngine.Infrastructure\Persistence\Seed\WorkflowSeedService.cs src\WorkflowEngine.Infrastructure\Persistence\Verification\SeedVerificationService.cs tests\WorkflowEngine.Tests\SeedDataSmokeTests.cs
git commit -m "feat: add notification template metadata"
```

---

### Task 3: Pass Notification Context Through Contracts, Action API, and Start API

**Files:**
- Modify: `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`
- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`

- [ ] **Step 1: Add failing action API mapping test**

In `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`, extend the `WorkflowActionRequest` in `ExecuteWorkflowActionBuildsWorkflowOnlyExecutionRequest`:

```csharp
NotificationContext = new Dictionary<string, string?>
{
    ["submitterDisplayName"] = " Kollipara, Kartheek ",
    ["deskName"] = "Taxable",
    ["emptyValue"] = " "
}
```

Add assertions after `Assert.Equal("TradingDeskTaxable", envelope.Workflow?.AssignedRoleKey);`:

```csharp
Assert.Equal("Kollipara, Kartheek", envelope.Workflow?.NotificationContext?["submitterDisplayName"]);
Assert.Equal("Taxable", envelope.Workflow?.NotificationContext?["deskName"]);
Assert.False(envelope.Workflow?.NotificationContext?.ContainsKey("emptyValue") ?? true);
```

- [ ] **Step 2: Add failing start API mapping test**

In `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`, extend the `WorkflowStartRequest` in `StartWorkflowPassesInitialActionAndActorEvidenceToApplicationStart`:

```csharp
NotificationContext = new Dictionary<string, string?>
{
    ["submitterDisplayName"] = " Kollipara, Kartheek ",
    ["deskName"] = "Taxable",
    ["emptyValue"] = " "
}
```

Add assertions after `Assert.Equal(["Maker"], applicationStart.LastRequest.StartedByRoles);`:

```csharp
Assert.NotNull(applicationStart.LastRequest.NotificationContext);
Assert.Equal("Kollipara, Kartheek", applicationStart.LastRequest.NotificationContext["submitterDisplayName"]);
Assert.Equal("Taxable", applicationStart.LastRequest.NotificationContext["deskName"]);
Assert.False(applicationStart.LastRequest.NotificationContext.ContainsKey("emptyValue"));
```

- [ ] **Step 3: Run API mapping tests to verify they fail**

Run:

```powershell
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "ExecuteWorkflowActionBuildsWorkflowOnlyExecutionRequest|StartWorkflowPassesInitialActionAndActorEvidenceToApplicationStart" /p:UseAppHost=false
```

Expected: build fails because `NotificationContext` does not exist on the API and application contracts.

- [ ] **Step 4: Add contract properties**

In `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`, add to `WorkflowActionBlock` after `AssignedRoleKey`:

```csharp
public Dictionary<string, string?>? NotificationContext { get; set; }
```

In `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`, replace `StartWorkflowRequest` with:

```csharp
public sealed record StartWorkflowRequest(
    string WorkflowKey,
    string BusinessObjectType,
    string BusinessObjectId,
    string StartedByUserId,
    bool Reset,
    string? InitialActionKey = null,
    string? StartedByDisplayName = null,
    IReadOnlyList<string>? StartedByRoles = null,
    IReadOnlyDictionary<string, string?>? NotificationContext = null);
```

In `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`, add:

```csharp
public Dictionary<string, string?>? NotificationContext { get; set; }
```

In `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`, add:

```csharp
public Dictionary<string, string?>? NotificationContext { get; set; }
```

- [ ] **Step 5: Include notification context in request hash**

In `CanonicalRequestHasher.ComputeHash`, add this canonical entry after `workflow.actionReason`:

```csharp
["workflow.notificationContext"] = envelope.Workflow?.NotificationContext is null
    ? null
    : envelope.Workflow.NotificationContext
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value!.Trim(), StringComparer.Ordinal),
```

- [ ] **Step 6: Map action API request context**

In `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`, set the workflow block:

```csharp
Workflow = new WorkflowActionBlock
{
    WorkflowInstanceId = workflowInstanceId,
    ActionKey = actionKey,
    AssignedRoleKey = assignedRoleKey,
    NotificationContext = NormalizeNotificationContext(request.NotificationContext),
    IdempotencyKey = generatedId
}
```

Add helper method before `OptionalText`:

```csharp
private static Dictionary<string, string?>? NormalizeNotificationContext(Dictionary<string, string?>? values)
{
    if (values is null || values.Count == 0)
    {
        return null;
    }

    var normalized = values
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
        .ToDictionary(
            pair => pair.Key.Trim(),
            pair => pair.Value!.Trim(),
            StringComparer.Ordinal);

    return normalized.Count == 0 ? null : normalized;
}
```

- [ ] **Step 7: Map start API request context**

In `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`, pass normalized context to the application request:

```csharp
var startResult = startWorkflowService.Start(new ApplicationStartWorkflowRequest(
    workflowKey,
    businessObjectType,
    businessObjectId,
    actor.UserId,
    Reset: false,
    InitialActionKey: initialActionKey,
    StartedByDisplayName: actor.DisplayName,
    StartedByRoles: actor.Roles,
    NotificationContext: NormalizeNotificationContext(request.NotificationContext)));
```

Add this helper before `OptionalText`:

```csharp
private static Dictionary<string, string?>? NormalizeNotificationContext(Dictionary<string, string?>? values)
{
    if (values is null || values.Count == 0)
    {
        return null;
    }

    var normalized = values
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
        .ToDictionary(
            pair => pair.Key.Trim(),
            pair => pair.Value!.Trim(),
            StringComparer.Ordinal);

    return normalized.Count == 0 ? null : normalized;
}
```

In `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`, pass the context into internally executed initial actions by adding this property in `ExecuteInitialAction`:

```csharp
Workflow = new WorkflowActionBlock
{
    WorkflowInstanceId = workflowInstanceId,
    ActionKey = request.InitialActionKey,
    NotificationContext = CopyNotificationContext(request.NotificationContext),
    IdempotencyKey = generatedId
}
```

Add this private helper before `OptionalText`:

```csharp
private static Dictionary<string, string?>? CopyNotificationContext(
    IReadOnlyDictionary<string, string?>? values)
{
    if (values is null || values.Count == 0)
    {
        return null;
    }

    var copied = values
        .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
        .ToDictionary(
            pair => pair.Key.Trim(),
            pair => pair.Value!.Trim(),
            StringComparer.Ordinal);

    return copied.Count == 0 ? null : copied;
}
```

- [ ] **Step 8: Update boundary doc**

In `workflow-api/WORKFLOW_API_BOUNDARIES.md`, update the `POST /v1/WorkflowAction` and `POST /v1/WorkflowStart` request body descriptions to include:

```text
- Optional `notificationContext`, a string key/value dictionary supplied by the upstream external API for email template rendering.
- `notificationContext` is notification data only. It is not form payload, is not persisted as form data, and does not drive workflow transitions.
```

- [ ] **Step 9: Run API tests**

Run:

```powershell
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Expected: all API tests pass.

- [ ] **Step 10: Commit**

```powershell
git add src\WorkflowEngine.Application\Contracts\WorkflowActionEnvelope.cs src\WorkflowEngine.Application\WorkflowStart\IStartWorkflowService.cs src\WorkflowEngine.Application\Normalization\CanonicalRequestHasher.cs src\WorkflowEngine.Infrastructure\WorkflowStart\StartWorkflowService.cs workflow-api\src\WorkflowEngine.WorkflowApi\Models\WorkflowActionRequest.cs workflow-api\src\WorkflowEngine.WorkflowApi\Models\WorkflowStartRequest.cs workflow-api\src\WorkflowEngine.WorkflowApi\Services\WorkflowActionService.cs workflow-api\src\WorkflowEngine.WorkflowApi\Services\WorkflowStartService.cs workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\Services\WorkflowActionServiceTests.cs workflow-api\tests\WorkflowEngine.WorkflowApi.Tests\Services\WorkflowStartServiceTests.cs workflow-api\WORKFLOW_API_BOUNDARIES.md
git commit -m "feat: accept workflow notification context"
```

---

### Task 4: Render Action Notifications and Store Task Context

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: DI in `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- Modify: test helpers in `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Modify: test helpers in `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- Modify: test helpers in `tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs`
- Modify: test helpers in `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`

- [ ] **Step 1: Add failing rendered notification test**

In `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`, add to `SubmitForCheckerMovesDraftAndCreatesCheckerTaskNotificationAuditAndIdempotency` after `var notification = Assert.Single(dbContext.NotificationOutbox);`:

```csharp
Assert.Equal("Directed Trade Pending Checker Approval", notification.Subject);
Assert.Contains("Kollipara, Kartheek", notification.Body, StringComparison.Ordinal);
Assert.Contains("Taxable desk", notification.Body, StringComparison.Ordinal);
Assert.Contains("Sharepoint link = https://sharepoint.example.test/trades/1050", notification.Body, StringComparison.Ordinal);
Assert.Contains("\"submitterDisplayName\":\"Kollipara, Kartheek\"", notification.PayloadJson, StringComparison.Ordinal);
var createdTask = Assert.Single(dbContext.WorkflowTasks);
Assert.Equal("RouteCheckerAssignedOnSubmit", createdTask.NotificationRouteKey);
Assert.Contains("\"deskName\":\"Taxable\"", createdTask.NotificationContextJson, StringComparison.Ordinal);
```

Add this helper inside `WorkflowTransitionTests`:

```csharp
private static Dictionary<string, string?> NotificationContext()
{
    return new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["referenceNumber"] = "1050",
        ["submitterDisplayName"] = "Kollipara, Kartheek",
        ["returnedByDisplayName"] = "Kollipara, Kartheek",
        ["recipientDisplayName"] = "Kollipara, Kartheek",
        ["traderDisplayName"] = "Kollipara, Kartheek",
        ["confirmerDisplayName"] = "Kollipara, Kartheek",
        ["deskName"] = "Taxable",
        ["securityType"] = "Other",
        ["accountShortName"] = "EFG",
        ["accountDisplayName"] = "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
        ["submittedDate"] = "4/24/2026",
        ["tradeLink"] = "https://sharepoint.example.test/trades/1050"
    };
}
```

In the existing `Envelope` helper, add:

```csharp
envelope.Workflow.NotificationContext = NotificationContext();
```

- [ ] **Step 2: Run focused transition test to verify it fails**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SubmitForCheckerMovesDraftAndCreatesCheckerTaskNotificationAuditAndIdempotency /p:UseAppHost=false
```

Expected: fail because notification body/context are not rendered or stored yet.

- [ ] **Step 3: Inject renderer**

In `WorkflowExecutionService` constructor, add dependency after `CanonicalWorkflowResultSerializer resultSerializer`:

```csharp
NotificationTemplateRenderer templateRenderer
```

Update every direct `new WorkflowExecutionService(...)` call in these files to pass `new NotificationTemplateRenderer()` as the final argument:

```text
src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs
tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs
tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs
tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs
```

The `StartWorkflowService` convenience constructor should build its internal execution service like this:

```csharp
new WorkflowExecutionService(
    dbContext,
    new WorkflowContextReader(dbContext, timeProvider),
    timeProvider,
    resultBuilder,
    new CanonicalWorkflowResultSerializer(),
    new NotificationTemplateRenderer())
```

In `InfrastructureServiceCollectionExtensions`, register:

```csharp
services.AddSingleton<NotificationTemplateRenderer>();
```

- [ ] **Step 4: Add rendering helpers to WorkflowExecutionService**

Add these private methods to `WorkflowExecutionService`:

```csharp
private NotificationTemplateRenderResult RenderTemplate(
    string template,
    IReadOnlyDictionary<string, string?> context)
{
    return templateRenderer.Render(template, context, "workflow.notificationContext");
}

private static Dictionary<string, string?> BuildNotificationContext(
    WorkflowActionEnvelope envelope,
    WorkflowInstance instance,
    WorkflowTransitionDefinition transition,
    string oldState,
    WorkflowTaskChange? createdTask,
    string? recipientRoleKey)
{
    var context = envelope.Workflow?.NotificationContext is null
        ? new Dictionary<string, string?>(StringComparer.Ordinal)
        : new Dictionary<string, string?>(envelope.Workflow.NotificationContext, StringComparer.Ordinal);

    context["workflowInstanceId"] = instance.WorkflowInstanceId;
    context["actionKey"] = envelope.Workflow?.ActionKey ?? string.Empty;
    context["oldState"] = oldState;
    context["newState"] = transition.ToStateKey;
    context["taskKey"] = createdTask?.TaskKey ?? transition.TaskToCreateKey ?? string.Empty;
    context["recipientRoleKey"] = recipientRoleKey ?? string.Empty;
    return context;
}

private WorkflowActionResultEnvelope? BuildTemplateValidationFailed(
    NormalizedWorkflowAction normalized,
    IReadOnlyList<WorkflowValidationError> errors)
{
    return errors.Count == 0 ? null : resultBuilder.BuildValidationFailed(normalized, errors);
}
```

- [ ] **Step 5: Render and persist notification body in action execution**

In the notification block inside `ExecuteSideEffects`, replace `Subject = route.SubjectTemplate` and the existing payload assignment with:

```csharp
var notificationContext = BuildNotificationContext(
    envelope,
    instance,
    transition,
    oldState,
    createdTask,
    recipientRoleKey);
var renderedSubject = RenderTemplate(route.SubjectTemplate, notificationContext);
var renderedBody = RenderTemplate(route.BodyTemplate, notificationContext);
var templateErrors = renderedSubject.Errors.Concat(renderedBody.Errors).ToArray();
if (templateErrors.Length > 0)
{
    transaction?.Rollback();
    dbContext.ChangeTracker.Clear();
    return BuildTemplateValidationFailed(normalized, templateErrors)!;
}

if (createdTask is not null)
{
    var createdTaskId = long.Parse(createdTask.TaskId!, CultureInfo.InvariantCulture);
    var trackedCreatedTask = dbContext.WorkflowTasks.Single(row => row.Id == createdTaskId);
    trackedCreatedTask.NotificationRouteKey = route.RouteKey;
    trackedCreatedTask.NotificationContextJson = JsonSerializer.Serialize(notificationContext);
}
```

Then create outbox with:

```csharp
Subject = renderedSubject.Rendered,
Body = renderedBody.Rendered,
PayloadJson = JsonSerializer.Serialize(notificationContext),
```

- [ ] **Step 6: Run focused transition tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "WorkflowTransitionTests|WorkflowStartInitialActionTests|NotificationTemplateRendererTests" /p:UseAppHost=false
```

Expected: transition, start-initial-action, and renderer tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src\WorkflowEngine.Infrastructure\WorkflowExecution\WorkflowExecutionService.cs src\WorkflowEngine.Infrastructure\WorkflowStart\StartWorkflowService.cs src\WorkflowEngine.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs tests\WorkflowEngine.Tests\WorkflowTransitionTests.cs tests\WorkflowEngine.Tests\WorkflowExecutionValidationTests.cs tests\WorkflowEngine.Tests\WorkflowOptionsConflictTests.cs tests\WorkflowEngine.Tests\WorkflowStartInitialActionTests.cs
git commit -m "feat: render workflow action notifications"
```

---

### Task 5: Reuse Stored Task Context for SLA Overdue Notifications

**Files:**
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- Modify: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`

- [ ] **Step 1: Add failing SLA body assertions**

In `Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification`, after the first SLA run, add:

```csharp
var overdueNotification = dbContext.NotificationOutbox.Single(row =>
    row.WorkflowTaskId == overdue.Id && row.NotificationType == "SlaTaskOverdue");
Assert.Equal("Overdue: Directed Trade Pending Checker Approval", overdueNotification.Subject);
Assert.Contains("Kollipara, Kartheek", overdueNotification.Body, StringComparison.Ordinal);
Assert.Contains("Sharepoint link = https://sharepoint.example.test/trades/1050", overdueNotification.Body, StringComparison.Ordinal);
```

Update `AddTask` to set context for tasks with `DueAtUtc`:

```csharp
NotificationRouteKey = taskKey switch
{
    "CheckerReviewTask" => "RouteCheckerAssignedOnSubmit",
    "TradingDeskReviewTask" => "RouteTradingDeskAssigned",
    _ => null
},
NotificationContextJson = dueAtUtc is null ? null : JsonSerializer.Serialize(NotificationContext())
```

Add `using System.Text.Json;` and this helper:

```csharp
private static Dictionary<string, string?> NotificationContext()
{
    return new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["referenceNumber"] = "1050",
        ["submitterDisplayName"] = "Kollipara, Kartheek",
        ["returnedByDisplayName"] = "Kollipara, Kartheek",
        ["recipientDisplayName"] = "Kollipara, Kartheek",
        ["traderDisplayName"] = "Kollipara, Kartheek",
        ["confirmerDisplayName"] = "Kollipara, Kartheek",
        ["deskName"] = "Taxable",
        ["securityType"] = "Other",
        ["accountShortName"] = "EFG",
        ["accountDisplayName"] = "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
        ["submittedDate"] = "4/24/2026",
        ["tradeLink"] = "https://sharepoint.example.test/trades/1050"
    };
}
```

- [ ] **Step 2: Run SLA test to verify it fails**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification /p:UseAppHost=false
```

Expected: fails because SLA still uses `RouteSlaTaskOverdue`.

- [ ] **Step 3: Inject and use renderer in RunSlaService**

Change constructor:

```csharp
public sealed class RunSlaService(
    WorkflowDbContext dbContext,
    NotificationTemplateRenderer templateRenderer) : IRunSlaService
```

Update DI and tests to pass `new NotificationTemplateRenderer()`.

In SLA row processing, replace the route lookup with:

```csharp
var route = !string.IsNullOrWhiteSpace(task.NotificationRouteKey)
    ? await dbContext.NotificationRoutes.AsNoTracking().SingleAsync(item =>
        item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
        && item.RouteKey == task.NotificationRouteKey
        && item.IsActive, cancellationToken)
    : await dbContext.NotificationRoutes.AsNoTracking().SingleAsync(item =>
        item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
        && item.NotificationType == "SlaTaskOverdue"
        && item.IsActive, cancellationToken);
```

Build context:

```csharp
var context = string.IsNullOrWhiteSpace(task.NotificationContextJson)
    ? new Dictionary<string, string?>(StringComparer.Ordinal)
    : JsonSerializer.Deserialize<Dictionary<string, string?>>(task.NotificationContextJson)
        ?? new Dictionary<string, string?>(StringComparer.Ordinal);
context["workflowInstanceId"] = instance.WorkflowInstanceId;
context["taskKey"] = task.TaskKey;
context["assignedRoleKey"] = task.AssignedRoleKey;
context["dueAtUtc"] = task.DueAtUtc?.ToString("O");
context["overdueAtUtc"] = now.ToString("O");
```

Render:

```csharp
var renderedSubject = templateRenderer.Render(route.SubjectTemplate, context, "workflow.notificationContext");
var renderedBody = templateRenderer.Render(route.BodyTemplate, context, "workflow.notificationContext");
var templateErrors = renderedSubject.Errors.Concat(renderedBody.Errors).ToArray();
if (templateErrors.Length > 0)
{
    await transaction.RollbackAsync(CancellationToken.None);
    warnings.Add($"task {task.Id} overdue notification template missing: {string.Join("; ", templateErrors.Select(error => error.Message))}");
    continue;
}
```

Set outbox fields:

```csharp
NotificationType = "SlaTaskOverdue",
Subject = route.OverdueSubjectPrefix + renderedSubject.Rendered,
Body = renderedBody.Rendered,
PayloadJson = JsonSerializer.Serialize(context),
```

- [ ] **Step 4: Run SLA tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter Phase5SlaTests /p:UseAppHost=false
```

Expected: SLA tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\WorkflowEngine.Infrastructure\Sla\RunSlaService.cs tests\WorkflowEngine.Tests\Phase5SlaTests.cs
git commit -m "feat: render overdue notifications from task context"
```

---

### Task 6: Send Persisted Notification Body

**Files:**
- Modify: `src/WorkflowEngine.Application/Notifications/INotificationSender.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs`
- Modify: `tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs`

- [ ] **Step 1: Update SMTP test expectation**

Replace `RoleMappingBuildsPlainTextKeyValueMessage` in `SmtpNotificationSenderTests` with:

```csharp
[Fact]
public void RoleMappingBuildsMessageFromPersistedBody()
{
    var sender = new SmtpNotificationSender(Configuration(new()
    {
        ["Smtp:Host"] = "localhost",
        ["Smtp:From"] = "workflow@example.test",
        ["Smtp:RoleEmailMap:Checker"] = "cdt-checkers@example.test"
    }));

    using var message = sender.BuildMessage(Delivery(body: "Rendered email body"));

    Assert.Equal("Task overdue", message.Subject);
    Assert.Contains("cdt-checkers@example.test", string.Join(",", message.To.Select(address => address.ToString())), StringComparison.Ordinal);
    Assert.Equal("Rendered email body", message.TextBody);
}
```

Update helper:

```csharp
internal static NotificationDelivery Delivery(string subject = "Task overdue", string body = "")
```

Pass `body` into the `NotificationDelivery` constructor.

- [ ] **Step 2: Run SMTP sender test to verify it fails**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter RoleMappingBuildsMessageFromPersistedBody /p:UseAppHost=false
```

Expected: build fails because `NotificationDelivery` has no body parameter.

- [ ] **Step 3: Add body to notification delivery**

Modify `NotificationDelivery` in `INotificationSender.cs`:

```csharp
public sealed record NotificationDelivery(
    long Id,
    string WorkflowInstanceId,
    long? WorkflowTaskId,
    string BusinessObjectType,
    string BusinessObjectId,
    string NotificationType,
    string? RecipientRoleKey,
    string? RecipientUserId,
    string Subject,
    string Body,
    string PayloadJson,
    string Status,
    string CorrelationId,
    DateTimeOffset CreatedAtUtc);
```

Update `ProcessNotificationOutboxService.ToDelivery` to pass `row.Body` after `row.Subject`.

In `SmtpNotificationSender.BuildBody`, return body first:

```csharp
if (!string.IsNullOrWhiteSpace(notification.Body))
{
    if (string.IsNullOrWhiteSpace(recipientOverride))
    {
        return notification.Body;
    }

    return $"OriginalRecipientRoleKey: {notification.RecipientRoleKey ?? "(none)"}{Environment.NewLine}{notification.Body}";
}
```

Keep the existing key/value payload fallback for legacy rows with empty `Body`.

- [ ] **Step 4: Run SMTP sender tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SmtpNotificationSenderTests /p:UseAppHost=false
```

Expected: SMTP sender unit tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\WorkflowEngine.Application\Notifications\INotificationSender.cs src\WorkflowEngine.Infrastructure\Notifications\ProcessNotificationOutboxService.cs src\WorkflowEngine.Infrastructure\Notifications\SmtpNotificationSender.cs tests\WorkflowEngine.Tests\SmtpNotificationSenderTests.cs
git commit -m "feat: send persisted notification body"
```

---

### Task 7: Update Samples for Required Notification Context

**Files:**
- Modify: `samples/workflow-actions/02-submit-for-checker.json`
- Modify: `samples/workflow-actions/03-checker-return-to-maker.json`
- Modify: `samples/workflow-actions/04-maker-resubmit-for-checker.json`
- Modify: `samples/workflow-actions/05-checker-send-to-trading-desk.json`
- Modify: `samples/workflow-actions/06-trading-return-to-maker.json`
- Modify: `samples/workflow-actions/07-trading-trade-executed.json`
- Modify: `samples/workflow-actions/08-bdrm-trade-fulfilled.json`
- Modify: `samples/workflow-actions/09-bdrm-trade-not-executed-properly.json`
- Modify: `samples/workflow-actions/10-trading-rework-trade-executed.json`

- [ ] **Step 1: Add notification context to each notification-producing sample**

Inside each sample's `workflow` object, add:

```json
"notificationContext": {
  "referenceNumber": "1050",
  "submitterDisplayName": "Kollipara, Kartheek",
  "returnedByDisplayName": "Kollipara, Kartheek",
  "recipientDisplayName": "Kollipara, Kartheek",
  "traderDisplayName": "Kollipara, Kartheek",
  "confirmerDisplayName": "Kollipara, Kartheek",
  "deskName": "Taxable",
  "securityType": "Other",
  "accountShortName": "EFG",
  "accountDisplayName": "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
  "submittedDate": "4/24/2026",
  "tradeLink": "https://sharepoint.example.test/trades/1050"
}
```

Keep `01-save-draft.json` unchanged because it does not queue a notification.

- [ ] **Step 2: Run sample expectation tests**

Run:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter SampleExpectationTests /p:UseAppHost=false
```

Expected: sample expectation tests pass.

- [ ] **Step 3: Commit**

```powershell
git add samples\workflow-actions\02-submit-for-checker.json samples\workflow-actions\03-checker-return-to-maker.json samples\workflow-actions\04-maker-resubmit-for-checker.json samples\workflow-actions\05-checker-send-to-trading-desk.json samples\workflow-actions\06-trading-return-to-maker.json samples\workflow-actions\07-trading-trade-executed.json samples\workflow-actions\08-bdrm-trade-fulfilled.json samples\workflow-actions\09-bdrm-trade-not-executed-properly.json samples\workflow-actions\10-trading-rework-trade-executed.json
git commit -m "test: add notification context to samples"
```

---

### Task 8: Final Verification and Handoff Update

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run API build and tests**

Run:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```

Expected: build passes with 0 warnings/errors and API tests pass.

- [ ] **Step 2: Run focused workflow engine tests**

Run:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "NotificationTemplateRendererTests|WorkflowTransitionTests|WorkflowStartInitialActionTests|Phase5SlaTests|SmtpNotificationSenderTests|SeedDataSmokeTests|SampleExpectationTests" /p:UseAppHost=false
```

Expected: focused workflow tests pass.

- [ ] **Step 3: Run build for shared engine**

Run:

```powershell
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
```

Expected: build passes with 0 warnings/errors.

- [ ] **Step 4: Run SQL seed verification**

Run:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Expected: `init-db` applies the new migration and seed data; `verify-seed` passes.

- [ ] **Step 5: Run one sample path**

Run:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-NOTIFY-TEMPLATE --user maker1 --reset
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-NOTIFY-TEMPLATE --workflow-instance WF-PT-NOTIFY-TEMPLATE
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-NOTIFY-TEMPLATE
```

Expected output includes `Directed Trade Pending Checker Approval` and a `WorkflowTaskAssigned` notification for `Checker`.

- [ ] **Step 6: Update handoff**

Add to `CODEX_HANDOFF.md`:

```text
Notification template feature:
- Added route-specific subject/body SQL metadata.
- Added workflow.notificationContext as generic external API-provided email data.
- Rendered action-created outbox rows with persisted subject/body.
- SLA overdue processing reuses stored task notification route/context and prefixes subject with Overdue:.
- Return reason is not required and is absent from seeded templates.
Verification:
- dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false: record the observed result from Step 1.
- dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false: record the observed result from Step 1.
- dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter "NotificationTemplateRendererTests|WorkflowTransitionTests|WorkflowStartInitialActionTests|Phase5SlaTests|SmtpNotificationSenderTests|SeedDataSmokeTests|SampleExpectationTests" /p:UseAppHost=false: record the observed result from Step 2.
- dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false: record the observed result from Step 3.
- dotnet run --project src\WorkflowEngine.Console -- verify-seed: record the observed result from Step 4.
- show-notifications WF-PT-NOTIFY-TEMPLATE: record the observed notification subject and recipient from Step 5.
```

- [ ] **Step 7: Commit verification docs**

```powershell
git add CODEX_HANDOFF.md
git commit -m "docs: record notification template verification"
```

---

## Self-Review Notes

- Spec coverage: schema metadata, route-specific templates, external API context, strict missing-token behavior, persisted body, SLA reuse, and return-reason removal are all mapped to tasks.
- Scope: this plan does not add user lookup, SQL role-email lookup, template packages, or external API callbacks from SLA processing.
- Risk: migration designer/snapshot edits are mechanical but verbose; verify with `dotnet build` before implementation continues past Task 2.
- Risk: strict templates require sample/test context; Task 7 updates all notification-producing samples.
