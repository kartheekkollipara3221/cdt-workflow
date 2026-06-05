# Codex Handoff

Last updated: 2026-06-05T15:09:35-04:00

## Current Goal

Continue the external target API integration handoff. The user restored/copied the repo bundle to another machine and is integrating workflow behavior into an IIS-hosted target API that also supports other application APIs.

Latest recipient decision:

- Maker can vary per business record, so Return-to-Maker email cannot rely only on `Smtp:RoleEmailMap:Maker`.
- SMTP delivery now resolves recipients in this order: `Smtp:RecipientOverride`, then `notificationContext.recipientEmail` persisted in notification `PayloadJson`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- Target API integration should provide `notificationContext.recipientEmail` for business-record-specific recipients.
- This keeps the no SQL user table / no role-to-email lookup table boundary intact.

The immediate design decision from the latest session:

- The target API should send normal action-created notifications when a user action moves a record to the next workflow state.
- `run-sla` is only for overdue SLA notification creation.
- Task Scheduler on the target machine should run the separate processor for `run-sla` and `process-notification-outbox`.
- `process-notification-outbox` remains the retry/backlog path for pending notification rows.
- Do not place the scheduled SLA/outbox loop inside the IIS target API.

## Required Reading

1. `AGENTS.MD`
2. `PROJECT.md`
3. `context.md`
4. `CODEX_HANDOFF.md`
5. `workflow-api/WORKFLOW_API_BOUNDARIES.md`
6. `TODO_WORKFLOW_API.md` when continuing API migration or operational follow-up

## Active Repo Shape

- `src/WorkflowEngine.sln`: reusable workflow engine, infrastructure, console/demo host, EF migrations, seed data, SLA, notification outbox, and SMTP sender.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx`: active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests`: shared engine/infrastructure tests.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests`: active API host tests.
- `samples/workflow-actions`: Portfolio Trade sample action JSON.
- `docs/superpowers`: active design and implementation plans/specs.
- `.tools/smtp4dev`: retained local SMTP test tool folder.

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

### Per-record SMTP recipient resolution

The Maker recipient changes per business record. The code supports that without adding a SQL user table or changing public request DTOs:

- `SmtpNotificationSender` resolves recipients in this order: `Smtp:RecipientOverride`, then payload `recipientEmail`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- Override-mode bodies preserve `OriginalRecipientRoleKey` and, when available, `OriginalRecipientEmail`.
- Return-to-Maker persistence is covered by `WorkflowExecutionValidationTests.ReturnToMakerPersistsRecipientEmailFromNotificationContext`.
- SMTP recipient behavior is covered by new `SmtpNotificationSenderTests`.
- `ConsoleNotificationSenderTests` and `tests/COVERAGE_AUDIT.md` were updated because PROJECT.md section 20 coverage audit is enforced by tests.

The target API should populate `workflow.notificationContext.recipientEmail` for Return-to-Maker and any other business-record-specific recipient.

Pushed commits:

- `95c0d4a feat: support per-notification SMTP recipients`
- `cb0d65b test: verify per-record SMTP recipient with smtp4dev`

### Documentation updates

`PROJECT.md`, `README.md`, `docs/beginners-api-console-handoff.html`, `context.md`, and this file now document the recipient resolution order and target API handoff guidance.

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

Expected local change for this bundle slice:

- `CODEX_HANDOFF.md`
- `restore-repo.ps1`

Run before further edits:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Verification Status

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

- For the target API integration, ensure Return-to-Maker request construction includes `notificationContext.recipientEmail`.
- Keep `Smtp:RecipientOverride` configured during live SMTP smoke testing, then remove it only when ready to send to real per-record or role-map recipients.
- For target-machine operations, publish/copy the workflow console processor, configure DB/SMTP settings, and schedule `run-sla` followed by `process-notification-outbox` in Windows Task Scheduler.
- Replace trusted-header actor resolution with the target auth/claims resolver when integrating into the real host.
- Integrate EF migrations into the target deployment process.

## Next Session Prompt

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD, PROJECT.md, context.md, CODEX_HANDOFF.md, and workflow-api/WORKFLOW_API_BOUNDARIES.md first. The user is pausing after deciding the external target API should dispatch normal action-created notifications immediately, while Task Scheduler handles run-sla overdue detection and process-notification-outbox retry/backlog processing. Do not put the scheduled loop inside the IIS target API. Before editing, inspect git status and diffs, then continue from the user's latest request.
```
