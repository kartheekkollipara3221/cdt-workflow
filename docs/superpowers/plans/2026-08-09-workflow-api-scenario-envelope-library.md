# Workflow API Scenario Envelope Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create an executable, tested sample pack for every distinct valid public Workflow Start and Workflow Action scenario supported by Portfolio Trade metadata.

**Architecture:** Keep the live public DTOs and runtime code unchanged. Add a documentation-only scenario tree under the existing Workflow API samples folder and protect it with a strict-deserialization test whose explicit matrix mirrors the approved design and seeded transitions.

**Tech Stack:** .NET 10, xUnit, `System.Text.Json`, ASP.NET Core controller DTOs, REST Client `.http` files, Markdown.

---

## File structure

Create one root index and the following pack files. Every scenario directory owns one public body, one executable request, and one expected-effects description.

- Create: `workflow-api/samples/scenarios/README.md`
- Create for each numbered directory below: `request.json`, `request.http`, and `README.md`
  - `01-start-only`
  - `02-start-save-draft`
  - `03-start-submit-for-checker`
  - `04-draft-save-draft`
  - `05-draft-submit-for-checker`
  - `06-returned-to-maker-save-draft`
  - `07-returned-to-maker-resubmit-for-checker`
  - `08-checker-return-to-maker`
  - `09-checker-send-to-trading-taxable`
  - `10-checker-send-to-trading-tax-exempt`
  - `11-checker-send-to-trading-equity`
  - `12-trading-desk-sla-sync-no-change`
  - `13-trading-desk-sla-reschedule`
  - `14-trading-desk-return-to-maker`
  - `15-trading-desk-trade-executed`
  - `16-bdrm-trade-fulfilled`
  - `17-bdrm-trade-not-executed-properly`
  - `18-trading-desk-rework-trade-executed`
- Create: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`
- Modify: `CODEX_HANDOFF.md`

### Task 1: Add the scenario-library contract test

**Files:**

- Create: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`

- [ ] **Step 1: Write the failing test**

Create an explicit 18-row `ScenarioExpectation` matrix. The test must locate the repository root, assert that the root index and all three files exist per scenario, strictly deserialize Start packs as `WorkflowStartRequest` and Action packs as `WorkflowActionRequest`, and compare action key, assigned role, SLA target presence, endpoint, and actor role to the matrix.

The test must also parse each body with `JsonDocument` and recursively reject these non-public/internal property names:

```csharp
private static readonly HashSet<string> ForbiddenProperties = new(StringComparer.Ordinal)
{
    "businessObjectType",
    "initialActionKey",
    "idempotencyKey",
    "slaInput",
    "dueAtUtc",
    "notificationContext",
    "notificationRecipients",
    "toState",
    "workflow",
    "options"
};
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/WorkflowEngine.WorkflowApi.Tests.csproj -m:1 --filter FullyQualifiedName~WorkflowScenarioSampleTests
```

Expected: FAIL because `workflow-api/samples/scenarios/README.md` and the scenario directories do not exist yet.

### Task 2: Add the 18 executable scenario packs

**Files:**

- Create: `workflow-api/samples/scenarios/README.md`
- Create: all `request.json`, `request.http`, and `README.md` files listed in the File structure section.

- [ ] **Step 1: Create the root index**

Document the public Start/Action boundary, trusted headers, the generated-instance-ID prerequisite, the 18-scenario matrix, and these executable sequences:

```text
Happy path: 03 -> 09/10/11 -> 15 -> 16
Checker return: 03 -> 08 -> 06 or 07
Trading return: 03 -> 09/10/11 -> 14 -> 07
BDRM rework: 03 -> 09/10/11 -> 15 -> 17 -> 18 -> 16
SLA sync: 03 -> 09/10/11 -> 12 or 13
```

State explicitly that `GET /v1/WorkflowContext` uses query/header inputs and has no JSON envelope.

- [ ] **Step 2: Create Start bodies**

Use only the live Start fields. Every Start body captures one immutable `originalMaker`. Scenario 03 also demonstrates `selectedCcUsers` and the checker-assignment template tokens.

```json
{
  "workflowKey": "PortfolioTrade",
  "businessObjectId": "PT-API-SCENARIO-01",
  "notification": {
    "recipientInputs": {
      "originalMaker": [
        {
          "userId": "maker.scenario",
          "emailAddress": "maker.scenario@example.test",
          "displayName": "Scenario Maker"
        }
      ]
    }
  }
}
```

Scenario 02 adds `"actionKey": "SaveDraft"`. Scenario 03 uses `SubmitForChecker`, its own business object ID, optional selected Cc input, and the exact notification template data required by `RouteCheckerAssignedOnSubmit`.

- [ ] **Step 3: Create action bodies**

Every action body uses `"workflowInstanceId": "{{workflowInstanceId}}"`. Use the exact metadata action key and include only route-supported notification values. The Trading Desk assignment variants set `assignedRoleKey` to `TradingDeskTaxable`, `TradingDeskTaxExempt`, or `TradingDeskEquity` and include `slaTargetDate`. SLA-only scenarios omit `actionKey` entirely.

- [ ] **Step 4: Create executable HTTP requests**

Every file follows this public API pattern with the scenario's actor values:

```http
@baseUrl = http://localhost:5058
@workflowInstanceId = REPLACE_WITH_PERSISTED_INSTANCE_ID

POST {{baseUrl}}/v1/WorkflowAction
Content-Type: application/json
X-Workflow-UserId: checker.scenario
X-Workflow-Roles: Checker
X-Workflow-DisplayName: Scenario Checker

< ./request.json
```

Start packs target `/v1/WorkflowStart` and do not declare `workflowInstanceId`. Action actors are Maker, Checker, TradingDesk, or BDRMClientService according to the transition metadata. The three specialized Trading Desk task assignments still authorize the action with the `TradingDesk` actor role.

- [ ] **Step 5: Document prerequisites and effects**

Each pack README records the source state, required pending task when applicable, actor, expected destination state, task close/create behavior, SLA behavior, notification route and recipient source, plus the next valid packs.

- [ ] **Step 6: Run the focused test and verify GREEN**

Run:

```powershell
dotnet test workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/WorkflowEngine.WorkflowApi.Tests.csproj -m:1 --filter FullyQualifiedName~WorkflowScenarioSampleTests
```

Expected: PASS for all scenario matrix and strict DTO checks.

### Task 3: Verify and hand off

**Files:**

- Modify: `CODEX_HANDOFF.md`

- [ ] **Step 1: Run API verification**

Run sequentially to avoid MSBuild output locks:

```powershell
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 -warnaserror
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1 --no-build
```

Expected: build exit code 0 and all non-SMTP API tests pass.

- [ ] **Step 2: Run repository checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only this task's intended files plus the user's pre-existing local-only changes are present.

- [ ] **Step 3: Update the handoff**

Record the scenario-library goal, design/plan paths, created pack/test files, exact verification results, clean-database non-execution decision, known risks, and next-session prompt in `CODEX_HANDOFF.md` while preserving existing user content.

- [ ] **Step 4: Commit atomically**

Stage only the test, scenario library, plan/handoff artifacts, and GSD task artifacts. Do not stage `.tools/`, `SQMS/`, or `tareget-api-integration/`.
