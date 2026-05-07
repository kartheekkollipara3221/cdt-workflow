---
phase: 04-workflow-engine
plan: 01
subsystem: workflow-result-contract
tags: [dotnet, json, idempotency, result-contract]
provides:
  - Stable WorkflowActionResultEnvelope result/workflowContext shape
  - WorkflowActionResultBuilder centralized construction
  - Canonical WorkflowActionResult JSON serialization and hash
requirements-completed: [ENG-03, ENG-06, ENG-07]
completed: 2026-05-07
---

# Phase 4 Plan 01: Result Contract Foundation Summary

## Accomplishments

- Extended `WorkflowActionResultEnvelope` with top-level `workflowContext`.
- Added flat `expected`, `actual`, `failedGuard`, `diagnostics`, and `existing*` result fields.
- Added `WorkflowActionResultBuilder` as the construction point for Phase 4 success and failure result shapes.
- Added canonical compact result serialization with deterministic property ordering and SHA-256 result hashing that excludes `diagnostics`.
- Routed the existing deferred `execute-json` Phase 3 path through the builder.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build`

## Notes

- Existing Phase 3 `AcceptedForPhase3Validation` behavior is preserved until later Phase 4 plans replace deferred execution with SQL-backed workflow transitions.
- `requestHash` is still mirrored on the legacy top-level field while the new canonical location is `result.diagnostics.requestHash`; later plans can remove or ignore the legacy field after callers move.
