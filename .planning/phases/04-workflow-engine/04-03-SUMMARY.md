---
phase: 04-workflow-engine
plan: 03
subsystem: workflow-execution-validation
tags: [dotnet, ef-core, idempotency, validation, concurrency]
provides:
  - SQL-backed execute-json validation pipeline
  - idempotency preflight replay/conflict
  - concurrency guard ordering
  - role authorization, ambiguity, and admin override checks
requirements-completed: [ENG-01, ENG-02, ENG-03, ENG-06, ENG-07, ENG-08]
completed: 2026-05-07
---

# Phase 4 Plan 03: Persisted Execution Validation Summary

## Accomplishments

- Added `IWorkflowExecutionService` and SQL-backed `WorkflowExecutionService`.
- Routed `execute-json` through persisted validation after envelope validation and single request-hash computation.
- Added `--admin-override` parsing and role-bypass behavior for authorization/ambiguity only.
- Implemented idempotency preflight replay/conflict before instance, version, transition, concurrency, or authorization checks.
- Implemented the locked persisted validation priority through the side-effect boundary.
- Hardened `CanonicalRequestHasher` to compact canonical JSON and role-array order preservation.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build --filter FullyQualifiedName~WorkflowExecution`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build --filter "FullyQualifiedName~WorkflowExecution|FullyQualifiedName~WorkflowActionEnvelopeNormalization"`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build`

## Notes

- SQL-backed tests require running outside the sandbox for Windows authentication.
- Successful execution currently returns a validation-passed `Success` without side effects; Plan 04-04 replaces that boundary with transition execution, data save, audit, task, notification, idempotency insert, and refreshed context.
