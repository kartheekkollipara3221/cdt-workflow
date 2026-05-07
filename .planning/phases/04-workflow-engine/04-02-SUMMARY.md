---
phase: 04-workflow-engine
plan: 02
subsystem: workflow-start-reset
tags: [dotnet, ef-core, sql-server, console]
provides:
  - start-workflow command
  - SQL-backed workflow start service
  - bounded transactional reset behavior
requirements-completed: [ENG-08, PT-04]
completed: 2026-05-07
---

# Phase 4 Plan 02: Start Workflow And Reset Summary

## Accomplishments

- Added `start-workflow <workflowKey> <businessObjectId> --user <userId> [--reset] [--format json]`.
- Implemented read-then-write `AlreadyExists` detection with existing instance details.
- Implemented fresh starts as a single `WorkflowInstance` insert in `Draft` with no task, audit, notification, mock data, or idempotency rows.
- Implemented bounded `--reset` runtime cleanup in the locked delete order, followed by fresh instance insert in one transaction.
- Wired the command through DI and `CommandRegistry`.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build --filter FullyQualifiedName~StartWorkflow`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-PLAN4 --user maker1 --reset`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show-context WF-PT-PLAN4 --user maker1 --roles Maker --format json`

## Notes

- The SQL-focused test command initially hit sandbox SSPI authentication and passed when rerun outside the sandbox.
- The console smoke confirmed `Draft`, `currentTask = null`, `sla.status = NoSla`, and Maker actions `SaveDraft` / `SubmitForChecker`.
