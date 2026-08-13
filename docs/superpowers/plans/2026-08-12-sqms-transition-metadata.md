# SQMS Transition Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver directly executable, idempotent SQL Server seed and verification scripts for the complete SQMS Opportunity and Bid transition matrices.

**Architecture:** Two independent metadata-driven workflows use the existing `dbo.Workflow*Definition` tables. The scripts seed only definitions, states, roles, actions, and transitions; later iterations may attach tasks, notifications, SLA, and coordinator timing without changing the transition keys.

**Tech Stack:** SQL Server T-SQL, existing .NET 10/EF Core workflow schema, `sqlcmd` verification.

---

### Task 1: Prove the SQMS definitions are missing

**Files:**
- Read: `dbo.WorkflowDefinition` in the configured SQL Server database

- [x] Run a SQL query for `SqmsOpportunity` and `SqmsBid`.
- [x] Confirm the query returns zero rows, proving the seed behavior is absent.

### Task 2: Add the transactional seed

**Files:**
- Create: `sql/sqms/001_seed_sqms_transition_metadata.sql`

- [x] Add schema preflight checks for the six required metadata tables.
- [x] Insert or safely refresh the two definitions and version 1 rows.
- [x] Stage and upsert the exact Opportunity and Bid states, roles, and actions.
- [x] Stage all 31 approved Opportunity and 9 approved Bid transitions.
- [x] Fail on conflicting destinations or unexpected active transitions.
- [x] Commit atomically and return row-count summaries.

### Task 3: Add independent SQL verification

**Files:**
- Create: `sql/sqms/002_verify_sqms_transition_metadata.sql`
- Create: `sql/sqms/003_validate_sqms_seed_in_rollback.sql`

- [x] Resolve both version 1 identifiers.
- [x] Compare expected and actual active states, roles, actions, and transitions in both directions.
- [x] Fail on a missing or unexpected row.
- [x] Run the seed twice plus verification inside an outer transaction and roll it back.

Run:

```powershell
sqlcmd -S DOLLY -d workflowengine -E -b -i .\sql\sqms\003_validate_sqms_seed_in_rollback.sql
```

Expected: both workflows report their expected counts and the final message confirms the outer transaction was rolled back.

Approved revision: verification expects 31 Opportunity transitions, including `Pending -> Draft` for IPG and 16 IPG/System cancellation rows. It expects zero active MFO cancellation rows. Bid remains unchanged at 9 transitions.

### Task 4: Run regression gates and record the handoff

**Files:**
- Modify: `CODEX_HANDOFF.md`

- [x] Run `git diff --check`.
- [x] Run the warnings-as-errors solution build.
- [x] Run the full default test suite.
- [x] Re-run the rollback SQL validation after application tests.
- [x] Record changed files, exact commands/results, risks, and the next iteration in `CODEX_HANDOFF.md` without disturbing existing user-owned changes.
