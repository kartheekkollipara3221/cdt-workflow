# Verification, Cutover, and Rollback

Use this checklist after manual API integration. The external API was not available during documentation creation, so API commands use placeholders.

## Verification Table

| Verification Area | Command / Action | Expected Result | Failure Meaning | Fix Direction |
|---|---|---|---|---|
| Current repo restore | `dotnet restore src/WorkflowEngine.sln` | Restore succeeds. | SDK/package/feed issue. | Fix SDK or package restore. |
| Current repo build | `dotnet build src/WorkflowEngine.sln -warnaserror` | Build succeeds with warnings as errors. | Source or package incompatibility. | Fix compile warnings/errors. |
| Current repo tests | `dotnet test src/WorkflowEngine.sln` | Default tests pass. | Reusable behavior regression or SQL config issue. | Inspect failing tests and SQL env vars. |
| Console DB init | `dotnet run --project src/WorkflowEngine.Console -- init-db` | Migrations and Portfolio Trade seed applied. | Connection/migration/seed failure. | Fix connection string or migration ownership. |
| Console seed verification | `dotnet run --project src/WorkflowEngine.Console -- verify-seed` | Seed verification passed. | Metadata mismatch. | Run seed and inspect verification errors. |
| Console demo | `dotnet run --project src/WorkflowEngine.Console -- demo` | Demo completed successfully. | Core flow or SQL issue. | Fix reusable workflow behavior before API cutover. |
| API restore | `dotnet restore <ApiSolutionPath>` | Restore succeeds. | Reference/package issue. | Fix project references or versions. |
| API build | `dotnet build <ApiSolutionPath> -warnaserror` | Build succeeds. | Integration compile issue. | Fix API code. |
| API tests | `dotnet test <ApiSolutionPath>` | API tests pass. | Controller/DI/auth/status mapping issue. | Fix API integration. |
| SQL migration | Apply workflow migrations. | Workflow tables and constraints exist. | Migration ownership/config problem. | Fix EF tooling/deployment scripts. |
| Seed verification | Run `ISeedVerificationService` path. | Passed. | Broken seed data. | Reapply seed or fix metadata. |
| HTTP smoke tests | Run curl examples below. | Expected status and result codes. | Endpoint/auth/body issue. | Fix controller/actor/mapper. |
| SLA/outbox | Run hosted service/job/admin op. | Overdue notification queued and processed. | Background config/sender issue. | Fix hosted scope, intervals, sender config. |
| Logging | Inspect logs. | Correlation IDs and workflow identifiers present; no raw payload. | Observability gap or data leak. | Adjust logging. |
| Security | Try normal user reset/admin override. | `403` or route absent. | Privilege exposure. | Remove or gate endpoints. |

## Pre-Migration Verification

Run in current repo before API changes:

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
```

With a configured SQL connection:

```bash
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
dotnet run --project src/WorkflowEngine.Console -- demo
```

## API Verification Commands

```bash
dotnet restore <ApiSolutionPath>
dotnet build <ApiSolutionPath> -warnaserror
dotnet test <ApiSolutionPath>
```

## Manual HTTP Smoke Tests

Routes are placeholders from document 05. Replace host, token, and IDs.

### Start Workflow

```bash
curl -i -X POST "https://localhost:5001/api/workflows/PortfolioTrade/instances" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d "{\"businessObjectType\":\"PortfolioTrade\",\"businessObjectId\":\"PT-1001\"}"
```

Expected:

- HTTP `200` or `201`.
- Body `result.resultCode = "Success"`.
- Body `result.workflowInstanceId = "WF-PT-1001"`.

### Get Context

```bash
curl -i "https://localhost:5001/api/workflows/instances/WF-PT-1001/context" \
  -H "Authorization: Bearer <maker-token>"
```

Expected:

- HTTP `200`.
- `workflowContext.workflow.currentState = "Draft"`.
- Maker sees `SaveDraft` and `SubmitForChecker` if claims map to `Maker`.

### Execute SubmitForChecker

```bash
curl -i -X POST "https://localhost:5001/api/workflows/instances/WF-PT-1001/actions" \
  -H "Authorization: Bearer <maker-token>" \
  -H "Content-Type: application/json" \
  -d @samples/workflow-actions/02-submit-for-checker.json
```

Expected:

- HTTP `200`.
- `result.resultCode = "Success"`.
- `result.oldState = "Draft"`.
- `result.newState = "CheckerReview"`.
- `result.createdTask.taskKey = "CheckerReviewTask"`.

In a real API test, remove or ignore `testExpectations`, and confirm actor fields are overwritten by claims.

### Concurrency Conflict

Submit the same stale `02-submit-for-checker.json` again with a different idempotency key and stale `expectedInstanceVersion = 1`.

Expected:

- HTTP `409`.
- `result.resultCode = "ConcurrencyConflict"` or `IdempotencyConflict` depending on idempotency key reuse.

### Idempotency Replay

Submit the exact same request with the exact same idempotency key after the first success.

Expected:

- HTTP `200`.
- `result.resultCode = "Success"`.
- `result.diagnostics.replay = true`.
- No duplicate tasks, audit rows, notification rows, or mock section rows.

### Admin Seed Verification If Exposed

```bash
curl -i -X POST "https://localhost:5001/api/admin/workflow/seed/verify" \
  -H "Authorization: Bearer <admin-token>"
```

Expected:

- HTTP `200`.
- Verification passes.
- Normal user token receives `403`, or the endpoint does not exist.

## Behavior Comparison With Console

Compare API behavior against console commands:

- `start-workflow` -> `POST /api/workflows/{workflowKey}/instances`
- `show-context` -> `GET /api/workflows/instances/{workflowInstanceId}/context`
- `execute-json` -> `POST /api/workflows/instances/{workflowInstanceId}/actions`
- `show` / `show-tasks` / `show-history` / `show-notifications` -> admin inspection endpoints if exposed
- `run-sla` -> hosted service, scheduled job, or admin endpoint
- `process-notification-outbox` -> hosted service, scheduled job, or admin endpoint
- `verify-seed` -> deployment check or admin endpoint

## Production Readiness Checklist

- API uses authenticated actor resolver.
- Body actor roles are ignored or overwritten.
- Reset is absent or admin/environment gated.
- Admin override is absent or strict admin-only.
- No EF entities are public DTOs.
- Portfolio Trade transitions are not hardcoded in controllers.
- SQL migrations applied.
- Seed verification passes.
- SLA/outbox processing path chosen.
- Notification sender configured safely.
- No raw payload logging.
- API tests pass.
- Rollback plan tested.

## Rollback Plan

1. Disable workflow API routes or feature flag.
2. Stop hosted SLA/outbox services if newly enabled.
3. Route clients back to previous console/manual process if applicable.
4. Preserve database state for investigation; do not delete workflow tables.
5. If schema rollback is required, use database backup/approved migration rollback plan.
6. Keep `WorkflowEngine.Console` available for inspection and seed verification during rollback.

## Cutover Plan

1. Deploy API code with workflow endpoints disabled or admin-only.
2. Apply migrations.
3. Seed and verify.
4. Enable read-only context endpoint for limited users.
5. Enable start workflow for limited domain flow.
6. Enable execute action for a controlled cohort.
7. Enable SLA/outbox processing.
8. Monitor logs, audit, notification outbox, and SQL errors.
9. Expand traffic when smoke tests and monitoring pass.

