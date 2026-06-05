# CDT Workflow Current Context

Last updated: 2026-06-05
Workspace: `C:\projects\cdt-workflow`
Branch: `console-code-first-0518`
Current HEAD before local cleanup/schema edits: `a0e5640`

## How To Resume

Start a new Codex session in `C:\projects\cdt-workflow` and paste:

```text
Read context.md first, then continue the CDT workflow work. The active repo shape is src/ plus workflow-api/. The old tracked api/ host and stale generated docs were removed. Preserve .tools/smtp4dev. Current uncommitted source work includes the dbo schema fix for EF migrations plus MigrationSchemaTests. Keep workflow logic generic and metadata-driven.
```

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

### SQL schema fix

Target DB problem: `init-db` could create tables under the executing user's default schema instead of `dbo`.

Implemented fix:

- `WorkflowDbContext` now defines `DefaultSchema = "dbo"` and calls `HasDefaultSchema(DefaultSchema)`.
- Runtime and design-time SQL Server configuration uses `MigrationsHistoryTable("__EFMigrationsHistory", WorkflowDbContext.DefaultSchema)`.
- Existing migrations and model snapshot were updated so tables, foreign keys, and migration history use `dbo`.
- Added `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`.

This fix applies to fresh/init migrations. Existing databases with tables already created under a user schema need a controlled cleanup or schema transfer.

### Documentation cleanup

- Rewrote `CODEX_HANDOFF.md`, `NEXT_CODEX_PROMPT.md`, and this `context.md` to match the new shape.
- Updated `README.md` to describe the current `src/` plus `workflow-api/` shape and current local verification DB.
- Added `docs/README.md`.
- Removed old generated docs that pointed at the deleted host and old controller names.

## Notification Template Feature State

The generic email notification template feature is complete through commit `a0e5640`.

Key behavior:

- Notification subject/body templates are SQL metadata on notification routes.
- Portfolio Trade/Directed Trade language is seed/sample metadata only.
- The external API or host supplies `notificationContext` values.
- The reusable engine renders simple `{{tokenName}}` placeholders.
- Missing required tokens return `ValidationFailed` and roll back workflow side effects.
- Action-created task notifications persist rendered subject/body in the notification outbox.
- SLA overdue processing uses the task's stored `NotificationRouteKey` and `NotificationContextJson`.
- Overdue notifications prefix the route subject with metadata, currently `Overdue: `.
- `returnReason` has been removed from all templates.
- SMTP sends the persisted outbox body.

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
- A hosted SLA service should call `IRunSlaService.RunAsync(...)` and then use the existing outbox processing path.
- The hosted SLA service should not call external-api to fetch overdue email data because the task snapshot already carries the route/context.

## Verification Already Completed

After cleanup of the legacy host, stale docs, and handoff files:

```powershell
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter MigrationSchemaTests /p:UseAppHost=false
```

Results:

```text
src build passed with 0 warnings/errors.
workflow-api build passed with 0 warnings/errors.
workflow-api tests passed: 41 passed.
MigrationSchemaTests passed: 1 passed.
```

## Known Risks And Remaining Work

- Existing target databases with non-`dbo` workflow tables need a controlled migration/cleanup plan.
- Live smtp4dev verification was not rerun for the notification-template slice; SMTP body delivery is covered by tests and persisted outbox behavior.
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
