# Phase 03 Plan 02 Summary: SQL-Backed Workflow Context

## Status
Complete

## Completed Work
- Added the form-open `WorkflowContextResponse` contract and `AvailableWorkflowAction` request defaults.
- Implemented a SQL-backed, read-only `IWorkflowContextReader` that loads workflow instance state, current pending task, metadata-derived actions, actor role access, and SLA status.
- Replaced the `show-context` stub with a DI-registered command handler.
- Made `show-context <workflowInstanceId> --user <userId> --roles <roles>` return JSON by default.
- Preserved null JSON fields in shared workflow serialization so required contract fields remain stable.
- Added serialization, SQL read model, command dispatch, wrong-role, no-SLA, overdue, completed workflow, unknown instance, and read-only row-count tests.

## Decisions Applied
- `show-context` is useful in Phase 3 and reads persisted SQL state and metadata.
- `show-context` remains read-only; tests assert runtime row counts are unchanged.
- `show-context` defaults to JSON and supports `--format json`.
- SLA status is computed from `WorkflowTask.DueAtUtc` with `TimeProvider`.
- Actor display name is populated from the supplied user id.

## Verification
- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build` passed: 62 tests.
- `dotnet run --no-build --project src\WorkflowEngine.Console -- show-context WF-PT-CTX-CHECKER --user checker1 --roles Checker --format json` returned valid `WorkflowContextResponse` JSON against the SQL test database.
- `dotnet run --no-build --project src\WorkflowEngine.Console -- verify-seed` passed.
- `dotnet run --no-build --project src\WorkflowEngine.Console -- execute-json <valid sample> --format json` returned `AcceptedForPhase3Validation`.

## Requirements Covered
- `CON-02`: form-open context response from SQL workflow state and metadata.
- `CON-04`: `show-context` console command emits the context contract as JSON.

## Follow-Up
- Phase 4 should consume the same request defaults and context snapshot concepts when implementing actual transition execution and persisted concurrency checks.
