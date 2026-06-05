# Codex Handoff

Last updated: 2026-06-05

## Current Goal

Clean the repository documentation and handoff files after pruning the legacy API host and local/generated folders. The active project shape is now the shared workflow engine under `src/` plus the ASP.NET Core workflow host under `workflow-api/`.

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

## Active Code Changes Since Last Commit

### SQL schema ownership fix

The target database issue was that `init-db` could create tables under the executing user's default schema instead of `dbo`. The fix pins EF Core to `dbo`:

- `WorkflowDbContext.DefaultSchema = "dbo"`
- `modelBuilder.HasDefaultSchema(DefaultSchema)`
- SQL Server migrations history table configured as `[dbo].[__EFMigrationsHistory]`
- Existing migrations and model snapshot updated to emit `[dbo]` schema-qualified tables and foreign keys
- Added `MigrationSchemaTests` to verify the generated migration script uses `dbo`

This affects fresh/init migrations. An existing database that already has user-schema tables still needs a controlled cleanup or schema transfer.

### Legacy host/docs cleanup

The old tracked `api/` host and stale generated docs are deleted in the working tree. The active API host is only `workflow-api/WorkflowEngine.WorkflowApi.slnx`.

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
- The hosted SLA service should call `IRunSlaService.RunAsync(...)` against the workflow DB. It should not call an external API to rebuild overdue email context; the task snapshot carries the notification route and context.

## Current Worktree Expectations

Expected local changes include:

- Deleted tracked `api/` files.
- Deleted stale tracked docs and generated bundle artifacts listed above.
- Modified infrastructure/migration files for the `dbo` schema fix.
- Added `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs`.
- Updated `PROJECT.md`, `README.md`, `CODEX_HANDOFF.md`, `NEXT_CODEX_PROMPT.md`, `context.md`, and `docs/README.md`.
- Untracked `.tools/` for smtp4dev should remain local unless the user asks to track it.
- Untracked `docs/superpowers/plans/2026-06-01-dynamic-trading-desk-assignment.md` already existed as local planning context.

Run before further edits:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Verification Status

Latest verification after the legacy host cleanup, stale docs removal, and handoff rewrite:

```powershell
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter MigrationSchemaTests /p:UseAppHost=false
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Results:

- Shared solution build passed with 0 warnings/errors.
- Active workflow API build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- Migration schema test passed: 1 passed.
- `init-db` completed on the configured local DB.
- `verify-seed` passed.

## Remaining Work

- Decide whether to commit the cleanup and schema fix together or split them.
- Review `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json` SMTP env vars before committing if that file becomes modified again.
- Decide how SLA/outbox processing will be hosted in the target environment.
- Replace trusted-header actor resolution with the target auth/claims resolver when integrating into the real host.
- Integrate EF migrations into the target deployment process.

## Next Session Prompt

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD, PROJECT.md, context.md, CODEX_HANDOFF.md, and workflow-api/WORKFLOW_API_BOUNDARIES.md first. The active repo shape is src/ plus workflow-api/; the old tracked api/ host, stale generated docs, and stale bundle/restore scripts were removed. Preserve .tools/smtp4dev. Current uncommitted source work includes the dbo schema fix and MigrationSchemaTests. Before editing, inspect git status and diffs, then continue from the user's latest request.
```
