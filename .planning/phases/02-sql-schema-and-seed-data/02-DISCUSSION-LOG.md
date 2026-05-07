# Phase 2: SQL Schema and Seed Data - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md - this log preserves the alternatives considered.

**Date:** 2026-05-07
**Phase:** 2-SQL Schema and Seed Data
**Areas discussed:** Schema model, seed data, command behavior, testing and verification

---

## Schema Model

| Option | Description | Selected |
|--------|-------------|----------|
| Full generic schema from PROJECT.md section 12 | Implement required metadata/runtime entities and constraints now. | Yes |
| Minimal metadata-only schema | Seed definitions only, defer runtime tables. | |
| Portfolio Trade-specific tables | Easier sample mapping but violates reusable engine boundary. | |

**User's choice:** Auto-selected recommended default.
**Notes:** Phase 2 owns schema and persistence, so runtime tables and constraints are required even if execution behavior arrives later.

---

## Seed Data

| Option | Description | Selected |
|--------|-------------|----------|
| Deterministic idempotent Portfolio Trade v1 seed service | Rerunnable seed keyed by workflow/version/key values. | Yes |
| One-time migration inserts only | Simpler but harder to verify/rerun safely. | |
| Hardcoded engine behavior | Not seed data; violates project contract. | |

**User's choice:** Auto-selected recommended default.
**Notes:** `init-db` must be safe to rerun and must seed the complete transition matrix plus notification routes.

---

## Command Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Real `init-db` and `verify-seed` handlers | Apply migrations, seed data, and validate SQL. | Yes |
| Leave command stubs until later | Would fail Phase 2 quality gates. | |
| Add runtime execution behavior now | Pulls Phase 3/4 work into Phase 2. | |

**User's choice:** Auto-selected recommended default.
**Notes:** Phase 2 should replace only the two schema/seed command stubs and keep later runtime command behavior deferred.

---

## Testing and Verification

| Option | Description | Selected |
|--------|-------------|----------|
| SQL-backed schema/seed tests | Verify migrations, constraints, seed rows, and `verify-seed` against SQL Server. | Yes |
| EF InMemory tests | Fast but not valid for SQL constraints or seed verification. | |
| Manual-only verification | Too weak for the required guardrails. | |

**User's choice:** Auto-selected recommended default.
**Notes:** SQL-backed verification is required by the source contract. Tests should stay focused on schema and seed, not transition execution side effects.

---

## the agent's Discretion

- Exact CLR names and EF configuration organization are left to the planner/executor as long as SQL behavior and seeded keys match `PROJECT.md`.

## Deferred Ideas

- JSON contracts, transition execution, SLA monitor processing, notification senders, samples, and README completion are later phases.
