# SQMS Target API Scenario Handoff Design

## 1. Purpose

Create a source-verified, scenario-driven SQMS integration handoff for the external target API team. The handoff must follow the executable documentation style already used under `workflow-api/samples/scenarios`: a catalog README plus numbered scenario directories containing exact requests, prerequisites, expected effects, and next valid scenarios.

The handoff documents both:

- the implemented in-process `ISqmsWorkflowService` contract; and
- a recommended resource-oriented HTTP facade owned by the target API.

The documentation must never claim that the recommended HTTP routes or DTOs are implemented in this repository. The external target API source has not been supplied as a buildable project.

## 2. Approved deliverable

Create the catalog under:

```text
docs/sqms/target-api-scenarios/
```

The root `README.md` is the primary handoff. The existing `docs/sqms/TARGET-API-INTEGRATION.md` remains the concise engineering contract and links to the scenario catalog.

The deliverable covers every Opportunity and Bid transition, context reads, scheduled close/classification, all six notification routes, failure behavior, replay, concurrency, transaction rollback, request-versus-scheduled projection ownership, and one complete Opportunity/four-Bid SMTP lifecycle.

The portable repository bundle is explicitly outside this documentation task and will be generated later.

## 3. Contract-status labels

Every scenario README must visibly use these labels where applicable:

- **Implemented** — confirmed behavior of the current in-process code.
- **Recommended target facade** — proposed HTTP route or DTO for the external target API.
- **Target implementation required** — persistence, transaction, actor, recipient, controller, or response mapping work owned externally.
- **Testing only** — isolated test-database setup or deliberate failure injection that must never run in production.

These labels prevent recommendations from being confused with code that already exists.

## 4. Ownership model

| Concern | Owner |
|---|---|
| HTTP routes and public SQMS DTOs | Target API |
| Authentication and authorization | Target API |
| Trusted actor construction | Target API |
| Relationship/team address resolution | Target API |
| Request-side target business persistence | Target API |
| Outer request transaction | Target API |
| Transition selection and role validation | Generic workflow engine metadata |
| Workflow runtime, audit, idempotency, and transition outbox | Generic workflow engine |
| SQMS target-data mapping and notification enrichment | `WorkflowEngine.Sqms` |
| Scheduled Opportunity close and Bid classification | SQMS scheduled service |
| Scheduled four-column target projection | SQMS scheduled adapter |
| UI action selection | UI renders returned `availableActions` and sends the selected `actionKey` |

Request-driven Start and Action operations never update an `SSQMS.Opportunity` or `SSQMS.Bid` row. After success, the target API persists the authoritative returned state through its own data-access layer inside the outer transaction. Scheduled transitions are the only adapter-owned write path and update exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.

## 5. Recommended HTTP facade

The scenario files use these proposed target-owned routes:

```text
POST /v1/sqms/opportunities/{id}/workflow/start
GET  /v1/sqms/opportunities/{id}/workflow
POST /v1/sqms/opportunities/{id}/workflow/actions

POST /v1/sqms/bids/{id}/workflow/start
GET  /v1/sqms/bids/{id}/workflow
POST /v1/sqms/bids/{id}/workflow/actions
```

Scheduled processing is not exposed as a browser-callable route. Scheduled scenarios document `ISqmsScheduledService` and the operator/console trigger only.

### 5.1 Authentication

HTTP samples use:

```http
Authorization: Bearer {{accessToken}}
```

The target API derives actor identity and roles from authenticated server-trusted information. Samples do not use browser-controlled workflow actor headers.

### 5.2 Start request

The numeric entity ID comes from the route. The Start body is an empty target-owned DTO:

```json
{}
```

Start does not accept an action key, target state, workflow identity, idempotency identity, actor, role, route, template, or recipient input.

### 5.3 Action request

```json
{
  "actionKey": "MarkOpportunityPending",
  "expectedWorkflowVersion": 2
}
```

The request contains only action intent and the optimistic-concurrency version. It never contains `toState`.

### 5.4 Context request

Context is a GET request with no body. The route entity ID plus authenticated actor determine the lookup and role-filtered action view.

### 5.5 Recommended target response

Start, Action, and Context use one UI-facing projection:

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

The target may add existing business DTO fields, but these workflow field semantics remain stable. The target does not need to expose internal workflow-instance or idempotency identifiers to the browser.

## 6. Recommended error mapping

The target uses its existing Problem Details conventions. Until the real target source is available, the following mappings are recommendations:

| SQMS/engine result | HTTP status | Meaning |
|---|---:|---|
| `TargetNotFound` | 404 | Target Opportunity or Bid does not exist |
| `NotWorkflowManaged` | 409 | Target row exists but its workflow has not started |
| `Forbidden` | 403 | Authenticated actor lacks the required metadata role |
| `ConcurrencyConflict` | 409 | Caller used a stale workflow version and must refresh |
| `IdempotencyConflict` | 409 | Reused identity conflicts with a different effective request |
| `InvalidTransition` | 422 | Action is not valid from the persisted state |
| `ValidationFailed` | 422 | Trusted target, recipient, link, or template data is incomplete |
| Unexpected persistence/transaction failure | 500 | The outer transaction must roll back |

A matching idempotent replay is a successful response with `replay = true`, not an error.

## 7. Scenario directory formats

### 7.1 Request-driven transition

```text
README.md
request.http
request.json
in-process.cs
expected-response.json
verify.sql
```

### 7.2 Context read

```text
README.md
request.http
expected-response.json
verify.sql
```

### 7.3 Replay

`request.http` sends the exact same request twice. The README and verifier prove the second result is a replay without additional audit, idempotency, target mutation, or outbox side effects.

### 7.4 Scheduled operation

```text
README.md
in-process.cs
verify.sql
```

When controlled date/state preparation is unavoidable, the pack may add `setup.testing.sql`. It must be explicitly labeled for an isolated test database and must not hide prerequisites.

### 7.5 Failure injection or rollback

```text
README.md
in-process.cs
verify.sql
```

The documentation does not invent public HTTP fields or routes for causing infrastructure failures.

### 7.6 Composite lifecycle

```text
README.md
sequence.http
in-process.cs
expected-results.sql
expected-emails.md
```

## 8. Standard README structure

Each numbered scenario README uses this order:

```text
Title
Contract status
Endpoint
Actor and required role
Implemented service method
Prerequisite
Public request notes
Matching in-process call
Target persistence ownership
Expected workflow effects
Expected target-row effects
Expected audit and idempotency effects
Expected notification effects
Expected available actions
Failure behavior
Next valid packs
```

Prerequisites link to the scenario that creates the required state. Scenarios never create hidden workflow state.

## 9. In-process example rules

`in-process.cs` uses real implemented types and methods:

- `ISqmsWorkflowService`;
- `StartOpportunityAsync` and `StartBidAsync`;
- `GetOpportunityContextAsync` and `GetBidContextAsync`;
- `ExecuteOpportunityActionAsync` and `ExecuteBidActionAsync`;
- `SqmsActor`;
- `SqmsExecuteRequest`;
- `SqmsOperationResult`.

Target-specific actor factories, recipient resolvers, repositories, error mappers, and transaction coordinators are explicitly marked as integration placeholders rather than represented as existing classes.

Every request-driven example follows this sequence:

1. Begin the target-owned transaction.
2. Persist target business changes needed before the workflow call.
3. Ensure `WorkflowDbContext` uses the same open SQL connection and transaction.
4. Construct the trusted normalized actor.
5. Resolve only metadata-approved recipient source inputs on the server.
6. Call `ISqmsWorkflowService`.
7. Roll back on an unsuccessful result or exception.
8. Extract authoritative `NewState` or `CurrentState`.
9. Persist the state through the target repository.
10. Commit once after target and workflow work succeeds.

The example uses `TimeProvider`; it does not introduce direct `DateTime.UtcNow` calls.

## 10. Verification rules

Each state-changing scenario includes a narrowly scoped `verify.sql` that checks applicable evidence:

- exact target `Status` and `WorkflowState`;
- workflow current state, instance version, runtime status, and terminal timestamp;
- expected audit action and no extra audit rows;
- expected idempotency row and replay stability;
- expected notification route/outbox count;
- zero unexpected SQMS task and SLA rows;
- preservation of non-workflow target columns where relevant.

The verifier is read-only. Any `setup.testing.sql` is separate and visibly destructive/test-only.

## 11. Complete scenario catalog

### 11.1 Opportunity paths

| # | Directory | Source | Result |
|---:|---|---|---|
| 01 | `opportunity-start-submit` | New saved Opportunity | `SUBMITTED` |
| 02 | `opportunity-get-context` | Existing workflow | State/version unchanged |
| 03 | `opportunity-submit-existing-draft` | `Draft` | `SUBMITTED` |
| 04 | `opportunity-submitted-return-to-draft` | `SUBMITTED` | `Draft` |
| 05 | `opportunity-submitted-mark-pending` | `SUBMITTED` | `PENDING` |
| 06 | `opportunity-pending-return-to-draft` | `PENDING` | `Draft` |
| 07 | `opportunity-pending-open-for-bidding` | `PENDING` | `OPEN TO BIDDING` |
| 08 | `opportunity-scheduled-close-bidding` | Due `OPEN TO BIDDING` | `CLOSED TO BIDDING` |
| 09 | `opportunity-request-seller-approval` | `CLOSED TO BIDDING` | `PENDING SELLER APPROVAL` |
| 10 | `opportunity-mark-awaiting-allocation` | `PENDING SELLER APPROVAL` | `AWAITING ALLOCATION` |
| 11 | `opportunity-mark-partially-allocated` | `PENDING SELLER APPROVAL` | `PARTIALLY ALLOCATED` |
| 12 | `opportunity-mark-not-fulfilled` | `PENDING SELLER APPROVAL` | `NOT FULFILLED` |
| 13 | `opportunity-complete-awaiting-allocation` | `AWAITING ALLOCATION` | `COMPLETED` |
| 14 | `opportunity-complete-partially-allocated` | `PARTIALLY ALLOCATED` | `COMPLETED` |

### 11.2 Bid paths

| # | Directory | Source | Result |
|---:|---|---|---|
| 15 | `bid-start-submit` | New saved Bid | `SUBMITTED` |
| 16 | `bid-get-context` | Existing workflow | State/version unchanged |
| 17 | `bid-submit-existing-draft` | `Draft` | `SUBMITTED` |
| 18 | `bid-submitted-return-to-draft` | `SUBMITTED` | `Draft` |
| 19 | `bid-scheduled-mark-not-allocated` | Submitted Discount Bid | `NOT ALLOCATED` |
| 20 | `bid-scheduled-move-to-ipg-review` | Qualifying submitted Bid | `IPG REVIEW` |
| 21 | `bid-send-to-seller-review` | `IPG REVIEW` | `UNDER REVIEW BY SELLER` |
| 22 | `bid-seller-accept` | `UNDER REVIEW BY SELLER` | `ACCEPTED` |
| 23 | `bid-seller-partially-accept` | `UNDER REVIEW BY SELLER` | `PARTIALLY ACCEPTED` |
| 24 | `bid-seller-reject` | `UNDER REVIEW BY SELLER` | `NOT ACCEPTED` |

### 11.3 Notification and scheduler paths

| # | Directory | Expected behavior |
|---:|---|---|
| 25 | `scheduled-opportunity-open-notification` | Queues one grouped open occurrence |
| 26 | `scheduled-opportunity-reminder` | Queues one grouped reminder occurrence |
| 27 | `scheduled-catch-up-close-first` | Closes due Opportunities before notification selection |
| 28 | `scheduled-disabled` | Returns successful zero work without target reads |
| 29 | `scheduled-mixed-bid-classification` | Applies the strict Fair Value plus Premium threshold |
| 30 | `notification-outbox-smtp-delivery` | Delivers all applicable SQMS routes from immutable snapshots |

Seller-approval email behavior is covered in scenario 09. Accepted, partially accepted, and rejected email behavior is covered in scenarios 22 through 24.

### 11.4 Failure, replay, and ownership paths

| # | Directory | Expected behavior |
|---:|---|---|
| 31 | `failure-target-row-not-found` | `TargetNotFound`; no runtime effects |
| 32 | `failure-workflow-not-started` | `NotWorkflowManaged`; no lazy start |
| 33 | `failure-invalid-action-or-role` | Metadata rejection; no target persistence |
| 34 | `failure-stale-workflow-version` | Concurrency conflict; caller refreshes |
| 35 | `failure-seller-approval-enrichment` | Missing trusted Opportunity data prevents side effects |
| 36 | `failure-bid-disposition-enrichment` | Missing Bid/parent data prevents side effects |
| 37 | `replay-request-action` | Prior success returned without duplicate side effects |
| 38 | `replay-notification-occurrence` | Existing occurrence returned without duplicate outbox row |
| 39 | `rollback-target-persistence-after-workflow` | Outer transaction rolls workflow work back |
| 40 | `rollback-scheduled-target-projection` | Projection failure rolls workflow transition back |
| 41 | `scheduled-per-record-continuation` | One candidate failure does not block later candidates |
| 42 | `request-versus-scheduled-projection-ownership` | Request unchanged, target-owned save, scheduled four-column projection |

### 11.5 Composite acceptance

| # | Directory | Purpose |
|---:|---|---|
| 43 | `complete-opportunity-and-four-bids` | One Opportunity plus Accepted, Partially Accepted, Not Accepted, and Not Allocated Bids with scheduling and six emails |

## 12. Documentation contract tests

Add automated tests that prove the catalog cannot silently drift from the implemented metadata and boundary rules:

- all 20 active metadata transitions are represented;
- Start and existing-Draft Submit are both documented;
- all six notification routes are covered;
- all terminal states are covered;
- every request JSON parses;
- no public request contains `toState`;
- no public request contains actor, role, recipient, route, template, target-state, workflow-instance, or idempotency authority fields;
- proposed routes follow the approved resource convention;
- every numbered directory appears in the root catalog;
- prerequisite and next-pack links resolve;
- in-process examples name existing SQMS service methods;
- every state-changing pack documents persistence ownership;
- scheduled packs do not suggest public UI invocation;
- the composite pack covers Accepted, Partially Accepted, Not Accepted, and Not Allocated Bids.

These tests belong in the existing test project and add no production behavior.

## 13. Authoring constraints

- Use exact stable workflow, state, action, role, and notification-route keys from current source/SQL metadata.
- Do not use internal `WorkflowActionEnvelope` as the target public request.
- Do not let `workflow.options` or request JSON override metadata.
- Do not add tasks, SLA policies, Bid-to-Opportunity roll-up, or cross-row request orchestration.
- Do not claim exact target controller, repository, DTO, or transaction-helper class names.
- Do not expose `ISqmsScheduledTransitionService` to request controllers.
- Do not restore request-driven adapter projection.
- Do not create a scheduler host, lock, route, or deployment as part of documentation.
- Do not run production SQL or rerun retained one-shot live acceptance.
- Clearly state that scheduling uses configured time zone with calendar-day `AddDays` behavior and currently has no scheduler-wide mutual-exclusion lock.
- Clearly state the notification-outbox downgrade limitation after occurrence-owned rows exist.

## 14. Validation and completion criteria

The documentation work is complete when:

1. All 43 catalog entries exist with the approved applicable files.
2. Every example is source-checked against current SQMS contracts and metadata.
3. All JSON, path, action, ownership, and coverage contract tests pass.
4. Existing generic/SQMS regression tests pass.
5. Existing Workflow API scenario packs remain unchanged and valid.
6. `docs/sqms/TARGET-API-INTEGRATION.md` links to the catalog without duplicating or contradicting it.
7. No HTTP route is represented as implemented before target source confirmation.
8. The AGENTS completion summary records tests, seed verification, sample execution, updated files, and remaining external target work.

## 15. Explicitly deferred work

- Generating the refreshed PowerShell repository bundle.
- Editing the external target API.
- Finalizing actual target route/controller/DTO names.
- Implementing the shared EF transaction enlistment in the target host.
- Deploying SQMS SQL to a production database.
- Registering or enabling a production scheduler.
- Production SMTP configuration.
