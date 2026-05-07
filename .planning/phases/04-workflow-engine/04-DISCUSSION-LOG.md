# Phase 4: Workflow Engine - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-07
**Phase:** 04-Workflow Engine
**Areas discussed:** Execution Transaction Semantics, Validation And Result Codes, Idempotency And Concurrency, Start Workflow And Reset, Refreshed Context After Action, Error JSON Shape

---

## Execution Transaction Semantics

| Option | Description | Selected |
|--------|-------------|----------|
| One transaction for every non-idempotent valid action | Validate, save mock data, apply workflow side effects, write audit/notifications/idempotency, and commit together. | |
| Split data save and workflow movement | Save mock data separately from workflow movement. | |
| You decide | Use section 15 atomic behavior. | yes |

**User's choice:** You decide, then locked atomic transaction behavior.
**Notes:** `SaveDraft` writes mock data, lightweight audit, and idempotency only. State-changing transitions use metadata resolve, mock data, close old task, move state/version, create new task, queue notification, audit, idempotency. Readable output uses a concise transition summary.

---

## Validation And Result Codes

| Option | Description | Selected |
|--------|-------------|----------|
| Recommended validation priority | Contract validation, idempotency, workflow/version, completed status, role/action transition, concurrency. | |
| Workflow/state/role/concurrency before idempotency | Do persisted checks before idempotency. | |
| Corrected priority | User-specified 14-step priority with explicit result codes. | yes |

**User's choice:** Corrected 14-step validation priority.
**Notes:** Added `WorkflowInstanceNotFound`; idempotent replay returns stored result/context before state/auth checks; completed workflows fail with `InvalidTransition`; admin override bypasses only authorization/ambiguity; stable messages include guarded values; result JSON carries structured expected/actual/failedGuard.

---

## Idempotency And Concurrency

| Option | Description | Selected |
|--------|-------------|----------|
| Full canonical result JSON in ResultJson | Store exactly what the original caller received. | yes |
| Compact summary | Store less data. | |
| Recompute on replay | Re-read current state to build replay output. | |

**User's choice:** Store full canonical `WorkflowActionResult` JSON and return it as-is on replay.
**Notes:** Reuse Phase 3 `CanonicalRequestHasher` as the only request hash path. Concurrency guard reporting order is state, taskId, taskKey, instanceVersion. Racing duplicate idempotency inserts rely on SQL unique constraint; only the idempotency insert unique violation is translated to replay/conflict.

---

## Start Workflow And Reset

| Option | Description | Selected |
|--------|-------------|----------|
| Return AlreadyExists with details | One workflow lifetime per business object. | yes |
| Create a new workflow if completed | Allow repeated lifetimes. | |
| Silent success with existing instance | Hide duplicate creation attempts. | |

**User's choice:** `AlreadyExists` with full details and one lifetime per business object.
**Notes:** Fresh start inserts only `WorkflowInstance` in Draft. No start task, audit, notification, mock data, or idempotency. Reset deletes six runtime row groups in order and inserts a fresh instance in one transaction; seed metadata is never touched.

---

## Refreshed Context After Action

| Option | Description | Selected |
|--------|-------------|----------|
| Always include context for submitting actor | Uniform result shape; post-commit view. | yes |
| Only for state-changing transitions | SaveDraft has no refreshed context. | |
| Never include context | Caller must run show-context separately. | |

**User's choice:** Always include refreshed context for successful non-replay actions.
**Notes:** Context is computed post-commit through the same `show-context` service and from the submitting actor's perspective. Replay returns stored context. Concurrency conflict and definition version mismatch return refreshed actual context. Other failures do not.

---

## Error JSON Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Flat top-level fields under result | One parseable shape: diagnostics, expected, actual, failedGuard. | yes |
| Nested per failure type | Different parsing path per result code. | |
| Message only | No structured fields. | |

**User's choice:** Flat top-level fields under `result`.
**Notes:** `workflowContext` is a top-level peer of `result`. Side-effect fields are explicitly null on failure. `diagnostics` is always present. `ValidationFailed` has `diagnostics.validationErrors`; `AlreadyExists` has flat `existing*` fields; `IdempotencyConflict` uses stored/current hash fields. Result construction should be centralized in `WorkflowActionResultBuilder`.

---

## the agent's Discretion

- Exact service/class names and implementation slicing.
- Mechanical logging discipline and console formatting details.
- Sample JSON fixture authoring from the locked project contract.
- Supplementary read-only command implementation details derived from `PROJECT.md` section 10.5.

## Deferred Ideas

- SLA scanning and notification sender processing remain Phase 5.
- Demo script polish and README completion remain Phase 6.
