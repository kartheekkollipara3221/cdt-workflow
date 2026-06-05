# Project Requirements: Reusable .NET Workflow Engine Console Demo

## 1. Purpose

Build a reusable .NET workflow engine that can replace SharePoint Designer workflow behavior.

This project is **not** the form project. The form/UI layer is already handled separately. This project focuses on workflow execution, task ownership, SLA tracking, notification outbox records, SQL seed data, audit history, and a console-based proof of concept.

The Portfolio Trade process is the first sample workflow definition used to prove the engine. The engine must remain reusable for other workflows.

## 2. One-line summary

Create a SQL-backed .NET console application that loads workflow definitions, starts workflow instances, returns workflow context for form-open scenarios, executes workflow actions from a standard JSON object, saves mocked section data, moves workflow state by metadata, creates/completes tasks, tracks SLA, queues notifications, writes audit history, and verifies seeded workflow data in SQL.

## 3. Business context

Legacy stack:

- SharePoint Designer workflows
- InfoPath forms
- SharePoint tasks/status/history

Target direction:

- Full-stack application
- Reusable workflow engine
- Modern API layer
- SQL-backed workflow definitions, instances, tasks, SLA, notification outbox, and audit
- Existing/new form UI calling the API with a JSON workflow action request

The console app is a proof of concept for the reusable workflow engine, not the final UI.

### 3.1 Glossary

| Term | Meaning |
|---|---|
| Maker | Role that drafts, saves, corrects, and submits/resubmits a Portfolio Trade request. |
| Checker | Role that reviews a Maker submission and can either return it to Maker or send it to Trading Desk. |
| Trading Desk | Role that reviews/executes the trade and can either return it to Maker or mark trade executed. |
| BDRM Client Service | Role that confirms the trade was fulfilled correctly or sends it back to Trading Desk Rework. |
| WorkflowActionEnvelope | Standard JSON request shape that drives `execute-json`; this is the button-click request object. |
| WorkflowContextResponse | Standard response object returned when the form opens or when `show-context` is executed; this tells the UI current state, task, SLA, and available actions. |
| AvailableWorkflowAction | A metadata-derived action the current actor can perform from the current workflow state. |
| WorkflowActionResult | Standard response object returned after processing a `WorkflowActionEnvelope`; it summarizes state/task changes and may include refreshed workflow context. |
| Workflow instance | A single in-flight workflow record, usually one per business object. |
| Transition | A metadata row that maps `(fromState, actionKey, allowedRole)` to `(toState, taskClose, taskCreate)`. |
| Notification outbox | Persisted notification records. Delivery uses the configured notification sender; dev/test defaults avoid real email, and SMTP can be enabled for fake/local or production relays by configuration. |
| SLA | Service-level deadline tied to a workflow task's `DueAtUtc`. |
| Single-active-state | One current workflow state and at most one pending task for a workflow instance in the MVP. |
| Return to Maker | A workflow route where Checker or Trading Desk sends the request back to Maker for correction/rework. |

## 4. Scope

### In scope

- .NET console app
- SQL Server integration
- EF Core migrations/entities
- Reusable workflow definition model
- Workflow instances
- Single-active-state workflow execution
- Back-and-forth transitions / return / rework states
- Checker Return to Maker transition
- Trading Desk Return to Maker transition
- Task creation and task completion
- Role-based workflow action checks
- Idempotency
- Optimistic concurrency checks
- SLA due-date calculation
- SLA overdue scan command
- Notification outbox records
- Audit history
- Standard `WorkflowActionEnvelope` JSON object
- Standard `WorkflowContextResponse` object for form-open workflow context
- Standard `WorkflowActionResult` object after button-click processing
- `execute-json` command
- `show-context` command
- Mock form-section data save
- Portfolio Trade sample workflow definition
- Portfolio Trade sample JSON requests
- SQL seed data
- `verify-seed` command
- Automated tests
- POC engineering discipline, as defined in section 16
- Notification sender abstraction with Console, NoOp, and SMTP implementations (see section 14). The SMTP implementation is fully written and tested against a fake SMTP server, but is disabled by configuration in dev/test.

### Out of scope for this POC

- Web UI
- InfoPath replacement UI
- Full field-level form validation
- Sending real emails to real recipients during the POC. The SMTP code path is in scope (so the UI integration team flips a config flag, not writes new code), but is exercised only against fake/local SMTP servers during this project.
- Enterprise authentication/authorization
- Parallel approvals / fan-out / fan-in
- Multiple simultaneous active workflow tasks
- Complex SharePoint migration tooling
- Business-day holiday calendars
- Dashboard/reporting UI
- Cancel/withdraw paths (a workflow in any state but `Completed` cannot currently be terminated; this is a known v1 limitation and may be added later as a `Cancel` action with an `AdminDemo` or `Maker` role)
- Failure audit (only successful actions are audited in v1; see section 16.1)
- User-to-role mapping table (see section 11)
- Conditional action availability / `enabledByWorkflow` / `disabledReason` (see section 8.1)
- Multiple sequential workflow instances on the same business object identifier (re-trade scenarios). v1 enforces one workflow lifetime per business object; new lifetimes require a new `BusinessObjectId` (see section 10.2).

## 4.1 Local SQL Server configuration

The POC uses SQL Server for schema, seed data, runtime workflow state, task data, SLA data, notification outbox records, audit history, idempotency records, and mocked section data.

The local SQL Server connection string for this project is:

```text
Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Required configuration behavior:

- The console app and tests must read the connection string from configuration under `ConnectionStrings:DefaultConnection`.
- No connection string may be hardcoded inside domain, application, or infrastructure workflow logic.
- This local integrated-security connection string is approved for the POC because it contains no password. Do not commit passwords, tokens, or developer-specific secrets.
- Recommended configuration source order:
  1. Environment variable `ConnectionStrings__DefaultConnection`
  1. `appsettings.Development.json`
  1. `appsettings.json`
- `init-db` applies migrations and seed data to the configured SQL database.
- `verify-seed` validates the configured SQL database.
- SQL-backed tests must not use EF Core InMemory as a substitute for seed or persistence verification.

Example `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
  }
}
```

## 5. Core architecture principle

The engine is reusable. Portfolio Trade is sample metadata.

Correct:

```text
Workflow engine
 -> loads workflow definition
 -> checks current state + action + role
 -> moves instance state
 -> manages tasks, SLA, notifications, and audit
```

Incorrect:

```text
PortfolioTradeWorkflowService
 -> hardcoded SubmitForChecker(), CheckerSendToTradingDesk(), etc.
 -> no reusable engine
```

The workflow engine should understand generic workflow concepts only:

- workflow definition
- workflow version
- workflow instance
- state
- action
- transition
- actor/role
- task
- SLA policy
- notification outbox
- audit history

The workflow engine must not understand Portfolio Trade form fields.

## 6. Workflow model

The MVP supports **single-active-state workflow execution**.

This means:

- One workflow instance has one current state.
- At most one pending task owns the instance at a time.
- Transitions can move forward, backward, or to rework states.

This does **not** mean forward-only sequential workflow.

The engine must support movement like:

```text
Maker
 -> Checker
 -> Return to Maker
 -> Maker corrections
 -> Checker
 -> Trading Desk
 -> Return to Maker
 -> Maker corrections
 -> Checker
 -> Trading Desk
 -> BDRM Client Service
 -> back to Trading Desk Rework
 -> BDRM Client Service
 -> Completed
```

The MVP does not support parallel branches such as:

```text
Checker
 -> Legal Review and Risk Review at the same time
 -> wait for both
 -> continue
```

### Transition rule

All next-state decisions must come from a `WorkflowTransitionDefinition` row matched by:

```text
WorkflowDefinitionVersionId + FromStateKey + ActionKey + AllowedRoleKey
```

The engine never computes the next state from `stageNumber + 1` or any other positional logic. The engine never accepts `toState` from request JSON as authority.

## 7. Portfolio Trade sample workflow

Portfolio Trade is the first sample workflow definition.

It must be represented as SQL-seeded workflow metadata, not hardcoded logic inside the engine.

### States

- `Draft`
- `ReturnedToMaker`
- `CheckerReview`
- `TradingDeskReview`
- `TradingDeskRework`
- `BdrmClientServiceReview`
- `Completed`

### Roles

- `Maker`
- `Checker`
- `TradingDesk`
- `BDRMClientService`

`AdminDemoOverride` is **not** a seeded workflow role and must not appear as `AllowedRoleKey` in any transition row. Demo override is a console-only flag (`--admin-override` on `execute-json`) that bypasses role validation; see section 10 for the canonical definition.

### Action matrix

The following matrix is mandatory seed data. `verify-seed` and automated tests must prove every row exists.

| Current state | Action key | Allowed role | Next state | Task closed | Task created | Notification intent |
|---|---|---|---|---|---|---|
| `Draft` | `SaveDraft` | `Maker` | `Draft` | None | None | None |
| `Draft` | `SubmitForChecker` | `Maker` | `CheckerReview` | None | `CheckerReviewTask` | `WorkflowTaskAssigned` to `Checker` |
| `ReturnedToMaker` | `SaveDraft` | `Maker` | `ReturnedToMaker` | None | None | None |
| `ReturnedToMaker` | `ResubmitForChecker` | `Maker` | `CheckerReview` | `MakerReworkTask` | `CheckerReviewTask` | `WorkflowTaskAssigned` to `Checker` |
| `CheckerReview` | `CheckerReturnToMaker` | `Checker` | `ReturnedToMaker` | `CheckerReviewTask` | `MakerReworkTask` | `WorkflowReturnedToMaker` to `Maker` |
| `CheckerReview` | `CheckerSendToTradingDesk` | `Checker` | `TradingDeskReview` | `CheckerReviewTask` | `TradingDeskReviewTask` | `WorkflowTaskAssigned` to `TradingDesk` |
| `TradingDeskReview` | `TradingReturnToMaker` | `TradingDesk` | `ReturnedToMaker` | `TradingDeskReviewTask` | `MakerReworkTask` | `WorkflowReturnedToMaker` to `Maker` |
| `TradingDeskReview` | `TradingTradeExecuted` | `TradingDesk` | `BdrmClientServiceReview` | `TradingDeskReviewTask` | `BdrmClientServiceReviewTask` | `WorkflowTaskAssigned` to `BDRMClientService` |
| `TradingDeskRework` | `TradingTradeExecuted` | `TradingDesk` | `BdrmClientServiceReview` | `TradingDeskReworkTask` | `BdrmClientServiceReviewTask` | `WorkflowTaskAssigned` to `BDRMClientService` |
| `BdrmClientServiceReview` | `BdrmTradeFulfilled` | `BDRMClientService` | `Completed` | `BdrmClientServiceReviewTask` | None | `WorkflowCompleted` if configured |
| `BdrmClientServiceReview` | `BdrmTradeNotExecutedProperly` | `BDRMClientService` | `TradingDeskRework` | `BdrmClientServiceReviewTask` | `TradingDeskReworkTask` | `WorkflowTaskAssigned` to `TradingDesk` |

### Required Return-to-Maker behavior

Checker and Trading Desk must both be able to send the workflow back to Maker.

#### Checker returns to Maker

```text
From state: CheckerReview
Action key: CheckerReturnToMaker
Allowed role: Checker
Task closed: CheckerReviewTask
Next state: ReturnedToMaker
Task created: MakerReworkTask
Assigned role: Maker
Notification: WorkflowReturnedToMaker
SLA: no Maker SLA in v1 unless a later requirement explicitly enables it
```

Expected system behavior:

```text
Save Checker section data.
Close CheckerReviewTask.
Move workflow state to ReturnedToMaker.
Create MakerReworkTask.
Queue WorkflowReturnedToMaker notification to Maker.
Write audit with actionKey = CheckerReturnToMaker.
Do not leave CheckerReviewTask pending.
Do not create TradingDeskReviewTask.
```

#### Trading Desk returns to Maker

```text
From state: TradingDeskReview
Action key: TradingReturnToMaker
Allowed role: TradingDesk
Task closed: TradingDeskReviewTask
Next state: ReturnedToMaker
Task created: MakerReworkTask
Assigned role: Maker
Notification: WorkflowReturnedToMaker
SLA: no Maker SLA in v1 unless a later requirement explicitly enables it
```

Expected system behavior:

```text
Save Trading Desk section data.
Close TradingDeskReviewTask.
Move workflow state to ReturnedToMaker.
Create MakerReworkTask.
Queue WorkflowReturnedToMaker notification to Maker.
Write audit with actionKey = TradingReturnToMaker.
Do not leave TradingDeskReviewTask pending.
Do not create BdrmClientServiceReviewTask.
```

#### Maker after a return

Once the workflow is in `ReturnedToMaker`, Maker can do either of the following:

```text
SaveDraft
 -> stay in ReturnedToMaker
 -> save data only
 -> no task movement
 -> no notification

ResubmitForChecker
 -> close MakerReworkTask (the same task created when the workflow was returned)
 -> move to CheckerReview
 -> create CheckerReviewTask
 -> notify Checker
```

Note: `MakerReworkTask` is created exactly once per return-to-Maker event, not on every `SaveDraft`. The single-pending-task constraint (section 12) enforces this at the schema level.

### SaveDraft semantics

`SaveDraft` is treated as a data-save action, not workflow movement.

Important rules:

- `SaveDraft` must still be resolved and authorized through workflow metadata.
- A valid `SaveDraft` transition row must exist for the current state and role. `SaveDraft` is **not universal** — it is only seeded for `Draft` and `ReturnedToMaker` (Maker role). Attempting `SaveDraft` from any other state returns `InvalidTransition`.
- Section data is saved.
- No task is closed.
- No task is created.
- No notification is queued.
- SLA is not evaluated.
- `WorkflowInstance.Version` is not incremented.
- A lightweight audit row is written with `ActionKey = SaveDraft` and `FromStateKey = ToStateKey`.
- A successful non-idempotent `SaveDraft` inserts one append-only `MockSectionData` row and one lightweight audit row. A matching idempotent replay returns stored `ResultJson` and inserts no additional mock data or audit rows.

### Completed-state behavior

When a transition moves an instance to `Completed`, the engine must:

- Set `WorkflowInstance.Status = Completed`.
- Set `WorkflowInstance.CompletedAtUtc`.
- Ensure no pending task remains for the instance.
- Not create a next SLA.
- Queue `WorkflowCompleted` notification if configured by metadata.

### Business labels

The actual form may show labels such as:

- Submit
- Submit for Checker
- Return to Maker
- Send to Trading Desk
- Trade Executed
- Trade Fulfilled
- Trade Not Executed Properly

The workflow engine should receive normalized `actionKey` values. It must not make decisions from button text alone.

### MakerPrimary action selection rule

The Maker section uses one physical primary button plus a `Save as Draft` checkbox. Workflow context may return multiple Maker actions that share `buttonKey = MakerPrimary`; the UI should group them by `buttonKey` and render one physical Maker button controlled by the checkbox.

When `currentState = Draft`:

- Save as Draft checked -> `actionKey = SaveDraft`
- Save as Draft unchecked -> `actionKey = SubmitForChecker`

When `currentState = ReturnedToMaker`:

- Save as Draft checked -> `actionKey = SaveDraft`
- Save as Draft unchecked -> `actionKey = ResubmitForChecker`

The UI must not render separate `Save Draft` and `Submit` buttons for the Maker section unless a future requirement explicitly changes this design.

Envelope consistency rules for MakerPrimary:

- `actionKey = SaveDraft` requires `uiAction.saveAsDraft = true`.
- `actionKey = SubmitForChecker` or `ResubmitForChecker` requires `uiAction.saveAsDraft = false`.
- If `workflow.actionKey` and `uiAction.saveAsDraft` conflict, return `ValidationFailed` before saving section data.

## 8. UI/API integration model

Eventually the UI/API will pass one JSON object into a workflow method.

Use one composite request, not two separate requests.

Correct:

```text
One user click
 -> one JSON request
 -> one API/application service call
 -> save mocked/form section data
 -> execute workflow action
 -> one transaction
```

Avoid:

```text
Call 1: save form data
Call 2: execute workflow action
```

Two calls can create inconsistent states, such as data saved but workflow not moved, or workflow moved with stale data.

The reusable workflow engine should receive workflow information only. The application layer may receive the full envelope, save section data, and then call the workflow engine within the same transaction.

### 8.1 Workflow context on form open

When the form opens, the UI needs workflow context in addition to the objects it already has.

The form-open inputs are:

```text
1. User identity
2. Form metadata
3. Form data
4. Workflow context
```

The missing workflow object is `WorkflowContextResponse`.

The console POC must expose this object through `show-context`:

```bash
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user checker1 --roles Checker --format json
```

The future API can expose the same object either inside the form-open response or through a dedicated endpoint such as:

```text
GET /api/portfolio-trades/PT-1001/workflow-context
```

The UI uses `workflowContext.availableActions` to decide which workflow buttons to show or enable for the logged-in user. The UI must still apply form validation rules before enabling data-completeness-sensitive actions like `SubmitForChecker` or `ResubmitForChecker`.

The workflow context is **not** form metadata and does **not** contain field definitions. Form metadata decides which fields/sections render and which fields are required. Workflow context decides current state, current task, SLA status, and allowed workflow actions.

Rules:

- `WorkflowContextResponse` must be computed from persisted workflow state, the current pending task, SQL workflow metadata, actor role(s), and SLA status.
- `workflow.workflowInstanceVersion` mirrors `WorkflowInstance.Version`. It exists so the UI can detect when the workflow has moved between form-open and button-click. The UI should echo it back in `workflow.expectedInstanceVersion` of the next `WorkflowActionEnvelope`.
- `availableActions` must come from SQL transition metadata. Do not hardcode Portfolio Trade action availability in the UI.
- `availableActions[].sectionKey` tells the UI where to render the action button and must be echoed back in `WorkflowActionEnvelope.uiAction.sectionKey` and `WorkflowActionEnvelope.dataSave.sectionKey` when that action is clicked.
- The UI must not compute workflow next state.
- The UI must not send `toState`.
- The UI should use `availableActions[*].requestDefaults` when building the later `WorkflowActionEnvelope`.
- In the real API, actor identity comes from authentication/claims. In the console POC, `--user` and `--roles` simulate the actor.
- If the current user has no valid workflow action from the current state, `availableActions` must be empty and `actorAccess.canAct` should be false.

Actual `show-context --format json` output and API responses must include the full `WorkflowContextResponse` contract. Examples in this document may omit fields for readability only.

### SLA block shape when no task or no SLA

When the current state has no pending task, or the pending task has `DueAtUtc = null` (e.g., `MakerReworkTask`), or the workflow is `Completed`, the `sla` block must use this shape:

```json
"sla": {
  "status": "NoSla",
  "dueAtUtc": null,
  "overdue": false,
  "overdueByMinutes": 0
}
```

Do not return `"sla": null` and do not omit the `sla` block. A consistent object shape avoids null-checks in UI code. `status` values are `OnTrack`, `Overdue`, and `NoSla`.

### Action availability is binary in v1

An action either appears in `availableActions` or it does not. v1 has no conditional-disable model: the Portfolio Trade transition matrix has no business-rule conditions on transitions, and `WorkflowActionDefinition` does not carry condition metadata.

Membership in `availableActions` is itself the enabled signal. The contract intentionally omits `enabledByWorkflow` and `disabledReason` fields. When a future workflow needs conditional availability (e.g., "Checker can approve only if `tradeAmount < threshold`"), the contract will add those fields at that time, alongside the conditional metadata model that produces them. Until then, the engine returns only actions that can actually be performed by the actor from the current state.

#### Rules for `requestDefaults`

- `requestDefaults` are suggested values. The UI is expected to copy them into the next `WorkflowActionEnvelope`, but normal validation, concurrency, and authorization checks still apply even if the UI builds the envelope manually.
- `requestDefaults` are stable for a given `(workflowInstanceId, currentState, currentTaskId, workflowInstanceVersion)` snapshot. Any change to those values produces a new `WorkflowContextResponse` with new defaults.
- The UI should send all four concurrency guards (`expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, `expectedInstanceVersion`) when present. Sending fewer guards is allowed but reduces stale-state detection precision.
- `requestDefaults` does not include `actor`, `dataSave.payload`, `actionReason`, `clientActionId`, `correlationId`, or `idempotencyKey`; those are owned by the UI at click time.

#### Rules for `actorAccess`

- `canAct` is `true` if and only if `availableActions` is non-empty.
- `readOnlyReason` is `null` when `canAct` is `true`. When `canAct` is `false`, it must be a human-readable, non-empty string.
- Suggested read-only reasons include: `Current task is assigned to {role}.`, `Workflow is completed.`, `Workflow is in {state} and you are not the {expectedRole}.`, and `You do not have a role on this workflow.`

Example `WorkflowContextResponse` for a Checker opening a form in `CheckerReview`:

```json
{
  "workflowContext": {
    "workflow": {
      "workflowKey": "PortfolioTrade",
      "workflowInstanceId": "WF-PT-1001",
      "workflowDefinitionVersion": 1,
      "workflowInstanceVersion": 3,
      "currentState": "CheckerReview",
      "currentStateLabel": "Checker Review",
      "status": "Active",
      "startedAtUtc": "2030-01-15T15:30:00Z",
      "completedAtUtc": null
    },
    "businessObject": {
      "objectType": "PortfolioTrade",
      "objectId": "PT-1001",
      "objectVersion": 7
    },
    "actorAccess": {
      "userId": "checker1",
      "displayName": "checker1",
      "effectiveRoles": ["Checker"],
      "canAct": true,
      "readOnlyReason": null
    },
    "currentTask": {
      "taskId": "TASK-CHECKER-PT-1001",
      "taskKey": "CheckerReviewTask",
      "taskLabel": "Checker Review",
      "assignedRole": "Checker",
      "assignedUserId": null,
      "status": "Pending",
      "createdAtUtc": "2030-01-15T15:30:00Z",
      "dueAtUtc": "2030-01-15T15:32:00Z"
    },
    "sla": {
      "status": "OnTrack",
      "dueAtUtc": "2030-01-15T15:32:00Z",
      "overdue": false,
      "overdueByMinutes": 0
    },
    "availableActions": [
      {
        "actionKey": "CheckerReturnToMaker",
        "sectionKey": "Checker",
        "buttonKey": "CheckerReturnToMaker",
        "buttonLabel": "Return to Maker",
        "intent": "Return",
        "requiresReason": true,
        "requestDefaults": {
          "workflowKey": "PortfolioTrade",
          "workflowInstanceId": "WF-PT-1001",
          "workflowDefinitionVersion": 1,
          "expectedCurrentState": "CheckerReview",
          "expectedCurrentTaskId": "TASK-CHECKER-PT-1001",
          "expectedCurrentTaskKey": "CheckerReviewTask",
          "expectedInstanceVersion": 3
        }
      },
      {
        "actionKey": "CheckerSendToTradingDesk",
        "sectionKey": "Checker",
        "buttonKey": "CheckerSendToTradingDesk",
        "buttonLabel": "Send to Trading Desk",
        "intent": "Approve",
        "requiresReason": false,
        "requestDefaults": {
          "workflowKey": "PortfolioTrade",
          "workflowInstanceId": "WF-PT-1001",
          "workflowDefinitionVersion": 1,
          "expectedCurrentState": "CheckerReview",
          "expectedCurrentTaskId": "TASK-CHECKER-PT-1001",
          "expectedCurrentTaskKey": "CheckerReviewTask",
          "expectedInstanceVersion": 3
        }
      }
    ],
    "serverTimeUtc": "2030-01-15T15:31:00Z"
  }
}
```

### 8.2 Workflow context by state

The workflow context should produce these available actions for the Portfolio Trade sample when the actor has the matching role.

| Current state | Current task | Actor role that can act | Available actions |
|---|---|---|---|
| `Draft` | None | `Maker` | `SaveDraft`, `SubmitForChecker` |
| `ReturnedToMaker` | `MakerReworkTask` | `Maker` | `SaveDraft`, `ResubmitForChecker` |
| `CheckerReview` | `CheckerReviewTask` | `Checker` | `CheckerReturnToMaker`, `CheckerSendToTradingDesk` |
| `TradingDeskReview` | `TradingDeskReviewTask` | `TradingDesk` | `TradingReturnToMaker`, `TradingTradeExecuted` |
| `TradingDeskRework` | `TradingDeskReworkTask` | `TradingDesk` | `TradingTradeExecuted` |
| `BdrmClientServiceReview` | `BdrmClientServiceReviewTask` | `BDRMClientService` | `BdrmTradeFulfilled`, `BdrmTradeNotExecutedProperly` |
| `Completed` | None | None | No workflow actions |

If the actor role does not match the current task/state, return the same workflow state/task information but return no available actions.

Example: Maker opens an item in `CheckerReview`:

```json
{
  "workflowContext": {
    "workflow": {
      "workflowKey": "PortfolioTrade",
      "workflowInstanceId": "WF-PT-1001",
      "workflowDefinitionVersion": 1,
      "workflowInstanceVersion": 1,
      "currentState": "CheckerReview",
      "currentStateLabel": "Checker Review",
      "status": "Active"
    },
    "actorAccess": {
      "userId": "maker1",
      "effectiveRoles": ["Maker"],
      "canAct": false,
      "readOnlyReason": "Current task is assigned to Checker."
    },
    "currentTask": {
      "taskId": "TASK-CHECKER-PT-1001",
      "taskKey": "CheckerReviewTask",
      "assignedRole": "Checker",
      "status": "Pending",
      "dueAtUtc": "2030-01-15T15:32:00Z"
    },
    "availableActions": []
  }
}
```

### 8.3 WorkflowActionResult after button click

After the UI sends a `WorkflowActionEnvelope`, the API should return a `WorkflowActionResult`. The console POC may print a human-readable result, but the application layer should still model this result so the future API has a stable response contract.

The result should summarize what changed and return refreshed workflow context when possible.

Example:

```json
{
  "result": {
    "success": true,
    "resultCode": "Success",
    "correlationId": "corr-pt-1001-checker-return-to-maker",
    "workflowInstanceId": "WF-PT-1001",
    "businessObject": {
      "objectType": "PortfolioTrade",
      "objectId": "PT-1001"
    },
    "actionKey": "CheckerReturnToMaker",
    "oldState": "CheckerReview",
    "newState": "ReturnedToMaker",
    "closedTask": {
      "taskId": "TASK-CHECKER-PT-1001",
      "taskKey": "CheckerReviewTask"
    },
    "createdTask": {
      "taskId": "TASK-MAKER-REWORK-PT-1001",
      "taskKey": "MakerReworkTask",
      "assignedRole": "Maker",
      "dueAtUtc": null
    },
    "notification": {
      "queued": true,
      "notificationType": "WorkflowReturnedToMaker",
      "recipientRole": "Maker"
    },
    "audit": {
      "written": true,
      "actionKey": "CheckerReturnToMaker"
    }
  },
  "workflowContext": {
    "workflow": {
      "workflowKey": "PortfolioTrade",
      "workflowInstanceId": "WF-PT-1001",
      "workflowDefinitionVersion": 1,
      "workflowInstanceVersion": 2,
      "currentState": "ReturnedToMaker",
      "currentStateLabel": "Returned to Maker",
      "status": "Active"
    },
    "currentTask": {
      "taskId": "TASK-MAKER-REWORK-PT-1001",
      "taskKey": "MakerReworkTask",
      "assignedRole": "Maker",
      "status": "Pending",
      "dueAtUtc": null
    },
    "availableActions": []
  }
}
```

For failed requests, `WorkflowActionResult` must include `success = false`, a structured `resultCode`, and a readable message. Failed requests must not partially save section data or partially move workflow state.

The refreshed `workflowContext` in `WorkflowActionResult` is computed for the same actor who submitted the request, not for the new owning role. Example: after Checker returns an item to Maker, the returned context is the Checker's view of the now-`ReturnedToMaker` item and will usually have `availableActions: []`. When Maker opens the form afterward, the UI should call `show-context` / the future API workflow-context endpoint for Maker's view.

The refreshed context should still include `actorAccess` for the submitting actor so the UI can tell why the post-action view is read-only. Examples may be abbreviated for readability, but the modeled `WorkflowActionResult.workflowContext` should use the full `WorkflowContextResponse` shape whenever refreshed context is returned.

## 9. Standard JSON object: WorkflowActionEnvelope

The console app must support:

```bash
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json
```

The JSON object should be workflow-heavy. Form section data can be mocked.

### Required top-level shape

```json
{
  "request": {
    "commandType": "ExecuteWorkflowAction",
    "commandVersion": 1,
    "clientActionId": "9a8f7f6c-2e0b-4b5a-81e4-3f8edc3e1a10",
    "correlationId": "corr-portfolio-trade-1001-submit",
    "requestedAtUtc": "2030-01-15T15:30:00Z",
    "sourceSystem": "PortfolioTradeConsoleTest"
  },
  "businessObject": {
    "objectType": "PortfolioTrade",
    "objectId": "PT-1001",
    "objectVersion": 7
  },
  "actor": {
    "userId": "maker1",
    "displayName": "Maker User One",
    "roles": ["Maker"]
  },
  "uiAction": {
    "sectionKey": "Maker",
    "buttonKey": "MakerPrimary",
    "buttonLabelShown": "Submit for Checker",
    "saveAsDraft": false
  },
  "dataSave": {
    "sectionKey": "Maker",
    "payload": {
      "customerName": "ABC Capital",
      "portfolioId": "PORT-123",
      "tradeType": "Buy",
      "securityName": "Mock Security",
      "tradeAmount": 1000000,
      "currency": "USD",
      "notes": "Mock Maker section data for console testing."
    }
  },
  "workflow": {
    "workflowKey": "PortfolioTrade",
    "workflowInstanceId": "WF-PT-1001",
    "workflowDefinitionVersion": 1,
    "expectedCurrentState": "Draft",
    "expectedCurrentTaskId": null,
    "expectedCurrentTaskKey": null,
    "expectedInstanceVersion": 1,
    "actionKey": "SubmitForChecker",
    "actionReason": "Maker submitted the portfolio trade for Checker review.",
    "idempotencyKey": "9a8f7f6c-2e0b-4b5a-81e4-3f8edc3e1a10",
    "options": {
      "closeCurrentTask": false,
      "createNextTask": true,
      "queueNotifications": true,
      "evaluateSla": true,
      "writeAudit": true
    }
  },
  "testExpectations": {
    "expectedResult": "Success",
    "expectedOldState": "Draft",
    "expectedNewState": "CheckerReview",
    "expectedClosedTaskKey": null,
    "expectedCreatedTaskKey": "CheckerReviewTask",
    "expectedAssignedRole": "Checker",
    "expectedNotificationType": "WorkflowTaskAssigned",
    "expectedAuditAction": "SubmitForChecker"
  }
}
```

### Rules for the envelope

- The JSON must pass `actionKey`, not `toState`.
- The engine determines next state from SQL metadata.
- Actor is allowed in console JSON for testing only.
- In the real API, actor identity must come from authentication/claims. The API must:
  - Ignore `actor.userId` and `actor.displayName` from the request body and substitute the authenticated identity.
  - Reject the request with `ActorRolesMismatch` if `actor.roles` is present and contains a role the authenticated user does not actually hold.
  - For the console POC, the envelope's `actor` block is honored as-is (this is what makes single-process role testing possible).
- Actor resolution belongs in the application/API layer. The reusable workflow engine receives an already-resolved actor user ID and effective role set. The console actor resolver reads the envelope. The future API actor resolver reads authenticated identity/claims and ignores request-body user identity.
- `clientActionId` / `idempotencyKey` must be used to prevent duplicate processing.
- `idempotencyKey` is the canonical key used by the engine for dedupe. If `idempotencyKey` is absent, the engine uses `clientActionId` as the idempotency key. If both are present and differ, `idempotencyKey` wins. The simplest UI integration is for the UI to send the same value in both fields.
- The idempotency lookup key (`IdempotencyRecord.IdempotencyKey`) is separate from `RequestHash`. Two requests that differ only in whether `idempotencyKey` is set will share the same normalized `RequestHash` but use different lookup keys (`idempotencyKey` vs `clientActionId`) and are therefore treated as independent actions for idempotency purposes. This is intentional — `idempotencyKey` is the UI's explicit dedup token; `clientActionId` is the fallback. They are not interchangeable lookup aliases.
- `correlationId` must be copied into audit and notification records.
- `expectedCurrentState` is a concurrency guard, not the source of truth.
- `expectedCurrentTaskId` is a concurrency guard for exact task ownership.
- For console sample JSON, `expectedCurrentTaskKey` may be used because task IDs are generated at runtime.
- `expectedInstanceVersion` is a concurrency guard for the workflow instance's `Version` field. The UI receives this value in `WorkflowContextResponse.workflow.workflowInstanceVersion` and should echo it in every state-changing envelope. A mismatch returns `ConcurrencyConflict`. It is optional in console samples, but required in production UI envelopes.
- If multiple concurrency guards are provided (`expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, `expectedInstanceVersion`), **all** of them must match the persisted state. No guard silently overrides another. Any mismatch returns `ConcurrencyConflict`.
- `businessObject.objectVersion` is informational only for the POC. The engine does not validate it and does not record it in `WorkflowAudit`. `WorkflowAudit` has no `BusinessObjectVersion` column. The field is accepted in the envelope without error and silently ignored by the engine. If a future requirement needs object version tracking in audit, a column addition and a stop-and-ask approval are required.
- If `workflow.workflowDefinitionVersion` does not match the persisted instance definition version, return `WorkflowDefinitionVersionMismatch` (a distinct result code from `ConcurrencyConflict`) and do not save section data. `ConcurrencyConflict` means "your view is stale, retry"; `WorkflowDefinitionVersionMismatch` means "the workflow definition itself was upgraded and the UI must be updated before retry."
- `workflow.options` are read-only advisory hints. The engine derives task close, task create, notification queueing, SLA evaluation, and audit writing from metadata regardless of option values. The engine never adds a side effect because an option says `true`; it never suppresses a metadata-required side effect because an option says `false`. Option values that disagree with metadata produce a warning in console output (`WARN: workflow.options.X = Y conflicts with metadata; using metadata`); option values that agree with metadata are silently honored. If options-vs-metadata behavior is ever needed to differ, that is a stop-and-ask change.
- `workflow.workflowDefinitionVersion` in JSON is the definition version number visible to clients. The persisted `WorkflowInstance.WorkflowDefinitionVersionId` is an internal database key; compare the JSON version number to the joined `WorkflowDefinitionVersion.VersionNumber`, not directly to the internal ID.

### Rules for `dataSave`

**Required vs optional:**

- `dataSave` is **required** on every `WorkflowActionEnvelope` in v1. Missing `dataSave`, missing `dataSave.sectionKey`, or missing `dataSave.payload` returns `ValidationFailed`.
- `dataSave.sectionKey` must be a non-empty string. The engine does not validate that the section key exists in any form metadata table — section identity belongs to the form layer.
- `dataSave.payload` must be a JSON object. An empty object `{}` is permitted; a missing or null payload is `ValidationFailed`.

**Engine treatment of `payload`:**

- The workflow engine treats `dataSave.payload` as opaque JSON. It serializes the payload to `MockSectionData.PayloadJson` exactly as received (canonicalized for hash purposes only — see idempotency rules below).
- The engine does not validate field names, field types, required fields, or business constraints inside `payload`. Field-level validation is the form layer's responsibility.
- The engine does not extract values from `payload` to drive workflow decisions. State transitions are determined by `actionKey`, current state, and actor role only — never by payload contents.

**Why mandatory in v1:**

Every Portfolio Trade transition in section 7 corresponds to a section the actor was looking at when they clicked. `MakerPrimary` writes Maker section data. `CheckerReturnToMaker` writes Checker section data (decision + comments). There is no v1 transition with zero section data. Making `dataSave` optional would add an untested code path for a use case nobody has.

If a future workflow needs pure workflow-movement (no section data), make `dataSave` optional then, with explicit test coverage for the "no `MockSectionData` row written" case.

### Return-to-Maker sample snippets

Checker Return to Maker sample must use:

```json
{
  "actor": { "userId": "checker1", "roles": ["Checker"] },
  "uiAction": {
    "sectionKey": "Checker",
    "buttonKey": "CheckerReturnToMaker",
    "buttonLabelShown": "Return to Maker",
    "saveAsDraft": false
  },
  "dataSave": {
    "sectionKey": "Checker",
    "payload": {
      "checkerDecision": "ReturnToMaker",
      "checkerComments": "Returned to Maker for correction."
    }
  },
  "workflow": {
    "expectedCurrentState": "CheckerReview",
    "expectedCurrentTaskKey": "CheckerReviewTask",
    "actionKey": "CheckerReturnToMaker"
  },
  "testExpectations": {
    "expectedNewState": "ReturnedToMaker",
    "expectedClosedTaskKey": "CheckerReviewTask",
    "expectedCreatedTaskKey": "MakerReworkTask",
    "expectedAssignedRole": "Maker",
    "expectedNotificationType": "WorkflowReturnedToMaker"
  }
}
```

Trading Desk Return to Maker sample must use:

```json
{
  "actor": { "userId": "trader1", "roles": ["TradingDesk"] },
  "uiAction": {
    "sectionKey": "TradingDesk",
    "buttonKey": "TradingReturnToMaker",
    "buttonLabelShown": "Return to Maker",
    "saveAsDraft": false
  },
  "dataSave": {
    "sectionKey": "TradingDesk",
    "payload": {
      "tradingDecision": "ReturnToMaker",
      "tradingComments": "Returned to Maker for trade detail correction."
    }
  },
  "workflow": {
    "expectedCurrentState": "TradingDeskReview",
    "expectedCurrentTaskKey": "TradingDeskReviewTask",
    "actionKey": "TradingReturnToMaker"
  },
  "testExpectations": {
    "expectedNewState": "ReturnedToMaker",
    "expectedClosedTaskKey": "TradingDeskReviewTask",
    "expectedCreatedTaskKey": "MakerReworkTask",
    "expectedAssignedRole": "Maker",
    "expectedNotificationType": "WorkflowReturnedToMaker"
  }
}
```

### Idempotency semantics

`idempotencyKey` is unique per logical action attempt.

On replay:

- If no `IdempotencyRecord` exists, process normally and write the record on success.
- If a record exists and the stored `RequestHash` matches the new request hash, return the stored `ResultJson`. Do not re-execute the transition. Do not save another `MockSectionData` row. Do not write a new audit row. Do not queue another notification.
- If a record exists and the stored `RequestHash` does not match, return `IdempotencyConflict` and do nothing.

`ResultHash` may be stored for diagnostics/integrity, but `ResultJson` is required because a hash alone cannot return the original workflow result to the UI or console. `ResultJson` must contain the canonical serialized `WorkflowActionResult` returned by the original successful execution.

A matching idempotent replay returns the stored `ResultJson` before rechecking current workflow state, current task, workflow instance version, or workflow definition version. This is intentional because the original logical action has already been completed. If the stored `RequestHash` differs, return `IdempotencyConflict` and do nothing.

The request hash must be computed from the normalized effective request. Include:

```text
workflow.workflowKey
workflow.workflowInstanceId
workflow.workflowDefinitionVersion
workflow.actionKey
workflow.actionReason
actor.userId
actor.roles
businessObject.objectType
businessObject.objectId
workflow.expectedCurrentState
workflow.expectedCurrentTaskId
workflow.expectedCurrentTaskKey
workflow.expectedInstanceVersion
dataSave.sectionKey
dataSave.payload
```

Exclude:

```text
request.clientActionId
workflow.idempotencyKey
request.correlationId
request.requestedAtUtc
```

Compute the hash after console override flags are applied and, in the real API, after authenticated actor identity has replaced any actor values from the request body. Use a canonical JSON representation with stable property ordering so semantically identical requests do not produce different hashes because of JSON property order.

### Concurrency semantics

`start-workflow` creates `WorkflowInstance.Version = 1`. `WorkflowInstance.Version` is incremented on every state-changing transition. It is not incremented on `SaveDraft`. `WorkflowContextResponse.workflow.workflowInstanceVersion` must always mirror the persisted `WorkflowInstance.Version`.

On `execute-json`:

- Load the persisted workflow instance, open task, and workflow definition/version before saving section data.
- If `expectedCurrentState` is provided and does not equal the persisted `CurrentStateKey`, return `ConcurrencyConflict`. Do not save section data. Do not write audit.
- If `expectedCurrentTaskId` is provided and does not match the open task on the instance, return `ConcurrencyConflict`. Do not save section data. Do not write audit.
- If `expectedCurrentTaskKey` is provided and does not match the open task's `TaskKey`, return `ConcurrencyConflict`. Do not save section data. Do not write audit.
- If `expectedInstanceVersion` is provided and does not equal the persisted `WorkflowInstance.Version`, return `ConcurrencyConflict`. Do not save section data. Do not write audit.
- The four guards (`expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, `expectedInstanceVersion`) are evaluated independently. Each one that is provided must match. The engine does not skip a guard because another guard already passed.
- The state-changing `UPDATE` on `WorkflowInstance` must include `Version` in its `WHERE` clause and increment it. Zero rows affected means `ConcurrencyConflict`.

### Actor validation rules

Envelope-level validation runs before transition resolution. The result codes are distinct so the UI/API can react differently.

**Envelope-shape errors → `ValidationFailed`:**

- `actor` block is missing.
- `actor.userId` is missing, null, or empty/whitespace.
- `actor.roles` is missing or null.
- `actor.roles` is an empty array.

These return `ValidationFailed` because the request itself is malformed. No section data is saved. No audit row is written.

**Action-existence errors → `InvalidTransition`:**

- The `actionKey` does not exist in `WorkflowActionDefinition` for the workflow definition version, regardless of state or role.
- No transition row exists with `FromStateKey = current state AND ActionKey = requested action`, regardless of role.

This means "the action is not possible from the current state." The UI should treat this as "this button should not have been shown."

**Role-authorization errors → `Forbidden`:**

- A transition exists for `(currentState, actionKey)` but none of `actor.roles` matches the `AllowedRoleKey` of any such transition.

This means "the action is real and possible from this state, but not for you." The UI can show a distinct message.

**Ambiguous role match → `AmbiguousRoleMatch`:**

- Multiple of `actor.roles` match transitions for `(currentState, actionKey)` with different `AllowedRoleKey` values.
- Bypassed by `--admin-override`.

Resolution order: validation → action existence → role authorization → ambiguity check → execute.

`--admin-override` bypasses `Forbidden` and `AmbiguousRoleMatch` only. It does **not** bypass `ValidationFailed`. A malformed envelope is malformed regardless of who sent it.

## 10. Console commands

### 10.1 Command inventory (capability list)

The console must support these commands. This list is a *capability inventory*, not an executable sequence. Running these top-to-bottom will produce errors at return-to-Maker steps because the workflow state changes in between. To run the actual demo paths, use section 25 demo scripts.

```bash
# Setup
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed

# Workflow lifecycle
dotnet run --project src/WorkflowEngine.Console -- start-workflow <workflowKey> <businessObjectId> --user <userId> [--reset]
dotnet run --project src/WorkflowEngine.Console -- execute-json <path-to-envelope.json> [--admin-override] [--business-object <id>] [--workflow-instance <id>]

# Read-only inspection
dotnet run --project src/WorkflowEngine.Console -- show-context <workflowInstanceId> --user <userId> --roles <roles> --format json
dotnet run --project src/WorkflowEngine.Console -- show <workflowInstanceId>
dotnet run --project src/WorkflowEngine.Console -- show-tasks <workflowInstanceId>
dotnet run --project src/WorkflowEngine.Console -- show-history <workflowInstanceId>
dotnet run --project src/WorkflowEngine.Console -- show-notifications <workflowInstanceId>

# Background processors
dotnet run --project src/WorkflowEngine.Console -- run-sla --now <ISO-8601-utc>
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox

# Aggregate demo
dotnet run --project src/WorkflowEngine.Console -- demo
```

Concrete runnable command sequences live in section 25.

### 10.2 `start-workflow` behavior

`start-workflow` must generate a deterministic workflow instance ID using:

```text
WF-{businessObjectId}
```

Example:

```text
PortfolioTrade + PT-1001 -> WF-PT-1001
```

`--user <userId>` is required and is persisted to `WorkflowInstance.StartedByUserId`. This records who started the workflow and is shown by `show <workflowInstanceId>`. The engine accepts arbitrary `--user` values without consistency checking (impersonation is the intended testing model).

`start-workflow` does **not** write a `WorkflowAudit` row. Workflow instance creation is not an audited transition — there is no `FromStateKey` to record. The starting user, time, and definition version are captured on `WorkflowInstance` directly (`StartedByUserId`, `StartedAtUtc`, `WorkflowDefinitionVersionId`). A coding agent must not add a synthetic `WorkflowStarted` audit row; this is a stop-and-ask change because it would require either a free-form `ActionKey` or a new seeded action plus a nullable `FromStateKey`.

If a workflow instance already exists for the same `workflowKey + businessObjectType + businessObjectId`, return `AlreadyExists` and do not modify existing runtime data.

### One workflow lifetime per business object

For CDT v1, each business object identifier gets **one workflow lifetime ever**. The unique constraint `(WorkflowKey, BusinessObjectType, BusinessObjectId)` on `WorkflowInstance` is permanent, not scoped by status.

Concretely:

- `start-workflow PortfolioTrade PT-1001 --user maker1` succeeds the first time and creates `WF-PT-1001` with `Status = 'Active'`, `Version = 1`.
- After `BdrmTradeFulfilled`, `WF-PT-1001` becomes `Status = 'Completed'`. The row stays in SQL permanently as historical record. All audit, notifications, tasks, and section data are retained.
- A second `start-workflow PortfolioTrade PT-1001` returns `AlreadyExists` regardless of whether the existing instance is `Active` or `Completed`. The engine does not check status before applying the unique constraint.
- To run a workflow on the same business semantics again, the calling system must assign a new `BusinessObjectId` (e.g., `PT-1001-R2`, `PT-1002`). This is a business-system responsibility, not a workflow-engine responsibility.
- `--reset` deletes the completed instance and all its history. `--reset` is a demo-only flag and must never be used in production to reuse a business object identifier — doing so destroys audit history that may be required for regulatory or operational record-keeping.

This is a deliberate v1 scope limitation. Multiple sequential workflow instances on the same business object identifier (e.g., re-trade scenarios where `PT-1001` legitimately needs a second workflow without losing the first one's history) are a v2 feature requiring schema changes (filtered unique index on `Status = 'Active'`, instance-ID generation that includes a sequence number, and updated `WF-{businessObjectId}` derivation rules). Adding this in v1 is a stop-and-ask change.

### `AlreadyExists` result payload

When `start-workflow` returns `AlreadyExists`, the result must include the existing instance's status so the caller can distinguish two cases:

```json
{
  "result": {
    "success": false,
    "resultCode": "AlreadyExists",
    "workflowInstanceId": "WF-PT-1001",
    "existingInstanceStatus": "Completed",
    "existingInstanceVersion": 8,
    "message": "A workflow instance already exists for PortfolioTrade / PT-1001 (Status: Completed). v1 does not support starting a second workflow on the same business object identifier. Assign a new BusinessObjectId or use --reset for demo purposes only."
  }
}
```

`existingInstanceStatus` values are `Active` or `Completed` (matching `WorkflowInstance.Status`). The console output for `start-workflow` must print these fields so the operator can distinguish "workflow is already in progress" from "workflow already finished."

For console/demo use only, `start-workflow` accepts `--reset`:

```bash
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
```

When `--reset` is provided, clear runtime/demo data for the workflow instance/business object and create a fresh `Draft` instance with `WorkflowInstance.Version = 1`. `--reset` must not clear workflow definition seed data.

Runtime data cleared by `--reset`, in this order, all inside one transaction:

1. `IdempotencyRecord` rows where `WorkflowInstanceId = @workflowInstanceId`. This includes derived idempotency keys produced by override-mode demo scripts, because those records carry the effective (overridden) `WorkflowInstanceId` in the column, not in the key string.
2. `NotificationOutbox` rows where `WorkflowInstanceId = @workflowInstanceId`.
3. `WorkflowAudit` rows where `WorkflowInstanceId = @workflowInstanceId`.
4. `MockSectionData` rows where `BusinessObjectType = @type AND BusinessObjectId = @id`. `MockSectionData` is keyed by business object, not workflow instance, so reset uses the business object identifiers derived from the `start-workflow` arguments.
5. `WorkflowTask` rows where `WorkflowInstanceId = @workflowInstanceId`.
6. `WorkflowInstance` row where `WorkflowInstanceId = @workflowInstanceId`.

Deletion is by column value, never by string-matching idempotency key text. A failure mid-reset rolls back the transaction so the instance is not left half-cleared.

Seed/configuration data that must not be cleared by `--reset`:

- `WorkflowDefinition`
- `WorkflowDefinitionVersion`
- `WorkflowStateDefinition`
- `WorkflowActionDefinition`
- `WorkflowRoleDefinition`
- `WorkflowTransitionDefinition`
- `WorkflowTaskDefinition`
- `SlaPolicy`
- `NotificationRoute`

### 10.3 `show-context` behavior

`show-context` accepts:

- `workflowInstanceId` positional argument.
- `--user <userId>` to simulate the actor in the console POC. Used to populate `actorAccess.userId` and `actorAccess.displayName` in the response.
- `--roles <comma-separated-roles>` to simulate actor roles. Used to populate `actorAccess.effectiveRoles`.
- `--format json` to print the `WorkflowContextResponse` object that the future API would return on form open.

`show-context` must not move workflow state, create tasks, close tasks, write audit, or queue notifications. It is a read-only command.

The console accepts arbitrary `--user` and `--roles` combinations without consistency checking; impersonation is the intended testing model. The future API will resolve roles from authentication claims instead.

`actorAccess.displayName` defaults to the value of `--user` when no separate display name source is available. The console does not maintain a user-name lookup table (consistent with the no-`DemoUser`-table rule in §11). The future API will resolve `displayName` from the authenticated user profile.

### 10.4 `execute-json` flags

`execute-json` accepts an optional `--admin-override` flag that bypasses role authorization (`Forbidden` and `AmbiguousRoleMatch`). It does not bypass `ValidationFailed`.

When `--admin-override` is used, the engine must:

- Print `WARN: admin override active` to console.
- Write the audit record with `PerformedByRoleKey = "AdminDemoOverride"`.
- For the audit `Reason` field: if the envelope's `actionReason` is non-empty, set `Reason = "[ADMIN OVERRIDE] " + actionReason`. If the envelope's `actionReason` is null or empty, set `Reason = "[ADMIN OVERRIDE]"` with no trailing whitespace.
- Otherwise execute the transition exactly as it would for a valid role.

`execute-json` also accepts two optional override flags used by demo scripts to reuse a single sample file across multiple business objects:

- `--business-object <id>` — override `businessObject.objectId` from the JSON file.
- `--workflow-instance <id>` — override `workflow.workflowInstanceId` from the JSON file.

These overrides are applied after the file is parsed but before idempotency and concurrency checks. They are intended for demo and test scripts only; production callers should send distinct envelopes.

Because `idempotencyKey` is globally unique, override mode must also derive deterministic request identity values before idempotency is checked:

```text
request.clientActionId = original clientActionId + ":" + effective workflowInstanceId
workflow.idempotencyKey = original idempotencyKey + ":" + effective workflowInstanceId
request.correlationId = original correlationId + ":" + effective workflowInstanceId
```

This keeps branch demo scripts repeatable when the same sample JSON file is reused for `PT-1001`, `PT-1002`, and `PT-1003`. Re-running the same sample with the same overrides should still be treated as an idempotent replay. Running the same sample with a different override should use a different derived idempotency key.

### 10.5 Read-only inspection command output shapes

Each inspection command prints a readable summary to console. The shapes below are the required minimum. Additional fields may be added but must not be removed.

#### `show <workflowInstanceId>`

Prints the current workflow instance summary:

```text
Workflow Instance: WF-PT-1001
  Business object: PortfolioTrade / PT-1001
  Workflow key:    PortfolioTrade
  Definition ver:  1
  Current state:   CheckerReview
  Status:          Active
  Version:         3
  Started at:      2030-01-15T15:30:00Z
  Started by:      maker1
  Completed at:    —
  Current task:    CheckerReviewTask (Checker) — Due: 2030-01-15T15:32:00Z
```

If no pending task exists, print `Current task: None`.

#### `show-tasks <workflowInstanceId>`

Prints all `WorkflowTask` rows for the instance, ordered by `CreatedAtUtc`:

```text
Tasks for WF-PT-1001:
  [Completed] CheckerReviewTask  | Checker | Created: 2030-01-15T15:30:00Z | Due: 2030-01-15T15:32:00Z | Closed by: CheckerReturnToMaker
  [Pending]   MakerReworkTask    | Maker   | Created: 2030-01-15T15:31:00Z | Due: —
```

Fields: `Status`, `TaskKey`, `AssignedRoleKey`, `CreatedAtUtc`, `DueAtUtc` (or `—` if null), `ClosedByActionKey` (or `—` if pending).

#### `show-history <workflowInstanceId>`

Prints `WorkflowAudit` rows only, ordered by `CreatedAtUtc`:

```text
Audit history for WF-PT-1001:
  2030-01-15T15:30:00Z | maker1      | Maker   | SaveDraft             | Draft           -> Draft
  2030-01-15T15:30:30Z | maker1      | Maker   | SubmitForChecker      | Draft           -> CheckerReview
  2030-01-15T15:31:00Z | checker1    | Checker | CheckerReturnToMaker  | CheckerReview   -> ReturnedToMaker
```

Fields: `CreatedAtUtc`, `PerformedByUserId`, `PerformedByRoleKey`, `ActionKey`, `FromStateKey -> ToStateKey`.

`show-history` does not include `NotificationOutbox` rows. Notifications are shown by `show-notifications`. `show-history` does not include a row for `start-workflow` because workflow instance creation is not an audited transition (see §16.1); the workflow start time and starting user are visible in `show <workflowInstanceId>` instead.

#### `show-notifications <workflowInstanceId>`

Prints `NotificationOutbox` rows only, ordered by `CreatedAtUtc`:

```text
Notifications for WF-PT-1001:
  [Sent]    2030-01-15T15:30:30Z | WorkflowTaskAssigned    | Checker         | Subject: New task: CheckerReviewTask
  [Pending] 2030-01-15T15:31:00Z | WorkflowReturnedToMaker | Maker           | Subject: Your trade was returned for correction
```

Fields: `Status`, `CreatedAtUtc`, `NotificationType`, `RecipientRoleKey`, `Subject`.

### 10.6 `execute-json` console output

Each command must print readable output suitable for team demonstration.

`show-context --format json` output and API responses must include the full `WorkflowContextResponse` shape from section 8.1.

Example `execute-json` output:

```text
Command: ExecuteWorkflowAction
Business object: PortfolioTrade / PT-1001
Workflow instance: WF-PT-1001
Actor: checker1
Roles: Checker

UI section: Checker
Button: CheckerReturnToMaker
Save as draft: False
Action key: CheckerReturnToMaker

Section data saved: Yes
Old state: CheckerReview
New state: ReturnedToMaker
Closed task: CheckerReviewTask
Created task: MakerReworkTask
Assigned role: Maker
SLA started: No
Notification queued: WorkflowReturnedToMaker
Audit written: CheckerReturnToMaker

Test expectation: Passed
```

### 10.7 `demo` command

`demo` is a required Milestone 6 deliverable. It runs the §25.1 happy path automatically end-to-end using the reserved business object `PT-DEMO`, requiring no operator input beyond the single command invocation.

```bash
dotnet run --project src/WorkflowEngine.Console -- demo
```

`demo` must:

1. Reset `PT-DEMO` using `--reset` (same logic as `start-workflow PortfolioTrade PT-DEMO --user maker1 --reset`).
2. Execute each step in the §25.1 happy path sequence in order, using `PT-DEMO` / `WF-PT-DEMO` as the business object and workflow instance.
3. Print a step header before each action, e.g. `--- Step 2: Save Draft ---`.
4. Print a brief pause indicator between steps (100ms is sufficient for readability).
5. After each step, print whether the step passed or failed its `testExpectations`.
6. If any step fails, print `Demo failed at step N: [resultCode] [message]` and stop. Do not continue to the next step.
7. If all steps pass, print `Demo completed successfully. N steps executed.`
8. Print the final audit history (same output as `show-history WF-PT-DEMO`) at the end of a successful run.

`demo` uses the same sample JSON files from §19, applying `--business-object PT-DEMO --workflow-instance WF-PT-DEMO` overrides automatically — it does not require separate demo-specific sample files.

`demo` covers the straight-through happy path only. The Checker Return to Maker and Trading Desk Return to Maker branches require the manual §25.2 and §25.3 scripts. `demo` does not need to cover these branches.

`PT-DEMO` is a reserved business object identifier. It must not be used in any other demo script or test fixture to avoid collision.

## 11. SQL seed data requirements

Seed data is required and testable.

`init-db` must create or update schema and seed:

- PortfolioTrade workflow definition
- Workflow definition version 1
- States
- Actions
- Roles
- Transitions
- Task definitions
- SLA policies
- Notification routes/templates

Note: v1 does not seed a user-to-role mapping table. The console POC simulates actor identity through `--user` and `--roles` flags, and the future API resolves identity from authentication claims. `userId` values in envelopes, audit, and notifications are recorded as opaque strings — the engine never joins them to a user table.

### Notification route seed table

`init-db` must seed exactly these `NotificationRoute` rows (workflow definition version 1):

| Transition (FromState → Action → Role) | NotificationRouteKey | NotificationType | RecipientRoleKey |
|---|---|---|---|
| `Draft` → `SubmitForChecker` → `Maker` | `RouteCheckerAssignedOnSubmit` | `WorkflowTaskAssigned` | `Checker` |
| `ReturnedToMaker` → `ResubmitForChecker` → `Maker` | `RouteCheckerAssignedOnResubmit` | `WorkflowTaskAssigned` | `Checker` |
| `CheckerReview` → `CheckerReturnToMaker` → `Checker` | `RouteReturnedToMakerFromChecker` | `WorkflowReturnedToMaker` | `Maker` |
| `CheckerReview` → `CheckerSendToTradingDesk` → `Checker` | `RouteTradingDeskAssigned` | `WorkflowTaskAssigned` | `TradingDesk` |
| `TradingDeskReview` → `TradingReturnToMaker` → `TradingDesk` | `RouteReturnedToMakerFromTradingDesk` | `WorkflowReturnedToMaker` | `Maker` |
| `TradingDeskReview` → `TradingTradeExecuted` → `TradingDesk` | `RouteBdrmAssigned` | `WorkflowTaskAssigned` | `BDRMClientService` |
| `TradingDeskRework` → `TradingTradeExecuted` → `TradingDesk` | `RouteBdrmAssigned` | `WorkflowTaskAssigned` | `BDRMClientService` |
| `BdrmClientServiceReview` → `BdrmTradeFulfilled` → `BDRMClientService` | `RouteWorkflowCompleted` | `WorkflowCompleted` | `Maker` |
| `BdrmClientServiceReview` → `BdrmTradeNotExecutedProperly` → `BDRMClientService` | `RouteTradingDeskReworkAssigned` | `WorkflowTaskAssigned` | `TradingDesk` |

Plus an SLA route used by `run-sla` (not bound to a transition):

| RouteKey | NotificationType | RecipientRoleKey |
|---|---|---|
| `RouteSlaTaskOverdue` | `SlaTaskOverdue` | (resolved at runtime from the overdue task's `AssignedRoleKey`) |

`RouteReturnedToMakerFromChecker` and `RouteReturnedToMakerFromTradingDesk` are intentionally separate routes because they carry different subject/body templates. `RouteBdrmAssigned` is intentionally shared between happy-path and rework paths because BDRM does not need to distinguish them. `RouteCheckerAssignedOnSubmit` and `RouteCheckerAssignedOnResubmit` are separate so Checker can distinguish a new submission from a resubmission.

### `verify-seed` validations

`verify-seed` must validate:

- Workflow definition exists.
- Workflow version exists.
- All required states exist.
- All required actions exist.
- All required roles exist.
- All required task definitions exist.
- All transitions from the action matrix exist, including both Return-to-Maker transitions and `ResubmitForChecker`.
- Every transition references valid states/actions/roles/tasks for the same `WorkflowDefinitionVersionId`.
- Every task definition has an owner role.
- Every SLA-enabled task has an `SlaPolicy` row resolvable by `(WorkflowDefinitionVersionId, SlaPolicyKey)`.
- Every notification-producing transition has a `NotificationRoute` row resolvable by `(WorkflowDefinitionVersionId, NotificationRouteKey)`.
- Every action that can appear in `WorkflowContextResponse.availableActions` has action presentation hints.
- `SaveDraft`, `SubmitForChecker`, and `ResubmitForChecker` share `buttonKey = MakerPrimary` and are placed in the Maker section.
- All workflow definition keys are unique per workflow definition version.
- No duplicate transitions exist for the same definition version + from state + action + role.
- Portfolio Trade matrix exactly matches section 7.
- The notification route seed table above is fully present.

Use SQL-backed verification. Do not rely only on EF InMemory for seed/persistence tests.

## 12. Suggested SQL/domain model

The first version should stay simple.

Required concepts:

- `WorkflowDefinition`
- `WorkflowDefinitionVersion`
- `WorkflowStateDefinition`
- `WorkflowActionDefinition`
- `WorkflowRoleDefinition`
- `WorkflowTransitionDefinition`
- `WorkflowTaskDefinition`
- `WorkflowInstance`
- `WorkflowTask`
- `MockSectionData`
- `WorkflowAudit`
- `SlaPolicy`
- `NotificationRoute`
- `NotificationOutbox`
- `IdempotencyRecord`

### WorkflowDefinition

```text
Id
WorkflowKey
DisplayName
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowKey)
```

### WorkflowDefinitionVersion

```text
Id
WorkflowDefinitionId
VersionNumber
IsActive
CreatedAtUtc
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionId, VersionNumber)
```

### WorkflowStateDefinition

```text
Id
WorkflowDefinitionVersionId
StateKey
DisplayName
IsTerminal
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, StateKey)
```

### WorkflowActionDefinition

```text
Id
WorkflowDefinitionVersionId
ActionKey
DisplayName
SectionKey nullable       -- workflow section hint for UI action placement
ButtonKey nullable        -- stable UI action/button key
ButtonLabel nullable      -- suggested button label; display only, not workflow logic
Intent nullable           -- SaveDraft / Submit / Approve / Return / Complete / Rework
RequiresReason            -- whether UI should ask for comments/reason before action
SortOrder
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, ActionKey)
```

These are workflow action presentation hints for `WorkflowContextResponse.availableActions`. They are not form field metadata and must not contain Portfolio Trade field-level validation rules. The workflow engine must still execute by `ActionKey`, current state, actor role, and transition metadata — never by `ButtonLabel`.

`RequiresReason` is a UI hint only. The engine does not validate `actionReason` content; it writes whatever string arrives to `WorkflowAudit.Reason`.

### WorkflowRoleDefinition

```text
Id
WorkflowDefinitionVersionId
RoleKey
DisplayName
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, RoleKey)
```

### WorkflowTaskDefinition

```text
Id
WorkflowDefinitionVersionId
TaskKey
DisplayName
OwnerRoleKey
SlaPolicyKey nullable
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, TaskKey)
```

### WorkflowTransitionDefinition

```text
Id
WorkflowDefinitionVersionId
FromStateKey
ActionKey
AllowedRoleKey
ToStateKey
TaskToCloseKey nullable
TaskToCreateKey nullable
NotificationRouteKey nullable
AuditLabel
IsActive
```

Required unique constraint:

```text
UNIQUE (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey)
```

Foreign keys for `FromStateKey`, `ActionKey`, `AllowedRoleKey`, `ToStateKey`, `TaskToCloseKey`, and `TaskToCreateKey` are **not** enforced at the SQL level (they would require composite FKs scoped by `WorkflowDefinitionVersionId`, which adds complexity for little POC value). Instead, `verify-seed` validates that every key referenced in `WorkflowTransitionDefinition` exists in the corresponding definition table for the same `WorkflowDefinitionVersionId`. This is acceptable because seed data is deterministic and `verify-seed` is part of the quality gates.

### Key resolution rules for SLA and notification

`WorkflowTaskDefinition.SlaPolicyKey` is resolved against `SlaPolicy` by `(WorkflowDefinitionVersionId, SlaPolicyKey)`. The `WorkflowDefinitionVersionId` comes from the same task definition row.

`WorkflowTransitionDefinition.NotificationRouteKey` is resolved against `NotificationRoute` by `(WorkflowDefinitionVersionId, RouteKey)`. The `WorkflowDefinitionVersionId` comes from the same transition row.

Both keys are not enforced as composite SQL foreign keys in the POC. `verify-seed` validates that every non-null `SlaPolicyKey` and `NotificationRouteKey` resolves to exactly one row in the corresponding table for the same `WorkflowDefinitionVersionId`. This matches the validation pattern used for state/action/role/task keys on transitions.

### WorkflowInstance

```text
Id
WorkflowInstanceId
WorkflowKey
WorkflowDefinitionVersionId  -- FK to WorkflowDefinitionVersion.Id
BusinessObjectType
BusinessObjectId
CurrentStateKey
Status
StartedAtUtc
StartedByUserId
CompletedAtUtc nullable
Version -- starts at 1; optimistic concurrency, incremented on state-changing transitions only
```

`Status` allowed values: `Active`, `Completed`. Set to `Active` on `start-workflow`. Set to `Completed` when a terminal transition fires (e.g., `BdrmTradeFulfilled`).

`StartedByUserId` is set from `start-workflow --user <userId>` (console) or from authenticated identity (future API). It records who started the workflow. The engine never updates this column after creation; it is the durable answer to "who started this workflow?"

`start-workflow` creates `WorkflowInstance.Version = 1`. `WorkflowContextResponse.workflow.workflowInstanceVersion` must mirror this value. `SaveDraft` does not increment it.

POC uniqueness:

```text
UNIQUE (WorkflowInstanceId)
UNIQUE (WorkflowKey, BusinessObjectType, BusinessObjectId)
```

The POC supports one workflow instance per business object. Historical multiple-instance behavior is out of scope.

### WorkflowTask

```text
Id
WorkflowInstanceId
TaskKey
AssignedRoleKey
AssignedUserId nullable
Status
CreatedAtUtc
DueAtUtc nullable
CompletedAtUtc nullable
CompletedByUserId nullable
ClosedByActionKey nullable
```

`Status` allowed values: `Pending`, `Completed`. A task is created with `Status = 'Pending'` and closed with `Status = 'Completed'`. There is no `Cancelled` status in v1 (no cancel path exists).

`AssignedUserId` is always null in v1. Tasks are assigned by role only (`AssignedRoleKey`). `AssignedUserId` exists for forward compatibility with future per-user task assignment. The engine must never populate it from `actor.userId` — the actor who triggers a transition is not the assignee of the created task.

MVP constraint:

```text
Only one Pending WorkflowTask is allowed per WorkflowInstanceId.
```

Preferred SQL Server implementation:

```text
Filtered unique index on WorkflowTask(WorkflowInstanceId) where Status = 'Pending'
```

### MockSectionData

```text
Id
BusinessObjectType
BusinessObjectId
SectionKey
PayloadJson
SavedByUserId
SavedAtUtc
CorrelationId
```

`MockSectionData` is append-only in the POC. Each successful non-idempotent `execute-json` action inserts one row for the submitted section payload. The engine does not update or delete previous mock data rows.

When latest mocked section data is displayed, select the newest row for the business object and section by `SavedAtUtc`, then `Id` as a tie-breaker.

Idempotent replay with a matching `RequestHash` returns stored `ResultJson` and must not insert another `MockSectionData` row. Failed validation, authorization, idempotency conflict, concurrency conflict, or workflow-definition-version mismatch must insert no `MockSectionData` row.

### WorkflowAudit

```text
Id
WorkflowInstanceId
BusinessObjectType
BusinessObjectId
ActionKey
FromStateKey
ToStateKey
PerformedByUserId
PerformedByRoleKey  -- free-form string, NO FK to WorkflowRoleDefinition
Reason
CorrelationId
CreatedAtUtc
```

`PerformedByRoleKey` is intentionally a free-form string with no foreign key to `WorkflowRoleDefinition`. This allows the audit to record `AdminDemoOverride` (which is not a seeded role per section 7) and any future override or system-actor values without breaking referential integrity. Audit tables should record what happened, even when actors are unusual.

### SlaPolicy

```text
Id
WorkflowDefinitionVersionId
SlaPolicyKey
DurationMinutes
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, SlaPolicyKey)
```

### NotificationRoute

```text
Id
WorkflowDefinitionVersionId
RouteKey
NotificationType
RecipientRoleKey nullable
RecipientUserId nullable
SubjectTemplate
IsActive
```

Required uniqueness:

```text
UNIQUE (WorkflowDefinitionVersionId, RouteKey)
```

### NotificationOutbox

```text
Id
WorkflowInstanceId
WorkflowTaskId nullable
BusinessObjectType
BusinessObjectId
NotificationType
RecipientRoleKey nullable
RecipientUserId nullable
Subject
PayloadJson
Status
CorrelationId
CreatedAtUtc
SentAtUtc nullable
```

`Status` allowed values in v1 are `Pending` and `Sent` only. No `Failed` status, no retry, no dead-letter (see section 14).

SLA duplicate rule:

```text
SlaTaskOverdue is queued at most once per WorkflowTaskId, ever.
```

Preferred SQL Server implementation:

```text
UNIQUE (WorkflowTaskId, NotificationType)
WHERE NotificationType = 'SlaTaskOverdue'
```

### IdempotencyRecord

```text
Id
IdempotencyKey
WorkflowInstanceId
ActionKey
RequestHash
ResultHash
ResultJson
CreatedAtUtc
```

Required unique constraint:

```text
UNIQUE (IdempotencyKey)
```

`ResultJson` stores the canonical serialized `WorkflowActionResult` returned by the original successful execution. It is not console display text and not a partial summary.

### Required SQL uniqueness summary

These constraints must exist in the SQL schema:

- `WorkflowDefinition`: `UNIQUE (WorkflowKey)`
- `WorkflowDefinitionVersion`: `UNIQUE (WorkflowDefinitionId, VersionNumber)`
- `WorkflowStateDefinition`: `UNIQUE (WorkflowDefinitionVersionId, StateKey)`
- `WorkflowActionDefinition`: `UNIQUE (WorkflowDefinitionVersionId, ActionKey)`
- `WorkflowRoleDefinition`: `UNIQUE (WorkflowDefinitionVersionId, RoleKey)`
- `WorkflowTaskDefinition`: `UNIQUE (WorkflowDefinitionVersionId, TaskKey)`
- `SlaPolicy`: `UNIQUE (WorkflowDefinitionVersionId, SlaPolicyKey)`
- `NotificationRoute`: `UNIQUE (WorkflowDefinitionVersionId, RouteKey)`
- `WorkflowTransitionDefinition`: `UNIQUE (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey)`
- `WorkflowInstance`: `UNIQUE (WorkflowInstanceId)` and `UNIQUE (WorkflowKey, BusinessObjectType, BusinessObjectId)`
- `WorkflowTask`: only one pending task per workflow instance for the MVP
- `NotificationOutbox`: `SlaTaskOverdue` queued at most once per `WorkflowTaskId`
- `IdempotencyRecord`: `UNIQUE (IdempotencyKey)`

## 13. SLA behavior

SLA is task-driven. A workflow task with a due date is the source of SLA truth.

```text
SubmitForChecker
  -> creates CheckerReviewTask
  -> starts Checker SLA

CheckerReturnToMaker
  -> closes CheckerReviewTask
  -> creates MakerReworkTask
  -> no Maker SLA in v1

CheckerSendToTradingDesk
  -> closes CheckerReviewTask
  -> creates TradingDeskReviewTask
  -> starts Trading Desk SLA

TradingReturnToMaker
  -> closes TradingDeskReviewTask
  -> creates MakerReworkTask
  -> no Maker SLA in v1

TradingTradeExecuted
  -> closes TradingDeskReviewTask or TradingDeskReworkTask
  -> creates BdrmClientServiceReviewTask
  -> starts BDRM Client Service SLA

BdrmTradeFulfilled
  -> closes BdrmClientServiceReviewTask
  -> creates no next task
  -> no active SLA remains

BdrmTradeNotExecutedProperly
  -> closes BdrmClientServiceReviewTask
  -> creates TradingDeskReworkTask
  -> starts Trading Desk rework SLA
```

For the POC, use short demo SLA durations so overdue behavior can be demonstrated quickly.

| Task key | Demo SLA duration | SLA enabled in v1 |
|---|---:|---|
| `CheckerReviewTask` | 2 minutes | Yes |
| `TradingDeskReviewTask` | 2 minutes | Yes |
| `TradingDeskReworkTask` | 2 minutes | Yes |
| `BdrmClientServiceReviewTask` | 2 minutes | Yes |
| `MakerReworkTask` | None | No |

`MakerReworkTask` is intentionally not SLA-tracked in v1: a returned request sitting in Maker's queue does not generate overdue notifications. This is acceptable for the POC because Maker holds the request and is expected to act on it. The schema fully supports adding a `MakerReworkTask` SLA later — it requires only seeding an `SlaPolicy` row and updating the `WorkflowTaskDefinition.SlaPolicyKey` reference. No engine code changes are required.

### Task `DueAtUtc` invariant

When a transition creates a new task, the engine sets `WorkflowTask.DueAtUtc` based on the task definition:

- If `WorkflowTaskDefinition.SlaPolicyKey` is non-null, look up `SlaPolicy` by `(WorkflowDefinitionVersionId, SlaPolicyKey)` and set `DueAtUtc = TimeProvider.GetUtcNow() + SlaPolicy.DurationMinutes`.
- If `WorkflowTaskDefinition.SlaPolicyKey` is null, set `DueAtUtc = null`. The engine does not invent a default deadline. The engine does not inherit `DueAtUtc` from the closing task.

`MakerReworkTask` has `SlaPolicyKey = null` and is created with `DueAtUtc = null` on every Return-to-Maker transition. `run-sla` selects pending tasks with `DueAtUtc < now`, which excludes null values by SQL three-valued logic. Tests must assert that `MakerReworkTask` is never selected by `run-sla` regardless of how much time passes.

### SLA levels for POC

The POC uses a single overdue level. `SlaTaskOverdue` is queued at most once per task ever, regardless of `process-notification-outbox` state. Multi-level escalation is out of scope and may be added later.

`show-context` computes `sla.status`, `sla.overdue`, and `sla.overdueByMinutes` by comparing `WorkflowTask.DueAtUtc` directly to `TimeProvider.GetUtcNow()`. It does **not** check `NotificationOutbox` to determine overdue status. This means a task can appear overdue in `show-context` before `run-sla` has run — this is correct and intentional. The SLA monitor's job is to queue notifications, not to gate the overdue signal.

`run-sla` must:

- Find pending tasks with `DueAtUtc` earlier than `now`.
- Before inserting an `SlaTaskOverdue` row, check whether any `NotificationOutbox` row already exists for `(WorkflowTaskId, NotificationType = 'SlaTaskOverdue')` regardless of `Status`. If one exists, skip. The filtered unique index from section 12 is the durable enforcement; this check is the friendly pre-flight that avoids relying on a unique-violation exception for normal flow.
- Queue SLA overdue notification records.
- Process each overdue task in its own short transaction so partial progress survives mid-batch failures.
- Not close tasks.
- Not move workflow state.

## 14. Notification behavior

Do not send real email in v1. Use notification outbox records.

Workflow transitions queue notifications such as:

- `WorkflowTaskAssigned`
- `WorkflowReturnedToMaker`
- `WorkflowCompleted`

SLA monitor queues notifications such as:

- `SlaTaskOverdue`

Required notification examples:

```text
SubmitForChecker
  -> queue WorkflowTaskAssigned to Checker group via RouteCheckerAssignedOnSubmit

ResubmitForChecker
  -> queue WorkflowTaskAssigned to Checker group via RouteCheckerAssignedOnResubmit

CheckerReturnToMaker
  -> queue WorkflowReturnedToMaker to Maker via RouteReturnedToMakerFromChecker

CheckerSendToTradingDesk
  -> queue WorkflowTaskAssigned to TradingDesk group via RouteTradingDeskAssigned

TradingReturnToMaker
  -> queue WorkflowReturnedToMaker to Maker via RouteReturnedToMakerFromTradingDesk

TradingTradeExecuted (from TradingDeskReview or TradingDeskRework)
  -> queue WorkflowTaskAssigned to BDRMClientService group via RouteBdrmAssigned

BdrmTradeNotExecutedProperly
  -> queue WorkflowTaskAssigned to TradingDesk group via RouteTradingDeskReworkAssigned

BdrmTradeFulfilled
  -> queue WorkflowCompleted via RouteWorkflowCompleted

run-sla detects overdue task
  -> queue SlaTaskOverdue via RouteSlaTaskOverdue, recipient resolved at runtime from the overdue task
```

### `NotificationOutbox.Status` allowed values

v1 uses three status values:

- `Pending` — newly queued. Set on insert by transition execution and SLA monitor.
- `Processing` — claimed by `process-notification-outbox` while delivery is in progress. The claim includes `ProcessingToken` and `ProcessingStartedAtUtc` so stale claims can be retried.
- `Sent` — terminal. Set by `process-notification-outbox` after the configured notification sender succeeds. `SentAtUtc` is set at the same time.

v1 does not implement a terminal `Failed` status or dead-letter queue. If `process-notification-outbox` encounters an exception while processing a row, the claim is released, `RetryCount` is incremented, `LastError` is stored, and the row returns to `Pending`. Earlier rows that already committed remain `Sent`; later rows remain available for the next run.

### `process-notification-outbox` behavior

1. Select all `NotificationOutbox` rows where `Status = 'Pending'`, plus stale `Processing` rows whose processing lease has expired, ordered by `CreatedAtUtc, Id`.
2. For each row:
   - Claim the row by setting `Status = 'Processing'`, `ProcessingToken`, and `ProcessingStartedAtUtc`.
   - Call the configured `INotificationSender.SendAsync(notification)` (see "Notification sender abstraction" below).
   - On success, update `Status = 'Sent'`, set `SentAtUtc = TimeProvider.GetUtcNow()`, and clear processing claim fields.
   - On exception, release the claim, increment `RetryCount`, store `LastError`, and abort the command. Earlier rows that already committed remain `Sent`; the failed row and later rows remain available for a later run.
3. Print a final count summary (`N notifications processed via {senderName}`).

Per-row claim/update behavior matches the `run-sla` pattern. A failure mid-batch leaves earlier rows correctly marked `Sent` and later rows still `Pending` for the next run.

Running `process-notification-outbox` with no available rows is a successful no-op that prints `0 notifications processed`. Running it twice in a row is safe — the second run finds no available rows.

### Notification sender abstraction

The notification sender is an interface, not a hardcoded delivery mechanism. This keeps dev/test delivery safe while leaving configuration-driven SMTP wiring in place.

```csharp
public interface INotificationSender
{
    string SenderName { get; }
    Task SendAsync(NotificationDelivery notification, CancellationToken cancellationToken);
}
```

Three concrete implementations are required in v1:

1. **`ConsoleNotificationSender`** (default for dev and tests) — prints a readable summary to console (`NotificationType`, recipient, subject, `WorkflowInstanceId`). Does not send any real email. `SenderName = "Console"`.
2. **`NoOpNotificationSender`** — does nothing, returns immediately. Used when the operator wants to mark notifications as sent without any output. `SenderName = "NoOp"`.
3. **`SmtpNotificationSender`** — fully implemented SMTP relay sender (see SMTP integration below). Used in production environments. `SenderName = "Smtp"`.

Implementation registration is **configuration-driven** in `Program.cs`. Reading from `Notifications:Sender` in configuration:

```csharp
var senderType = configuration.GetValue<string>("Notifications:Sender") ?? "Console";
switch (senderType.ToLowerInvariant())
{
    case "smtp":   services.AddSingleton<INotificationSender, SmtpNotificationSender>(); break;
    case "noop":   services.AddSingleton<INotificationSender, NoOpNotificationSender>(); break;
    case "console":
    default:       services.AddSingleton<INotificationSender, ConsoleNotificationSender>(); break;
}
```

Default in `appsettings.json` is `"Console"`. Default in test fixtures is `"Console"` (or `"NoOp"` if console output noise is undesirable). To enable real SMTP delivery, change the configured value to `"Smtp"` and provide the SMTP settings described below. **No code change is needed** to switch from console output to SMTP — the UI integration team flips a configuration flag.

`SmtpNotificationSender` must throw `InvalidOperationException` at startup (not at first send) if `Notifications:Sender = "Smtp"` is configured but `Smtp:Host` is missing or empty. Failing fast at startup avoids "queued for hours, all failures" scenarios.

### SMTP integration (on-prem Outlook via SMTP relay)

`SmtpNotificationSender` targets an **on-prem SMTP relay** (the standard pattern for enterprise Outlook/Exchange environments). The relay typically accepts unauthenticated connections from approved IP ranges on port 25. No credentials are stored in code or configuration.

Use **MailKit** (`MailKit` NuGet package) as the SMTP client library. Microsoft has deprecated `System.Net.Mail.SmtpClient` for new development; MailKit is the official recommended replacement and is the only third-party package added to the project for this feature. Adding MailKit requires §22 stop-and-ask approval is **not** required because this section pre-approves it; coding agents may add `MailKit` without further confirmation.

Configuration shape:

```json
{
  "Notifications": {
    "Sender": "Smtp"
  },
  "Smtp": {
    "Host": "smtp-relay.yourfirm.local",
    "Port": 25,
    "From": "noreply-cdt@yourfirm.com",
    "RecipientOverride": null,
    "RoleEmailMap": {
      "Maker": "cdt-makers@yourfirm.com",
      "Checker": "cdt-checkers@yourfirm.com",
      "TradingDesk": "cdt-tradingdesk@yourfirm.com",
      "BDRMClientService": "cdt-bdrm@yourfirm.com"
    }
  }
}
```

Required configuration behavior:

- `Smtp:Host` and `Smtp:Port` come from configuration. `Port` defaults to `25`. MailKit uses automatic socket option negotiation in v1; there is no separate TLS flag.
- `Smtp:From` is the envelope-from and header-from address. Must be a real address your SMTP relay accepts as an authorized sender.
- `Smtp:RecipientOverride`, when non-null, **redirects all outgoing emails to this address regardless of the notification's intended recipient**. This is the safety hatch that prevents accidental email floods during integration testing — set `RecipientOverride` to your own mailbox during pre-production smoke tests, then null it out for go-live. The notification body must still include the original intended recipient (e.g., "Original recipient: Checker group") so the test reader knows what would have been delivered.
- No SMTP credentials. The relay is IP-restricted, not credential-restricted. If your firm's relay does require credentials, that is a stop-and-ask change because it requires secret management (`Smtp:Username` from environment variable, never from `appsettings.json`).

Required v1 behavior of `SmtpNotificationSender`:

- Resolves recipient email address in this order:
  1. `Smtp:RecipientOverride`, when configured.
  2. `recipientEmail` from the notification's persisted `PayloadJson` / `workflow.notificationContext`.
  3. `RecipientRoleKey` using the configured `Smtp:RoleEmailMap`.
- v1 **does not** look up users in SQL (consistent with the no-`DemoUser`-table rule in §11). Instead, the target API supplies per-record recipient email addresses through `notificationContext` when needed, and configuration carries a static role fallback map:

```json
{
  "Smtp": {
    "RoleEmailMap": {
      "Maker": "cdt-makers@yourfirm.com",
      "Checker": "cdt-checkers@yourfirm.com",
      "TradingDesk": "cdt-tradingdesk@yourfirm.com",
      "BDRMClientService": "cdt-bdrm@yourfirm.com"
    }
  }
}
```

- If no `RecipientOverride`, no payload `recipientEmail`, and `RecipientRoleKey` has no entry in `RoleEmailMap`, throw — the row stays `Pending` and the operator must fix configuration or request data. Do not silently drop the notification.
- Subject is taken from `NotificationOutbox.Subject`. Body is taken from persisted `NotificationOutbox.Body` when present. Older rows without a persisted body fall back to readable key/value lines built from `NotificationOutbox.PayloadJson`.
- Connect to `Host:Port`, send the message, disconnect. Use MailKit's `SmtpClient.Connect`, `Send`, `Disconnect` lifecycle. Connection per send is acceptable for v1; connection pooling is a v2 optimization.
- Log the SMTP response code at INFO level. Log failures at ERROR level with full exception detail.

### SMTP testing strategy

SMTP code must be testable **without a real relay**. Tests run in three tiers:

**Tier 1 — Unit tests against a mock `INotificationSender`.** All workflow-engine tests that assert "notification was sent" use a test double like `RecordingNotificationSender` that captures sent rows in memory. These tests never touch SMTP code paths. This is the default coverage.

**Tier 2 — Integration tests against a fake SMTP server.** A small set of tests that exercise `SmtpNotificationSender` directly against a local fake SMTP server. Recommended option: `smtp4dev` running in a Docker container (`docker run -p 3025:25 rnwood/smtp4dev`) or `Papercut SMTP` as a standalone Windows service. The test fixture starts the fake server, points `Smtp:Host` to `localhost:3025`, sends one email, asserts the fake server received it with the expected subject/recipient/body, then tears down. These tests must be marked with a `[Trait("Category", "Smtp")]` attribute (or equivalent for the chosen test framework) and excluded from default `dotnet test` runs. Run them on demand with `dotnet test --filter Category=Smtp`.

**Tier 3 — Live smoke test against the firm's SMTP relay.** Manual only. Not part of automated CI. Operator runs `process-notification-outbox` against a `start-workflow PT-SMOKE` instance with `Notifications:Sender = "Smtp"` and `Smtp:RecipientOverride = "operator-mailbox@yourfirm.com"`. Verifies one email lands in the override mailbox. Used once during UI integration go-live, not during normal development.

Tier 1 is mandatory for v1. Tier 2 is mandatory for v1 (proves `SmtpNotificationSender` works). Tier 3 is documentation-only in v1; it is run during UI integration, which is out of scope for this project.

### Notification configuration matrix

| Environment | `Notifications:Sender` | Behavior | Real emails sent? |
|---|---|---|---|
| Local dev (default) | `Console` | Prints to console | No |
| Unit tests | `Console` or `NoOp` | In-memory test double via DI override | No |
| Integration tests (Tier 2) | `Smtp` (with localhost fake server) | Sends to `smtp4dev` / `Papercut` on `localhost` | No (caught by fake server) |
| UI integration smoke test (Tier 3, manual) | `Smtp` (with `RecipientOverride` set) | Sends to operator's mailbox only | One controlled email |
| Production (post-CDT, out of scope here) | `Smtp` (no override) | Sends to real role recipients | Yes |

The CDT POC ships with the first two rows working out of the box. The third and fourth rows are exercised manually during UI integration. The fifth row is enabled by configuration only — no code change.

### Recipient resolution in v1

Notifications in v1 are still routed by workflow metadata to a recipient role, but SMTP delivery can use a per-notification email address when the target API supplies one:

- `NotificationOutbox.RecipientRoleKey` is set from the resolved `NotificationRoute.RecipientRoleKey` (e.g., `Checker`, `TradingDesk`, `BDRMClientService`, `Maker`).
- `NotificationOutbox.RecipientUserId` is **always null** in v1. The column exists for forward compatibility with future per-user notifications, but the v1 SLA monitor and transition handlers never populate it.
- `workflow.notificationContext.recipientEmail`, when present, is persisted in `NotificationOutbox.PayloadJson` and is preferred by `SmtpNotificationSender` after `Smtp:RecipientOverride`.
- `Smtp:RoleEmailMap` remains the fallback for role-group recipients and for notifications where the target API did not supply a per-record email address.

The "Checker group" / "TradingDesk group" language above is descriptive — it means "whoever holds the Checker role at the time of delivery." v1 does not maintain a user-to-role mapping table, so no group expansion happens in `process-notification-outbox`. For business-record-specific recipients such as the Maker on a returned item, the target API must supply `recipientEmail` in `notificationContext`.

## 15. Transaction rule

For `execute-json`, save mocked section data and execute the workflow action inside one transaction.

Transaction should include:

1. Validate envelope.
1. Compute request hash.
1. Check idempotency. If a matching record exists, return stored `ResultJson` immediately. If the key exists with a different hash, return `IdempotencyConflict` immediately.
1. Load workflow instance, open task, and workflow definition/version.
1. Check workflow definition version guard.
1. Check `expectedCurrentState`.
1. Check `expectedCurrentTaskId` / `expectedCurrentTaskKey` / `expectedInstanceVersion`, if provided.
1. Verify action is allowed from current state for actor role.
1. Save mocked section data.
1. If action is `SaveDraft`, write lightweight audit, write idempotency record, and commit without changing state/version/tasks/notifications.
1. Close current task if the transition requires it. Closing a task means: `UPDATE WorkflowTask SET Status = 'Completed', CompletedAtUtc = TimeProvider.GetUtcNow(), CompletedByUserId = actor.userId, ClosedByActionKey = actionKey WHERE Id = @taskId`. When `--admin-override` is used, `CompletedByUserId` still records `actor.userId` from the envelope (the human who triggered the action); the override is noted in `WorkflowAudit`, not in the task row.
1. Move workflow state using optimistic concurrency.
1. If moving to `Completed`, set `WorkflowInstance.Status = 'Completed'` and `WorkflowInstance.CompletedAtUtc = TimeProvider.GetUtcNow()` and ensure no pending task remains.
1. Create next task if the transition requires it.
1. Create notification outbox record if required.
1. Write workflow audit record. For `SaveDraft`, `ToStateKey` records the unchanged current state (e.g., `Draft → Draft` or `ReturnedToMaker → ReturnedToMaker`). This is intentional — `SaveDraft` does not move state, so `FromStateKey` and `ToStateKey` are equal. Audit queries that interpret `ToStateKey` as "forward movement" must exclude `SaveDraft` rows when computing state progression.
1. Write idempotency record.
1. Commit.

If any step fails, the workflow must not be partially moved. The transaction is rolled back and a structured error result is returned.

### Transaction isolation level

Use SQL Server's default `READ COMMITTED` isolation level for `execute-json` transactions. Correctness is provided by:

- Optimistic concurrency: `UPDATE WorkflowInstance SET CurrentStateKey = @new, Version = Version + 1 WHERE Id = @id AND Version = @expected`. Zero rows affected returns `ConcurrencyConflict`.
- The filtered unique index on `WorkflowTask(WorkflowInstanceId) WHERE Status = 'Pending'` prevents two concurrent transitions from both creating a pending task on the same instance.
- The unique constraint on `IdempotencyRecord(IdempotencyKey)` prevents duplicate idempotency record inserts.

Do not use `SERIALIZABLE` or `SNAPSHOT` isolation in v1. They are not required for correctness given the optimistic concurrency design, and `SERIALIZABLE` introduces lock contention and deadlock risk under concurrent actors.

The `run-sla` and `process-notification-outbox` commands also use `READ COMMITTED`. Each task processed within `run-sla` is its own short transaction, not one large transaction wrapping the whole scan.

## 16. POC engineering discipline

This is a POC, but it must be implemented with engineering discipline. The label "production-grade" should not invite scope creep; the standards below are exhaustive for this project.

### Required

- Clear architecture boundaries
- Simple, readable code
- SQL-backed persistence
- Deterministic seed data
- Repeatable console demos
- Automated tests
- Structured errors
- Idempotent workflow execution
- Transaction around save-data + workflow action
- Audit history
- Notification outbox instead of direct email side effects
- No hidden state
- No hardcoded secrets
- No silent failures
- Warnings treated as errors

### Not required and not desired

- Hardcoded workflow behavior
- Skipping tests
- Putting business logic directly in `Program.cs`
- Using fake in-memory behavior that hides SQL issues
- Disabling warnings to make builds pass
- Committing passwords, tokens, secrets, or embedding local-only connection strings in code
- Adding complex patterns that are not required

### 16.1 Logging severity and result codes

| Event | Severity | Result returned |
|---|---|---|
| Successful state transition | Info | `Success` |
| `SaveDraft` no state change | Info | `Success` |
| Checker Return to Maker | Info | `Success` |
| Trading Desk Return to Maker | Info | `Success` |
| Idempotent replay with matching request | Info | Original `Success` result |
| Validation failure | Warning | `ValidationFailed` |
| Existing workflow instance without `--reset` | Warning | `AlreadyExists` |
| Action does not exist or not possible from current state | Warning | `InvalidTransition` |
| Role does not authorize action | Warning | `Forbidden` |
| Multiple roles match same `(state, action)` with different `AllowedRoleKey` | Warning | `AmbiguousRoleMatch` |
| Concurrency conflict | Warning | `ConcurrencyConflict` |
| Workflow definition version mismatch | Warning | `WorkflowDefinitionVersionMismatch` |
| Actor roles in body do not match authenticated identity (API only) | Warning | `ActorRolesMismatch` |
| Idempotency conflict | Warning | `IdempotencyConflict` |
| `--admin-override` used | Warning | `Success` with audit override marker |
| SLA monitor queued overdue notification | Info | n/a |
| Unhandled exception | Error | Process aborts and transaction rolls back |

All log lines should include `correlationId` and `workflowInstanceId` when available.

### Audit on failure

v1 writes `WorkflowAudit` rows only for successful actions (`Success` result, including `SaveDraft`). Failed actions (`ValidationFailed`, `Forbidden`, `InvalidTransition`, `ConcurrencyConflict`, `WorkflowDefinitionVersionMismatch`, `IdempotencyConflict`, `AmbiguousRoleMatch`, `ActorRolesMismatch`, `AlreadyExists`) write **no** audit row. The transaction is rolled back per section 15.

This is a deliberate v1 limitation. Failure audit is valuable for security forensics and debugging, but adding it correctly requires a separate transaction that commits independently of the main rollback, plus a schema change to distinguish "attempted" from "executed" rows. Out of scope for the POC.

Failure forensics in v1 come from console output and structured log lines (correlation ID + workflow instance ID).

Failed `--admin-override` attempts (e.g., admin override used but envelope was malformed) also write no audit, consistent with the rule above. The override flag's purpose is to bypass *role authorization*, not to add a separate audit code path for failures.

If a future requirement adds failure audit, the schema already supports it: `WorkflowAudit.PerformedByRoleKey` is free-form and there are no FK constraints that would block recording an unusual actor or null `ToStateKey`. Likely additions: an `Outcome` column (`Success` / `Failed`), a `ResultCode` column, and a separate transaction in the application layer for the failure-audit insert.

### 16.2 Simplicity rule

Use the smallest clear design that satisfies the current requirement. Add complexity only when the requirement proves it is needed and the decision is approved.

Do not introduce these unless explicitly approved:

- CQRS / MediatR
- Event sourcing
- Distributed saga / orchestration framework
- BPMN engine
- Plugin framework
- Message broker
- Dynamic rule-language engine
- Microservices
- Parallel workflow engine
- Custom dependency injection framework

Before adding complexity, write a short decision note:

```text
Decision needed:
Problem:
Simple option:
More complex option:
Why the simple option is not enough:
Impact on schema/tests/commands:
Recommendation:
```

### 16.3 Surgical changes

- Touch only what the task requires.
- Do not rewrite, refactor, or rename adjacent code unless the task requires it.
- Do not rename public types or JSON fields without approval.
- Do not remove existing behavior unless the requirement says to.
- Every changed line should trace back to the requested task.
- Mention unrelated issues in the completion summary instead of fixing them silently.

### 16.4 Pushback list

The coding agent should push back, with a simpler alternative, when a request would:

- Hardcode Portfolio Trade into the reusable engine
- Make the workflow forward-only
- Remove Checker Return to Maker
- Remove Trading Desk Return to Maker
- Let request JSON control next state
- Let request `workflow.options` override seeded workflow metadata
- Remove SQL-backed seed verification
- Skip tests for workflow transitions
- Add unnecessary architecture complexity
- Mix form validation, workflow execution, SLA, and notifications into one service

### 16.5 Research rule

When unsure, research before implementing. Authoritative sources first:

- Microsoft docs for .NET, EF Core, SQL Server, DI, testing, analyzers
- OWASP / NIST for security guidance
- Official package documentation before adding/using a package

Do not add a new production dependency without approval. If research changes an implementation choice, mention the source and reason in the completion summary.

## 17. Source anchors

- Microsoft .NET documentation: https://learn.microsoft.com/dotnet/
- EF Core documentation: https://learn.microsoft.com/ef/core/
- TimeProvider documentation: https://learn.microsoft.com/dotnet/api/system.timeprovider
- MailKit documentation: https://github.com/jstedfast/MailKit
- MailKit on NuGet: https://www.nuget.org/packages/MailKit
- smtp4dev (fake SMTP server for Tier 2 tests): https://github.com/rnwood/smtp4dev
- OWASP ASVS: https://owasp.org/www-project-application-security-verification-standard/
- NIST Secure Software Development Framework: https://csrc.nist.gov/Projects/ssdf

## 18. Architecture expectations

Suggested solution structure:

```text
src/
  WorkflowEngine.Console/
    Program.cs
    Command handlers
  WorkflowEngine.Application/
    WorkflowActionEnvelope DTOs
    WorkflowContextResponse DTOs
    WorkflowActionResult DTOs
    AvailableWorkflowAction DTOs
    ExecuteJsonApplicationService
    WorkflowContextApplicationService
    StartWorkflowApplicationService
    SeedVerificationService
    Transaction boundary
  WorkflowEngine.Domain/
    WorkflowEngine
    WorkflowTransitionResolver
    WorkflowInstance domain model
    WorkflowTask domain model
    Result/error types
  WorkflowEngine.Infrastructure/
    EF Core DbContext
    Entities
    Repositories
    SQL seeding
    Notification outbox persistence
    Notification senders (ConsoleNotificationSender, NoOpNotificationSender, SmtpNotificationSender)
    SLA monitor persistence
  WorkflowEngine.PortfolioTradeSeed/
    Portfolio Trade workflow definition seed data
    Sample JSON files
tests/
  WorkflowEngine.Tests/
    Domain tests
    Application tests
    SQL-backed seed tests
```

Responsibilities:

- Console project: parse commands, load JSON files, print readable demo output.
- Application layer: validate `WorkflowActionEnvelope`, produce `WorkflowContextResponse`, produce `WorkflowActionResult`, own transaction boundary, save mocked data, call workflow engine.
- Domain / workflow engine: resolve metadata-driven transitions, update instance state, manage tasks, enforce state/action/role/task rules, and compute available actions from current state + actor role + transition metadata.
- Infrastructure: EF Core, SQL persistence, seeding, repositories, SLA monitor, notification outbox persistence.
- Portfolio Trade seed/data: workflow definition seed data and sample envelopes only.

### Clock abstraction

The codebase must not call `DateTime.UtcNow` directly outside the composition root.

Use .NET 10's built-in `TimeProvider` abstract class. Inject `TimeProvider` into any service, domain type, or repository that needs the current UTC time. Read the current time as `_timeProvider.GetUtcNow().UtcDateTime`.

- **Production registration:** `services.AddSingleton(TimeProvider.System)` in `Program.cs`.
- **Test registration:** use `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` (or a hand-rolled stub) so tests can set, advance, and assert against deterministic times.
- **Console `--now` override:** `run-sla --now "2030-01-15T16:00:00Z"` registers a fixed-time `TimeProvider` for the duration of the command. `--now` is supported on `run-sla` only in v1; other commands use the system clock.
- **EF Core entity defaults:** do not use `DateTime.UtcNow` in entity property initializers or `HasDefaultValueSql("GETUTCDATE()")`. Timestamps come from application/domain code that received the `TimeProvider`. Entities are dumb data carriers.

The `WorkflowContextResponse.serverTimeUtc` field must come from `TimeProvider`, not `DateTime.UtcNow`.

## 19. Required sample JSON files

Create sample files under `samples/workflow-actions/`:

```text
01-save-draft.json
02-submit-for-checker.json
03-checker-return-to-maker.json
04-maker-resubmit-for-checker.json
05-checker-send-to-trading-desk.json
06-trading-return-to-maker.json
07-trading-trade-executed.json
08-bdrm-trade-fulfilled.json
09-bdrm-trade-not-executed-properly.json
10-trading-rework-trade-executed.json
```

### Default business object identifiers

All sample files must use these baked-in values:

- `businessObject.objectType = "PortfolioTrade"`
- `businessObject.objectId = "PT-1001"`
- `workflow.workflowKey = "PortfolioTrade"`
- `workflow.workflowInstanceId = "WF-PT-1001"`
- `workflow.workflowDefinitionVersion = 1`

Demo scripts that need to run on a different business object use `execute-json --business-object <id> --workflow-instance <id>` overrides to retarget the same sample file. This is why §25.1 (which uses `PT-1001`) does not need overrides while §25.2 (`PT-1002`), §25.3 (`PT-1003`), and the `demo` command (`PT-DEMO`) do.

Sample files must not be edited per-demo to change business object values. Edit-by-override keeps demo branches reproducible and avoids drift between the canonical sample and what was actually executed.

Every sample must include `testExpectations`.

Mandatory expectations for Return-to-Maker samples:

```text
03-checker-return-to-maker.json
  expectedOldState = CheckerReview
  expectedNewState = ReturnedToMaker
  expectedClosedTaskKey = CheckerReviewTask
  expectedCreatedTaskKey = MakerReworkTask
  expectedAssignedRole = Maker
  expectedNotificationType = WorkflowReturnedToMaker
  expectedAuditAction = CheckerReturnToMaker

06-trading-return-to-maker.json
  expectedOldState = TradingDeskReview
  expectedNewState = ReturnedToMaker
  expectedClosedTaskKey = TradingDeskReviewTask
  expectedCreatedTaskKey = MakerReworkTask
  expectedAssignedRole = Maker
  expectedNotificationType = WorkflowReturnedToMaker
  expectedAuditAction = TradingReturnToMaker
```

## 20. Required tests

Add tests for:

- Seed data exists in SQL.
- Portfolio Trade transition matrix matches requirements (all eleven rows from section 7, including `ResubmitForChecker`).
- Every transition has valid state/action/role/task references.
- Every notification-producing transition resolves to a `NotificationRoute` row by `(WorkflowDefinitionVersionId, NotificationRouteKey)`.
- Every SLA-enabled task definition resolves to an `SlaPolicy` row by `(WorkflowDefinitionVersionId, SlaPolicyKey)`.
- `SubmitForChecker` creates `CheckerReviewTask`.
- `CheckerReturnToMaker` closes `CheckerReviewTask`, moves to `ReturnedToMaker`, creates `MakerReworkTask`, and queues `WorkflowReturnedToMaker`.
- `CheckerSendToTradingDesk` closes `CheckerReviewTask` and creates `TradingDeskReviewTask`.
- `TradingReturnToMaker` closes `TradingDeskReviewTask`, moves to `ReturnedToMaker`, creates `MakerReworkTask`, and queues `WorkflowReturnedToMaker`.
- `ResubmitForChecker` closes `MakerReworkTask`, moves to `CheckerReview`, creates `CheckerReviewTask`, and queues `WorkflowTaskAssigned`.
- `TradingTradeExecuted` routes to `BdrmClientServiceReview`.
- `BdrmTradeNotExecutedProperly` routes to `TradingDeskRework`.
- `BdrmTradeFulfilled` completes workflow.
- `MakerReworkTask` is created with `DueAtUtc = null` on both Checker and Trading Desk return paths.
- `run-sla --now` advanced arbitrarily far never queues `SlaTaskOverdue` for a `MakerReworkTask`.
- Envelope missing `actor` block returns `ValidationFailed`.
- Envelope with empty `actor.roles: []` returns `ValidationFailed`.
- Envelope missing `dataSave` block returns `ValidationFailed`.
- Envelope with empty payload `dataSave.payload = {}` is permitted.
- Action that does not exist returns `InvalidTransition`.
- Valid action with non-matching role returns `Forbidden`.
- Multiple matching roles with different `AllowedRoleKey` returns `AmbiguousRoleMatch`.
- `--admin-override` bypasses `Forbidden` and `AmbiguousRoleMatch` but not `ValidationFailed`.
- `SaveDraft` does not change state, version, or tasks.
- Idempotent replay returns the original result without re-executing.
- Idempotency key reused with a different request returns `IdempotencyConflict`.
- Override-mode sample reuse derives distinct idempotency keys per effective workflow instance.
- `expectedCurrentState` mismatch returns `ConcurrencyConflict`.
- `expectedCurrentTaskKey` mismatch returns `ConcurrencyConflict`.
- `expectedInstanceVersion` mismatch returns `ConcurrencyConflict`.
- `start-workflow` creates `WorkflowInstance.Version = 1`.
- `start-workflow` creates deterministic `WorkflowInstanceId = WF-{businessObjectId}`.
- `start-workflow` without `--reset` returns `AlreadyExists` when the instance exists.
- `start-workflow` after `BdrmTradeFulfilled` returns `AlreadyExists` with `existingInstanceStatus = "Completed"`.
- `start-workflow` on a still-running instance returns `AlreadyExists` with `existingInstanceStatus = "Active"`.
- `AlreadyExists` result includes `existingInstanceStatus` and `existingInstanceVersion`.
- A new `start-workflow` for a different `BusinessObjectId` (e.g., `PT-1001-R2`) succeeds even if `PT-1001` is `Completed`.
- `start-workflow --reset` clears runtime data only and does not delete seed data.
- `start-workflow --reset` clears `IdempotencyRecord` rows by `WorkflowInstanceId` column, including override-mode derived keys.
- Same idempotency key + same request returns stored `ResultJson` without re-executing.
- Same idempotency key + changed normalized request returns `IdempotencyConflict`.
- Successful actions insert one append-only `MockSectionData` row.
- Idempotent replay and failed concurrency checks do not insert duplicate `MockSectionData` rows.
- Failed actions write no audit row (`ValidationFailed`, `Forbidden`, `InvalidTransition`, `ConcurrencyConflict`, `IdempotencyConflict`, `AmbiguousRoleMatch`).
- MakerPrimary `SaveDraft` requires `uiAction.saveAsDraft = true`.
- MakerPrimary `SubmitForChecker` and `ResubmitForChecker` require `uiAction.saveAsDraft = false`.
- Definition keys are unique per workflow definition version.
- `workflow.options` cannot suppress metadata-required task creation, notification creation, or audit writing.
- SLA monitor queues overdue notifications once per task, regardless of `process-notification-outbox` state.
- `process-notification-outbox` marks `Pending` rows as `Sent` and is safe to run twice.
- `execute-json` command handles every sample envelope successfully.
- `--admin-override` writes a distinguishable audit record on success.
- Workflow context for `Draft` and Maker returns `SaveDraft` and `SubmitForChecker`.
- Workflow context for `ReturnedToMaker` and Maker returns `SaveDraft` and `ResubmitForChecker`.
- Workflow context for `CheckerReview` and Checker returns `CheckerReturnToMaker` and `CheckerSendToTradingDesk`.
- Workflow context for `TradingDeskReview` and Trading Desk returns `TradingReturnToMaker` and `TradingTradeExecuted`.
- Workflow context for `TradingDeskRework` and Trading Desk returns `TradingTradeExecuted`.
- Workflow context for `BdrmClientServiceReview` and BDRM Client Service returns `BdrmTradeFulfilled` and `BdrmTradeNotExecutedProperly`.
- Workflow context for a wrong role returns no available actions and a read-only reason.
- Workflow context for `Completed` returns no available actions.
- `show-context` command prints valid `WorkflowContextResponse` JSON.
- Successful `execute-json` returns or models a `WorkflowActionResult` with old/new state, closed/created task, notification, audit summary, and refreshed workflow context.
- Closed task has `Status = 'Completed'`, `CompletedAtUtc` set, `CompletedByUserId = actor.userId`, and `ClosedByActionKey = actionKey`.
- Created task has `Status = 'Pending'`, `AssignedUserId = null`, and `AssignedRoleKey` matching the transition metadata.
- `WorkflowInstance.Status = 'Active'` after `start-workflow`.
- `WorkflowInstance.Status = 'Completed'` after `BdrmTradeFulfilled`.
- `WorkflowAudit.ToStateKey = FromStateKey` for `SaveDraft` rows.
- `show-context` returns `sla.status = 'NoSla'` and `sla.dueAtUtc = null` for `Draft` state (no task).
- `show-context` returns `sla.status = 'NoSla'` and `sla.dueAtUtc = null` for `ReturnedToMaker` state (`MakerReworkTask` has null SLA).
- `show-context` returns `sla.status = 'OnTrack'` when task `DueAtUtc` is in the future.
- `show-context` returns `sla.status = 'Overdue'` when task `DueAtUtc` is in the past, even before `run-sla` has run.
- `show-context` never returns a null `sla` block regardless of state.
- `demo` command runs end-to-end successfully using `PT-DEMO` and prints `Demo completed successfully`.
- `demo` command resets `PT-DEMO` before running, making it safe to run twice.
- `start-workflow --user maker1` persists `maker1` to `WorkflowInstance.StartedByUserId`.
- `start-workflow` writes no `WorkflowAudit` row.
- `show <workflowInstanceId>` displays `Started by:` from `StartedByUserId`.
- `show-context` returns `actorAccess.displayName` equal to the `--user` value.
- `workflow.options.closeCurrentTask = false` on a transition that requires task close still closes the task and prints a warning.
- `workflow.options.closeCurrentTask = true` on a transition that does not require task close does not close any task and prints a warning.
- All sample files in §19 use `PT-1001` / `WF-PT-1001` as default identifiers.
- `--admin-override` with non-empty `actionReason` produces audit `Reason = "[ADMIN OVERRIDE] <reason>"`.
- `--admin-override` with null or empty `actionReason` produces audit `Reason = "[ADMIN OVERRIDE]"` with no trailing whitespace.
- `process-notification-outbox` calls the configured `INotificationSender` for each pending row.
- `ConsoleNotificationSender` prints to console and does not throw for any well-formed `NotificationDelivery`.
- `NoOpNotificationSender` returns immediately and does nothing.
- `RecordingNotificationSender` (test double) captures sent rows in memory for unit-test assertions.
- `SmtpNotificationSender` throws at startup when `Notifications:Sender = "Smtp"` and `Smtp:Host` is missing or empty.
- `SmtpNotificationSender` uses `workflow.notificationContext.recipientEmail` when present and does not require a role map for that row.
- `SmtpNotificationSender` throws when neither payload `recipientEmail` nor `RecipientRoleKey` role mapping can resolve a recipient; the row remains `Pending`.
- `SmtpNotificationSender` honors `Smtp:RecipientOverride` and redirects all mail to the override address while preserving original-recipient metadata in the body.
- `SmtpNotificationSender` integration test (Tier 2, `Category=Smtp`) sends to `smtp4dev`/`Papercut` on `localhost` and asserts the fake server received the email with the expected subject, recipient, and body.
- Tier 2 SMTP tests are excluded from default `dotnet test` runs and only execute under `dotnet test --filter Category=Smtp`.

Example test names:

```text
SeedDataSmokeTests.PortfolioTradeDefinition_IsSeeded
SeedDataSmokeTests.PortfolioTradeTransitions_MatchMatrix
SeedDataSmokeTests.NotificationRoutes_AreSeeded
SeedDataSmokeTests.SlaPolicyKeys_ResolveByVersionScopedLookup
SeedDataSmokeTests.NotificationRouteKeys_ResolveByVersionScopedLookup
SeedDataSmokeTests.DefinitionKeys_AreUniquePerWorkflowVersion
WorkflowExecutionTests.SubmitForChecker_MovesDraftToCheckerReview
WorkflowExecutionTests.CheckerReturnToMaker_CreatesMakerReworkTask
WorkflowExecutionTests.TradingReturnToMaker_CreatesMakerReworkTask
WorkflowExecutionTests.ResubmitForChecker_ClosesMakerReworkTaskAndMovesToCheckerReview
WorkflowExecutionTests.MakerReworkTask_HasNullDueAtUtc
WorkflowExecutionTests.MakerReworkTask_NeverSelectedByRunSla
WorkflowExecutionTests.SaveDraft_DoesNotIncrementVersion
EnvelopeValidationTests.MissingActorBlock_ReturnsValidationFailed
EnvelopeValidationTests.EmptyActorRoles_ReturnsValidationFailed
EnvelopeValidationTests.MissingDataSave_ReturnsValidationFailed
EnvelopeValidationTests.EmptyPayload_IsPermitted
EnvelopeValidationTests.MakerPrimary_SaveDraftRequiresSaveAsDraftTrue
AuthorizationTests.NonexistentAction_ReturnsInvalidTransition
AuthorizationTests.WrongRole_ReturnsForbidden
AuthorizationTests.MultipleMatchingRoles_ReturnsAmbiguousRoleMatch
AuthorizationTests.AdminOverride_BypassesForbidden
AuthorizationTests.AdminOverride_DoesNotBypassValidationFailed
IdempotencyTests.SameKey_DifferentRequest_ReturnsConflict
IdempotencyTests.SameKey_SameRequest_ReturnsStoredResultJsonWithoutReexecuting
IdempotencyTests.OverrideMode_DerivesKeyPerWorkflowInstance
ConcurrencyTests.StaleExpectedState_ReturnsConflict
ConcurrencyTests.StaleExpectedInstanceVersion_ReturnsConflict
StartWorkflowTests.CreatesInstanceWithVersionOne
StartWorkflowTests.ExistingInstanceWithoutReset_ReturnsAlreadyExists
StartWorkflowTests.ResetClearsRuntimeDataInOrder
StartWorkflowTests.ResetClearsIdempotencyByWorkflowInstanceIdColumn
MockSectionDataTests.IdempotentReplay_DoesNotInsertDuplicateSectionDataRow
AuditTests.FailedActions_WriteNoAuditRow
SlaMonitorTests.OverdueTask_QueuesNotificationOnce
SlaMonitorTests.QueuedSlaNeverDuplicatedRegardlessOfOutboxStatus
NotificationOutboxTests.ProcessOutbox_MarksPendingAsSent
NotificationOutboxTests.ProcessOutbox_RunTwice_IsSafe
ExecuteJsonCommandTests.CheckerReturnToMakerSample_PassesExpectations
ExecuteJsonCommandTests.TradingReturnToMakerSample_PassesExpectations
WorkflowContextTests.CheckerReview_ForChecker_ReturnsReturnToMakerAndSendToTradingDesk
WorkflowContextTests.TradingDeskReview_ForTradingDesk_ReturnsReturnToMakerAndTradeExecuted
WorkflowContextTests.WrongRole_ReturnsNoAvailableActions
ShowContextCommandTests.CheckerReview_PrintsWorkflowContextJson
WorkflowActionResultTests.SuccessfulTransition_ReturnsStateTaskNotificationAuditSummary
WorkflowTaskTests.ClosedTask_HasCompletedStatusAndAllCloseColumns
WorkflowTaskTests.CreatedTask_HasPendingStatusAndNullAssignedUserId
WorkflowInstanceTests.StartWorkflow_SetsStatusActive
WorkflowInstanceTests.BdrmTradeFulfilled_SetsStatusCompleted
AuditTests.SaveDraft_ToStateKeyEqualsFromStateKey
WorkflowContextTests.Draft_SlaBlock_IsNoSla
WorkflowContextTests.ReturnedToMaker_SlaBlock_IsNoSla
WorkflowContextTests.CheckerReview_SlaBlock_IsOnTrack_WhenDueInFuture
WorkflowContextTests.CheckerReview_SlaBlock_IsOverdue_WhenDueInPast_BeforeRunSla
WorkflowContextTests.SlaBlock_IsNeverNull
DemoCommandTests.Demo_RunsHappyPathSuccessfully
DemoCommandTests.Demo_IsSafeToRunTwice
```

## 21. Quality gates

Before claiming completion, run the relevant subset of:

```bash
dotnet restore
dotnet build -warnaserror
dotnet test
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed

# Checker Return to Maker branch; this must run on a valid CheckerReview path
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json
dotnet run --project src/WorkflowEngine.Console -- show-context WF-PT-1001 --user checker1 --roles Checker --format json
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/03-checker-return-to-maker.json

# Trading Desk Return to Maker branch; this must run on a valid TradingDeskReview path
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003

# Aggregate demo (happy path end-to-end, uses reserved PT-DEMO business object)
dotnet run --project src/WorkflowEngine.Console -- demo
```

Recommended project settings:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisMode>Recommended</AnalysisMode>
</PropertyGroup>
```

If a command cannot be run, state exactly why in the completion summary.

## 22. Stop-and-ask approval triggers

Stop before doing any of the following:

- Changing the `WorkflowActionEnvelope` shape
- Changing the `WorkflowContextResponse` shape
- Changing the `WorkflowActionResult` shape
- Changing the Portfolio Trade transition matrix
- Removing Checker Return to Maker
- Removing Trading Desk Return to Maker
- Adding a new external package
- Changing the SQL schema in a non-trivial way
- Introducing CQRS, MediatR, event sourcing, BPMN, saga frameworks, or message brokers
- Removing SQL-backed seed verification
- Removing sample JSON files or required console commands
- Removing the `show-context` command or form-open workflow context behavior
- Changing from single-active-state workflow to parallel workflow
- Making the workflow engine depend on Portfolio Trade fields
- Allowing request JSON to specify next state / `toState` as authority
- Allowing request `workflow.options` to override metadata-required side effects
- Adding `AdminDemoOverride` as an `AllowedRoleKey` in any transition row
- Adding `enabledByWorkflow`, `disabledReason`, or any conditional-action field to `AvailableWorkflowAction` without first adding the underlying conditional metadata model
- Adding a `DemoUser` table or any user-to-role mapping in SQL
- Adding failure audit (auditing failed actions) to v1
- Calling `DateTime.UtcNow` outside the composition root instead of `TimeProvider`
- Using `SERIALIZABLE` or `SNAPSHOT` isolation level in v1
- Using `PT-DEMO` as a business object identifier in any test fixture, demo script, or sample file other than the `demo` command
- Adding a synthetic `WorkflowStarted` audit row, free-form `WorkflowAudit.ActionKey` values, or making `WorkflowAudit.FromStateKey` nullable to accommodate workflow creation
- Adding a user-to-name lookup table or `--display-name` console flag to populate `actorAccess.displayName`
- Allowing multiple sequential workflow instances on the same `(WorkflowKey, BusinessObjectType, BusinessObjectId)` (re-trade scenarios). v1 enforces one workflow lifetime per business object identifier. Schema changes (filtered unique on `Status = 'Active'`, sequence-aware ID generation) are required and out of scope.
- Adding `Smtp:Username`, `Smtp:Password`, or any SMTP credential support. v1 assumes IP-restricted on-prem relay with no credentials. Authenticated relay requires secret management.
- Adding any third-party NuGet package other than MailKit (which is pre-approved for `SmtpNotificationSender` in §14).
- Adding a SQL-backed role-to-email lookup table. Role-to-email resolution is configuration-only (`Smtp:RoleEmailMap`) in v1.

## 23. Completion summary format for coding agents

After a meaningful change, summarize:

```text
Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:
```

If something was not run, state exactly why.

## 24. Implementation milestones

Each milestone is a stop-the-world checkpoint where the listed quality gates must pass before the next milestone starts.

### Milestone 1: Solution skeleton

Solution and project structure exist. Console command routing dispatches a stub for every command in section 10.1, including `show-context`. Configuration and EF Core wiring compile. `TimeProvider` is registered. `dotnet build -warnaserror` passes.

### Milestone 2: SQL schema and seed data

All entities in section 12 exist with required unique constraints. `init-db` creates the schema and seeds the Portfolio Trade definition. `verify-seed` passes against SQL Server. The transition matrix in section 7 is exactly reflected in seeded rows, including `ResubmitForChecker`, Checker Return to Maker, and Trading Desk Return to Maker. The notification route seed table from section 11 is fully present.

### Milestone 3: WorkflowActionEnvelope execution path

`WorkflowActionEnvelope` DTOs and validation exist. `WorkflowContextResponse`, `WorkflowActionResult`, and `AvailableWorkflowAction` DTOs exist. `execute-json` parses a sample file, saves mock section data, and prints the demo output shape from section 10.5. `show-context` parses user/role arguments and prints the section 8.1 response shape.

### Milestone 4: Workflow engine

Transition resolver, role/action/state validation, task close/create, audit writing, notification outbox creation, idempotency, concurrency guards, metadata-driven available action resolution, and refreshed workflow context generation are implemented. `02-submit-for-checker.json`, `03-checker-return-to-maker.json`, and `06-trading-return-to-maker.json` produce the expected state, task, audit, and notification results.

### Milestone 5: SLA monitor and notification senders

`run-sla` and `process-notification-outbox` work. SLA durations are short enough to demo overdue behavior. `SlaTaskOverdue` is queued at most once per task. `--now` override on `run-sla` works correctly via injected `TimeProvider`. The `INotificationSender` abstraction is in place with `ConsoleNotificationSender` (default), `NoOpNotificationSender`, and `SmtpNotificationSender` registered by configuration. Tier 1 unit tests against a recording test double pass. Tier 2 SMTP integration tests against a local fake SMTP server (`smtp4dev` or `Papercut`) pass when run with `--filter Category=Smtp`. Default `dotnet test` does not invoke Tier 2 tests.

### Milestone 6: Samples, tests, and demo

All sample JSON files in section 19 exist and pass their `testExpectations`. All workflow context tests and action-result tests pass. All tests in section 20 pass. Demo scripts in section 25 run end-to-end. The `demo` command runs the §25.1 happy path automatically using `PT-DEMO` and prints `Demo completed successfully`. README is written per section 26.

## 25. Demo scripts

Because return paths change the current state, use separate business objects or reset/restart the workflow between branches.

### 25.1 Main happy path with BDRM-to-Trading rework

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

Expected narrative:

- Maker can save draft without moving workflow.
- Maker can submit to Checker.
- Checker task is created and SLA starts.
- SLA monitor can create overdue notifications.
- Checker can send to Trading Desk.
- Trading Desk can mark trade executed.
- BDRM can reject execution and route back to Trading Desk Rework.
- Trading Desk can execute again.
- BDRM can fulfill trade and complete workflow.
- Audit history shows the entire back-and-forth flow.

### 25.2 Checker Return to Maker branch

```bash
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1002
```

Expected narrative:

- Checker returns the request to Maker.
- `CheckerReviewTask` is closed.
- `ReturnedToMaker` becomes the current state.
- `MakerReworkTask` is created with `DueAtUtc = null`.
- `WorkflowReturnedToMaker` notification is queued via `RouteReturnedToMakerFromChecker`.

### 25.3 Trading Desk Return to Maker branch

```bash
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1003
```

Expected narrative:

- Trading Desk returns the request to Maker.
- `TradingDeskReviewTask` is closed.
- `ReturnedToMaker` becomes the current state.
- `MakerReworkTask` is created with `DueAtUtc = null`.
- `WorkflowReturnedToMaker` notification is queued via `RouteReturnedToMakerFromTradingDesk`.

## 26. README guidance

The README should explain:

- This is a reusable workflow engine demo.
- It replaces SharePoint Designer workflow behavior, not the form UI.
- Portfolio Trade is the first sample workflow definition.
- Checker and Trading Desk can both return the workflow to Maker.
- The console can return `WorkflowContextResponse` using `show-context`, matching the workflow object the future API gives the UI when the form opens.
- The console accepts a standard JSON action envelope.
- The JSON contains mocked section data and a workflow action command.
- The application service saves data and executes workflow in one transaction.
- The workflow engine manages state, tasks, SLA, notifications, and audit.
- The UI should combine user identity, form metadata, form data, and workflow context on form open.
- The UI should use workflow context `requestDefaults` when building `WorkflowActionEnvelope`.
- SLA notifications are task-driven and created by a monitor command, not by form buttons.
- Notification delivery is abstracted via `INotificationSender`. Default is `ConsoleNotificationSender` (no real email). `SmtpNotificationSender` is fully implemented for on-prem SMTP relay delivery and enabled by configuration only — flip `Notifications:Sender` from `"Console"` to `"Smtp"` and provide `Smtp:Host`, `Smtp:From`, and `Smtp:RoleEmailMap`. No code change required.

## 27. Final success definition

This project succeeds if the team can run the console app, pass a realistic JSON action object, and see that the reusable workflow engine can replace SharePoint Designer workflow behavior for the Portfolio Trade process without being hardcoded to that single process.

The success demo must prove all of the following:

- Maker can save draft.
- Maker can submit to Checker.
- Checker can return to Maker.
- Maker can resubmit to Checker.
- Checker can send to Trading Desk.
- Trading Desk can return to Maker.
- Trading Desk can mark trade executed.
- BDRM can mark trade fulfilled.
- BDRM can mark trade not executed properly and route back to Trading Desk Rework.
- SLA notifications are task-driven.
- Audit history shows the full back-and-forth workflow.
- When the form opens, `show-context` can return workflow context showing current state, current task, SLA status, and available actions for the simulated actor.
- After an action is processed, the application layer can model a `WorkflowActionResult` that summarizes the transition and returns refreshed workflow context.

## 28. Pitfall review and gap-closure checklist

This section captures known implementation pitfalls. Coding agents must use this as a guardrail before claiming the requirements are complete.

| Pitfall / gap | Required guardrail |
|---|---|
| UI only has user identity, form metadata, and form data on open. | Add `WorkflowContextResponse` and `show-context`; the UI needs workflow state, task, SLA, and available actions too. |
| Confusing form-open response with button-click request. | `WorkflowContextResponse` is returned on open. `WorkflowActionEnvelope` is sent on button click. `WorkflowActionResult` is returned after button click. |
| UI computes next state. | UI sends `actionKey` only. SQL transition metadata decides next state. |
| Button labels drive workflow. | Labels are display only. `actionKey`, current state, and actor role drive workflow. |
| Workflow options override metadata. | `workflow.options` are advisory/test hints only. Metadata is authoritative for tasks, SLA, notification, and audit behavior. |
| SaveDraft accidentally moves workflow or creates tasks. | `SaveDraft` is seeded only for `Draft` and `ReturnedToMaker`, saves data, writes lightweight audit, does not move state, does not increment version, and does not touch tasks/SLA/notifications. |
| Return-to-Maker paths are missed. | `CheckerReturnToMaker` and `TradingReturnToMaker` are mandatory seed rows, samples, tests, demo branches, and success criteria. |
| Maker rework task duplication. | Create one `MakerReworkTask` per return event. `SaveDraft` from `ReturnedToMaker` must not create another task. Single pending task constraint enforces this. |
| Static sample JSON has runtime task IDs. | Use `expectedCurrentTaskKey` in samples; real API may use `expectedCurrentTaskId`. If multiple guards are provided, all must match. |
| Stale forms submit after another actor already moved workflow. | UI echoes `workflowInstanceVersion`; backend checks `expectedInstanceVersion` before saving data or moving workflow. |
| Idempotent replay cannot return the original result. | Store `IdempotencyRecord.ResultJson` and return it on matching replay. |
| Different requests accidentally reuse the same idempotency key. | Compute `RequestHash` from the normalized effective request, excluding identity/correlation timestamps but including action, actor, guards, business object, and payload. |
| Maker section renders too many buttons. | Group Maker actions by `buttonKey = MakerPrimary`; checkbox chooses `SaveDraft` vs submit/resubmit action. |
| Demo reruns behave unpredictably. | `start-workflow` is deterministic, returns `AlreadyExists` by default, and supports console-only `--reset` for runtime data. |
| Mock form data overwrites useful demo history. | `MockSectionData` is append-only; replays/failures insert no duplicate rows. |
| Duplicate workflow definition keys cause ambiguous metadata. | SQL unique constraints enforce definition-key uniqueness per workflow definition version. |
| Workflow definition version comparison is wrong. | JSON carries version number. Persisted instance carries `WorkflowDefinitionVersionId`; join to compare against `WorkflowDefinitionVersion.VersionNumber`. |
| Idempotency conflicts when reusing samples with overrides. | `execute-json --business-object` / `--workflow-instance` must derive effective clientActionId, idempotencyKey, and correlationId before dedupe. |
| SLA duplicates. | `NotificationOutbox` includes `WorkflowTaskId`; `SlaTaskOverdue` is queued at most once per task ever. |
| Parallel workflow sneaks into v1. | MVP is single-active-state only. No fan-out/fan-in or multiple pending tasks. |
| Portfolio Trade is hardcoded into the engine. | Portfolio Trade belongs in seed metadata and sample JSON only. Engine remains generic. |
| Agents over-engineer the POC. | Follow the simplicity rule and stop-and-ask triggers. Do not add CQRS, MediatR, event sourcing, BPMN, brokers, plugin systems, or microservices without approval. |
| Engine validates payload field contents. | `dataSave.payload` is opaque JSON. Engine writes to `MockSectionData.PayloadJson` and never parses field names. Field validation belongs to the form layer. |
| Coding agent adds failure audit unprompted. | v1 writes audit only for `Success` results. Failed actions roll back the entire transaction including any audit attempt. Failure audit is a deliberate v2 feature. |
| Coding agent adds a `DemoUser` table to "complete" the schema. | v1 has no user-to-role mapping in SQL. `userId` is an opaque string recorded as evidence. Authorization uses the `roles` array from the envelope (console) or auth claims (future API). |
| Coding agent treats section 10 command inventory as a runnable script. | Section 10.1 is a capability list. Concrete runnable sequences are in section 25. Integration tests follow section 25 branch scripts, not section 10 line order. |
| Coding agent adds conditional-action fields. | `enabledByWorkflow` and `disabledReason` are not in v1. Adding them requires the underlying conditional metadata model and is a stop-and-ask trigger. |
| Coding agent calls `DateTime.UtcNow` directly. | Inject `TimeProvider` everywhere a timestamp is needed. The composition root is the only place `TimeProvider.System` is referenced. |
| Coding agent uses `SERIALIZABLE` for "safety". | Use `READ COMMITTED`. Optimistic concurrency on `WorkflowInstance.Version` and the filtered unique index on pending tasks provide the correctness guarantees. |
| Coding agent invents a default `DueAtUtc` for `MakerReworkTask`. | Null `SlaPolicyKey` produces null `DueAtUtc`. The engine never invents deadlines and never inherits from the closing task. |
| Coding agent uses wrong `WorkflowTask.Status` string in filtered index or queries. | Allowed values are `Pending` and `Completed` only. The filtered unique index literal must be `WHERE Status = 'Pending'`. `run-sla` must filter `WHERE Status = 'Pending' AND DueAtUtc < now`. |
| Coding agent uses wrong `WorkflowInstance.Status` string. | Allowed values are `Active` and `Completed` only. Set `Active` on `start-workflow`, `Completed` on terminal transition. |
| Coding agent populates `WorkflowTask.AssignedUserId` from `actor.userId`. | `AssignedUserId` is always null in v1. Tasks are role-assigned only. The actor who triggers a transition is not the assignee of the created task. |
| Coding agent omits `CompletedAtUtc`, `CompletedByUserId`, or `ClosedByActionKey` when closing a task. | All four columns must be set on task close: `Status = 'Completed'`, `CompletedAtUtc = TimeProvider.GetUtcNow()`, `CompletedByUserId = actor.userId`, `ClosedByActionKey = actionKey`. |
| Coding agent reads `ToStateKey` in `WorkflowAudit` to infer forward movement and includes `SaveDraft` rows. | For `SaveDraft`, `ToStateKey = FromStateKey`. Exclude `SaveDraft` rows when querying audit for state progression. |
| Coding agent treats `idempotencyKey` absent and `idempotencyKey` present as the same lookup. | They use different `IdempotencyRecord.IdempotencyKey` values (`clientActionId` vs `idempotencyKey`). Same `RequestHash` but different lookup keys — independent actions for idempotency purposes. |
| Coding agent returns `"sla": null` or omits `sla` block for no-SLA states. | Always return `{ "status": "NoSla", "dueAtUtc": null, "overdue": false, "overdueByMinutes": 0 }`. The three `sla.status` values are `OnTrack`, `Overdue`, `NoSla`. |
| Coding agent gates `show-context` SLA overdue on `NotificationOutbox` having a record. | `show-context` computes overdue by comparing `DueAtUtc` to `TimeProvider.GetUtcNow()` directly. SLA monitor fires notifications independently. |
| Coding agent records `businessObject.objectVersion` in `WorkflowAudit`. | `WorkflowAudit` has no `BusinessObjectVersion` column. The field is silently ignored by the engine. |
| `demo` command uses its own sample files or hardcodes Portfolio Trade steps. | `demo` reuses the §19 sample files with `--business-object PT-DEMO --workflow-instance WF-PT-DEMO` overrides. No demo-specific samples needed. |
| `start-workflow --user` is treated as informational only and not persisted. | `--user` is required and persisted to `WorkflowInstance.StartedByUserId`. It records who started the workflow and is shown by `show <workflowInstanceId>`. |
| Coding agent adds a synthetic `WorkflowStarted` audit row for `start-workflow`. | Workflow instance creation is not an audited transition. Starting user/time are captured on `WorkflowInstance` directly. Adding a synthetic audit row requires schema and seed changes and is a stop-and-ask change. |
| Console invents a user-name lookup table to populate `actorAccess.displayName`. | `displayName` defaults to the `--user` value in console output. The future API resolves it from authenticated profile data. No SQL lookup table in v1. |
| Coding agent treats `workflow.options.X = true` as a request to add a side effect that metadata says not to do. | Options never add side effects. Options never suppress metadata-required side effects. Any disagreement produces a warning; metadata wins both directions. |
| Sample files are edited per-demo to change business object IDs. | All samples bake `PT-1001` / `WF-PT-1001`. Different business objects use `--business-object` / `--workflow-instance` overrides, never file edits. |
| `[ADMIN OVERRIDE]` prefix produces trailing whitespace when `actionReason` is null. | If `actionReason` is null or empty, audit `Reason = "[ADMIN OVERRIDE]"` exactly (no trailing space). If non-empty, `Reason = "[ADMIN OVERRIDE] " + actionReason`. |
| Coding agent allows a second `start-workflow` on a `Completed` instance to create a new active workflow. | One business object identifier gets one workflow lifetime ever in v1. `AlreadyExists` is returned regardless of `Status`. New trade lifetimes require a new `BusinessObjectId` from the calling system. |
| Coding agent suggests `--reset` as a way to "restart" a workflow in production. | `--reset` is demo-only. It deletes audit history, notifications, tasks, and section data. Production callers must never use it to reuse a business object identifier — assign a new ID instead. |
| `AlreadyExists` result lacks status information. | The result must include `existingInstanceStatus` (`Active` or `Completed`) and `existingInstanceVersion` so the caller can distinguish "in progress" from "already finished." |
| Coding agent hardcodes SMTP delivery into `process-notification-outbox`. | Use the `INotificationSender` interface and configuration-driven registration. Hardcoded SMTP defeats the testability and dev-vs-prod separation. |
| Coding agent uses `System.Net.Mail.SmtpClient` for the SMTP implementation. | Use MailKit. `System.Net.Mail.SmtpClient` is deprecated by Microsoft for new development. MailKit is pre-approved for this project. |
| Coding agent looks up role recipients from a SQL table. | v1 has no user-to-role mapping in SQL (see §11). `SmtpNotificationSender` resolves per-record email from persisted notification payload and falls back to `Smtp:RoleEmailMap`. |
| `SmtpNotificationSender` silently drops a notification when recipient resolution fails. | Throw. The row stays `Pending` and the operator must fix configuration or request data. Silent drops cause undetectable notification loss. |
| Coding agent enables real SMTP delivery in dev/test default config. | Default `Notifications:Sender` is `"Console"` everywhere except production. Tier 2 SMTP integration tests opt in explicitly via `Category=Smtp` filter and target a local fake server. |
| Tier 2 SMTP tests run by default and slow down CI or hit a real relay. | Tier 2 tests must be marked with `Category=Smtp` and excluded from default `dotnet test`. They run only when explicitly requested. |
| Coding agent adds SMTP credential support to enable an authenticated relay. | v1 assumes IP-restricted on-prem relay with no credentials. Adding `Smtp:Username`/`Smtp:Password` requires secret management and is a stop-and-ask change. |
