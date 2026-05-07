---
status: complete
phase: 03-workflow-contracts-and-console-actions
source:
  - 03-01-SUMMARY.md
  - 03-02-SUMMARY.md
started: 2026-05-07T15:50:00Z
updated: 2026-05-07T15:55:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Valid Execute JSON Contract
expected: Running `execute-json <valid-envelope> --format json` should return JSON with `result.success=true`, `result.resultCode=AcceptedForPhase3Validation`, an effective idempotency key, a request hash, and explicit no-side-effect markers such as `notification.queued=false` and `audit.written=false`.
result: skipped
reason: Will test in later phases.

### 2. Invalid Envelope Validation
expected: Running `execute-json <invalid-envelope> --format json` should return JSON with `result.success=false`, `result.resultCode=ValidationFailed`, and readable validation messages for missing or inconsistent actor/action/workflow/data-save fields.
result: skipped
reason: Will test in later phases.

### 3. Execute JSON Overrides
expected: Running `execute-json <valid-envelope> --business-object <id> --workflow-instance <id> --format json` should apply those override ids before validation/result construction and produce a deterministic request hash for the effective request.
result: skipped
reason: Will test in later phases.

### 4. Checker Show Context
expected: Running `show-context <workflowInstanceId> --user checker1 --roles Checker --format json` against a prepared SQL workflow instance should return `workflowContext` JSON with workflow state, business object, current pending task, actor access, SLA status, and Checker metadata-derived available actions.
result: skipped
reason: Will test in later phases.

### 5. Wrong-Role Show Context
expected: Running `show-context <workflowInstanceId> --user maker1 --roles Maker --format json` for a Checker-owned pending task should still return workflow and task context, but `actorAccess.canAct=false`, `availableActions=[]`, and a read-only reason that identifies the Checker assignment.
result: skipped
reason: Will test in later phases.

### 6. Show Context Read-Only Behavior
expected: Running `show-context` should not create or update workflow instances, tasks, audit records, mocked section data, or notification outbox rows.
result: skipped
reason: Will test in later phases.

## Summary

total: 6
passed: 0
issues: 0
pending: 0
skipped: 6
blocked: 0

## Gaps

[none yet]
