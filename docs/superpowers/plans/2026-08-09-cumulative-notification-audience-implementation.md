# Cumulative Notification Audience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every Portfolio Trade task assignment, SLA reminder, return, rework, and completion notification include the immutable original Maker plus the caller-supplied cumulative audience, while keeping the current task owner in To.

**Architecture:** Keep the generic recipient resolver and public DTOs unchanged. Express the behavior entirely through versioned `NotificationRecipientRule` seed metadata: static task owners or the original Maker populate To, while `originalMaker` and the caller-owned `selectedCcUsers` snapshot populate Cc. Persist the resolved task snapshot once and continue reusing it unchanged for SLA notifications.

**Tech Stack:** .NET 10, C#, xUnit, EF Core, SQL Server, ASP.NET Core Workflow API, JSON scenario packs.

---

## File map

- Modify `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`: add the approved `originalMaker` and `selectedCcUsers` rules to every notification-producing Portfolio Trade route.
- Modify `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`: assert every route's ordered recipient contract, required flags, and total rule count.
- Modify `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`: prove assignment, return, completion, and SLA snapshots resolve the cumulative audience correctly, including ordering and To precedence.
- Modify `PROJECT.md`: update the authoritative notification route table and acceptance language without changing milestones or architecture.
- Modify `workflow-api/WORKFLOW_API_BOUNDARIES.md`: document that `selectedCcUsers` is a complete caller-owned snapshot on each notification-producing action.
- Modify `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`: define and enforce the stage-appropriate cumulative user IDs for every notification-producing public scenario.
- Modify the `request.json` and `README.md` files in public scenarios `03`, `05`, `07`, `08`, `09`, `10`, `11`, `14`, `15`, `16`, `17`, and `18`: pass and explain the complete cumulative audience at that stage.
- Modify `CODEX_HANDOFF.md`: record implementation, verification, risks, and the next-session prompt while preserving unrelated existing handoff content.

## Task 1: Lock the recipient metadata contract with failing tests

**Files:**

- Modify: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`
- Test: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [ ] **Step 1: Replace partial recipient-rule assertions with exact route collections**

  In `PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded`, change the expected total from 13 to 22 and assert the ordered rows for all eight transition routes:

  ```text
  RouteCheckerAssignedOnSubmit:
    1 To StaticAddress checker@example.test required
    2 Cc WorkflowParticipant originalMaker required
    3 Cc RequestInput selectedCcUsers optional

  RouteCheckerAssignedOnResubmit:
    1 To StaticAddress checker@example.test required
    2 Cc WorkflowParticipant originalMaker required
    3 Cc RequestInput selectedCcUsers optional

  RouteReturnedToMakerFromChecker:
    1 To WorkflowParticipant originalMaker required
    2 Cc RequestInput selectedCcUsers optional
    3 Cc StaticAddress workflow-operations@example.test optional

  RouteTradingDeskAssigned:
    1 To StaticAddress trading-desk@example.test required
    2 Cc WorkflowParticipant originalMaker required
    3 Cc RequestInput selectedCcUsers optional

  RouteReturnedToMakerFromTradingDesk:
    1 To WorkflowParticipant originalMaker required
    2 Cc RequestInput selectedCcUsers optional

  RouteBdrmAssigned:
    1 To StaticAddress bdrm-client-service@example.test required
    2 Cc WorkflowParticipant originalMaker required
    3 Cc RequestInput selectedCcUsers optional

  RouteTradingDeskReworkAssigned:
    1 To StaticAddress trading-desk@example.test required
    2 Cc WorkflowParticipant originalMaker required
    3 Cc RequestInput selectedCcUsers optional

  RouteWorkflowCompleted:
    1 To WorkflowParticipant originalMaker required
    2 Cc RequestInput selectedCcUsers optional
  ```

  Add a small local assertion helper/expected-record type so every field is compared: route key, recipient kind, source type, source key/static address, sort order, and required flag.

- [ ] **Step 2: Run the focused seed test and confirm RED**

  Run:

  ```powershell
  dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded" -m:1
  ```

  Expected: FAIL because the current seed has 13 rules and lacks cumulative Maker/Cc rules on most routes.

- [ ] **Step 3: Commit the failing contract test**

  ```powershell
  git add tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
  git commit -m "test: define cumulative notification audience routes"
  ```

## Task 2: Implement the metadata-only routing change

**Files:**

- Modify: `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs`
- Modify: `PROJECT.md`
- Modify: `workflow-api/WORKFLOW_API_BOUNDARIES.md`
- Test: `tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs`

- [ ] **Step 1: Update the Portfolio Trade recipient rule seeds**

  Edit only `NotificationRecipientRules`. Add `originalMaker` and `selectedCcUsers` in the exact order locked by Task 1. Keep `selectedCcUsers` optional and keep `originalMaker` required wherever it is used. Do not change DTOs, entities, migrations, the resolver, the outbox lifecycle, or SLA logic.

- [ ] **Step 2: Update the source-of-truth notification table**

  In `PROJECT.md`, update the eight transition route rows so they describe the same To/Cc rules as the seed. Explicitly state that `selectedCcUsers` is the complete current cumulative snapshot supplied by the caller, not a delta, and that the engine does not resolve email addresses or accumulate prior request inputs.

- [ ] **Step 3: Update the public API boundary documentation**

  In `workflow-api/WORKFLOW_API_BOUNDARIES.md`, document these invariants:

  - `originalMaker` is supplied once at start and persisted as an immutable participant.
  - Each later notification-producing action supplies the complete current cumulative audience under `notification.recipientInputs.selectedCcUsers`.
  - The acting Checker, Trader, or BDRM user belongs in that array when the caller wants that person copied on later notifications.
  - The caller passes ready-to-send email addresses; the engine performs no directory lookup.
  - Current task owner remains To; Maker/cumulative audience are Cc, except Maker is To on return/completion.
  - Existing task/outbox snapshots remain immutable and SLA uses the task snapshot unchanged.

- [ ] **Step 4: Run the focused seed test and confirm GREEN**

  Run:

  ```powershell
  dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~SeedDataSmokeTests.PortfolioTradeBusinessObjectParticipantAndRecipientRulesAreSeeded" -m:1
  ```

  Expected: PASS with exactly 22 ordered recipient rules.

- [ ] **Step 5: Commit metadata and contract documentation**

  ```powershell
  git add src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md
  git commit -m "feat: route cumulative notification audience"
  ```

## Task 3: Prove runtime snapshots and SLA reuse

**Files:**

- Modify: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`
- Test: `tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs`

- [ ] **Step 1: Add a Checker assignment snapshot test**

  Add `SubmitForCheckerResolvesOwnerMakerAndCumulativeAudience`. Seed a Draft instance with immutable `originalMaker`, execute `SubmitForChecker` with `selectedCcUsers` containing the Maker, Checker, manager, and a case-variant duplicate. Assert:

  - To is exactly `checker@example.test`.
  - Cc preserves route/input order and first opaque spelling.
  - The Checker and Maker values duplicated from To/participant do not appear twice.
  - Task and assignment outbox `NotificationRecipientsJson` are byte-for-byte equal.

- [ ] **Step 2: Add a Trading assignment snapshot test**

  Add `CheckerSendToTradingDeskResolvesOwnerMakerAndCumulativeAudience`. Seed a CheckerReview instance/current task and immutable Maker, execute `CheckerSendToTradingDesk` with manager and acting Checker in `selectedCcUsers`, and assert:

  ```text
  To: trading-desk@example.test
  Cc: maker@example.test, manager@example.test, checker@example.test
  ```

  Assert the created task and assignment outbox reuse the identical serialized snapshot.

- [ ] **Step 3: Strengthen the BDRM assignment and overdue test**

  Extend `TradingTradeExecutedResolvesBdrmAudienceAndSlaReusesSnapshot` so `selectedCcUsers` includes manager, Checker, and acting Trader. Assert BDRM is the sole To recipient, Cc is Maker then the three caller values, and the assignment and overdue outbox rows both equal the task snapshot exactly.

- [ ] **Step 4: Cover return, rework, and completion shapes**

  Add focused tests (a theory with per-route setup is acceptable) for:

  - `TradingReturnToMaker`: Maker To; manager, Checker, Trader Cc.
  - `BdrmTradeNotExecutedProperly`: Trading Desk To; Maker, manager, Checker, Trader, BDRM Cc.
  - `BdrmTradeFulfilled`: Maker To; manager, Checker, Trader, BDRM Cc.

  For each, assert the persisted outbox snapshot. For task-producing routes, also assert task/outbox equality.

- [ ] **Step 5: Run focused runtime tests**

  Run:

  ```powershell
  dotnet test src/WorkflowEngine.sln --filter "FullyQualifiedName~WorkflowExecutionValidationTests" -m:1
  ```

  Expected: PASS. No engine implementation change should be required because the generic resolver already implements ordered deduplication and To precedence.

- [ ] **Step 6: Commit runtime coverage**

  ```powershell
  git add tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
  git commit -m "test: verify cumulative audience snapshots"
  ```

## Task 4: Make every public scenario pass a complete cumulative snapshot

**Files:**

- Modify: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`
- Modify: `workflow-api/samples/scenarios/03-start-submit-for-checker/request.json`
- Modify: `workflow-api/samples/scenarios/03-start-submit-for-checker/README.md`
- Modify: `workflow-api/samples/scenarios/05-draft-submit-for-checker/request.json`
- Modify: `workflow-api/samples/scenarios/05-draft-submit-for-checker/README.md`
- Modify: `workflow-api/samples/scenarios/07-returned-to-maker-resubmit-for-checker/request.json`
- Modify: `workflow-api/samples/scenarios/07-returned-to-maker-resubmit-for-checker/README.md`
- Modify: `workflow-api/samples/scenarios/08-checker-return-to-maker/request.json`
- Modify: `workflow-api/samples/scenarios/08-checker-return-to-maker/README.md`
- Modify: `workflow-api/samples/scenarios/09-checker-send-to-trading-taxable/request.json`
- Modify: `workflow-api/samples/scenarios/09-checker-send-to-trading-taxable/README.md`
- Modify: `workflow-api/samples/scenarios/10-checker-send-to-trading-tax-exempt/request.json`
- Modify: `workflow-api/samples/scenarios/10-checker-send-to-trading-tax-exempt/README.md`
- Modify: `workflow-api/samples/scenarios/11-checker-send-to-trading-equity/request.json`
- Modify: `workflow-api/samples/scenarios/11-checker-send-to-trading-equity/README.md`
- Modify: `workflow-api/samples/scenarios/14-trading-desk-return-to-maker/request.json`
- Modify: `workflow-api/samples/scenarios/14-trading-desk-return-to-maker/README.md`
- Modify: `workflow-api/samples/scenarios/15-trading-desk-trade-executed/request.json`
- Modify: `workflow-api/samples/scenarios/15-trading-desk-trade-executed/README.md`
- Modify: `workflow-api/samples/scenarios/16-bdrm-trade-fulfilled/request.json`
- Modify: `workflow-api/samples/scenarios/16-bdrm-trade-fulfilled/README.md`
- Modify: `workflow-api/samples/scenarios/17-bdrm-trade-not-executed-properly/request.json`
- Modify: `workflow-api/samples/scenarios/17-bdrm-trade-not-executed-properly/README.md`
- Modify: `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/request.json`
- Modify: `workflow-api/samples/scenarios/18-trading-desk-rework-trade-executed/README.md`
- Test: `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs`

- [ ] **Step 1: Generalize the scenario audience test and confirm RED**

  Extend `ScenarioExpectation` with expected cumulative user IDs and replace the two-scenario `TradingExecutionScenariosSupplySelectedCcUsers` test with an assertion covering every notification-producing pack. Use these exact stage snapshots:

  ```text
  03, 05: manager.scenario
  07, 08, 09, 10, 11: manager.scenario, checker.scenario
  14, 15: manager.scenario, checker.scenario, trader.scenario
  16, 17, 18: manager.scenario, checker.scenario, trader.scenario, bdrm.scenario
  ```

  Also assert every entry has nonblank `userId`, `emailAddress`, and `displayName`, with exact ordering.

  Run:

  ```powershell
  dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --filter "FullyQualifiedName~WorkflowScenarioSampleTests" -m:1
  ```

  Expected: FAIL because most packs omit `selectedCcUsers` or contain only part of the cumulative audience.

- [ ] **Step 2: Update the scenario JSON bodies**

  Add the full `notification.recipientInputs.selectedCcUsers` array to each listed request. Keep `originalMaker` only in start scenario 03. Use these stable identities wherever required by the stage list:

  ```json
  { "userId": "manager.scenario", "emailAddress": "manager.scenario@example.test", "displayName": "Scenario Manager" }
  { "userId": "checker.scenario", "emailAddress": "checker.scenario@example.test", "displayName": "Scenario Checker" }
  { "userId": "trader.scenario", "emailAddress": "trader.scenario@example.test", "displayName": "Scenario Trader" }
  { "userId": "bdrm.scenario", "emailAddress": "bdrm.scenario@example.test", "displayName": "Scenario BDRM Reviewer" }
  ```

- [ ] **Step 3: Update scenario README guidance**

  For each listed pack, state that `selectedCcUsers` is the complete current cumulative audience, not newly added users only. Explain the expected To/Cc outcome for that transition and that subsequent SLA reminders reuse the assignment task's snapshot.

- [ ] **Step 4: Run scenario tests and confirm GREEN**

  Run:

  ```powershell
  dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --filter "FullyQualifiedName~WorkflowScenarioSampleTests" -m:1
  ```

  Expected: PASS for all 18 public request packs and exact cumulative audience expectations.

- [ ] **Step 5: Commit scenario contract changes**

  ```powershell
  git add workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs workflow-api/samples/scenarios
  git commit -m "docs: pass cumulative audience in API scenarios"
  ```

## Task 5: Run quality gates and record the handoff

**Files:**

- Modify: `CODEX_HANDOFF.md`
- Verify: all changed source, tests, docs, and scenario packs

- [ ] **Step 1: Run sequential restore/build/test gates**

  Run the shared engine and API gates sequentially to avoid output-file locks:

  ```powershell
  dotnet restore src/WorkflowEngine.sln
  dotnet build src/WorkflowEngine.sln -warnaserror -m:1
  dotnet test src/WorkflowEngine.sln -m:1
  dotnet restore workflow-api/WorkflowEngine.WorkflowApi.slnx
  dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1
  dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx -m:1
  ```

  Expected: all commands pass. Record any pre-existing advisory separately; do not suppress warnings.

- [ ] **Step 2: Verify seed metadata against an isolated SQL database**

  Point `ConnectionStrings__DefaultConnection` at a new task-specific database, then run:

  ```powershell
  dotnet run --project src/WorkflowEngine.Console -- init-db
  dotnet run --project src/WorkflowEngine.Console -- verify-seed
  ```

  Expected: schema initializes and seed verification passes with the 22-rule route contract. Do not reuse or rewrite the prior live happy-path database.

- [ ] **Step 3: Execute representative sample JSON**

  On that isolated database, execute the PROJECT section 21 Checker-return and Trading-return paths plus the aggregate `demo`. Confirm task/outbox JSON shows owner in To and Maker plus the supplied cumulative audience in Cc. Run an overdue SLA scan and confirm the reminder JSON exactly matches the task snapshot.

- [ ] **Step 4: Run recipient-boundary negative searches**

  Search active source/API/samples/docs/tests for forbidden regressions called out by PROJECT section 21: singular runtime recipient fields, SMTP role-address maps, active `NotificationRoute.RecipientRoleKey`/`RecipientUserId`, caller `toState`, and delimited address strings. Historical migrations are excluded.

- [ ] **Step 5: Review the final diff and commit the handoff**

  Update `CODEX_HANDOFF.md` with current goal, completed changes, changed files, build/test status, seed verification, sample execution, remaining tasks, known risks, and next-session prompt. Preserve unrelated pre-existing edits and stage only this task's intended hunk.

  ```powershell
  git diff --check
  git status --short
  git diff -- src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs PROJECT.md workflow-api/WORKFLOW_API_BOUNDARIES.md workflow-api/tests/WorkflowEngine.WorkflowApi.Tests/Models/WorkflowScenarioSampleTests.cs workflow-api/samples/scenarios CODEX_HANDOFF.md
  git add CODEX_HANDOFF.md
  git commit -m "docs: record cumulative audience verification"
  ```

- [ ] **Step 6: Prepare the required completion summary**

  Report exactly:

  ```text
  Changed:
  Why:
  Tests run:
  Seed verification run:
  Sample JSON executed:
  Files updated:
  Open issues:
  ```

  Do not push until the user explicitly asks.
