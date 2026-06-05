# Ready-to-paste prompt for next Codex session

You are working in `C:\projects\cdt-workflow` on branch `console-code-first-0518`.

Before planning or editing:

1. Read `AGENTS.MD`.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `context.md`.
4. Read `CODEX_HANDOFF.md`.
5. Read `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
6. Inspect the current worktree:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

Current repo shape:

- `src/WorkflowEngine.sln` is the reusable workflow engine and console/demo host.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx` is the active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests` and `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests` are the active test suites.
- `samples/workflow-actions` contains Portfolio Trade sample action JSON.
- `docs/superpowers` contains current design/planning docs.
- `.tools/smtp4dev` is intentionally retained for local SMTP verification.

Important cleanup state:

- The old tracked `api/` host has been removed by user approval.
- Stale generated/historical docs for the old host were removed.
- Stale generated bundle/restore scripts were removed.
- Do not restore the deleted host, deleted docs, or deleted bundle scripts unless the user explicitly asks.
- Do not add `.tools/` unless the user asks to track the local smtp4dev tool.

Current uncommitted source work:

- EF Core is pinned to the `dbo` schema through `WorkflowDbContext.DefaultSchema`, `HasDefaultSchema("dbo")`, and `MigrationsHistoryTable("__EFMigrationsHistory", "dbo")`.
- Existing migrations and the model snapshot were updated to schema-qualify tables and foreign keys.
- `tests/WorkflowEngine.Tests/MigrationSchemaTests.cs` verifies generated migration SQL uses `[dbo]`.

Current feature state:

- Notification templates are implemented through SQL metadata and persisted outbox body content.
- The workflow API accepts optional `notificationContext` on action and start requests.
- SLA overdue notifications render from stored task notification snapshots and do not call an external API for email data.
- Portfolio Trade/Directed Trade wording remains seed/sample metadata only.

Latest external target-machine decision:

- The IIS-hosted target API should send normal action-created notifications immediately after the form action executes workflow movement.
- `run-sla` is only for overdue SLA detection.
- Windows Task Scheduler is available on the target machine and should run the separate workflow processor for `run-sla` and `process-notification-outbox`.
- `process-notification-outbox` is still needed for pending-row retry/backlog, including failed action-created SMTP rows and SLA overdue rows.
- Do not put the scheduled SLA/outbox loop inside the IIS target API.
- If implementation guidance resumes, keep the target API edit narrow: hook notification dispatch immediately after the service method that calls workflow execution, selecting created rows by `WorkflowInstanceId` + `CorrelationId`.

Useful verification commands:

```powershell
dotnet build src\WorkflowEngine.sln -m:1 /p:UseAppHost=false
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 --filter MigrationSchemaTests /p:UseAppHost=false
```

Current endpoint contracts:

`POST /v1/WorkflowStart`

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectType": "PortfolioTrade",
  "businessObjectId": "PT-123",
  "initialActionKey": "SubmitForChecker",
  "notificationContext": {
    "submitterDisplayName": "Maker One",
    "deskName": "Taxable",
    "securityType": "Other",
    "accountShortName": "EFG",
    "accountDisplayName": "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
    "tradeLink": "https://sharepoint.example/trades/PT-123"
  }
}
```

`GET /v1/WorkflowContext?workflowInstanceId=WF-PT-123`

`POST /v1/WorkflowAction`

```json
{
  "workflowInstanceId": "WF-PT-123",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable",
  "notificationContext": {
    "submitterDisplayName": "Maker One",
    "deskName": "Taxable",
    "securityType": "Other",
    "accountShortName": "EFG",
    "accountDisplayName": "INSURANCE-5091003029-EFG INSURANCE, LTD./ASI",
    "tradeLink": "https://sharepoint.example/trades/PT-123"
  }
}
```

Headers for local testing:

```text
X-Workflow-UserId: maker1
X-Workflow-Roles: Maker
X-Workflow-DisplayName: Maker One
```

For Checker actions:

```text
X-Workflow-UserId: checker1
X-Workflow-Roles: Checker
X-Workflow-DisplayName: Checker One
```

Suggested next task:

Continue target-machine integration guidance. First clarify whether the user wants one-step-at-a-time target API code changes for immediate action notification dispatch, or Task Scheduler setup steps for SLA/outbox processing. If code guidance resumes, stay destination-first and do not assume the local `workflow-api` host is the target being edited.
