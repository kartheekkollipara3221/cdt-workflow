# Multiple Notification Recipients Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to execute this plan task by task, with superpowers:test-driven-development for every behavior change.

**Goal:** Carry caller-supplied ordered multiple `To` and `Cc` recipients through workflow start/action requests, persist the exact snapshot on tasks and outbox messages, and reuse it for SLA reminders and one-message SMTP delivery.

**Architecture:** CDT, SQMS, and other callers own recipient selection and validation. The generic engine transports and snapshots typed lists separately from template context, creates one outbox row per email, and retains message-level status/retry semantics. SLA copies the task snapshot without lookup or refresh; SMTP uses the typed lists directly, except that `Smtp:RecipientOverride` suppresses all intended recipients in development/testing.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, SQL Server, MailKit/MimeKit, xUnit, `TimeProvider`.

---

## Constraints and approved compatibility break

- Do not validate, trim, normalize, deduplicate, resolve, or refresh email addresses inside the engine.
- Preserve list values, order, and `To` versus `Cc` placement.
- Keep `notificationContext` exclusively for notification-template values.
- Remove runtime use of singular `recipientEmail`, delimited `ccEmailAddresses`, and `Smtp:RoleEmailMap`.
- Keep workflow task assignment roles and authorization unchanged.
- Existing `NotificationOutbox` and `WorkflowTask` development rows may be deleted by the migration; do not delete workflow instances, audit rows, mock data, or workflow metadata.
- Do not change `ROADMAP.md`, transition metadata, SLA calculations, templates, or add packages.

### Task 1: Typed contracts, exact hash semantics, and SQL snapshot schema

**Files:**

- Create: `src/WorkflowEngine.Application/Contracts/NotificationRecipients.cs`
- Modify: `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
- Modify: `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs`
- Modify: `src/WorkflowEngine.Application/Notifications/INotificationSender.cs`
- Modify: `src/WorkflowEngine.Application/Notifications/IProcessNotificationOutboxService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260803120000_AddNotificationRecipientSnapshots.cs`
- Create: `src/WorkflowEngine.Infrastructure/Migrations/20260803120000_AddNotificationRecipientSnapshots.Designer.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Migrations/WorkflowDbContextModelSnapshot.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionEnvelopeTestData.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionEnvelopeSerializationTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs`
- Test: `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs`
- Test: `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`

1. Add failing JSON tests for this exact public shape and assert both order and values survive round-trip:

   ```json
   "notificationRecipients": {
     "toEmailAddresses": ["first@example.test", "second@example.test"],
     "ccEmailAddresses": ["manager@example.test", "operations@example.test"]
   }
   ```

2. Add failing hash tests that independently change a value, reverse list order, and move an address from `To` to `Cc`. Run:

   ```powershell
   dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowActionEnvelopeSerializationTests|FullyQualifiedName~WorkflowActionEnvelopeNormalizationTests"
   ```

   Expected RED: contract/property is missing and recipient-only changes do not change the canonical request hash.

3. Add the minimal shared contract:

   ```csharp
   public sealed class NotificationRecipients
   {
       public List<string> ToEmailAddresses { get; set; } = [];
       public List<string> CcEmailAddresses { get; set; } = [];
   }
   ```

   Add `NotificationRecipients? NotificationRecipients` to `WorkflowActionBlock`, and the corresponding optional value to `StartWorkflowRequest`. Hash the block as `workflow.notificationRecipients` with its list order intact; do not pass it through notification-context normalization.

4. Replace `NotificationDelivery.RecipientRoleKey` and `.RecipientUserId` with typed `IReadOnlyList<string>` `ToEmailAddresses` and `CcEmailAddresses`. Update the processing result records so diagnostics no longer imply role-based address resolution.

5. Add failing EF model/migration tests asserting required `NotificationRecipientsJson` columns on both runtime tables and absence of scalar recipient columns from `NotificationOutbox`.

6. Add required `NotificationRecipientsJson` entity properties and EF mappings. Generate the migration, retaining migration id `20260803120000`. Its `Up` must:

   - delete all rows from `dbo.NotificationOutbox`;
   - delete all rows from `dbo.WorkflowTask`;
   - add required `nvarchar(max)` snapshot columns to both tables;
   - drop `RecipientRoleKey` and `RecipientUserId` only from `dbo.NotificationOutbox`.

   `Down` drops snapshot columns and recreates the two legacy outbox columns as nullable. It does not attempt a backfill.

7. Run the focused tests again. Expected GREEN: serialization, hashing, model, and migration tests pass.

8. Commit the task atomically:

   ```powershell
   git add -- src/WorkflowEngine.Application src/WorkflowEngine.Infrastructure/Persistence src/WorkflowEngine.Infrastructure/Migrations tests/WorkflowEngine.Tests/WorkflowActionEnvelopeTestData.cs tests/WorkflowEngine.Tests/WorkflowActionEnvelopeSerializationTests.cs tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs tests/WorkflowEngine.Tests/SchemaConstraintTests.cs tests/WorkflowEngine.Tests/MigrationSchemaTests.cs
   git commit -m "feat: add notification recipient snapshots"
   ```

### Task 2: Persist once, reuse for SLA, and deliver one multi-recipient message

**Files:**

- Modify: `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs`
- Modify: `src/WorkflowEngine.Infrastructure/Notifications/ConsoleNotificationSender.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs`
- Test: `tests/WorkflowEngine.Tests/BranchScriptTests.cs`
- Test: `tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowStartInitialActionTests.cs`
- Test: `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`
- Test: `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`
- Test: `tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs`
- Test: `tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs`

1. Add failing transition/start tests proving the same ordered recipient JSON is stored on the created task and initial outbox row while `PayloadJson` contains template context only.

2. Add failing SLA tests proving one overdue row copies the stored task snapshot exactly, duplicate prevention remains per task, and malformed/missing snapshot JSON is warned and skipped without role or payload fallback.

3. Add failing outbox/SMTP tests proving:

   - all supplied `To` and `Cc` addresses are added to one `MimeMessage`;
   - one outbox row receives one `Sent` or one retry/status outcome;
   - a MailKit parse/send failure returns the whole row to `Pending` and records `RetryCount`/`LastError`;
   - `Smtp:RecipientOverride` sends only to the override and includes ordered intended `To` and `Cc` lists in diagnostic plain-text and HTML content;
   - no role map, singular payload field, or delimited string is used.

4. Run the focused suite and verify RED for the missing snapshot plumbing:

   ```powershell
   dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~WorkflowExecutionValidationTests|FullyQualifiedName~WorkflowTransitionTests|FullyQualifiedName~WorkflowStartInitialActionTests|FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~Phase5OutboxTests|FullyQualifiedName~SmtpNotificationSenderTests"
   ```

5. Implement the minimal flow:

   - copy start request recipients into the initial action envelope without recipient normalization;
   - serialize the action block once and assign that snapshot to every created task and its initial outbox row;
   - copy `WorkflowTask.NotificationRecipientsJson` unchanged into the SLA outbox row;
   - deserialize only `NotificationOutbox.NotificationRecipientsJson` into typed delivery lists;
   - iterate those lists into `MimeMessage.To` and `.Cc`;
   - retain the existing claim token, `Pending`/`Processing`/`Sent` statuses, retry count, last error, cancellation, and batch-abort behavior.

6. Run the same suite for GREEN, then run all engine tests to catch affected existing assertions:

   ```powershell
   dotnet test src\WorkflowEngine.sln -m:1
   ```

7. Commit the task atomically:

   ```powershell
   git add -- src/WorkflowEngine.Infrastructure tests/WorkflowEngine.Tests
   git commit -m "feat: reuse notification recipients for SLA delivery"
   ```

### Task 3: API boundary, inspection, samples, and authoritative documentation

**Files:**

- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs`
- Modify: `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowActionServiceTests.cs`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Services/WorkflowStartServiceTests.cs`
- Modify: `src/WorkflowEngine.Application/Inspection/IWorkflowInspectionReader.cs`
- Modify: `src/WorkflowEngine.Infrastructure/WorkflowInspection/WorkflowInspectionReader.cs`
- Modify: `src/WorkflowEngine.Application/Commands/ShowNotificationsCommandHandler.cs`
- Test: `tests/WorkflowEngine.Tests/ShowInspectionCommandTests.cs`
- Modify: notification-producing files `samples/workflow-actions/02-*.json` through `10-*.json`
- Modify: `PROJECT.md`, `README.md`, `workflow-api/WORKFLOW_API_BOUNDARIES.md`, `docs/api-json-bodies-and-console-real-usage.html`, `docs/beginners-api-console-handoff.html`, `docs/frontend-workflow-api-handoff.html`, `docs/target-api-notification-changes-handoff.html`, `tests/COVERAGE_AUDIT.md`, and `CODEX_HANDOFF.md`

1. Add failing API tests proving start/action request models expose the typed block and services preserve exact values/order while only trusted identity headers replace actor identity.

2. Add a failing inspection test proving `show-notifications` reports persisted intended `To` and `Cc` lists rather than a role as an email-routing mechanism.

3. Run focused API/inspection tests for RED:

   ```powershell
   dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --filter "FullyQualifiedName~WorkflowActionServiceTests|FullyQualifiedName~WorkflowStartServiceTests"
   dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~ShowInspectionCommandTests"
   ```

4. Add typed recipient mapping to both API services without calling notification-context normalization on the lists. Update inspection models/reader/console output to show the stored snapshot.

5. Update the nine notification-producing action samples with at least two `To` and two `Cc` addresses. Keep `01-save-draft.json` without recipients because it creates no notification or task.

6. Reconcile the live source of truth and active handoff material. In particular, update `PROJECT.md` sections 3.1, 9, 12, 14, 19-22, 26, and 28 so they specify caller validation, exact snapshots, SLA reuse, override suppression, and message-level retries, and remove role-map/singular/delimited recipient guidance. Do not change section 24 phase mapping or `ROADMAP.md`.

7. Run focused tests for GREEN, then all quality gates required by `PROJECT.md` section 21 with `-m:1`:

   ```powershell
   dotnet restore src\WorkflowEngine.sln
   dotnet build src\WorkflowEngine.sln -m:1
   dotnet test src\WorkflowEngine.sln -m:1
   dotnet restore workflow-api\WorkflowEngine.WorkflowApi.slnx
   dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1
   dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1
   ```

8. With a configured SQL Server connection, run `init-db`, seed verification, one multi-recipient notification-producing sample, `run-sla`, `show-notifications`, and `process-notifications` using a safe Console/NoOp sender. Record exact commands and results in `CODEX_HANDOFF.md`; if SQL configuration is absent, report that gate as not run rather than fabricating evidence.

9. Confirm no obsolete behavior remains and no placeholders were introduced:

   ```powershell
   rg -n "recipientEmail|Smtp:RoleEmailMap|RecipientRoleKey|RecipientUserId" src workflow-api samples README.md PROJECT.md docs tests -g "*"
   git diff --check
   ```

   Also inspect both plan files for placeholder language before execution.

   Any remaining `RecipientRoleKey`/`RecipientUserId` references must belong only to dormant notification-route metadata or historical migration snapshots, never runtime recipient delivery.

10. Update `CODEX_HANDOFF.md` by applying only the feature-related additions around the user’s existing uncommitted content. Stage only the intended hunks/files so unrelated edits and untracked `SQMS/` or `tareget-api-integration/` content are not committed.

11. Commit the task atomically:

   ```powershell
   git add -- workflow-api src/WorkflowEngine.Application/Inspection src/WorkflowEngine.Application/Commands src/WorkflowEngine.Infrastructure/WorkflowInspection samples README.md PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md docs tests/COVERAGE_AUDIT.md
   git commit -m "docs: expose multiple notification recipients"
   ```

## Final verification and completion evidence

- Run `git status --short` and verify only pre-existing unrelated changes remain.
- Re-run `git diff --check` and the full builds/tests from fresh command output.
- Verify the database schema, seeded metadata, sample JSON, SLA reminder snapshot, and one-row message-level outbox result.
- Perform specification compliance review before code-quality review; resolve every finding and rerun affected tests.
- Use the required completion summary labels: `Changed`, `Why`, `Tests run`, `Seed verification run`, `Sample JSON executed`, `Files updated`, and `Open issues`.
