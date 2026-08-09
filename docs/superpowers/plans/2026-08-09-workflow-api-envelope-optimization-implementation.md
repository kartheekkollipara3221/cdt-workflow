# Workflow API Envelope Optimization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current UI-facing Start/Action payload with the approved minimal contract and resolve final notification recipients from versioned route rules, an immutable original-Maker participant, and permitted action recipient inputs.

**Architecture:** The public ASP.NET Core DTOs remain separate from the console `WorkflowActionEnvelope`. The API maps the new body into existing application services plus a focused `WorkflowNotificationInput`; SQL metadata determines recipient sources and To/Cc placement, while runtime workflow participants provide immutable instance-scoped values. Workflow execution resolves a deterministic recipient snapshot before side effects, persists it on the created task/outbox row, and the existing SLA monitor reuses the task snapshot unchanged.

**Tech Stack:** .NET 10, ASP.NET Core controllers, EF Core, SQL Server, System.Text.Json, MailKit/MimeKit, xUnit, `TimeProvider`.

---

## Scope and execution constraints

- Work on `console-code-first-0518` and preserve the existing uncommitted `CODEX_HANDOFF.md` plus untracked `.tools/`, `SQMS/`, and `tareget-api-integration/` content.
- Do not redesign endpoint routes. Keep `POST /v1/WorkflowStart`, `POST /v1/WorkflowAction`, and the same-endpoint actionless SLA synchronization path.
- Do not alter the console JSON envelope except where an internal optional application contract must coexist with it.
- Do not add packages, a directory lookup, a user-to-role table, a plugin system, or application-specific form parsing.
- Use TDD for every behavior change and commit each task atomically.
- Run affected milestone gates sequentially: schema/seed (Phase 2), contracts/API mapping (Phase 3), engine execution (Phase 4), SLA/outbox (Phase 5), then documentation/samples/full verification (Phase 6).

## File responsibility map

### Public API boundary

- `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`: optimized Start body only.
- `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`: optimized Action and SLA-sync body only.
- `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowNotificationRequest.cs`: public notification grouping and typed dynamic recipient values.
- `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`: maps Start DTO and trusted actor into the application Start request.
- `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`: maps Action DTO into the internal envelope/execution request and generates request identity internally.

### Application contracts

- `src/WorkflowEngine.Application/Contracts/WorkflowNotificationInput.cs`: application-level recipient-input values passed to generic infrastructure.
- `src/WorkflowEngine.Application/WorkflowExecution/IWorkflowExecutionService.cs`: carries optional public notification input separately from the console envelope.
- `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`: removes caller business-object type and carries Start notification input.
- `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs`: includes named recipient inputs in the effective hash while preserving collection order.

### Metadata and runtime persistence

- `WorkflowDefinition`: owns `BusinessObjectType`.
- `WorkflowParticipantDefinition`: declares participant key/cardinality/start capture/immutability.
- `WorkflowParticipant`: stores instance participant snapshots.
- `NotificationRecipientRule`: declares ordered To/Cc sources for each notification route.
- `WorkflowTask.NotificationRecipientsJson` and `NotificationOutbox.NotificationRecipientsJson`: retain the existing final delivery snapshots.

### Resolution services

- `WorkflowParticipantCaptureService`: validates/captures Start participants and separates route request inputs.
- `NotificationRecipientResolver`: resolves static, participant, and request-input rules, applies ordering/deduplication, and returns workflow validation errors without parsing addresses.
- `WorkflowExecutionService`: invokes resolution before side effects and writes the resolved snapshot.
- `StartWorkflowService`: derives business-object type, captures participants transactionally, and invokes the initial action with non-participant recipient inputs.

## Task 1: Reconcile the repository source of truth

**Files:**

- Modify: `PROJECT.md`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- Modify: `docs/superpowers/specs/2026-08-03-multiple-notification-recipients-design.md`

- [ ] **Step 1: Update the authoritative contract before code changes**

  Replace caller-owned final To/Cc guidance with the approved ownership split:

  ```text
  Metadata owns recipient source rules and To/Cc placement.
  Workflow Start captures originalMaker once as an immutable participant.
  Action requests may supply only route-declared named recipient inputs.
  The engine resolves, deduplicates, snapshots, and reuses final recipients.
  Address strings are opaque and receive no syntax/directory/SMTP validation.
  ```

  Update the public request examples to use `actionKey`, `slaTargetDate`, and `notification.{recipientInputs,templateData}`. Remove body `businessObjectType`, `idempotencyKey`, `slaInput`, `notificationContext`, and `notificationRecipients` from the public API sections only. Preserve the internal console envelope and section 24 milestone mapping.

- [ ] **Step 2: Mark only the superseded portions of the prior recipient spec**

  Add a note that the 2026-08-03 spec remains valid for task/outbox snapshots, SLA reuse, one-message delivery, and message-level retries, while recipient source ownership and duplicate behavior are superseded by the 2026-08-09 design.

- [ ] **Step 3: Verify documentation consistency**

  Run:

  ```powershell
  rg -n "caller owns recipient|caller-supplied notificationRecipients|slaInput.targetDate|initialActionKey|businessObjectType" PROJECT.md workflow-api\WORKFLOW_API_BOUNDARIES.md docs\superpowers\specs
  git diff --check -- PROJECT.md workflow-api\WORKFLOW_API_BOUNDARIES.md docs\superpowers\specs
  ```

  Expected: active public API guidance uses the optimized terms; historical descriptions are explicitly marked superseded; no whitespace errors.

- [ ] **Step 4: Commit the source-of-truth update**

  ```powershell
  git add -- PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md docs/superpowers/specs/2026-08-03-multiple-notification-recipients-design.md
  git commit -m "docs: align workflow api envelope boundaries"
  ```

## Task 2: Add metadata and participant schema with seed verification

**Files:**

- Create: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowParticipantDefinition.cs`
- Create: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowParticipant.cs`
- Create: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRecipientRule.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowDefinition.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRoute.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/WorkflowSeedService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Verification/SeedVerificationService.cs`
- Create through EF: `src/WorkflowEngine.Infrastructure/Migrations/20260809150000_OptimizeWorkflowApiEnvelope.cs`
- Create through EF: `src/WorkflowEngine.Infrastructure/Migrations/20260809150000_OptimizeWorkflowApiEnvelope.Designer.cs`
- Modify through EF: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`
- Test: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`
- Test: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`
- Test: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [ ] **Step 1: Write failing model and seed tests**

  Add assertions for:

  ```csharp
  Assert.Equal("PortfolioTrade", definition.BusinessObjectType);
  Assert.Equal("originalMaker", participantDefinition.ParticipantKey);
  Assert.Equal("Single", participantDefinition.Cardinality);
  Assert.True(participantDefinition.IsImmutable);
  Assert.Contains(recipientRules, rule =>
      rule.RecipientKind == "To"
      && rule.SourceType == "WorkflowParticipant"
      && rule.SourceKey == "originalMaker");
  Assert.DoesNotContain(notificationRouteType.GetProperties(), property =>
      property.Name is "RecipientRoleKey" or "RecipientUserId");
  ```

  Add EF model checks for unique participant/rule ordering indexes and foreign keys to workflow definition version, workflow instance, and notification route.

- [ ] **Step 2: Run the Phase 2 focused tests for RED**

  ```powershell
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SeedDataSmokeTests"
  ```

  Expected RED: new entity properties/types/tables are absent.

- [ ] **Step 3: Add the minimal entities and EF mappings**

  Use these exact domain values:

  ```csharp
  public static class RecipientSourceTypes
  {
      public const string StaticAddress = "StaticAddress";
      public const string WorkflowParticipant = "WorkflowParticipant";
      public const string RequestInput = "RequestInput";
  }

  public static class RecipientKinds
  {
      public const string To = "To";
      public const string Cc = "Cc";
  }
  ```

  `WorkflowParticipantDefinition` contains definition-version ID, participant key, `Single|Many` cardinality, `Start` capture stage, required/immutable/active flags. `WorkflowParticipant` contains instance ID, participant key, nullable user/display values, opaque required `EmailAddress`, order, and `CreatedAtUtc`. `NotificationRecipientRule` contains route ID, kind, source type, nullable source key/static address, order, required flag, and active flag.

  Add SQL check constraints for enum-like structural fields only. Do not add an email-format constraint.

- [ ] **Step 4: Seed generic sample metadata**

  Seed `BusinessObjectType = "PortfolioTrade"` and one required immutable `originalMaker` definition. Replace dormant route recipient hints with ordered explicit rules. Use `.example.test` sample addresses for static Checker, Trading Desk, BDRM, and Operations mailboxes. Configure `selectedCcUsers` as an optional Cc `RequestInput` on `RouteCheckerAssignedOnSubmit` and `RouteReturnedToMakerFromChecker`; configure Maker return/completion routes to use `WorkflowParticipant("originalMaker")` as To. SLA routes receive no recipient rules because SLA copies the task snapshot.

- [ ] **Step 5: Extend seed verification**

  Verify exact seeded rule sets, source-field consistency, route/version ownership, participant references, unique `SortOrder`, and at least one To rule for every transition route. Do not parse or validate `StaticAddress` or participant address contents.

- [ ] **Step 6: Generate the EF migration**

  ```powershell
  dotnet ef migrations add OptimizeWorkflowApiEnvelope --project src\WorkflowEngine.Infrastructure --startup-project src\WorkflowEngine.Console
  ```

  Rename the generated migration prefix and its `[Migration(...)]` attribute to the fixed ID `20260809150000_OptimizeWorkflowApiEnvelope`, then inspect `Up` and `Down`. `Up` adds the new metadata/runtime structures, adds required `WorkflowDefinition.BusinessObjectType`, removes dormant `NotificationRoute.RecipientRoleKey`/`RecipientUserId`, and explicitly clears incompatible runtime workflow data in foreign-key-safe order without deleting workflow metadata. `Down` restores only the dropped schema and removes new structures; no legacy recipient backfill is attempted.

- [ ] **Step 7: Run the Phase 2 gate for GREEN**

  ```powershell
  dotnet build src\WorkflowEngine.sln -m:1 -warnaserror
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~SchemaConstraintTests|FullyQualifiedName~MigrationSchemaTests|FullyQualifiedName~SeedDataSmokeTests"
  dotnet run --project src\WorkflowEngine.Console -- init-db
  dotnet run --project src\WorkflowEngine.Console -- verify-seed
  ```

  Expected: zero warnings/errors, focused tests pass, migration applies to a clean database, and seed verification reports success.

- [ ] **Step 8: Commit the schema and metadata task**

  ```powershell
  git add -- src/WorkflowEngine.Infrastructure/Persistence src/WorkflowEngine.Infrastructure/Migrations tests/WorkflowEngine.Tests/SchemaConstraintTests.cs tests/WorkflowEngine.Tests/MigrationSchemaTests.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
  git commit -m "feat: add metadata recipient rules and participants"
  ```

## Task 3: Add application notification input, participant capture, and recipient resolution

**Files:**

- Create: `src/WorkflowEngine.Application/Contracts/WorkflowNotificationInput.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowExecution/IWorkflowExecutionService.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs`
- Create: `src/WorkflowEngine.Infrastructure/Notifications/NotificationRecipientResolver.cs`
- Create: `src/WorkflowEngine.Infrastructure/WorkflowStart/WorkflowParticipantCaptureService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- Test: `tests/WorkflowEngine.Tests/NotificationRecipientResolverTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowParticipantCaptureTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionResultHashTests.cs`

- [ ] **Step 1: Write failing participant and resolver tests**

  Cover:

  ```csharp
  var input = new WorkflowNotificationInput
  {
      RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
      {
          ["selectedCcUsers"] =
          [
              new("manager1", "operations@example.test", "Manager"),
              new("manager2", "MANAGER@example.test", "Manager Two")
          ]
      }
  };
  ```

  Prove Start captures exactly one `originalMaker`; absent/multiple required single participants fail; later resolution loads the stored Maker; unknown request keys fail; static/participant/request sources preserve rule and input order; case-insensitive duplicates retain the first opaque string; To removes the same address from Cc; blank/malformed strings are not parsed or rejected.

- [ ] **Step 2: Write a failing hash test**

  Prove changing a recipient-input key, address, or list order changes the canonical hash, while dictionary insertion order does not.

- [ ] **Step 3: Run focused tests for RED**

  ```powershell
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~NotificationRecipientResolverTests|FullyQualifiedName~WorkflowParticipantCaptureTests|FullyQualifiedName~WorkflowActionResultHashTests"
  ```

- [ ] **Step 4: Add the application contracts**

  ```csharp
  public sealed record WorkflowRecipientInput(
      string? UserId,
      string EmailAddress,
      string? DisplayName);

  public sealed class WorkflowNotificationInput
  {
      public IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInputs { get; init; }
          = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

      public IReadOnlyDictionary<string, string?> TemplateData { get; init; }
          = new Dictionary<string, string?>(StringComparer.Ordinal);
  }
  ```

  Add `WorkflowNotificationInput? NotificationInput` to `WorkflowExecutionRequest` and `StartWorkflowRequest`. Remove `BusinessObjectType`, `NotificationContext`, and `NotificationRecipients` from the application Start record; retain internal `InitialActionKey`, `WorkflowSlaInput`, and generated/internal idempotency support for console/service behavior.

- [ ] **Step 5: Implement participant capture**

  `WorkflowParticipantCaptureService` loads active Start definitions, validates known keys/cardinality/required values, creates runtime rows with `TimeProvider.GetUtcNow()`, and returns a new `WorkflowNotificationInput` containing the unchanged template data plus only non-participant request inputs for the initial action. It never edits an existing participant row.

- [ ] **Step 6: Implement recipient resolution**

  `NotificationRecipientResolver` returns:

  ```csharp
  public sealed record NotificationRecipientResolution(
      NotificationRecipients Recipients,
      IReadOnlyList<WorkflowValidationError> Errors);
  ```

  It resolves only active rules for the effective versioned route, rejects undeclared request keys, enforces required source presence and at least one resolved To value, treats address strings as opaque, and applies the approved deterministic duplicate rules using `StringComparer.OrdinalIgnoreCase` without trimming or rewriting.

- [ ] **Step 7: Extend canonical hashing and DI**

  Add `ComputeHash(WorkflowActionEnvelope envelope, WorkflowNotificationInput? notificationInput)` while retaining the existing one-argument overload for console callers. Canonicalize recipient-input dictionary keys and preserve each list's order. Register the two infrastructure services as scoped dependencies.

- [ ] **Step 8: Run focused tests for GREEN and commit**

  ```powershell
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~NotificationRecipientResolverTests|FullyQualifiedName~WorkflowParticipantCaptureTests|FullyQualifiedName~WorkflowActionResultHashTests"
  git add -- src/WorkflowEngine.Application src/WorkflowEngine.Infrastructure/Notifications src/WorkflowEngine.Infrastructure/WorkflowStart/WorkflowParticipantCaptureService.cs src/WorkflowEngine.Infrastructure/DependencyInjection tests/WorkflowEngine.Tests/NotificationRecipientResolverTests.cs tests/WorkflowEngine.Tests/WorkflowParticipantCaptureTests.cs tests/WorkflowEngine.Tests/WorkflowActionResultHashTests.cs
  git commit -m "feat: resolve metadata notification recipients"
  ```

## Task 4: Integrate derived business type, participant capture, and resolved snapshots

**Files:**

- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Modify: `src/WorkflowEngine.Application/Commands/StartWorkflowCommandHandler.cs`
- Test: `tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`

- [ ] **Step 1: Replace caller-recipient assertions with failing resolved-recipient tests**

  Add tests proving:

  - Start derives `PortfolioTrade` business-object type from `WorkflowDefinition`.
  - Start stores `originalMaker` in the same transaction as the instance.
  - an invalid initial action rolls back both instance and participant rows;
  - return-to-Maker resolves the stored Maker into To;
  - `selectedCcUsers` and static Operations metadata merge into Cc;
  - duplicate To/Cc values follow the approved precedence;
  - created task and initial outbox store identical resolved JSON;
  - request recipient inputs on an undeclared route yield `ValidationFailed` and no state/task/outbox changes;
  - console requests with no `WorkflowExecutionRequest.NotificationInput` retain their existing internal-envelope recipient behavior.

- [ ] **Step 2: Run focused Phase 3/4 tests for RED**

  ```powershell
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~StartWorkflowServiceTests|FullyQualifiedName~WorkflowStartInitialActionTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~WorkflowExecutionValidationTests"
  ```

- [ ] **Step 3: Derive workflow identity during Start**

  Resolve the active `WorkflowDefinition` and version once:

  ```csharp
  var definition = dbContext.WorkflowDefinitions
      .AsNoTracking()
      .Where(row => row.WorkflowKey == request.WorkflowKey && row.IsActive)
      .Select(row => new
      {
          row.WorkflowKey,
          row.BusinessObjectType,
          VersionId = row.Versions.Single(version => version.IsActive).Id
      })
      .Single();
  ```

  Use the derived type for duplicate detection, reset cleanup, instance persistence, and the Start result. Capture participants immediately after adding the instance and before executing/committing an initial action.

- [ ] **Step 4: Resolve recipients before workflow side effects**

  After transition/assignment/SLA validation and before `ExecuteSideEffects`, resolve the final recipients when `WorkflowExecutionRequest.NotificationInput` is non-null. Convert resolver errors with `WorkflowActionResultBuilder.BuildValidationFailed`. Pass the resolved `NotificationRecipients` into `ExecuteSideEffects`; serialize once and reuse for task/outbox persistence.

  When `NotificationInput` is null, use the existing internal `workflow.NotificationRecipients` path so console samples remain stable. Do not add public fallback behavior.

- [ ] **Step 5: Preserve transactional and delivery behavior**

  Participant capture and initial action stay in the existing Start transaction. Recipient-resolution failures occur before workflow effects. Template validation still rolls back. SMTP dispatch remains after commit and retains the pending outbox row on failure.

- [ ] **Step 6: Run the Phase 4 gate and commit**

  ```powershell
  dotnet build src\WorkflowEngine.sln -m:1 -warnaserror
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~StartWorkflowServiceTests|FullyQualifiedName~WorkflowStartInitialActionTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~WorkflowExecutionValidationTests"
  dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
  git add -- src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs src/WorkflowEngine.Application/Commands/StartWorkflowCommandHandler.cs tests/WorkflowEngine.Tests
  git commit -m "feat: snapshot resolved workflow recipients"
  ```

## Task 5: Replace the public Start and Action DTOs

**Files:**

- Create: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowNotificationRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowStartControllerTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Controllers/WorkflowActionControllerTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Startup/WorkflowApiProjectStructureTests.cs`

- [ ] **Step 1: Write failing DTO and mapping tests**

  Assert the public property sets are exact:

  ```csharp
  Assert.Equal(
      ["ActionKey", "BusinessObjectId", "Notification", "SlaTargetDate", "WorkflowKey"],
      typeof(WorkflowStartRequest).GetProperties().Select(property => property.Name).Order());

  Assert.Equal(
      ["ActionKey", "AssignedRoleKey", "Notification", "SlaTargetDate", "WorkflowInstanceId"],
      typeof(WorkflowActionRequest).GetProperties().Select(property => property.Name).Order());
  ```

  Add service tests for mapping trusted actor, `actionKey`, flat target date, template data, original Maker, and selected CC. Prove internally generated client/correlation/idempotency values are nonblank and equal where current behavior requires. Add JSON deserialization tests proving removed public fields are rejected.

- [ ] **Step 2: Run focused API tests for RED**

  ```powershell
  dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "FullyQualifiedName~WorkflowStartServiceTests|FullyQualifiedName~WorkflowActionServiceTests|FullyQualifiedName~WorkflowStartControllerTests|FullyQualifiedName~WorkflowActionControllerTests|FullyQualifiedName~WorkflowApiProjectStructureTests"
  ```

- [ ] **Step 3: Implement the public models**

  ```csharp
  [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
  public sealed class WorkflowNotificationRequest
  {
      public Dictionary<string, List<WorkflowRecipientRequest>>? RecipientInputs { get; set; }
      public Dictionary<string, string?>? TemplateData { get; set; }
  }

  public sealed class WorkflowRecipientRequest
  {
      public string? UserId { get; set; }
      public string? EmailAddress { get; set; }
      public string? DisplayName { get; set; }
  }
  ```

  Apply `JsonUnmappedMemberHandling.Disallow` to both root request DTOs so old fields fail rather than being silently ignored. Do not validate or trim `EmailAddress`; copy it as an opaque string, using `string.Empty` only when the JSON property is absent/null.

- [ ] **Step 4: Update API service mapping**

  Start maps public `ActionKey` to internal `InitialActionKey`, flat `SlaTargetDate` to `WorkflowSlaInput.TargetDate`, `TemplateData` to internal `NotificationContext`, and named values to `WorkflowNotificationInput`. It no longer asks for `BusinessObjectType` or accepts idempotency.

  Action performs the same notification/SLA mapping and attaches a non-null `WorkflowNotificationInput` to `WorkflowExecutionRequest`, even when the public notification block is absent, so public calls always use metadata resolution. The API generates command/correlation/idempotency identity internally.

- [ ] **Step 5: Run focused API tests for GREEN and commit**

  ```powershell
  dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror
  dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "FullyQualifiedName~WorkflowStartServiceTests|FullyQualifiedName~WorkflowActionServiceTests|FullyQualifiedName~WorkflowStartControllerTests|FullyQualifiedName~WorkflowActionControllerTests|FullyQualifiedName~WorkflowApiProjectStructureTests"
  git add -- workflow-api/src/WorkflowEngine.WorkflowApi workflow-api/tests/WorkflowEngine.WorkflowApi.Tests
  git commit -m "feat: simplify public workflow api payloads"
  ```

## Task 6: Prove task snapshot reuse through SLA and delivery

**Files:**

- Test: `tests/WorkflowEngine.Tests/RecurringSlaMonitorTests.cs`
- Test: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- Test: `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`
- Test: `tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionNotificationDispatcherTests.cs`

- [ ] **Step 1: Add failing end-to-end snapshot assertions**

  Create an SLA-enabled task through metadata resolution with static To plus UI-selected Cc. Assert the task JSON equals the immediate outbox JSON, then run the SLA monitor and assert the reminder JSON is byte-for-byte equal to the task snapshot even after modifying route rules or the original Maker row.

- [ ] **Step 2: Preserve opaque-address failure behavior**

  Add or update tests showing `"<"`, whitespace, and other opaque values persist successfully. When MailKit parses an unusable address, the entire row returns to `Pending`, increments one retry count, and stores one error; no per-recipient state is created.

- [ ] **Step 3: Run the Phase 5 gate**

  ```powershell
  dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~RecurringSlaMonitorTests|FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~Phase5OutboxTests|FullyQualifiedName~SmtpNotificationSenderTests"
  dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "FullyQualifiedName~WorkflowActionNotificationDispatcherTests"
  dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
  ```

  Expected: resolved snapshots remain fixed per task; reminders copy them unchanged; message-level retry behavior passes.

- [ ] **Step 4: Commit the SLA/delivery verification task**

  ```powershell
  git add -- tests/WorkflowEngine.Tests workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionNotificationDispatcherTests.cs
  git commit -m "test: verify resolved recipients on sla reminders"
  ```

## Task 7: Update samples, integration guidance, and complete all gates

**Files:**

- Modify: `README.md`
- Modify: `docs/api-json-bodies-and-console-real-usage.html`
- Modify: `docs/beginners-api-console-handoff.html`
- Modify: `docs/frontend-workflow-api-handoff.html`
- Modify: `docs/target-api-notification-changes-handoff.html`
- Modify carefully without overwriting unrelated edits: `CODEX_HANDOFF.md`
- Test: `tests/WorkflowEngine.Tests/WorkflowSamplesTests.cs`
- Test: `tests/WorkflowEngine.Tests/SampleExpectationTests.cs`

- [ ] **Step 1: Update API examples without changing console samples**

  Use these public shapes:

  ```json
  {
    "workflowKey": "PortfolioTrade",
    "businessObjectId": "PT-1001",
    "actionKey": "SubmitForChecker",
    "notification": {
      "recipientInputs": {
        "originalMaker": [
          {
            "userId": "maker1",
            "emailAddress": "maker@example.test",
            "displayName": "Maker One"
          }
        ],
        "selectedCcUsers": []
      },
      "templateData": {
        "submitterDisplayName": "Maker One"
      }
    }
  }
  ```

  Keep `samples/workflow-actions/*.json` as internal console-envelope fixtures. Clearly label them so they are never presented as the public API body.

- [ ] **Step 2: Update handoff and integration documentation**

  Explain field ownership, original-Maker capture, route-declared request keys, opaque stored addresses, deterministic merge, task snapshot immutability, and SLA reuse. Update `CODEX_HANDOFF.md` by applying only feature-related additions around the user's existing uncommitted text; do not stage or commit unrelated hunks.

- [ ] **Step 3: Run negative contract searches**

  ```powershell
  rg -n "BusinessObjectType|InitialActionKey|IdempotencyKey|SlaInput|NotificationContext|NotificationRecipients" workflow-api\src\WorkflowEngine.WorkflowApi\Models
  rg -n "RecipientRoleKey|RecipientUserId" src\WorkflowEngine.Infrastructure --glob "!Migrations/**"
  rg -n "caller supplies.*To|caller owns recipient|notificationRecipients" PROJECT.md workflow-api\WORKFLOW_API_BOUNDARIES.md README.md docs
  git diff --check
  ```

  Expected: removed fields are absent from public request models; dormant route fields are absent from active entities/services; active documentation contains no superseded final-list ownership guidance.

- [ ] **Step 4: Run complete sequential builds and tests**

  ```powershell
  dotnet restore src\WorkflowEngine.sln
  dotnet build src\WorkflowEngine.sln -m:1 -warnaserror
  dotnet test src\WorkflowEngine.sln -m:1
  dotnet restore workflow-api\WorkflowEngine.WorkflowApi.slnx
  dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror
  dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1
  ```

- [ ] **Step 5: Run clean database, seed, sample, and SLA verification**

  ```powershell
  dotnet run --project src\WorkflowEngine.Console -- init-db
  dotnet run --project src\WorkflowEngine.Console -- verify-seed
  dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\03-checker-return-to-maker.json
  dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
  dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003
  dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
  dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-1001
  ```

  If SQL configuration is unavailable, record the exact missing configuration/error instead of claiming these gates passed.

- [ ] **Step 6: Exercise the live HTTP payloads**

  Start the API using its project `http` launch profile, call `POST /v1/WorkflowStart` with `originalMaker`, call a metadata-valid `POST /v1/WorkflowAction` with `selectedCcUsers`, and inspect SQL/outbox evidence that immediate and SLA rows use the expected snapshots. Use Console/NoOp sender unless the user explicitly requests smtp4dev.

- [ ] **Step 7: Commit documentation and samples**

  ```powershell
  git add -- README.md PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md workflow-api docs tests/WorkflowEngine.Tests/WorkflowSamplesTests.cs tests/WorkflowEngine.Tests/SampleExpectationTests.cs
  git commit -m "docs: publish optimized workflow api contract"
  ```

  Exclude `CODEX_HANDOFF.md` from this command unless its feature-only hunks can be staged without including the user's pre-existing changes.

## Final verification and completion evidence

- Run `git status --short --branch` and confirm only pre-existing unrelated changes remain.
- Run `git diff --check` and repeat any failed focused gate after its fix.
- Verify no code uses `DateTime.UtcNow`; new timestamps must use injected `TimeProvider`.
- Verify public bodies never accept `toState`, form payload, actor identity, workflow options, SLA configuration, final To/Cc arrays, or removed compatibility fields.
- Verify the database stores opaque static addresses, immutable original Maker, route rules, final task snapshot, and final outbox snapshot.
- Verify a task's SLA reminders copy its original recipient snapshot after metadata/participant changes.
- Record exact evidence in the completion summary using: `Changed`, `Why`, `Tests run`, `Seed verification run`, `Sample JSON executed`, `Files updated`, and `Open issues`.
