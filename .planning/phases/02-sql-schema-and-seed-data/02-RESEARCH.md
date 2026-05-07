# Phase 2: SQL Schema and Seed Data - Research

**Date:** 2026-05-07
**Mode:** Inline fallback; GSD planning agents are not installed in this environment.

## Implementation Approach

Phase 2 should extend the Phase 1 skeleton without changing the reusable-engine boundary:

- Keep entities, DbContext mappings, seed services, migrations, and SQL verification generic.
- Keep Portfolio Trade only as deterministic seed data and focused seed tests.
- Replace only the `init-db` and `verify-seed` command stubs with real SQL-backed handlers.
- Leave JSON envelope parsing, transition execution, SLA scanning, notification sending, and demo flows to later phases.

## Schema Scope

Implement the required section 12 concepts:

- `WorkflowDefinition`
- `WorkflowDefinitionVersion`
- `WorkflowStateDefinition`
- `WorkflowActionDefinition`
- `WorkflowRoleDefinition`
- `WorkflowTaskDefinition`
- `WorkflowTransitionDefinition`
- `WorkflowInstance`
- `WorkflowTask`
- `MockSectionData`
- `WorkflowAudit`
- `SlaPolicy`
- `NotificationRoute`
- `NotificationOutbox`
- `IdempotencyRecord`

EF Core mappings should enforce the required uniqueness:

- Unique definition keys scoped by workflow definition version.
- Unique transitions by `(WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey)`.
- Unique workflow instances by `WorkflowInstanceId`.
- Unique workflow lifetime by `(WorkflowKey, BusinessObjectType, BusinessObjectId)`.
- Filtered unique pending-task index on `WorkflowTask(WorkflowInstanceId)` with literal filter `Status = 'Pending'`.
- Filtered unique SLA-overdue index on `NotificationOutbox(WorkflowTaskId, NotificationType)` with filter `NotificationType = 'SlaTaskOverdue'`.
- Unique idempotency key.

Composite foreign keys for transition key fields, task SLA policy keys, and notification route keys are intentionally not required. `verify-seed` must validate those references by `(WorkflowDefinitionVersionId, key)`.

## Seed Scope

Seed exactly one active Portfolio Trade workflow definition and active version `1`.

Seed:

- States: `Draft`, `ReturnedToMaker`, `CheckerReview`, `TradingDeskReview`, `TradingDeskRework`, `BdrmClientServiceReview`, `Completed`.
- Roles: `Maker`, `Checker`, `TradingDesk`, `BDRMClientService`.
- Actions required by the section 7 matrix, including `ResubmitForChecker`, `CheckerReturnToMaker`, and `TradingReturnToMaker`.
- Task definitions: `CheckerReviewTask`, `TradingDeskReviewTask`, `TradingDeskReworkTask`, `BdrmClientServiceReviewTask`, `MakerReworkTask`.
- SLA policies for every SLA-enabled task; `MakerReworkTask` keeps `SlaPolicyKey = null`.
- All 11 transition rows from section 7.
- Exactly the notification routes from section 11, plus `RouteSlaTaskOverdue`.

`AdminDemoOverride` must not appear in seeded roles or transition `AllowedRoleKey`.

## Command Scope

`init-db`:

- Uses configured SQL Server through `ConnectionStrings:DefaultConnection`.
- Applies migrations.
- Runs the deterministic seed service.
- Does not delete runtime data or reset demo rows.

`verify-seed`:

- Reads from SQL Server.
- Validates required keys and exact matrix content.
- Validates transition key references, task owner roles, SLA policy lookup, notification route lookup, action presentation hints, Maker button hints, duplicate transition assumptions, and notification route completeness.
- Returns non-zero with clear messages if validation fails.

## Testing Scope

Add tests for:

- EF Core model metadata for required unique and filtered indexes.
- Portfolio Trade seed data shape using SQL-backed EF Core database provider.
- `verify-seed` success after `init-db`.
- Negative validation where practical by mutating seeded rows in a test database.

Tests must not use EF Core InMemory for persistence/seed verification. Use the configured SQL Server connection or a SQL Server LocalDB/database-name suffix strategy if available. If the local SQL Server is unavailable in this sandbox, document the exact failure and keep non-connection model tests passing.

## Risks

- EF migration tooling may not be installed. If `dotnet ef` is unavailable, add a local tool manifest and `Microsoft.EntityFrameworkCore.Design`; installing the tool may require network approval.
- The local SQL Server named in development config may be unavailable from the sandbox. Build and non-DB tests should still pass, but Phase 2 cannot be fully claimed without `init-db` and `verify-seed` passing against SQL Server.
- Parallel MSBuild restore/build failed in Phase 1. Continue using `-m:1` for reliable verification.

