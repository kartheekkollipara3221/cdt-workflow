# CDT Workflow Engine

Reusable .NET workflow engine proof of concept for replacing SharePoint Designer workflow behavior. Portfolio Trade is the first sample workflow definition; the current interface is a console POC, with the engine boundary designed for a future API/UI integration.

## Quickstart

### Prerequisites

- .NET 10 SDK.
- SQL Server local instance.
- Default connection string: `Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`.
- Override with `ConnectionStrings__DefaultConnection` or `appsettings.Development.json`.

### First run

```bash
dotnet build
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
dotnet run --project src/WorkflowEngine.Console -- demo
```

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
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox
```

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

`show-context` returns the same workflow object the future API should return when a form opens. The response contains workflow identity/version/current state, business object identity, actor access, current task, SLA, available actions, and server time.

Abbreviated shape:

```json
{
  "workflowContext": {
    "workflow": { "workflowInstanceId": "WF-PT-1001", "currentState": "CheckerReview", "workflowInstanceVersion": 2 },
    "businessObject": { "objectType": "PortfolioTrade", "objectId": "PT-1001" },
    "actorAccess": { "userId": "checker1", "effectiveRoles": ["Checker"], "canAct": true },
    "currentTask": { "taskKey": "CheckerReviewTask", "assignedRole": "Checker" },
    "sla": { "status": "OnTrack", "dueAtUtc": "2030-01-15T16:00:00Z", "overdue": false },
    "availableActions": [{ "actionKey": "CheckerSendToTradingDesk", "requestDefaults": {} }]
  }
}
```

The UI uses `availableActions` and `requestDefaults` to build the later button-click envelope. It does not compute next state. See `PROJECT.md` §8.1 for the full field reference.

### Button-click contract: WorkflowActionEnvelope

`execute-json` accepts one composite envelope for a user click: request metadata, business object identity, actor, UI action, section data save, workflow action, and optional `testExpectations` in samples.

Abbreviated shape:

```json
{
  "request": { "clientActionId": "sample-02-submit-for-checker", "correlationId": "corr-sample-02" },
  "businessObject": { "objectType": "PortfolioTrade", "objectId": "PT-1001" },
  "actor": { "userId": "maker1", "roles": ["Maker"] },
  "uiAction": { "sectionKey": "Maker", "buttonKey": "MakerPrimary", "saveAsDraft": false },
  "dataSave": { "sectionKey": "Maker", "payload": {} },
  "workflow": { "workflowInstanceId": "WF-PT-1001", "actionKey": "SubmitForChecker" }
}
```

`MakerPrimary` uses one physical Maker button: `saveAsDraft=true` maps to `SaveDraft`; `saveAsDraft=false` maps to submit/resubmit. Idempotency and concurrency guards belong in this envelope. See `PROJECT.md` §9 for the full field reference.

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

`idempotencyKey` is the canonical dedupe key; absent that, `clientActionId` is used. Matching replays return the stored result and do not re-run side effects. Same key plus different normalized request returns `IdempotencyConflict`.

Concurrency guards are independent: `expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, and `expectedInstanceVersion`. Any supplied guard must match persisted state. `--admin-override` bypasses role authorization only; it does not bypass validation, idempotency, concurrency, or definition-version checks. See `PROJECT.md` §9.

### SLA model

SLA is task-driven. Task definitions may point at an SLA policy; tasks without a policy get `DueAtUtc = null`. `MakerReworkTask` intentionally has no SLA in v1.

`run-sla --now ...` scans pending tasks with due dates in the past and queues one `SlaTaskOverdue` notification per task ever. `show-context` computes `OnTrack`, `Overdue`, or `NoSla` directly from the current task and time. See `PROJECT.md` §12 and §13.

### Notifications

Notifications use an outbox table. Workflow transitions queue rows; `process-notification-outbox` sends pending rows through `INotificationSender`.

Implementations are `ConsoleNotificationSender`, `NoOpNotificationSender`, and `SmtpNotificationSender`. SMTP resolves role recipients from configuration only. `Smtp:RecipientOverride` is the smoke-test safety hatch. See `PROJECT.md` §14.

### Configuration

Copy-pasteable `appsettings.json` shape:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
  },
  "Notifications": {
    "Sender": "Console"
  },
  "Smtp": {
    "Host": "localhost",
    "Port": 25,
    "From": "workflow-demo@example.test",
    "RecipientOverride": "",
    "RoleEmailMap": {
      "Maker": "maker@example.test",
      "Checker": "checker@example.test",
      "TradingDesk": "trading@example.test",
      "BDRMClientService": "bdrm@example.test"
    }
  }
}
```

Environment overrides use .NET configuration keys, for example `ConnectionStrings__DefaultConnection` and `Notifications__Sender`.

### Going to production

Switch `Notifications:Sender` from `Console` to `Smtp`. Configure `Smtp:Host`, `Smtp:From`, and `Smtp:RoleEmailMap`. Use `Smtp:RecipientOverride` during UI integration smoke tests.

Never use `--reset` in production. One business object identifier means one workflow lifetime ever in v1. Assign a new `BusinessObjectId` for a new business lifetime.

### Testing

- Tier 1 default: `dotnet test`
- Tier 2 SMTP: `dotnet test --filter Category=Smtp`
- Fresh-clone gate: `dotnet test --filter Category=FreshClone`
- Manual smoke: run README Quickstart and one return-to-Maker branch.

### Extending the engine

Add a new workflow by seeding states, actions, roles, transitions, task definitions, SLA policies, notification routes, and action presentation hints. Add sample envelopes and update seed verification expectations.

Stop and ask before adding parallel branches, multiple pending tasks, CQRS, MediatR, event sourcing, BPMN, message brokers, plugin frameworks, user-to-role SQL tables, failure audit, conditional-action fields, or schema changes outside the locked v1 scope. The stop-and-ask list lives in `PROJECT.md` §22.

### References

- Full spec and source of truth: `PROJECT.md`
- Required tests: `PROJECT.md` §20
- Quality gates: `PROJECT.md` §21
- Demo scripts: `PROJECT.md` §25
- README guidance: `PROJECT.md` §26
