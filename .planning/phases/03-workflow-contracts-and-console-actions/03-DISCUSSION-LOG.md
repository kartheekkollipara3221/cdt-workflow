# Phase 3: Workflow Contracts and Console Actions - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-07
**Phase:** 3-Workflow Contracts and Console Actions
**Areas discussed:** Command behavior boundary, DTO validation strictness, Console JSON output style, Override and idempotency normalization

---

## Command Behavior Boundary

| Option | Description | Selected |
|--------|-------------|----------|
| Contract-shaped only | `execute-json` parses/validates envelopes and prints result-shaped output; `show-context` remains placeholder/error-shaped until runtime execution exists. | |
| Read-only SQL context for `show-context` | `show-context` reads persisted workflow instance, pending task, and metadata to produce useful `WorkflowContextResponse`; `execute-json` still does not execute transitions. | Yes |
| Thin vertical slice | Add enough start/context/mock-save behavior for realistic result shells while still avoiding full transition execution. | |

**User's choice:** 2
**Notes:** `show-context` should be useful in Phase 3. `execute-json` execution side effects remain Phase 4.

---

## DTO Validation Strictness

| Option | Description | Selected |
|--------|-------------|----------|
| Full contract validation now | Implement validation for all rules that do not require database workflow execution or persisted state checks. | Yes |
| Shape-only validation | Parse JSON and required top-level blocks only; defer most field-level rules. | |
| Layered validation | Build full validators but report only a Phase 3 subset from `execute-json`. | |

**User's choice:** 1
**Notes:** Authorization, transition validity, persisted concurrency checks, and side effects remain Phase 4.

---

## Console JSON Output Style

| Option | Description | Selected |
|--------|-------------|----------|
| Human-readable default, JSON via flag | Both commands default to readable console text; `--format json` prints contracts. | |
| JSON primary for contract commands | Both commands default to JSON because contract proof is the main job. | |
| Mixed | `show-context` defaults to JSON; `execute-json` defaults to readable text unless `--format json` is passed. | Yes |

**User's choice:** 3
**Notes:** `show-context` is the form-open/API contract object, so JSON should be the default there.

---

## Override and Idempotency Normalization

| Option | Description | Selected |
|--------|-------------|----------|
| Full deterministic normalization now | Apply overrides before validation/hash, derive effective client action/idempotency/correlation values, and compute stable canonical request hash. | Yes |
| Parse and carry only | Parse flags and DTO fields but leave normalization/hash behavior to Phase 4. | |
| Overrides now, hashing later | Apply business object/workflow instance overrides now and defer canonical hash/idempotency derivation. | |

**User's choice:** 1
**Notes:** Hash and normalization behavior should be built in Phase 3 and reused by Phase 4.

---

## the agent's Discretion

- Exact namespace/file organization, validator class names, and JSON helper structure are left to the implementer.

## Deferred Ideas

- Transition execution, side effects, persisted concurrency checks, audit, notification outbox, and idempotency replay persistence are Phase 4.
- SLA monitor processing is Phase 5.
- Full sample set and demo flow are Phase 6.

