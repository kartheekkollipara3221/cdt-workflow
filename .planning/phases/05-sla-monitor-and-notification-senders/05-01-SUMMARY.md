# 05-01 Summary: SLA Monitor

## What Changed

- Added `IRunSlaService`, `RunSlaRequest`, `RunSlaResult`, and per-task queue result records.
- Implemented SQL-backed `RunSlaService` that scans active pending tasks with non-null `DueAtUtc`, applies strict `< now` overdue logic, suppresses any existing `SlaTaskOverdue`, and queues one outbox row per overdue task.
- Added `run-sla` command handler and command registry/DI wiring.
- Added SQL-backed SLA tests for boundary behavior, null-SLA exclusion, completed task/workflow exclusion, deterministic ordering, duplicate suppression, and rerun idempotency.

## Key Files

- `src/WorkflowEngine.Application/Sla/IRunSlaService.cs`
- `src/WorkflowEngine.Application/Commands/IRunSlaCommandHandler.cs`
- `src/WorkflowEngine.Application/Commands/RunSlaCommandHandler.cs`
- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`
- `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror`
- `dotnet test src\WorkflowEngine.sln`
- `dotnet run --project src\WorkflowEngine.Console -- run-sla --now 2030-01-15T16:00:00Z`
- Consecutive `run-sla` smoke confirmed existing sent/queued `SlaTaskOverdue` rows suppress duplicates.
