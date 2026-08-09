# Workflow API Scenario Envelope Library Design

**Date:** 2026-08-09
**Status:** Approved

## Goal

Add a discoverable, executable sample library covering every distinct valid public Workflow Start and Workflow Action request behavior supported by the Portfolio Trade metadata, without changing the public API contract, database schema, workflow engine, or seeded behavior.

## Scope

The library covers meaningful request behaviors, not the unbounded Cartesian product of optional notification values. It includes:

- all valid Workflow Start variants;
- all seeded Portfolio Trade state/action transitions;
- all three valid dynamic Trading Desk assignment roles;
- SLA-only no-change synchronization and rescheduling;
- the same action from different valid source states when its prerequisites or effects differ.

Invalid/negative payloads are excluded from this user-facing library. They remain the responsibility of automated contract and controller tests.

`GET /v1/WorkflowContext` is documented in the library index but has no envelope pack because it has no JSON request body.

## Location and pack structure

Create `workflow-api/samples/scenarios/` with a root `README.md` and 18 numbered scenario subfolders. Each subfolder contains:

- `request.json`: the canonical public JSON body;
- `request.http`: an executable REST Client request with endpoint and trusted actor headers;
- `README.md`: prerequisite state, actor, expected state/task/SLA/notification effects, and sequence guidance.

Existing flat samples under `workflow-api/samples/` remain unchanged for compatibility with current tests and documentation.

## Scenario matrix

| # | Scenario | Endpoint | Prerequisite | Expected state |
|---|---|---|---|---|
| 01 | Start without an initial action | WorkflowStart | New business object | Draft |
| 02 | Start with SaveDraft | WorkflowStart | New business object | Draft |
| 03 | Start with SubmitForChecker | WorkflowStart | New business object | CheckerReview |
| 04 | Save an existing Draft | WorkflowAction | Draft | Draft |
| 05 | Submit an existing Draft | WorkflowAction | Draft | CheckerReview |
| 06 | Save a ReturnedToMaker draft | WorkflowAction | ReturnedToMaker | ReturnedToMaker |
| 07 | Resubmit after Maker rework | WorkflowAction | ReturnedToMaker | CheckerReview |
| 08 | Checker returns to Maker | WorkflowAction | CheckerReview | ReturnedToMaker |
| 09 | Checker sends to Taxable Trading Desk | WorkflowAction | CheckerReview | TradingDeskReview |
| 10 | Checker sends to Tax Exempt Trading Desk | WorkflowAction | CheckerReview | TradingDeskReview |
| 11 | Checker sends to Equity Trading Desk | WorkflowAction | CheckerReview | TradingDeskReview |
| 12 | Synchronize an unchanged Trading Desk SLA target | WorkflowAction | TradingDeskReview | TradingDeskReview |
| 13 | Reschedule a Trading Desk SLA target | WorkflowAction | TradingDeskReview | TradingDeskReview |
| 14 | Trading Desk returns to Maker | WorkflowAction | TradingDeskReview | ReturnedToMaker |
| 15 | Trading Desk executes the trade | WorkflowAction | TradingDeskReview | BdrmClientServiceReview |
| 16 | BDRM confirms fulfillment | WorkflowAction | BdrmClientServiceReview | Completed |
| 17 | BDRM returns the trade for Trading Desk rework | WorkflowAction | BdrmClientServiceReview | TradingDeskRework |
| 18 | Trading Desk executes a reworked trade | WorkflowAction | TradingDeskRework | BdrmClientServiceReview |

## Public contract rules

- Start JSON uses only `workflowKey`, `businessObjectId`, optional `actionKey`, optional `slaTargetDate`, and optional `notification`.
- Action JSON uses only `workflowInstanceId`, optional `actionKey`, optional `assignedRoleKey`, optional `slaTargetDate`, and optional `notification`.
- Notification JSON uses only typed `recipientInputs` and flat string `templateData`.
- Start requests capture the required immutable `originalMaker` participant.
- Routes with metadata-resolved To recipients do not ask the caller to repeat those addresses.
- `selectedCcUsers` appears only on routes whose metadata allows that request input.
- Trading Desk creation requests include `slaTargetDate`; the API maps it internally to the application-target-date SLA input.
- SLA-only requests omit `actionKey` and carry only `workflowInstanceId` plus `slaTargetDate`.
- Actor identity is represented only by `X-Workflow-UserId`, `X-Workflow-Roles`, and `X-Workflow-DisplayName` headers in `request.http`.
- Request JSON never specifies `toState`, server-derived due timestamps, workflow options, internal console fields, or caller-controlled idempotency/concurrency fields.

## Executability and prerequisites

Start packs are directly executable against a clean initialized API database after their sample `businessObjectId` is unique. Action packs use a `{{workflowInstanceId}}` REST Client variable because the API generates instance IDs and each action requires a persisted instance in the documented source state. The root index provides the valid pack sequences used to reach each prerequisite state.

## Automated verification

Add focused API tests that enumerate all 18 folders and prove:

- every pack contains the required files;
- each `request.json` strictly deserializes to the correct live public DTO;
- the scenario/action/role/SLA matrix is complete;
- forbidden internal and removed public fields do not appear;
- every `request.http` targets the correct endpoint and carries the required trusted actor headers.

The tests protect the scenario library from drifting away from the live API contract while leaving runtime behavior unchanged.
