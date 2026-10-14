# C06 SLA schedule failure isolation

Status: complete; two pre-existing clean-build gate failures are documented in SUMMARY.

## Scope and decisions

The user approved the C06-only design on 2026-10-14. Keep active workflows,
schema, public contracts, dependency registrations, reminder timing/content,
recipient snapshots and occurrence uniqueness unchanged. No production data
repair, workflow closure, scheduler installation or mail delivery is authorized
or needed. Execute inline in the existing checkout; preserve unrelated work.

## Implementation plan

- [x] Add SQL-backed regression cases to `tests/WorkflowEngine.Tests/Phase5SlaTests.cs`:
  a bad schedule precedes a valid one; missing metadata, missing/inactive route,
  invalid time zone, DST gap, and next-occurrence overflow. Check rollback,
  unchanged failed schedule, warning identity, healthy mail content and replay.
- [x] Add failure-injection tests at SaveChanges to prove cancellation, database
  errors and unexpected InvalidOperationException still propagate.
- [x] Run the new tests before implementation and record the expected failures.
- [x] Update only `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs`:
  explicitly identify invalid schedule metadata and absent active routes;
  translate known calendar exceptions only around calendar calculations;
  catch only that private schedule-data exception at the per-row transaction;
  roll back, clear tracked state, append an identifying warning, continue.
  Existing database concurrency/uniqueness handlers stay in place.
- [x] Run focused SLA tests, then sequential core/API regression suites, builds,
  SQL seed verification and supported console samples on dedicated test databases.
  Preserve package versions and warnings-as-errors; report any existing gate failures.
- [x] Review the final diff for error boundaries and compatibility. Update SUMMARY,
  STATE and CODEX_HANDOFF; commit only this task's owned source/tests/planning files.

## Commands and expected evidence

Set `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` to the approved local integrated
security server. Test factories replace InitialCatalog with test database names.

`dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~C06`

Expected before fix: metadata/calendar/route cases throw and stop the scan.
Expected after fix: every new case passes; fatal failures still escape.

`dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter Category!=Smtp`

`dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1`

Run builds with warnings as errors. Check seed/sample commands under an isolated
local verification database with Console/NoOp notifications. Do not run init-db
against the application database. No claim of deployment or production reproduction.
