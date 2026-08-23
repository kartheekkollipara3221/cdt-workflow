# SQMS Target API scenario catalog

This catalog is the primary scenario-driven handoff for integrating SQMS with the external target API and UI. It documents both the implemented in-process contract and a proposed HTTP surface. The recommended HTTP facade is not implemented in this repository because the external target API has not been supplied as a buildable project.

## Contract legend

- **Implemented** — confirmed behavior of the current in-process `WorkflowEngine.Sqms` code.
- **Recommended target facade** — a proposed HTTP route or public DTO owned by the external target API.
- **Target implementation required** — persistence, transaction, actor, recipient, controller, response, or error-mapping work that the external target team must supply.
- **Testing only** — isolated test-database setup or deliberate failure injection that must never run in production.

The **Implemented in-process contract** is `ISqmsWorkflowService`, its implemented SQMS request/result types, the scheduled services, and the generic workflow engine behavior they invoke. The **Recommended target facade** is an integration design only; every HTTP controller, public SQMS DTO, authentication adapter, repository, and outer transaction coordinator remains **Target implementation required**.

## How to use the HTTP packs

Each numbered request-driven pack will contain the proposed `request.http`, public `request.json`, recommended `expected-response.json`, and a read-only `verify.sql`. Start the external target API at its team-owned base URL, set the pack variables, and send the request with this authentication convention:

```http
Authorization: Bearer {{accessToken}}
```

The target API derives the actor identity and roles from the authenticated principal. These packs do not use `X-Workflow-*` actor headers and never accept browser-controlled actor or recipient authority. Treat every route and public DTO in an HTTP pack as **Recommended target facade** until the target implementation is source-confirmed.

Scenario prerequisites are explicit. Do not create hidden workflow state. SQL verifiers are read-only; any future `setup.testing.sql` is **Testing only**, belongs only in a scenario that requires controlled setup, and must target an isolated test database.

## How to use the in-process examples

The `in-process.cs` files will show the **Implemented** calls using real types and methods:

- `ISqmsWorkflowService`
- `StartOpportunityAsync` and `StartBidAsync`
- `GetOpportunityContextAsync` and `GetBidContextAsync`
- `ExecuteOpportunityActionAsync` and `ExecuteBidActionAsync`
- `SqmsActor`, `SqmsExecuteRequest`, and `SqmsOperationResult`

Target-specific actor factories, relationship/team recipient resolvers, repositories, error mappers, controllers, and transaction coordinators are integration placeholders and are not represented as existing classes. Request-driven examples use `TimeProvider`; they do not introduce direct `DateTime.UtcNow` calls.

Scheduled examples call the implemented scheduled service directly or use the operator/console trigger. There is no public scheduled UI route, and request controllers must not expose the internal scheduled-transition service.

## Target API transaction sequence

For every request-driven Start or Action, the target API owns one outer SQL transaction:

1. Begin the target-owned transaction.
2. Load the target row and validate or stage any target business changes without writing them yet.
3. Enlist `WorkflowDbContext` in the same open SQL connection and transaction.
4. Construct the trusted normalized `SqmsActor` from authenticated server-owned information.
5. Resolve only metadata-approved recipient source inputs on the server.
6. Call the implemented `ISqmsWorkflowService` Start or Action method.
7. Roll back on an unsuccessful result or exception. A duplicate Start returns `AlreadyExists`; it is not a replay.
8. Read the authoritative returned `NewState` or `CurrentState`, workflow version, and replay indicator.
9. For `replay = true`, perform no target write. Read and verify that the already committed target projection matches the authoritative replay result, then return the successful replay.
10. For `replay = false`, persist staged target business changes and the returned target `Status` and `WorkflowState` through the target repository.
11. Commit once after the applicable target and workflow work succeeds.

Request-driven SQMS adapter calls do not update `SSQMS.Opportunity` or `SSQMS.Bid` directly. The target API save is conditional on a successful non-replay result. A persistence failure after a non-replay workflow execution must roll the workflow transaction back with the target save. A replay verifies prior committed target state instead of issuing another update.

## Trusted actor and recipient boundary

Authentication and authorization are target API responsibilities. The target constructs the trusted actor from the bearer-authenticated principal and forwards only normalized identity and roles. Public JSON never supplies actor identity, role authority, workflow identity, idempotency identity, notification route/template selection, or final To/Cc recipients.

Relationship and team address resolution is also target-owned. The target may supply only server-resolved inputs allowed by notification metadata. Generic workflow metadata selects the transition and notification route, and the workflow runtime persists the immutable recipient snapshot used by the outbox and later delivery.

## Request versus scheduled persistence ownership

| Operation | Implemented behavior | Persistence owner |
|---|---|---|
| Request-driven Start | Starts workflow and returns authoritative state/version; does not update the SQMS target row | On non-replay success, target API persists returned target status/workflow state inside the outer transaction; duplicate Start returns `AlreadyExists` with no target write |
| Request-driven Action | Executes metadata transition and returns authoritative state/version; does not update the SQMS target row | On non-replay success, target API persists returned target status/workflow state; replay performs no target write and verifies the existing projection |
| Context | Reads persisted workflow and target information without side effects | Read-only; no target or workflow persistence |
| Scheduled Opportunity close or Bid classification | Executes scheduled transition and projects the result to the target row | SQMS scheduled adapter owns the four columns `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` |

Request-driven Start does not update the SQMS target row. Request-driven Action does not update the SQMS target row. After a non-replay success (`replay = false`), the target API persists the returned `Status` and `WorkflowState`. After a replay success (`replay = true`), the target API performs no target write; it reads and verifies the already committed projection.

Scheduled transitions are the only adapter-owned target write path. Scheduled projection updates exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`, and no other target columns. Scheduling uses its configured time zone and calendar-day `AddDays` behavior. The current implementation has no scheduler-wide mutual-exclusion lock, so operators must avoid overlapping scheduler invocations.

Once occurrence-owned notification outbox rows exist, a downgrade to a schema that cannot represent those occurrences is not data-preserving. Archive or otherwise resolve those rows before an intentional rollback; never silently delete and recreate the database.

## Recommended HTTP routes

All six routes are **Recommended target facade** and **Target implementation required**:

```text
POST /v1/sqms/opportunities/{id}/workflow/start
GET  /v1/sqms/opportunities/{id}/workflow
POST /v1/sqms/opportunities/{id}/workflow/actions

POST /v1/sqms/bids/{id}/workflow/start
GET  /v1/sqms/bids/{id}/workflow
POST /v1/sqms/bids/{id}/workflow/actions
```

Start takes the numeric entity ID from the route and an empty target-owned request DTO:

```json
{}
```

Action accepts exactly these public request fields:

```json
{
  "actionKey": "MarkOpportunityPending",
  "expectedWorkflowVersion": 2
}
```

The target may define `expectedWorkflowVersion` as optional for compatibility, but when supplied it is the optimistic-concurrency guard. The request never contains `toState`; actor/role fields; recipient, route, or template authority; workflow-instance or idempotency identity; or internal `WorkflowActionEnvelope` fields. Context is a GET with no body. The route ID and authenticated actor determine the lookup and role-filtered action view.

There is no browser-callable or public scheduled route. Scheduled processing is invoked through `ISqmsScheduledService` or its operator/console trigger.

## Recommended response

Start, Action, and Context return one target-owned UI projection with these stable workflow fields:

```json
{
  "entityId": 10000,
  "status": "PENDING",
  "workflowState": "PENDING",
  "workflowVersion": 3,
  "terminal": false,
  "replay": false,
  "availableActions": [
    {
      "actionKey": "ReturnOpportunityToDraft",
      "label": "Return Opportunity to Draft"
    },
    {
      "actionKey": "OpenOpportunityForBidding",
      "label": "Open Opportunity for Bidding"
    }
  ]
}
```

`entityId` is the target Opportunity or Bid identifier. `status` and `workflowState` are the authoritative normalized target projections. `workflowVersion` supports optimistic concurrency. `terminal` identifies a terminal workflow state. `replay` is true only when the engine returns a matching prior successful operation. `availableActions` is metadata-derived and role-filtered; the UI renders these actions and sends the selected `actionKey` without computing the next state.

The target may add existing business DTO fields, but it should not expose internal workflow-instance or idempotency identifiers to the browser.

## Recommended error mapping

The target should use its existing Problem Details convention. These mappings remain **Recommended target facade** until the real target source is available:

| SQMS/engine result | HTTP status | Meaning |
|---|---:|---|
| `TargetNotFound` | 404 | Target Opportunity or Bid does not exist |
| `NotWorkflowManaged` | 409 | Target row exists but its workflow has not started |
| `AlreadyExists` | 409 | Duplicate Start; GET the workflow context and use the current existing state |
| `Forbidden` | 403 | Authenticated actor lacks the required metadata role |
| `ConcurrencyConflict` | 409 | Caller used a stale workflow version and must refresh |
| `IdempotencyConflict` | 409 | Reused identity conflicts with a different effective request |
| `InvalidTransition` | 422 | Action is not valid from the persisted state |
| `ValidationFailed` | 422 | Trusted target, recipient, link, or template data is incomplete |
| Unexpected persistence/transaction failure | 500 | The outer transaction must roll back |

`AlreadyExists` is a duplicate Start error, not an Action replay. The caller should follow the matching GET workflow route, refresh context, and use the current existing state. A matching Action replay is a successful response with `replay = true`, not an error. Failed operations must not partially persist target data, workflow runtime, audit, idempotency, or outbox effects.

## Scenario catalog

Every numbered row below is approved. A row can be referenced before its directory exists; later authoring adds only the pack files defined by the design and must not rename or reinterpret these integration identifiers.

**Opportunity paths**

| # | Directory | Source | Result |
|---:|---|---|---|
| 01 | `01-opportunity-start-submit` | New saved Opportunity | `SUBMITTED` |
| 02 | `02-opportunity-get-context` | Existing workflow | State/version unchanged |
| 03 | `03-opportunity-submit-existing-draft` | `Draft` | `SUBMITTED` |
| 04 | `04-opportunity-submitted-return-to-draft` | `SUBMITTED` | `Draft` |
| 05 | `05-opportunity-submitted-mark-pending` | `SUBMITTED` | `PENDING` |
| 06 | `06-opportunity-pending-return-to-draft` | `PENDING` | `Draft` |
| 07 | `07-opportunity-pending-open-for-bidding` | `PENDING` | `OPEN TO BIDDING` |
| 08 | `08-opportunity-scheduled-close-bidding` | Due `OPEN TO BIDDING` | `CLOSED TO BIDDING` |
| 09 | `09-opportunity-request-seller-approval` | `CLOSED TO BIDDING` | `PENDING SELLER APPROVAL` |
| 10 | `10-opportunity-mark-awaiting-allocation` | `PENDING SELLER APPROVAL` | `AWAITING ALLOCATION` |
| 11 | `11-opportunity-mark-partially-allocated` | `PENDING SELLER APPROVAL` | `PARTIALLY ALLOCATED` |
| 12 | `12-opportunity-mark-not-fulfilled` | `PENDING SELLER APPROVAL` | `NOT FULFILLED` |
| 13 | `13-opportunity-complete-awaiting-allocation` | `AWAITING ALLOCATION` | `COMPLETED` |
| 14 | `14-opportunity-complete-partially-allocated` | `PARTIALLY ALLOCATED` | `COMPLETED` |

**Bid paths**

| # | Directory | Source | Result |
|---:|---|---|---|
| 15 | `15-bid-start-submit` | New saved Bid | `SUBMITTED` |
| 16 | `16-bid-get-context` | Existing workflow | State/version unchanged |
| 17 | `17-bid-submit-existing-draft` | `Draft` | `SUBMITTED` |
| 18 | `18-bid-submitted-return-to-draft` | `SUBMITTED` | `Draft` |
| 19 | `19-bid-scheduled-mark-not-allocated` | Submitted Discount Bid | `NOT ALLOCATED` |
| 20 | `20-bid-scheduled-move-to-ipg-review` | Qualifying submitted Bid | `IPG REVIEW` |
| 21 | `21-bid-send-to-seller-review` | `IPG REVIEW` | `UNDER REVIEW BY SELLER` |
| 22 | `22-bid-seller-accept` | `UNDER REVIEW BY SELLER` | `ACCEPTED` |
| 23 | `23-bid-seller-partially-accept` | `UNDER REVIEW BY SELLER` | `PARTIALLY ACCEPTED` |
| 24 | `24-bid-seller-reject` | `UNDER REVIEW BY SELLER` | `NOT ACCEPTED` |

**Notification and scheduler paths**

| # | Directory | Expected behavior |
|---:|---|---|
| 25 | `25-scheduled-opportunity-open-notification` | Queues one grouped open occurrence |
| 26 | `26-scheduled-opportunity-reminder` | Queues one grouped reminder occurrence |
| 27 | `27-scheduled-catch-up-close-first` | Closes due Opportunities before notification selection |
| 28 | `28-scheduled-disabled` | Returns successful zero work without target reads |
| 29 | `29-scheduled-mixed-bid-classification` | Applies the strict Fair Value plus Premium threshold |
| 30 | `30-notification-outbox-smtp-delivery` | Delivers all applicable SQMS routes from immutable snapshots |

**Failure, replay, and ownership paths**

| # | Directory | Expected behavior |
|---:|---|---|
| 31 | `31-failure-target-row-not-found` | `TargetNotFound`; no runtime effects |
| 32 | `32-failure-workflow-not-started` | `NotWorkflowManaged`; no lazy start |
| 33 | `33-failure-invalid-action-or-role` | Metadata rejection; no target persistence |
| 34 | `34-failure-stale-workflow-version` | Concurrency conflict; caller refreshes |
| 35 | `35-failure-seller-approval-enrichment` | Missing trusted Opportunity data prevents side effects |
| 36 | `36-failure-bid-disposition-enrichment` | Missing Bid/parent data prevents side effects |
| 37 | `37-replay-request-action` | Prior success returned without duplicate side effects |
| 38 | `38-replay-notification-occurrence` | Existing occurrence returned without duplicate outbox row |
| 39 | `39-rollback-target-persistence-after-workflow` | Outer transaction rolls workflow work back |
| 40 | `40-rollback-scheduled-target-projection` | Projection failure rolls workflow transition back |
| 41 | `41-scheduled-per-record-continuation` | One candidate failure does not block later candidates |
| 42 | `42-request-versus-scheduled-projection-ownership` | Request unchanged, target-owned save, scheduled four-column projection |

**Composite acceptance**

| # | Directory | Purpose |
|---:|---|---|
| 43 | `43-complete-opportunity-and-four-bids` | One Opportunity plus Accepted, Partially Accepted, Not Accepted, and Not Allocated Bids with scheduling and six emails |

## Valid Opportunity sequences

- Direct submit and progress: `01 -> 05 -> 07 -> 08 -> 09 -> 10 -> 13`.
- Direct submit with partial allocation: `01 -> 05 -> 07 -> 08 -> 09 -> 11 -> 14`.
- Direct submit with no fulfillment: `01 -> 05 -> 07 -> 08 -> 09 -> 12`.
- Submitted correction: `01 -> 04 -> 03 -> 05`.
- Pending correction: `01 -> 05 -> 06 -> 03 -> 05`.
- Context read: `02` may run after any successful Opportunity Start or Action and does not change state or version.

Scenario `08` is scheduled. It has no public UI route, and the target API must not present it in `availableActions` as a user command.

## Valid Bid sequences

- Accepted: `15 -> 20 -> 21 -> 22`.
- Partially accepted: `15 -> 20 -> 21 -> 23`.
- Not accepted: `15 -> 20 -> 21 -> 24`.
- Discount Bid not allocated: `15 -> 19`.
- Existing Draft: `17 -> 20` or `17 -> 19`, according to scheduled classification.
- Submitted correction: `15 -> 18 -> 17`.
- Context read: `16` may run after any successful Bid Start or Action and does not change state or version.

Scenarios `19` and `20` are scheduled and have no public UI route. The classification uses the strict Fair Value plus Premium threshold; it is not a browser-selected action or target-computed next state.

## Scheduled and notification sequences

- Opportunity opening and reminders: `07 -> 25 -> 26`; each eligible run queues one grouped occurrence with immutable recipient evidence.
- Close-first catch-up: `07 -> 27`; due Opportunities close before notification candidates are selected, preventing stale open/reminder messages.
- Disabled scheduler: `28` proves a successful zero-work result without reading target candidates.
- Mixed Bid classification: four submitted Bids enter `29`; the qualifying Fair Value/Premium Bids move to `IPG REVIEW`, while the Discount Bid moves to `NOT ALLOCATED`.
- SMTP delivery: action and scheduled paths create outbox rows, then `30` delivers the six applicable SQMS notification routes from persisted immutable snapshots.

Seller-approval email behavior is part of `09`. Accepted, partially accepted, and rejected Bid emails are part of `22`, `23`, and `24`. No public scheduled UI route exists; the external scheduler invokes the implemented scheduled service through the operator-owned host/console integration.

## Failure, replay, rollback, and ownership sequences

- Run `31` and `32` before any workflow side effects to prove missing-target and not-started behavior.
- Run `33` or `34` against a valid persisted workflow; rejection occurs before target persistence.
- Run `35` before seller approval and `36` before a Bid disposition to prove trusted enrichment is required before any side effect commits.
- Run `37` immediately after a successful request Action; the repeated request returns `replay = true` without another audit, idempotency, target write, or outbox effect. The target API reads and verifies the already committed projection instead of persisting it again.
- Run `38` after a scheduled notification occurrence; the repeated occurrence returns the existing result without another outbox row.
- Run `39` with **Testing only** target persistence failure injection; the target-owned outer transaction rolls back workflow work.
- Run `40` with **Testing only** scheduled projection failure injection; the scheduled transition and projection roll back together.
- Run `41` with one deliberately failing scheduled candidate followed by valid candidates; processing continues per record and reports the candidate failure.
- Run `42` to compare ownership: request-driven adapter execution leaves the target row unchanged until the target API persists the returned state, while scheduled execution projects exactly the four columns.

Failure injection is never represented as a public request field or route.

## Complete acceptance sequence

Scenario `43` combines one Opportunity and four Bids:

1. Start and submit the Opportunity (`01`), mark it pending (`05`), and open it for bidding (`07`).
2. Queue the grouped open and reminder occurrences (`25`, `26`).
3. Start four submitted Bids (`15` for each target Bid).
4. Close the due Opportunity (`08`) and classify the four Bids (`29`): three qualify for `IPG REVIEW`; one Discount Bid becomes `NOT ALLOCATED`.
5. Request seller approval (`09`), including its notification.
6. Send the three qualifying Bids to seller review (`21`) and finish one as `ACCEPTED` (`22`), one as `PARTIALLY ACCEPTED` (`23`), and one as `NOT ACCEPTED` (`24`).
7. Mark the Opportunity `PARTIALLY ALLOCATED` (`11`) and complete it (`14`).
8. Deliver the outbox through `30` and verify six emails: grouped open, grouped reminder, seller approval, Accepted, Partially Accepted, and Not Accepted.

Acceptance verifies one terminal Opportunity, the four terminal Bid dispositions, target/workflow consistency, expected audit and idempotency rows, zero unexpected SQMS tasks/SLA rows, immutable outbox snapshots, and preservation of target columns outside the owned projection. The HTTP calls remain **Recommended target facade**; the in-process and scheduled behavior is **Implemented**, and the external transaction/controller/persistence work remains **Target implementation required**.
