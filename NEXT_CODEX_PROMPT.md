# Ready-to-paste prompt for next Codex session

You are working in `C:\projects\cdt-workflow`.

Continue the `WorkflowEngine.WorkflowApi` migration/alignment work. Do not recreate the project.

Before planning or editing:

1. Read `AGENTS.md` / `AGENTS.MD` if present.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `CODEX_HANDOFF.md`.
4. Read `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
5. Read `workflow-api/final-state-project.txt`.
6. Inspect the current worktree:

```powershell
git status --short --untracked-files=all
git diff --stat
git diff --name-only
```

Then continue from the highest-priority incomplete task in `TODO_WORKFLOW_API.md`.

Important constraints:

- Avoid redoing completed work.
- Preserve existing workflow engine behavior.
- Keep workflow logic generic and metadata-driven.
- Keep API controllers thin.
- Keep changes minimal and migration-friendly for later movement into the target API project.
- Do not move `WorkflowEngine.Console` into the target API as-is.
- Prefer reusing `WorkflowEngine.Domain`, `WorkflowEngine.Application`, and `WorkflowEngine.Infrastructure`.
- Do not add new external packages unless clearly required and approved by project constraints.
- Keep the temporary trusted-header actor adapter for now; do not implement real user lookup/auth/role mapping.
- Workflow API is workflow-only. Do not add form data validation, form persistence, business object lifecycle, or public console admin behavior.
- Do not revert the user's local changes unless explicitly asked.

Completed in latest sessions:

- Added `POST /v1/WorkflowStart`.
- Added target-style `GET /v1/WorkflowContext` response shape.
- Added `POST /v1/WorkflowAction` workflow-only request body and nested response shape.
- Added targeted action-created notification delivery for `POST /v1/WorkflowAction`: if a successful action queues metadata notification rows, the API sends only rows matching the action workflow instance ID and correlation ID before returning success; delivery failure becomes `NotificationDeliveryFailed` and HTTP 500.
- Confirmed `POST /v1/WorkflowStart` sends no email unless a future metadata-backed start notification is added.
- Added/updated `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
- Verified live SQL-backed start/context/action sequence using `PT-API-START`.
- Verified live API SMTP delivery with smtp4dev and a real `POST /v1/WorkflowAction`.
- User also verified Swagger Return to Maker on `WF-PT-SWAGGER-001XX`.
- User verified console `process-notification-outbox` SMTP path works.
- User confirmed local API and console testing should use `Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.
- After the user stopped the running WorkflowApi process, Debug build/test passed for `workflow-api\WorkflowEngine.WorkflowApi.slnx`: build 0 warnings/errors, tests 38 passed.
- For this DB alignment update, Release builds passed for `workflow-api\WorkflowEngine.WorkflowApi.slnx`, `src\WorkflowEngine.sln`, and `api\WorkflowEngine.Api.sln`.
- Release tests passed for `workflow-api\WorkflowEngine.WorkflowApi.slnx` and `api\WorkflowEngine.Api.sln`: 38 passed each, 0 failed.
- Dynamic trading desk assignment is in progress: `CheckerSendToTradingDesk` accepts optional `assignedRoleKey`; valid values are `TradingDeskTaxable`, `TradingDeskTaxExempt`, and `TradingDeskEquity`; broad `TradingDesk` remains the actor role for later trading actions.
- `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false` passed with 0 warnings and 0 errors before the final docs-only handoff update.
- `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false` passed with 34 tests before the final docs-only handoff update.
- Broader `dotnet test src\WorkflowEngine.sln -m:1 /p:UseAppHost=false` passed earlier with `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` set: 206 passed, 2 skipped.

Current endpoint contracts:

`POST /v1/WorkflowStart`

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectType": "PortfolioTrade",
  "businessObjectId": "PT-123"
}
```

`GET /v1/WorkflowContext?workflowInstanceId=WF-PT-123`

`POST /v1/WorkflowAction`

```json
{
  "workflowInstanceId": "WF-PT-123",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable"
}
```

Headers for all endpoints during local testing:

```text
X-Workflow-UserId: maker1
X-Workflow-Roles: Maker
X-Workflow-DisplayName: Maker One
```

For Checker actions, use:

```text
X-Workflow-UserId: checker1
X-Workflow-Roles: Checker
X-Workflow-DisplayName: Checker One
```

Important current worktree notes:

- `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json` has local SMTP env vars in the `http` profile. Review before commit. Current file shows `Smtp__Port = 3025`; verify this against the current smtp4dev listener.
- The action notification dispatcher source files are tracked now:
  - `workflow-api/src/WorkflowEngine.WorkflowApi/Services/IWorkflowActionNotificationDispatcher.cs`
  - `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs`
- Unrelated untracked `~/.codex/superpowers/` should not be added.

Suggested next task:

Review the current worktree and decide whether to keep or adjust the local SMTP env vars in `launchSettings.json`; then continue from the highest-priority unchecked item in `TODO_WORKFLOW_API.md`.

Before final handoff, run:

```powershell
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false
```
