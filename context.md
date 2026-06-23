# CDT Workflow Current Context

Last updated: 2026-06-20T13:19:41-04:00
Workspace: `C:\projects\cdt-workflow`
Branch: `console-code-first-0518`
Current HEAD before beginner guide commit: `00bcf07`

## Latest Session Context

The current work is an external target-machine integration handoff, not broad source implementation in this repo.

Latest recipient decision:

- Maker can vary per business record.
- SMTP delivery now resolves recipients in this order: `Smtp:RecipientOverride`, then `notificationContext.recipientEmail` persisted in notification `PayloadJson`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- The target API should supply `recipientEmail` in `notificationContext` for Return-to-Maker and any other per-record recipient scenario.
- The UI can supply copied recipients as `notificationContext.ccEmailAddresses`, using comma- or semicolon-separated addresses.
- `ccEmailAddresses` is persisted in notification payloads and snapshotted on created workflow tasks, so both immediate action-created emails and later SLA overdue emails use the same copied recipients.
- `Smtp:RecipientOverride` suppresses real CC delivery during smoke tests and records the intended copied recipients in the email body.
- Notification emails now persist both plain text and HTML bodies. The target API/UI should keep sending plain data in `notificationContext`; the engine HTML-escapes caller-provided values, bolds important seeded values, and renders `tradeLink` as a clickable link in HTML emails.
- No workflow SQL user table or role-to-email lookup table was added.

Key conclusion from today's discussion:

- Normal action-created notifications belong in the target API action path. When a user clicks a form action and the target API executes workflow movement, the API should dispatch notification rows created by that same action immediately after workflow execution succeeds.
- `run-sla` is only for overdue SLA detection. It scans active pending tasks whose `DueAtUtc` has breached and queues `SlaTaskOverdue` rows.
- Task Scheduler on the target machine should run the separate processor for:
  - `run-sla`
  - `process-notification-outbox`
- `process-notification-outbox` is still needed as the retry/backlog path for pending rows, including failed action-created SMTP rows and SLA overdue rows.
- Do not put the SLA/outbox polling loop inside the IIS-hosted target API. Use Task Scheduler or a separate worker process.
- If the target API must send action notifications, it needs only a narrow hook after the service method that calls workflow execution, not a broad target API rewrite.

Target action-notification shape:

```csharp
var actionResult = workflowExecuteService.Execute(...);

DispatchCreatedNotifications(actionResult);

return actionResult;
```

The dispatcher should select only rows created by that action, using the action result's `WorkflowInstanceId` and `CorrelationId`.

## How To Resume

Start a new Codex session in `C:\projects\cdt-workflow` and paste:

```text
$gsd-resume-work
```

If not using GSD, paste the contents of `NEXT_CODEX_PROMPT.md`.

Before editing, run:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Required Reading Order

1. `context.md`
2. `AGENTS.MD`
3. `PROJECT.md`
4. `CODEX_HANDOFF.md`
5. `workflow-api/WORKFLOW_API_BOUNDARIES.md`
6. `TODO_WORKFLOW_API.md` only when continuing API migration or operational tasks

## Active Repo Shape

- `src/WorkflowEngine.sln`: reusable workflow engine, infrastructure, console/demo host, EF migrations, seed data, SLA, and notification outbox.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx`: active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests`: shared engine/infrastructure tests.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests`: workflow API tests.
- `samples/workflow-actions`: Portfolio Trade sample requests.
- `docs/superpowers`: active specs/plans.
- `.tools/smtp4dev`: retained local SMTP test tool.

Removed by user-approved cleanup:

- Local/generated folders: `.dotnet/`, `.dotnet_home/`, `.nuget/`, `.templateengine/`, `.tmp/`, `.vs/`, `.\~`, and `console-dbfirst-clean/`.
- Old tracked `api/` host.
- Stale generated/historical docs: `docs/api-conversion/`, `docs/workflow-map/`, `docs/workflow-visualization.html`, and `docs/console-to-api-integration-analysis.html`.
- Stale root/extracted-copy docs: `FULL_PROJECT_REVIEW.html` and `docs/BEGINNERS_GUIDE.md`.
- Stale generated bundle/restore scripts: `cdt-src-bundle.ps1`, `cdt-src-bundle.sh`, and `restore-repo.ps1`.

## Current Source Changes

### Per-record SMTP recipient resolution

Return-to-Maker can target a different Maker for each business record, so SMTP recipient resolution no longer depends only on the static `Smtp:RoleEmailMap:Maker` entry.

Implemented fix:

- `SmtpNotificationSender` resolves recipients in this order: `Smtp:RecipientOverride`, then payload `recipientEmail`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- `SmtpNotificationSender` adds CC recipients from payload `ccEmailAddresses`, split by comma or semicolon.
- Override-mode bodies include original role metadata and, when available, `OriginalRecipientEmail` and `OriginalCcEmailAddresses`.
- `WorkflowExecutionValidationTests.ReturnToMakerPersistsRecipientEmailFromNotificationContext` proves `workflow.notificationContext.recipientEmail` is persisted into `NotificationOutbox.PayloadJson`.
- `SmtpNotificationSenderTests` covers direct per-notification email, CC delivery, no-role-map delivery, override precedence, override CC suppression, and missing-recipient failure.
- `Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification` proves SLA overdue payloads preserve `ccEmailAddresses` from the task notification snapshot.
- `ConsoleNotificationSenderTests` and `tests/COVERAGE_AUDIT.md` were updated so PROJECT.md section 20 coverage stays complete.

No SQL user table, SQL role-to-email lookup, public API DTO change, or schema migration was added.

### HTML notification email rendering

The email format change is schema-backed:

- `NotificationRoute.HtmlBodyTemplate` stores seeded HTML templates.
- `NotificationOutbox.HtmlBody` stores the rendered HTML body.
- Migration `20260620130000_AddHtmlNotificationBodies` adds both columns.
- `NotificationTemplateRenderer.RenderHtml(...)` escapes caller-provided token values and preserves template-owned markup.
- Seeded action-created and SLA overdue templates bold important `notificationContext` values and render `tradeLink` as a clickable link.
- `SmtpNotificationSender` sends `multipart/alternative` email with text/plain and text/html parts when `HtmlBody` is present.

The UI or target API should never send HTML in `notificationContext`; send the same plain values already owned by the business/form layer.

### Documentation updates

- `PROJECT.md`, `README.md`, `docs/frontend-workflow-api-handoff.html`, and `docs/beginners-api-console-handoff.html` document the recipient, CC, and HTML email behavior.
- `workflow-api/WORKFLOW_API_BOUNDARIES.md`, `CODEX_HANDOFF.md`, and this `context.md` record that the target API should supply `notificationContext.recipientEmail` for business-record-specific recipients and `notificationContext.ccEmailAddresses` for UI-provided copied recipients, while leaving HTML rendering to the engine.

## Notification Template Feature State

The generic email notification template feature is complete through commit `a0e5640`.

Key behavior:

- Notification subject/body templates are SQL metadata on notification routes.
- Notification routes store both plain-text and HTML body templates.
- Portfolio Trade/Directed Trade language is seed/sample metadata only.
- The external API or host supplies `notificationContext` values.
- The reusable engine renders simple `{{tokenName}}` placeholders.
- HTML rendering escapes caller-provided token values while preserving seeded template markup.
- Missing required tokens return `ValidationFailed` and roll back workflow side effects.
- Action-created task notifications persist rendered subject/body/html body in the notification outbox.
- SLA overdue processing uses the task's stored `NotificationRouteKey` and `NotificationContextJson`.
- Overdue notifications prefix the route subject with metadata, currently `Overdue: `.
- `returnReason` has been removed from all templates.
- SMTP sends persisted outbox bodies as text/plain, or multipart text/html when an HTML body is available.

## Required Notification Context Tokens

Common task assignment tokens:

```text
submitterDisplayName
deskName
securityType
accountShortName
accountDisplayName
tradeLink
```

Return-to-Maker tokens:

```text
referenceNumber
returnedByDisplayName
recipientDisplayName
recipientEmail
accountShortName
accountDisplayName
submittedDate
tradeLink
```

Trading execution to BDRM tokens:

```text
traderDisplayName
deskName
securityType
accountShortName
accountDisplayName
tradeLink
```

Workflow completion tokens:

```text
confirmerDisplayName
accountShortName
accountDisplayName
tradeLink
```

SLA processing also adds:

```text
workflowInstanceId
taskKey
assignedRoleKey
dueAtUtc
overdueAtUtc
```

## External API Notes

- The external API can provide email data through `notificationContext`.
- Do not add a user lookup table or SQL role-to-user mapping.
- `WorkflowActionRequest.NotificationContext` maps into the internal action envelope.
- `WorkflowStartRequest.NotificationContext` maps into the application start request and is reused by `initialActionKey` execution.
- The target API should send normal action-created notifications immediately after the workflow action commits, by dispatching rows created for the action's workflow instance and correlation id.
- A scheduled processor should call `IRunSlaService.RunAsync(...)` for overdue detection and then use the existing outbox processing path.
- The scheduled SLA processor should not call the external API to fetch overdue email data because the task snapshot already carries the route/context.
- Target-machine Task Scheduler is available and is the preferred no-IIS-change host for SLA and outbox retry processing.

Suggested scheduled wrapper on the target machine:

```powershell
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
& dotnet .\WorkflowEngine.Console.dll run-sla
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet .\WorkflowEngine.Console.dll process-notification-outbox
exit $LASTEXITCODE
```

## Verification Already Completed

After the HTML notification email change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "NotificationTemplateRendererTests|SmtpNotificationSenderTests|CoverageAuditTests" -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "SeedDataSmokeTests|WorkflowTransitionTests|Phase5SlaTests" -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- init-db
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
```

Results:

```text
Focused renderer/SMTP/audit tests passed: 19 passed.
SQL-backed seed/transition/SLA tests passed: 19 passed.
src warnings-as-errors build passed with 0 warnings/errors.
workflow-api warnings-as-errors build passed with 0 warnings/errors.
workflow-api tests passed: 41 passed.
init-db applied schema and seed.
verify-seed passed.
Sample JSON 02-submit-for-checker.json executed successfully and queued a notification.
Full core test project passed: 235 passed, 3 skipped SMTP integration tests.
```

## Known Risks And Remaining Work

- Existing target databases with non-`dbo` workflow tables need a controlled migration/cleanup plan.
- Live smtp4dev/Tier 2 SMTP verification was not rerun for this slice; default tests skip those integration tests by design.
- `show-notifications` displays subject/recipient/status, not the persisted body.
- Hosted SLA/outbox processing still needs a production hosting decision.
- If the external API omits required `notificationContext` tokens, workflow actions fail validation and roll back.

## Completion Summary Format

AGENTS.md requires final summaries in this shape:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```
