# CDT Workflow Engine

Reusable .NET workflow engine for replacing SharePoint Designer workflow behavior. Portfolio Trade is the first sample workflow definition. The active repo shape is the reusable engine and console/demo host under `src/`, plus the ASP.NET Core workflow host under `workflow-api/`.

## Quickstart

### Prerequisites

- .NET 10 SDK.
- SQL Server local instance.
- Configure `ConnectionStrings__DefaultConnection` or a local, untracked `appsettings.Development.json`.
- Current local verification connection string: `Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.

### First run

```bash
dotnet build src/WorkflowEngine.sln
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
dotnet run --project src/WorkflowEngine.Console -- demo
```

The console commands are still the easiest end-to-end demo path. The active workflow API host is `workflow-api/WorkflowEngine.WorkflowApi.slnx`; read `workflow-api/WORKFLOW_API_BOUNDARIES.md` before changing endpoints.

### Main happy path

<!-- BEGIN: HappyPath_Section25_1 -->
```bash
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/01-save-draft.json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user checker1 --roles Checker --format json
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user trader1 --roles TradingDesk --format json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/07-trading-trade-executed.json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/09-bdrm-trade-not-executed-properly.json
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user trader1 --roles TradingDesk --format json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/10-trading-rework-trade-executed.json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/08-bdrm-trade-fulfilled.json
dotnet run --project src/WorkflowEngine.Console -- show-history WF-PT-1001
```
<!-- END: HappyPath_Section25_1 -->

### Exploring branches manually

#### Checker Return to Maker

<!-- BEGIN: CheckerReturnBranch_Section25_2 -->
```bash
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1002
```
<!-- END: CheckerReturnBranch_Section25_2 -->

#### Trading Desk Return to Maker

<!-- BEGIN: TradingReturnBranch_Section25_3 -->
```bash
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1003
```
<!-- END: TradingReturnBranch_Section25_3 -->

### SLA and notifications

```bash
# Duration-mode example (Checker deadline fast-forward)
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox

# After sample 05 creates TradingDeskReviewTask with targetDate 2030-01-20:
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-20T14:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-21T14:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox
```

The first Trading Desk scan is the 9:00 AM Eastern due-today occurrence; the second is the next-calendar-day overdue occurrence. Repeating the same scan does not duplicate an occurrence. Local verification fast-forwards injected time; SIT/UAT invoke `run-sla` on wall-clock time.

### Inspection commands

```bash
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- show-history WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1001
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user checker1 --roles Checker --format json
```

### Troubleshooting

- `AlreadyExists`: use `--reset` for demos, but assign a new `BusinessObjectId` in production.
- Connection string failures: check `ConnectionStrings:DefaultConnection`.
- `verify-seed failed`: run `init-db` again.
- Tier 2 SMTP tests fail: install `smtp4dev` or Papercut, then see Testing below.

---

## Integration Guide

`PROJECT.md` is the source of truth. This README explains the operating model and points to the spec; when the two disagree, `PROJECT.md` wins.

### Engine boundary

The engine owns persisted workflow state, metadata-driven transitions, pending/completed tasks, SLA due dates, notification outbox rows, idempotency records, audit history, and the standard workflow result/context contracts.

The engine does not own form metadata, field-level validation, authentication, role lookup tables, UI button rendering, or Portfolio Trade field semantics. Portfolio Trade lives in seed metadata and sample envelopes only. See `PROJECT.md` §5, §8, §11, and §22.

### Form-open contract: WorkflowContextResponse

`show-context` returns the same workflow object the workflow API returns when a form opens. The response contains workflow identity/version/current state, business object identity, actor access, current task, SLA, available actions, and server time.

Abbreviated shape:

```json
{
  "workflowContext": {
    "workflow": { "workflowInstanceId": "WF-PT-1001", "currentState": "CheckerReview", "workflowInstanceVersion": 2 },
    "businessObject": { "objectType": "PortfolioTrade", "objectId": "PT-1001" },
    "actorAccess": { "userId": "checker1", "effectiveRoles": ["Checker"], "canAct": true },
    "currentTask": { "taskKey": "CheckerReviewTask", "assignedRole": "Checker" },
    "sla": {
      "status": "OnTrack",
      "dueAtUtc": "2030-01-21T14:00:00Z",
      "overdue": false,
      "calculationMode": "ApplicationTargetDate",
      "targetDate": "2030-01-20",
      "scheduleVersion": 1,
      "nextReminderAtUtc": "2030-01-20T14:00:00Z"
    },
    "availableActions": [{ "actionKey": "CheckerSendToTradingDesk", "requestDefaults": {} }]
  }
}
```

The UI uses `availableActions` and `requestDefaults` to build the later public workflow action request. It submits `actionKey`; it does not compute next state. See `PROJECT.md` §8.1 for the full field reference.

### UI-facing Workflow API contract

`POST /v1/WorkflowStart` accepts only workflow identity, an optional first action, an optional flat SLA date, and named notification inputs:

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectId": "PT-1001",
  "actionKey": "SubmitForChecker",
  "notification": {
    "recipientInputs": {
      "originalMaker": [
        {
          "userId": "maker1",
          "emailAddress": "maker@example.test",
          "displayName": "Maker One"
        }
      ]
    },
    "templateData": {
      "referenceNumber": "1050"
    }
  }
}
```

`POST /v1/WorkflowAction` uses the persisted instance plus the metadata action key. `assignedRoleKey`, `slaTargetDate`, and `notification` are optional and used only when the selected metadata requires them:

```json
{
  "workflowInstanceId": "WF-PT-1001",
  "actionKey": "CheckerSendToTradingDesk",
  "assignedRoleKey": "TradingDeskTaxable",
  "slaTargetDate": "2030-01-20",
  "notification": {
    "templateData": {
      "referenceNumber": "1050"
    }
  }
}
```

The active workflow definition supplies `businessObjectType`; the API generates correlation and idempotency identity. Current state, task, version, route, final To/Cc placement, and SLA timing rules come from persistence and versioned metadata. Unknown root or nested fields are rejected. See `workflow-api/WORKFLOW_API_BOUNDARIES.md` for the full boundary and `workflow-api/samples` for complete request examples.

For `CheckerSendToTradingDesk`, `assignedRoleKey` is the stable metadata key used for both task assignment and notification-rule selection. The seeded `RouteTradingDeskAssigned` metadata can map one desk role to multiple ordered To addresses; the engine sends one message to all matching addresses and snapshots the same audience for SLA reminders. `notification.templateData.deskName` is display-only. The UI does not send desk email addresses.

### Console-only contract: WorkflowActionEnvelope

`execute-json` accepts one composite test/demo envelope: request metadata, business object identity, actor, UI action, section data save, workflow action, and optional `testExpectations` in samples. This is not the UI-facing HTTP request DTO.

Abbreviated shape:

```json
{
  "request": { "clientActionId": "sample-02-submit-for-checker", "correlationId": "corr-sample-02" },
  "businessObject": { "objectType": "PortfolioTrade", "objectId": "PT-1001" },
  "actor": { "userId": "maker1", "roles": ["Maker"] },
  "uiAction": { "sectionKey": "Maker", "buttonKey": "MakerPrimary", "saveAsDraft": false },
  "dataSave": { "sectionKey": "Maker", "payload": {} },
  "workflow": {
    "workflowInstanceId": "WF-PT-1001",
    "actionKey": "CheckerSendToTradingDesk",
    "assignedRoleKey": "TradingDeskTaxable",
    "slaInput": { "targetDate": "2030-01-20" }
  }
}
```

`MakerPrimary` uses one physical Maker button: `saveAsDraft=true` maps to `SaveDraft`; `saveAsDraft=false` maps to submit/resubmit. Idempotency and concurrency guards belong in this envelope. See `PROJECT.md` §9 for the full field reference.

For application-date policies, the public caller maps its business date to flat `slaTargetDate`. The API maps that value to internal `slaInput.targetDate`. The caller sends no duration, reminder time, timezone, overdue offset, recurrence, route, or `dueAtUtc`; versioned `SlaPolicy` metadata owns them. The engine does not parse public form data.

### Action result contract: WorkflowActionResult

Successful and failed actions return one result envelope. Success includes old/new state, closed/created task, notification summary, audit summary, refreshed context, request hash, and idempotency metadata. Failure includes a result code, message, diagnostics, and refreshed context for concurrency/version failures when available.

Abbreviated shape:

```json
{
  "result": {
    "success": true,
    "resultCode": "Success",
    "workflowInstanceId": "WF-PT-1001",
    "oldState": "Draft",
    "newState": "CheckerReview",
    "createdTask": { "taskKey": "CheckerReviewTask", "assignedRole": "Checker" }
  },
  "workflowContext": {}
}
```

See `PROJECT.md` §8.3 for result field details.

### Result codes

- `Success`: action completed.
- `ValidationFailed`: envelope shape or MakerPrimary consistency failed.
- `Forbidden`: actor roles do not match the transition role.
- `InvalidTransition`: action/state combination is not valid.
- `ConcurrencyConflict`: one or more expected-state guards did not match.
- `WorkflowDefinitionVersionMismatch`: client workflow definition version is stale.
- `IdempotencyConflict`: same idempotency key used for a different request.
- `AmbiguousRoleMatch`: actor has multiple roles that match different transitions.
- `ActorRolesMismatch`: reserved for actor role consistency errors.
- `AlreadyExists`: start requested for an existing workflow lifetime.
- `WorkflowInstanceNotFound`: requested instance does not exist.

### Idempotency and concurrency

The public Workflow API generates its correlation, client-action, and idempotency identities inside the trusted service boundary. In the internal console envelope, `idempotencyKey` is the canonical dedupe key; absent that, `clientActionId` is used. Matching replays return the stored result and do not re-run side effects. Same key plus different normalized request returns `IdempotencyConflict`.

Concurrency guards are independent: `expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, and `expectedInstanceVersion`. Any supplied guard must match persisted state. `--admin-override` bypasses role authorization only; it does not bypass validation, idempotency, concurrency, or definition-version checks. See `PROJECT.md` §9.

### SLA model

SLA is task-driven and metadata-driven. Task definitions may point at either a configured duration policy or an application-target-date policy; tasks without a policy get `DueAtUtc = null`. `MakerReworkTask` intentionally has no SLA.

`TradingDeskReviewTask` is the seeded application-date example. The public start/action request forwards the application's current date as `slaTargetDate`. The engine creates schedule version 1, treats the same date as a no-op, and automatically supersedes/reschedules when a later request carries a changed date. Pending old occurrences are cancelled; sent history is preserved. An actionless body on the same `POST /v1/WorkflowAction` can synchronize the current pending task without moving workflow state or writing audit—there is no second SLA API.

The Trading Desk policy sends `SlaTaskDueToday` at configured 09:00 `America/New_York` on the target date, becomes overdue on target + 1 calendar day, then queues one `SlaTaskOverdue` occurrence per calendar day including weekends. `run-sla` deduplicates by schedule/date/kind, never backfills every missed day, and stops when the task completes. `show-context` computes `OnTrack`, `Overdue`, or `NoSla` from the persisted schedule projection and current time. See `PROJECT.md` sections 12 and 13.

### Notifications

Notifications use an outbox table. Workflow transitions queue rows; `process-notification-outbox` sends pending rows through `INotificationSender`.

### SQMS scheduled integration

SQMS is disabled in checked-in configuration. After the DBA package has been applied and environment-specific SQMS settings are supplied, Task Scheduler invokes:

```bash
dotnet run --project src/WorkflowEngine.Console -- run-sqms-scheduled
```

The SQMS integration is a thin in-process adapter around the existing metadata-driven engine; the target API continues to own its HTTP contract, target rows, trusted actors, and address resolution. Production uses the explicit DBA SQL package and never application-startup schema creation or seeding. See [target API integration](docs/sqms/TARGET-API-INTEGRATION.md), the [approved design](docs/superpowers/specs/2026-08-20-sqms-thin-layer-integration-design.md), and the [DBA deployment runbook](docs/sqms/DBA-DEPLOYMENT.md).

Implementations are `ConsoleNotificationSender`, `NoOpNotificationSender`, and `SmtpNotificationSender`. Public callers supply only named metadata-declared inputs:

```json
{
  "notification": {
    "recipientInputs": {
      "selectedCcUsers": [
        {
          "userId": "manager1",
          "emailAddress": "manager@example.test",
          "displayName": "Manager One"
        }
      ]
    },
    "templateData": {
      "referenceNumber": "1050"
    }
  }
}
```

Start captures immutable participants such as `originalMaker`; action requests may add only route-declared inputs such as `selectedCcUsers`. Metadata resolves final To/Cc placement. Address strings are stored without syntax, directory, mailbox, or SMTP validation. Resolution preserves source/rule order, compares duplicates case-insensitively, retains the first opaque string, and gives To precedence over Cc. The final snapshot is stored on the task and initial outbox row; SLA reminders reuse it unchanged. One outbox row is one email with all To/Cc recipients and one message-level retry lifecycle. Internal console samples continue supplying final arrays directly for console testing only. `Smtp:RecipientOverride` sends only to the override mailbox and adds intended recipients to controlled diagnostic content. See `PROJECT.md` §14.

### Configuration

Copy-pasteable `appsettings.json` shape:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DOLLY;Database=workflowengine;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
  },
  "Notifications": {
    "Sender": "Console"
  },
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "From": "workflow-demo@example.test",
    "RecipientOverride": ""
  }
}
```

Environment overrides use .NET configuration keys, for example `ConnectionStrings__DefaultConnection` and `Notifications__Sender`.

### Going to production

Switch `Notifications:Sender` from `Console` to `Smtp`. Configure `Smtp:Host` and `Smtp:From`; callers supply To/Cc arrays on each notification-producing request. Use `Smtp:RecipientOverride` during UI integration smoke tests.

Never use `--reset` in production. One business object identifier means one workflow lifetime ever in v1. Assign a new `BusinessObjectId` for a new business lifetime.

The application-anchored SLA migration targets a clean latest-schema database and does not backfill legacy active SLA tasks/outbox rows. Do not recreate an existing application database without explicit approval and a deployment plan.

### Testing

- Tier 1 default: `dotnet test`
- SQL-backed tests require `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` or `ConnectionStrings__DefaultConnection`; the test database name is replaced per suite.
- Tier 2 SMTP: start smtp4dev with `docker run --rm -p 3025:25 -p 5000:80 rnwood/smtp4dev`, set `RUN_SMTP_TESTS=1`, then run `dotnet test --filter Category=Smtp`
- Fresh-clone gate: `dotnet test --filter Category=FreshClone`
- Manual smoke: run README Quickstart and one return-to-Maker branch.
- GitHub Actions SQL jobs require repository secret `CI_SQL_SA_PASSWORD` for the SQL Server container.

### Extending the engine

Add a new workflow by seeding states, actions, roles, transitions, task definitions, SLA policies, notification routes, and action presentation hints. Choose `DurationFromTaskCreation` or `ApplicationTargetDate` in metadata; no reusable engine change is needed to use the target-date mode at another task step. Add sample envelopes and update seed verification expectations.

Stop and ask before adding parallel branches, multiple pending tasks, CQRS, MediatR, event sourcing, BPMN, message brokers, plugin frameworks, user-to-role SQL tables, failure audit, conditional-action fields, or schema changes outside the locked v1 scope. The stop-and-ask list lives in `PROJECT.md` §22.

### References

- Full spec and source of truth: `PROJECT.md`
- Required tests: `PROJECT.md` §20
- Quality gates: `PROJECT.md` §21
- Demo scripts: `PROJECT.md` §25
- README guidance: `PROJECT.md` §26
- Workflow API boundaries: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
