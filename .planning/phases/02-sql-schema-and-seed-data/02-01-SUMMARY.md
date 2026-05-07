# Phase 2 Plan 01 Summary: SQL Schema and Seed Data

**Completed:** 2026-05-07
**Status:** Complete

## Changed

- Added generic EF Core entities and DbContext mappings for workflow definitions, versions, states, actions, roles, task definitions, transitions, workflow instances, workflow tasks, mocked section data, audit, SLA policies, notification routes, notification outbox, and idempotency records.
- Added the `InitialWorkflowSchema` EF migration and local `dotnet-ef` tool manifest.
- Added deterministic Portfolio Trade v1 seed data for required states, roles, actions, tasks, SLA policies, notification routes, and all 11 transition rows.
- Added SQL-backed `init-db` and `verify-seed` command behavior.
- Added seed verification guardrails for exact matrix content, version-scoped key references, notification/SLA resolution, action hints, no `AdminDemoOverride`, and no Maker rework SLA.
- Added tests for command dispatch, required unique/filtered indexes, and SQL Server-backed Portfolio Trade seed verification.

## Requirements Covered

- SQL-01: `init-db` applies migrations and seeds Portfolio Trade metadata.
- SQL-02: `verify-seed` validates required seed keys, matrix rows, routes, SLA references, and constraints.
- SQL-03: EF schema includes required unique constraints and filtered indexes for single pending task and SLA-overdue duplication.
- SQL-04: Runtime persistence entities exist for instances, tasks, SLA, notification outbox, audit, idempotency, and mocked section data.
- PT-01: Portfolio Trade states, roles, actions, tasks, and transition matrix match the Phase 2 seed contract.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --no-build` passed outside the sandbox with local SQL Server access.
- `dotnet run --no-build --project src\WorkflowEngine.Console -- init-db` passed.
- `dotnet run --no-build --project src\WorkflowEngine.Console -- verify-seed` passed.
- `rg "AdminDemoOverride" src\WorkflowEngine.Infrastructure\Persistence\Seed src\WorkflowEngine.Infrastructure\Persistence\Verification` confirms it appears only in verification guard code, not seed data.
- `rg "PortfolioTrade" src\WorkflowEngine.Application src\WorkflowEngine.Domain` returned no matches.
- `rg "ResubmitForChecker|CheckerReturnToMaker|TradingReturnToMaker" src\WorkflowEngine.Infrastructure\Persistence\Seed tests\WorkflowEngine.Tests` confirmed required return/resubmit rows are present in seed/test coverage.

## Notes

- SQL-backed tests failed inside the default sandbox with `Cannot generate SSPI context` but passed when rerun with local SQL Server access approved.
- Later workflow execution commands remain intentional stubs.

