# Ready-to-paste prompt for next Codex session

You are working in `C:\projects\cdt-workflow`.

Continue the `WorkflowEngine.WorkflowApi` migration/alignment work. Do not recreate the project.

Before planning or editing:

1. Read `AGENTS.md` if present.
2. Read `PROJECT.md`; it is the source of truth.
3. Read `CODEX_HANDOFF.md`.
4. Read `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
5. Read `workflow-api/final-state-project.txt`.
6. Inspect the current worktree:

```powershell
git status --short
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

Completed in the latest sessions:

- Added `POST /v1/WorkflowStart`.
- Added `GET /v1/WorkflowContext` target-style nested response shape.
- Added `POST /v1/WorkflowAction` workflow-only request body and nested response shape.
- Added `workflow-api/WORKFLOW_API_BOUNDARIES.md`.
- Verified live SQL-backed start/context/action sequence using `PT-API-START`.
- `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false` passed, with one warning only when a running API process locked the apphost exe.
- `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false` passed with 32 tests.
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
  "actionKey": "SubmitForChecker"
}
```

Headers for all endpoints during local testing:

```text
X-Workflow-UserId: maker1
X-Workflow-Roles: Maker
X-Workflow-DisplayName: Maker One
```

Suggested next task:

Review and normalize `workflow-api/src/WorkflowEngine.WorkflowApi/Properties/launchSettings.json`, then decide whether to keep `src/WorkflowEngine.Console/appsettings.Development.json` pointing at the API development database.
