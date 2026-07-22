# Codex Handoff

Last updated: 2026-07-21T23:06:04-04:00

## Current Goal

Target API integration is complete. The current work is paused before implementing a new workflow definition/step slice for the `SQMS` module, described by the user as Secondary Sales Qualified Matching Service.

Current SQMS state:

- The user added the first SQMS reference file under `SQMS/Opportunity status.txt`.
- The user said they will add all remaining SQMS code files and revisit.
- Do not make code changes yet.
- Continue discovery first once the remaining files are present.

Initial observations from `SQMS/Opportunity status.txt`:

- The file is legacy SharePoint-style reference code.
- It appears to describe opportunity status behavior, not already-integrated workflow-engine metadata.
- Visible concepts include `Pending Seller Approval`, open-to-bidding notification/reminder behavior, `CLOSED TO BIDDING`, bid status `NOT ALLOCATED`, and bid status `IPG REVIEW`.
- Some behavior appears user workflow-like; some appears scheduled/background-job-like.
- The current reusable engine can support another workflow definition through metadata, but the current seed and verification services are still PortfolioTrade-specific.

Unresolved next clarification:

- After all SQMS files are added, identify the exact two workflow steps the user wants.
- Initial inferred candidates were `PendingSellerApproval` and `OpenToBidding`, but this is not confirmed.

Boundary for the next session:

- Keep SQMS metadata-driven.
- Do not hardcode SQMS business behavior inside the reusable workflow engine.
- Separate true user workflow steps/actions from scheduled jobs such as daily notifications or automatic close-to-bidding processing.

## Required Reading

1. `AGENTS.MD`
2. `PROJECT.md`
3. `context.md`
4. `CODEX_HANDOFF.md`
5. `NEXT_CODEX_PROMPT.md`
6. `SQMS/` after the user adds the remaining SQMS files
7. `workflow-api/WORKFLOW_API_BOUNDARIES.md` if API contract questions come up
8. `TODO_WORKFLOW_API.md` when continuing API migration or operational follow-up

## Active Repo Shape

- `src/WorkflowEngine.sln`: reusable workflow engine, infrastructure, console/demo host, EF migrations, seed data, SLA, notification outbox, and SMTP sender.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx`: active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests`: shared engine/infrastructure tests.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests`: active API host tests.
- `samples/workflow-actions`: Portfolio Trade sample action JSON.
- `docs/superpowers`: active design and implementation plans/specs.
- `.tools/smtp4dev`: retained local SMTP test tool folder.
- `SQMS/`: currently untracked SQMS reference/source material. First observed file is `Opportunity status.txt`; more files are expected before design resumes.

The old tracked `api/` host has been removed from the working tree by user approval. Do not bring it back unless the user explicitly asks.

## Completed Cleanup

- Removed local/generated folders unrelated to the current source shape: `.dotnet/`, `.dotnet_home/`, `.nuget/`, `.templateengine/`, `.tmp/`, `.vs/`, `.\~`, and `console-dbfirst-clean/`.
- Preserved `.tools/smtp4dev`.
- Removed the old tracked `api/` host.
- Removed stale generated/historical docs that described the deleted host:
  - `docs/api-conversion/`
  - `docs/workflow-map/`
  - `docs/workflow-visualization.html`
  - `docs/console-to-api-integration-analysis.html`
- Removed stale root/extracted-copy artifacts:
  - `FULL_PROJECT_REVIEW.html`
  - `docs/BEGINNERS_GUIDE.md`
  - `cdt-src-bundle.ps1`
  - `cdt-src-bundle.sh`
  - `restore-repo.ps1`
- Updated `README.md` to describe the current `src/` plus `workflow-api/` shape and current local verification DB.
- Updated `PROJECT.md` to remove stale notification-delivery wording, align the local SQL database name, and match the current SMTP/outbox implementation shape.
- Rewrote `CODEX_HANDOFF.md`, `NEXT_CODEX_PROMPT.md`, and `context.md` around the current shape.
- Added `docs/README.md` to describe the remaining documentation surface.

## Beginner Handoff Guide

Added `docs/beginners-api-console-handoff.html` as the single-file beginner handoff requested for:

- current API and console project layout
- local console/API smoke-test commands
- workflow-only API request/response shapes
- target API integration steps
- action-created notification dispatch
- Task Scheduler setup for `run-sla` and `process-notification-outbox`
- database/migration and SMTP configuration notes

The guide intentionally distinguishes the local `workflow-api/` reference host from the external target API so future work does not drift back to the removed `api/` host.

## Latest Completed Changes

### SQMS discovery pause

No code was changed for SQMS.

The only current SQMS artifact is an untracked folder:

```text
SQMS/Opportunity status.txt
```

The first pass identified likely status/workflow concepts but did not confirm the two new workflow steps. The user will add the remaining SQMS files and revisit. The next session should inspect all files under `SQMS/` before asking the next clarifying question.

### Frontend workflow API handoff

Added `docs/frontend-workflow-api-handoff.html` as a frontend-focused handoff for the current reference API in `workflow-api/WorkflowEngine.WorkflowApi.slnx`.

The guide documents:

- `POST /v1/WorkflowStart`, `GET /v1/WorkflowContext`, and `POST /v1/WorkflowAction`.
- Required trusted actor headers.
- Current API JSON casing: lower-case wrapper properties with PascalCase model fields.
- Input and output parameter tables.
- The approved full happy path: Maker submit, Checker reject, Maker resubmit, Checker approve, Trading Desk reject, Maker save draft, Maker submit again, Checker approve, Trading Desk approve, BDRM approve/complete.
- The reference API boundary that `WorkflowAction` is workflow-only and does not save form payload data.

Updated `docs/README.md` so the new frontend handoff is discoverable.

### Per-record SMTP recipient resolution

The Maker recipient changes per business record. The code supports that without adding a SQL user table or changing public request DTOs:

- `SmtpNotificationSender` resolves recipients in this order: `Smtp:RecipientOverride`, then payload `recipientEmail`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- `SmtpNotificationSender` adds SMTP CC recipients from payload `ccEmailAddresses`, split by comma or semicolon.
- Override-mode bodies preserve `OriginalRecipientRoleKey`, `OriginalRecipientEmail`, and `OriginalCcEmailAddresses` when available.
- `Smtp:RecipientOverride` suppresses real CC delivery during smoke tests.
- Return-to-Maker persistence is covered by `WorkflowExecutionValidationTests.ReturnToMakerPersistsRecipientEmailFromNotificationContext`.
- SMTP recipient and CC behavior is covered by `SmtpNotificationSenderTests`.
- SLA CC persistence is covered by `Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification`.
- `ConsoleNotificationSenderTests` and `tests/COVERAGE_AUDIT.md` were updated because PROJECT.md section 20 coverage audit is enforced by tests.

The target API should populate `workflow.notificationContext.recipientEmail` for Return-to-Maker and any other business-record-specific recipient. If the UI supplies copied recipients, the target API should pass them through as `workflow.notificationContext.ccEmailAddresses` so action-created and SLA overdue emails share the same CC list.

### HTML notification email rendering

The email format change is implemented in schema-backed metadata, not hardcoded Portfolio Trade logic:

- `NotificationRoute` now has `HtmlBodyTemplate`.
- `NotificationOutbox` now has rendered `HtmlBody`.
- Migration `20260620130000_AddHtmlNotificationBodies` adds both columns under schema `dbo`.
- The renderer has `RenderHtml(...)`, which HTML-escapes token values while preserving template-owned markup.
- Seeded notification templates include plain text and HTML versions for action-created and SLA overdue routes.
- Important UI-provided values are wrapped with `<strong>...</strong>` in seeded HTML templates, and `tradeLink` renders as `<a href="...">Open trade</a>`.
- `SmtpNotificationSender` sends `multipart/alternative` email when `HtmlBody` is present, with text/plain and text/html parts.
- Override-mode SMTP still suppresses real CC delivery and records original recipient/CC metadata in the test email body.

Pushed commits:

- `95c0d4a feat: support per-notification SMTP recipients`
- `cb0d65b test: verify per-record SMTP recipient with smtp4dev`

### Documentation updates

`PROJECT.md`, `README.md`, `docs/beginners-api-console-handoff.html`, `context.md`, and this file now document the recipient resolution order and target API handoff guidance.

Added `docs/target-api-notification-changes-handoff.html` as the precise target API integration checklist for the last two notification changes: per-record SMTP recipients/copied recipients and persisted HTML notification bodies. It lists target API files to update, corresponding source files in this repo, SQL column changes, request DTO shape, dispatch pattern, SMTP configuration, and verification steps.

### API JSON body and console real-usage handoff

Added `docs/api-json-bodies-and-console-real-usage.html` as the current one-file reference for:

- all public workflow API JSON body shapes for `POST /v1/WorkflowStart` and `POST /v1/WorkflowAction`;
- the no-body shape for `GET /v1/WorkflowContext`;
- trusted actor headers;
- the Portfolio Trade sample action flow and dynamic trading desk role keys;
- action-specific `notificationContext` examples;
- fields the target API must not send, including `toState`, form payload, UI echo fields, expected state/task/version guards, and generated IDs;
- console commands classified as real operational use, support diagnostics, or demo-only.

Updated `docs/README.md` so the new API JSON body handoff is discoverable.

### Restore bundle

`restore-repo.ps1` is the portable base64 PowerShell bundle for target-api handoff. It restores the repository source/handoff content without `.git`, `bin`, `obj`, `.tools`, local caches, logs, or the bundle script itself.

## Notification Template Feature State

Latest feature commit before cleanup: `a0e5640 docs: record notification template verification`.

The generic notification-template feature is implemented and verified:

- Notification subject/body templates live in SQL metadata through `NotificationRoute`.
- Portfolio Trade/Directed Trade wording is seed/sample metadata only.
- The reusable engine stays generic and renders `{{tokenName}}` placeholders from caller-provided notification context.
- Workflow action and workflow start API requests accept optional `notificationContext`.
- SLA overdue notifications reuse the route/context snapshotted on the created task and prefix the subject with metadata, currently `Overdue: `.
- `returnReason` was removed from all seeded templates.
- SMTP sends persisted outbox body content.

## Current Operational Notes

- Local API and console testing use:

```text
Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

- Trusted local actor headers for the workflow API remain:

```text
X-Workflow-UserId
X-Workflow-Roles
X-Workflow-DisplayName
```

- `POST /v1/WorkflowAction` commits workflow/task/audit/outbox data before SMTP delivery. If delivery fails, the retry path is `process-notification-outbox`.
- Normal action notifications are API-owned: after the workflow action execution returns success and queues notification rows, the target API should dispatch the rows created by that action.
- The action dispatcher should select by `WorkflowInstanceId` plus `CorrelationId`, matching the existing `WorkflowActionNotificationDispatcher` pattern in `workflow-api/src/WorkflowEngine.WorkflowApi/Services/`.
- The hosted/scheduled SLA service should call `IRunSlaService.RunAsync(...)` against the workflow DB. It should not call an external API to rebuild overdue email context; the task snapshot carries the notification route and context.
- Task Scheduler is available on the target machine. Preferred processor shape:

```powershell
dotnet .\WorkflowEngine.Console.dll run-sla
dotnet .\WorkflowEngine.Console.dll process-notification-outbox
```

- Keep `RecipientOverride` configured during live SMTP smoke testing so real users do not receive test email.

## Current Worktree Expectations

Expected local state after this pause:

- `SQMS/` may be untracked while the user is adding remaining source files.
- No SQMS implementation edits should exist yet.
- Handoff/context files may contain this SQMS pause state.

Run before further edits:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Verification Status

Latest bundle and push-prep verification on 2026-06-22:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath .\.bundle-test\restore-20260622-225258 -Force
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false
git diff --check
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "Category!=FreshClone" -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
```

Results:

- `restore-repo.ps1` regenerated as a base64 ZIP PowerShell restore bundle.
- Bundle selected 294 files and excluded `.git`, `.tools`, `.bundle-test`, `.bundle-work`, build outputs, caches, and the previous restore script.
- Bundle SHA-256 verified: `d6ca44471252e6af0a7a6753dc7f8a6521531ce61795fe0fe11c118b0005d85b`.
- Restored file count matched: 294 files.
- Required target API handoff/source files were present in the restored tree; forbidden paths were absent.
- Restored `workflow-api\WorkflowEngine.WorkflowApi.slnx` warnings-as-errors build passed with 0 warnings/errors.
- Main tree `git diff --check` was clean except Git line-ending warnings.
- Main tree shared solution warnings-as-errors build passed with 0 warnings/errors.
- Main tree workflow API warnings-as-errors build passed with 0 warnings/errors.
- Workflow API tests passed: 41 passed.
- Core tests excluding known long `FreshClone` walkthrough category passed: 229 passed, 3 skipped SMTP integration tests.
- Full unfiltered core test command timed out under the tool after 5 minutes and left stale test processes; those PIDs were stopped before rerunning the filtered core suite.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.

Latest retest of the last two notification changes on 2026-06-22:

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
git diff --check
```

Results:

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed when run outside the sandbox with normal Windows credentials.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and Portfolio Trade seed data.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.
- `git diff --check` was clean except Git line-ending warnings.
- Note: SQL-backed commands fail under the sandbox identity `dolly\codexsandboxoffline` with `Cannot generate SSPI context`; run SQL Integrated Security gates outside the sandbox.

Latest verification after the HTML notification email change:

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

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and seed.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.

Latest verification for the frontend handoff documentation slice:

- `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore` passed: 41 passed, 0 failed, 0 skipped.
- `git diff --check` reported only Git line-ending warnings for edited markdown files, with no whitespace errors.

Latest verification for the API JSON body and console real-usage documentation slice:

- Targeted text check confirmed the new HTML contains the current endpoints, key action names, dynamic Trading Desk role key, and console outbox command.
- `git diff --check` passed with no whitespace errors; it reported only Git line-ending warnings for edited files.

Latest verification after the per-record recipient change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter SmtpNotificationSenderTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowExecutionValidationTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
git diff --check
```

Results:

- `SmtpNotificationSenderTests` passed: 7 passed.
- `WorkflowExecutionValidationTests` passed: 19 passed.
- Full core test project passed: 231 passed, 2 skipped SMTP integration tests.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `verify-seed` passed.
- `git diff --check` was clean except Git line-ending warnings.

Latest smtp4dev verification:

```powershell
RUN_SMTP_TESTS=1 dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter Category=Smtp -m:1 /p:UseAppHost=false --no-restore
```

Result: 3 passed, 0 failed, including per-record `recipientEmail` delivery to `maker.one@example.test`.

Latest restore-bundle verification:

- `restore-repo.ps1` was generated from commit `cb0d65b`.
- Restored into a temp folder using `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath <temp> -Force`.
- Restored file count matched source-selected count.
- Source/restored SHA-256 comparison found no mismatches.
- Required target-api handoff files were present.
- Forbidden paths were absent: `.git`, `bin`, `obj`, `.tools`, local caches, logs, temp files, and `restore-repo.ps1`.

## Remaining Work

- Wait for the user to add the remaining SQMS files.
- Reread the complete `SQMS/` folder.
- Understand the new module's status/action flow before coding.
- Confirm the exact two SQMS workflow steps with the user.
- Propose metadata-driven approaches and get design approval before implementation.
- For the target API integration, ensure Return-to-Maker request construction includes `notificationContext.recipientEmail`.
- Keep `Smtp:RecipientOverride` configured during live SMTP smoke testing, then remove it only when ready to send to real per-record or role-map recipients.
- For target-machine operations, publish/copy the workflow console processor, configure DB/SMTP settings, and schedule `run-sla` followed by `process-notification-outbox` in Windows Task Scheduler.
- Replace trusted-header actor resolution with the target auth/claims resolver when integrating into the real host.
- Integrate EF migrations into the target deployment process.

## Next Session Prompt

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD, PROJECT.md, context.md, CODEX_HANDOFF.md, NEXT_CODEX_PROMPT.md, and then inspect the full SQMS/ folder. Target API integration is complete. The user is adding remaining SQMS files before revisiting a request to add two workflow steps for the Secondary Sales Qualified Matching Service module. Do not edit code yet. First understand the SQMS source/status flow, separate workflow actions from scheduled/background jobs, and confirm the exact two workflow steps with the user before proposing a metadata-driven design.
```
