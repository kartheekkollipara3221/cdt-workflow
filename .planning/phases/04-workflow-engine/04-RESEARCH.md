# Phase 04 Research: Workflow Engine

## Research Complete

Phase 4 is implementation-heavy but not conceptually open-ended. The critical path is to turn the locked `04-CONTEXT.md` decisions into a small set of reusable application services, with SQL persistence isolated behind infrastructure services and command handlers kept thin.

## What Planning Must Account For

### Existing Assets To Reuse

- `WorkflowActionEnvelopeValidator` already handles non-persisted envelope checks. Phase 4 should keep it as the first validation gate and layer persisted checks after it.
- `WorkflowActionEnvelopeNormalizer` already applies console overrides and derives effective client/correlation/idempotency values. Phase 4 should extend its call path, not duplicate override logic.
- `CanonicalRequestHasher` is the locked request-hash source of truth. Any Phase 4 canonicalization change must happen in this class and preserve Phase 3 tests.
- `WorkflowContextReader` is the committed-read context builder for `show-context`. Phase 4 should call it after committing successful actions and after conflict/version failures that need refreshed actual context.
- `WorkflowDbContext` already contains all runtime tables needed for Phase 4: instances, tasks, mock section data, audit, notifications, and idempotency records.
- `CommandRegistry` already dispatches `execute-json` and stubs `start-workflow`; Phase 4 should wire `start-workflow` and replace the deferred `execute-json` behavior.

### Required New Application Concepts

- `WorkflowActionResultBuilder`: single construction point for all `WorkflowActionResult` shapes, including success, failures, diagnostics, expected/actual fields, failed guard, validation errors, and `AlreadyExists`.
- `WorkflowExecutionService`: orchestrates `execute-json` persisted validation, idempotency, transaction, side effects, and context refresh.
- `StartWorkflowService`: owns `start-workflow`, duplicate detection, reset delete-counts, and fresh instance creation.
- Persisted validation helpers: instance lookup, definition-version guard, action/transition lookup, concurrency guard checks, role/ambiguity resolution, and admin override resolution.
- Idempotency helper: preflight read, request-hash compare, canonical result serialization, result-hash calculation, insert, and unique-violation recovery.

### SQL/EF Pitfalls

- Use `READ COMMITTED` default isolation; do not introduce `SERIALIZABLE` or `SNAPSHOT`.
- The only expected SQL exception translated to a workflow result is the idempotency-record unique violation at the idempotency insert step. Other SQL exceptions should propagate.
- The unique pending-task index can raise unrelated unique violations if task side effects are wrong. Do not catch these under the idempotency handler.
- `MockSectionData` reset is keyed by business object, not workflow instance.
- `IdempotencyRecord` reset is keyed by `WorkflowInstanceId`, not by parsing key text.
- If the plan adds SQL exception handling, it should identify SQL Server unique violations by `SqlException.Number` and idempotency constraint identity, not broad message matching.

### Result JSON Pitfalls

- Current `WorkflowActionResultEnvelope` only has `result`; Phase 4 must add top-level `workflowContext`.
- Current `WorkflowActionResult` lacks `expected`, `actual`, `failedGuard`, `diagnostics`, `validationErrors`, and `AlreadyExists` fields.
- `WorkflowJson.Options` is indented and contract-oriented. Persisted `ResultJson` needs canonical, no-whitespace, alphabetically ordered serialization for hashing. Do not change display JSON options globally if it would break readable/test expectations; add a canonical result serializer.
- `resultHash` must exclude diagnostics to avoid recursion.
- `ValidationFailed` has `requestHash = null`; do not compute request hash for malformed envelopes.

### Side-Effect Rules To Preserve

- `SaveDraft`: mock data + lightweight audit + idempotency only. No state, version, task, SLA, or notification changes.
- State transitions: mock data, close old task, move state/version, create new task, notification, audit, idempotency.
- Audit writes only successful actions, including `SaveDraft`. Failed actions write no audit.
- Notifications are queued to outbox; Phase 4 does not send them.
- Task `DueAtUtc` is set when creating tasks with SLA policies; `MakerReworkTask` has no SLA and due date remains null.

### Test Strategy

SQL-backed tests are required. The existing Phase 3 context tests already establish a pattern using a fixed SQL Server database, migrations, seed service, and `FixedTimeProvider`. Phase 4 should use a dedicated database name such as `burrito_phase4_engine_tests` to avoid cross-phase test contamination.

Focus tests by behavior:

- `start-workflow`: fresh creation, duplicate active/completed `AlreadyExists`, reset existing/missing instances, no seed deletion, no synthetic start audit.
- Result shape: every result code deserializes into one type; explicit null side-effect fields on failures; diagnostics present.
- Persisted validation priority: multi-failure requests return the first locked result code.
- Idempotency: pre-existing replay/conflict, race recovery, no duplicate side effects, result hash.
- Concurrency: guard order and `failedGuard`.
- Transitions: submit for checker, checker return to maker, checker send to trading desk, trading return to maker, BDRM complete/rework where feasible.
- Admin override: succeeds for missing actor role but does not bypass validation/concurrency/idempotency/version.
- Refreshed context: success non-replay, replay stale context, concurrency/version conflict actual context, no context for validation/forbidden/invalid/idempotency/not-found.

### Recommended Plan Slicing

Use four plans with dependencies:

1. **Result and command contract foundation** - extend result DTOs, diagnostics, canonical result serialization, result builder, command parsing for `--admin-override` and `start-workflow` surface. This unblocks all later plans.
2. **Start workflow and reset** - implement `start-workflow`, `AlreadyExists`, reset transaction, and related tests. Independent from action execution after result builder exists.
3. **Execution validation and idempotency core** - implement persisted validation order, role/ambiguity/admin override, request hash reuse, idempotency preflight/replay/conflict, and result-code tests.
4. **Transition side effects and refreshed context** - implement state-changing transaction side effects, SaveDraft behavior, notification outbox, audit, idempotency insert race recovery, post-commit context refresh, samples needed for quality gates.

This keeps early plans small enough to verify while ensuring the final plan has the complete execution path.

## Research Risks

- The Phase 4 decision set is large. Every plan must cite decision IDs from `04-CONTEXT.md` in `must_haves` so the decision coverage gate can see them.
- If Phase 4 attempts to implement all supplementary read-only commands in the same plan as transition execution, the plan will be too broad. Treat them as optional/mechanical unless needed by quality gates.
- SQL Server Windows auth may fail in sandboxed runs. Verification plans should mention rerunning SQL-backed tests/CLI smokes outside the sandbox if SSPI fails.

## Planning Recommendation

Proceed to planning with the four-plan breakdown above. Do not ask more questions before planning; the remaining details are implementation mechanics already bounded by `PROJECT.md` and `04-CONTEXT.md`.
