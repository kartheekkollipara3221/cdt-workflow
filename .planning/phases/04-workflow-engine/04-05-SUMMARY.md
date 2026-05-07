---
phase: 04-workflow-engine
plan: 05
subsystem: workflow-inspection-commands
tags: [dotnet, ef-core, console, uat-gap]
provides:
  - show command implementation
  - show-tasks command implementation
  - show-history command implementation
  - show-notifications command implementation
requirements-completed: [ENG-03, ENG-04, ENG-05, PT-03, PT-04]
completed: 2026-05-07
---

# Phase 4 Plan 05: Inspection Command Gap Closure Summary

## Accomplishments

- Added a read-only workflow inspection abstraction and EF Core implementation over workflow instances, tasks, audit history, and notification outbox rows.
- Implemented `show`, `show-tasks`, `show-history`, and `show-notifications` command handlers.
- Routed the four commands through `CommandRegistry` and registered them in console DI.
- Added tests proving the commands return useful data, fail for missing instances, and no longer use the placeholder "recognized but not implemented yet" response.
- Resolved the phase 4 UAT gap that blocked Checker Return to Maker verification.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1`
- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1 --no-restore`
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- execute-json samples\workflow-actions\02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- execute-json samples\workflow-actions\03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show WF-PT-1002`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show-tasks WF-PT-1002`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show-notifications WF-PT-1002`
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show-history WF-PT-1002`

## Notes

- Commands are read-only and use existing runtime tables.
- No SQL schema or workflow metadata changes were required.
- Portfolio Trade behavior remains sample data only; the new inspection path reads generic workflow runtime records.
