# SQMS Request Projection Ownership Correction Design

## Goal

Correct the SQMS integration ownership boundary so request-driven workflow operations never update `[SSQMS].[Opportunity]` or `[SSQMS].[Bid]`. They return authoritative workflow state and context to the target API, which owns target-row persistence. Scheduled transitions remain the explicit exception and may project workflow state into SQMS rows.

This is an explicitly approved behavior correction. It preserves the existing .NET integration surface but intentionally removes the request-driven target-row mutation on which current tests and acceptance tooling incorrectly rely.

## Governing decisions

- The generic workflow engine remains the only transition authority.
- Request-driven callers send `actionKey`, not `toState`.
- `ISqmsWorkflowService` Start, Context, and Action signatures remain stable.
- `SqmsOperationResult` remains the authoritative result/context returned to the target API.
- The target API owns request-driven Opportunity/Bid persistence, including `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`.
- The thin layer may update those four fields only while executing scheduled transitions.
- No fallback, configuration switch, or compatibility mode may silently restore request-driven projection.
- Existing retained SQL databases and smtp4dev mailboxes must not be replayed, modified, or removed.

## Compatibility boundary

The following integration surfaces remain backward compatible:

- `ISqmsWorkflowService` method names and parameters;
- `SqmsExecuteRequest`, `SqmsActor`, and `SqmsOperationResult` shapes;
- `AddSqmsWorkflowIntegration(...)` registration;
- workflow, state, action, role, notification-route, and template keys;
- scheduled console command and arguments;
- public CDT Workflow API contracts and generic-engine behavior.

The following behavior intentionally changes:

- `StartOpportunityAsync` and `StartBidAsync` no longer update target rows.
- `ExecuteOpportunityActionAsync` and `ExecuteBidActionAsync` no longer update target rows.
- A successful request-driven result requires the target API to persist the returned authoritative state as part of its own application transaction.
- Existing tests, documentation, and acceptance tooling that assume automatic request-driven target parity must be corrected.

## Architecture

### Request-driven service

`ISqmsWorkflowService` remains the target API integration surface. It:

1. validates the target record exists;
2. starts or executes the generic workflow through existing application services;
3. commits only workflow-owned runtime, audit, idempotency, task/SLA, and outbox effects;
4. reads refreshed workflow context;
5. returns the authoritative workflow state/version/context;
6. performs no `UPDATE` against either SQMS target table.

Failed operations and Context reads remain read-only for target rows.

### Scheduled transition service

Add a separate scheduled-transition interface used only by `SqmsScheduledService`. It exposes scheduled Opportunity and Bid action execution without changing the target API-facing interface.

The scheduled implementation reuses the same action construction and generic workflow execution logic, but it enables target-state projection within the same SQL transaction. On success it updates exactly:

- `Status`;
- `WorkflowState`;
- `ModifiedBy`;
- `ModifiedDate`.

No calculated, amount, account, effective-date, RM/team, or other application fields are updated.

Both the generic workflow transition and scheduled target projection commit together. Projection failure rolls back workflow runtime, audit, idempotency, and outbox effects for that scheduled action.

### Dependency injection

Register the concrete SQMS workflow service once per scope and map both interfaces to that scoped instance:

- `ISqmsWorkflowService` for target API request-driven operations;
- the scheduled-transition interface for `SqmsScheduledService` only.

The existing `AddSqmsWorkflowIntegration(...)` entry point remains unchanged.

## Data flow

### Request-driven Start or Action

1. Target API validates and saves its business data.
2. Target API calls the existing SQMS Start or Action method.
3. SQMS executes the generic workflow operation.
4. SQMS returns `SqmsOperationResult` containing authoritative workflow result and refreshed context.
5. SQMS has not changed the Opportunity/Bid row.
6. Target API writes the returned state to its target row and completes its transaction according to its application policy.

### Scheduled close or classification

1. `SqmsScheduledService` selects eligible target rows.
2. It invokes the scheduled-transition interface with the trusted System actor.
3. The workflow transition executes and the target row is projected in one transaction.
4. Opportunity close commits before Bid classification begins.
5. Each Bid classification remains an independent transaction so one failure does not roll back successful siblings.
6. The run continues per record and returns a non-success result when any candidate fails.

Open and reminder notifications remain notification-only and do not update target state.

## Error and replay behavior

- Request-driven workflow failure returns the existing structured failure and never updates target rows.
- Request-driven idempotent success replay returns the stored workflow result/context and never refreshes target modification columns.
- Scheduled idempotent success replay may confirm/reapply the same target state only through the scheduled path; it must not create duplicate workflow side effects or notifications.
- Scheduled projection failure throws or returns failure according to the existing scheduled error boundary and rolls back that action atomically.
- Context reads never lazy-start workflows and never update target rows.

## Automated test design

Tests must be written and observed failing before production changes.

### Request-driven ownership tests

- Start Opportunity returns `SUBMITTED` and version 2 while the complete target row remains byte/value-equivalent to its pre-call snapshot.
- Start Bid provides the same proof.
- Successful manual Opportunity and Bid actions return their new workflow states while target `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` remain unchanged.
- Idempotent replay leaves target rows unchanged.
- Failed actions and Context reads remain unchanged.
- The public request contract still contains no client next-state field.

### Scheduled ownership tests

- Scheduled Opportunity close updates the four allowed target fields.
- Scheduled Bid classification updates the four allowed target fields.
- Scheduled projection failure rolls back workflow runtime and target changes.
- Open/reminder notification-only processing does not update target state.

### Regression tests

- Full WorkflowEngine solution tests.
- Full Workflow API tests.
- Warning-as-error builds.
- Production SQMS metadata/deployment verification.
- Existing Portfolio Trade seed and supported sample workflows.
- Negative source searches proving request-driven methods do not call projection methods.

## Isolated live SMTP acceptance

Create a new acceptance profile and one-shot launcher with a fresh database, unique marker, unique smtp4dev ports, and a new smtp4dev data directory. Do not reuse or alter retained evidence.

The scenario uses one Opportunity and four related Bids.

### Opportunity lifecycle

Drive:

`Draft -> Submitted -> Pending -> Open To Bidding -> Closed To Bidding -> Pending Seller Approval -> Awaiting Allocation -> Completed`

Immediately after every request-driven Start/Action call:

1. record the authoritative returned workflow state/version;
2. query and prove the complete target row has not changed;
3. apply a visibly separate acceptance-driver SQL update that simulates target-API-owned persistence;
4. verify target state then matches the returned state before continuing.

The scheduled close is the exception: it must update the Opportunity target state itself and the proof must show the four allowed fields changed without the acceptance driver applying them.

### Bid lifecycles

Use four Bids:

1. `Draft -> Submitted -> IPG Review -> Under Review By Seller -> Accepted`;
2. `Draft -> Submitted -> IPG Review -> Under Review By Seller -> Partially Accepted`;
3. `Draft -> Submitted -> IPG Review -> Under Review By Seller -> Not Accepted`;
4. `Draft -> Submitted -> Not Allocated` through scheduled classification.

For the first three Bids, each request-driven step uses the unchanged-row proof followed by separate target-owned persistence. The fourth Bid proves scheduled classification performs the permitted target projection directly.

### SMTP proof

Deliver and verify exactly the expected messages for this lifecycle:

- Opportunity open notice;
- Opportunity reminder;
- Seller approval request;
- Bid accepted;
- Bid partially accepted;
- Bid not accepted.

Verify exact subject/body/HTML route semantics, intended To/Cc snapshots, smtp4dev message count, sent outbox status, and zero retries. `Not Allocated` remains notification-free unless metadata explicitly says otherwise.

### SQL and workflow proof

Retain a results report containing:

- target-row before/after snapshots for every request-driven call;
- separate target-owned persistence records from the acceptance driver;
- scheduled projection evidence;
- workflow instance state/version parity after the target-owned or scheduled update;
- ordered workflow audit history;
- outbox and smtp4dev message counts;
- zero tasks and zero SLA schedules for SQMS;
- unchanged calculated-column sentinels;
- database name, marker, ports, and mailbox path.

## Documentation corrections

Update active SQMS integration documentation to say:

- request-driven adapter calls never persist target state;
- target API must persist returned state;
- scheduled transitions are the only adapter-owned projection path;
- exact target API HTTP routes/DTOs remain target-owned and must not be invented here.

Historical plans/results remain historical evidence. If they assert the old ownership as current behavior, label them superseded or add a prominent correction rather than rewriting executed history invisibly.

## Completion gate

The change is complete only when:

- request-driven code contains no target projection call;
- scheduled projection is atomic and isolated behind its dedicated interface;
- focused RED/GREEN evidence is recorded;
- full regression and warning-as-error gates pass;
- the fresh full Opportunity/Bid SMTP acceptance succeeds;
- retained acceptance evidence remains untouched;
- `CODEX_HANDOFF.md` and the required completion summary identify the target API integration action and all retained live evidence.
