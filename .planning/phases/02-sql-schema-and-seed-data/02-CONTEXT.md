# Phase 2: SQL Schema and Seed Data - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 2 delivers the SQL-backed workflow metadata/runtime persistence baseline: EF Core entities, DbContext mappings, migrations, deterministic Portfolio Trade seed data, and real `init-db` / `verify-seed` command behavior. It should prove schema constraints and seed correctness, but it should not implement workflow action execution, JSON envelopes, workflow context responses, SLA scanning, notification sending, sample JSON execution, or demo scripts beyond rows/schema needed by later phases.

</domain>

<decisions>
## Implementation Decisions

### Schema Model
- **D-01:** Implement the section 12 SQL/domain model as generic workflow entities in infrastructure/domain boundaries: workflow definitions, versions, state/action/role/task definitions, transition definitions, SLA policies, notification routes, workflow instances, workflow tasks, mock section data, audit rows, notification outbox rows, and idempotency records.
- **D-02:** Keep SQL table/entity names generic. Portfolio Trade appears only in seed data and tests, never as a workflow-engine service or entity type.
- **D-03:** Apply required uniqueness from `PROJECT.md` section 12, including definition-key uniqueness, `(WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey)` transition uniqueness, unique `(WorkflowKey, BusinessObjectType, BusinessObjectId)` workflow lifetime, unique `IdempotencyKey`, and unique SLA overdue notification protection.
- **D-04:** Use the exact v1 status strings required by the project contract: `WorkflowInstance.Status` values are `Active` and `Completed`; `WorkflowTask.Status` values are `Pending` and `Completed`; filtered pending-task index literal must be `WHERE Status = 'Pending'`.

### Seed Data
- **D-05:** Seed Portfolio Trade workflow version 1 deterministically through an idempotent seed service invoked by `init-db`.
- **D-06:** Seed every state, role, action, task definition, SLA policy, transition, and notification route required by `PROJECT.md` sections 7 and 11, including `ResubmitForChecker`, Checker Return to Maker, Trading Desk Return to Maker, and BDRM Trading Desk Rework paths.
- **D-07:** Do not seed `AdminDemoOverride` as a workflow role and do not seed any transition using it as `AllowedRoleKey`.
- **D-08:** Maker rework stays no-SLA in v1: `MakerReworkTask` has a null SLA policy and produces null `DueAtUtc` when created later.

### Command Behavior
- **D-09:** Replace the Phase 1 stubs for `init-db` and `verify-seed` with real command handlers that use configured SQL Server via `ConnectionStrings:DefaultConnection`.
- **D-10:** `init-db` applies migrations and seed data; it must not delete runtime data or behave like demo reset.
- **D-11:** `verify-seed` must validate key existence, full transition matrix, notification route resolution, SLA policy resolution, uniqueness/constraint assumptions, and referential consistency that is intentionally not enforced by composite SQL foreign keys.

### Testing and Verification
- **D-12:** Add SQL-backed tests for migrations/schema and seed verification; do not use EF Core InMemory as a substitute for seed or persistence verification.
- **D-13:** Keep Phase 2 tests focused on schema/seed/constraints. Transition execution side effects belong to Phase 4 even though Phase 2 stores the tables those side effects will use.
- **D-14:** Continue using `TimeProvider` injection for timestamp-capable seed/runtime infrastructure; do not introduce direct `DateTime.UtcNow` usage in services.

### the agent's Discretion
The agent may choose exact CLR property names and EF mapping organization if the SQL behavior, constraints, seed keys, and command contracts remain faithful to `PROJECT.md`. Use pragmatic EF Core configuration, not a new framework.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` section 4.1 - local SQL Server configuration and connection string source rules.
- `PROJECT.md` section 7 - mandatory Portfolio Trade states, roles, actions, transition matrix, and Return to Maker behavior.
- `PROJECT.md` section 10 - required console command behavior for `init-db`, `verify-seed`, and runtime command inventory.
- `PROJECT.md` section 11 - SQL seed data requirements and notification route seed table.
- `PROJECT.md` section 12 - suggested SQL/domain model and required uniqueness summary.
- `PROJECT.md` section 13 - SLA policy behavior relevant to task definitions and due dates.
- `PROJECT.md` section 14 - notification route/outbox fields and v1 recipient rules.
- `PROJECT.md` section 15 - transaction and concurrency constraints that schema must support.
- `PROJECT.md` section 16 - POC engineering discipline and simplicity rule.
- `PROJECT.md` section 20 - required tests, especially seed/constraint tests.
- `PROJECT.md` section 21 - quality gates for build, tests, `init-db`, and `verify-seed`.
- `PROJECT.md` section 22 - stop-and-ask approval triggers for schema/behavior expansion.
- `PROJECT.md` section 28 - pitfall checklist for schema, statuses, user-role mapping, audit, reset, and SMTP boundaries.

### GSD Planning
- `.planning/PROJECT.md` - project context and validated Phase 1 foundation.
- `.planning/REQUIREMENTS.md` - Phase 2 requirement IDs `SQL-01`, `SQL-02`, `SQL-03`, `SQL-04`, and `PT-01`.
- `.planning/ROADMAP.md` - Phase 2 goal and success criteria.
- `.planning/STATE.md` - current project state and known local build note (`-m:1`).
- `.planning/phases/01-solution-skeleton/01-01-SUMMARY.md` - Phase 1 skeleton outputs and established patterns.
- `AGENTS.MD` - repo-local Codex/GSD instructions and completion summary format.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` - empty compile-ready DbContext to extend with DbSets and model configuration.
- `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` - existing SQL Server registration using `ConnectionStrings:DefaultConnection`.
- `src/WorkflowEngine.Application/Commands/CommandRegistry.cs` - recognizes `init-db` and `verify-seed`; Phase 2 should route these two commands to real handlers while leaving later command stubs intact.
- `src/WorkflowEngine.Console/Program.cs` - builds configuration and DI; Phase 2 should add application/infrastructure services here.
- `tests/WorkflowEngine.Tests` - xUnit test baseline.

### Established Patterns
- Multi-project .NET 10 solution with `Directory.Build.props` strict compile settings.
- Generic workflow names and boundaries; no Portfolio Trade hardcoding outside seed/test data.
- Local verification should use `dotnet build src/WorkflowEngine.sln -warnaserror -m:1` and `dotnet test src/WorkflowEngine.sln -m:1` because parallel MSBuild restore/build failed silently in this sandbox.

### Integration Points
- Phase 2 adds EF entities/migrations under infrastructure/domain/application and command handlers wired into the existing console dispatch path.
- Later Phase 3/4 work will consume the persisted schema, so Phase 2 must avoid fake runtime behavior while making the storage model complete.

</code_context>

<specifics>
## Specific Ideas

- Treat `verify-seed` as a first-class guardrail, not just a smoke command. It should explain missing/mismatched seed rows clearly enough for tests and operators.
- Prefer deterministic upsert-style seeding keyed by workflow keys and version number so `init-db` is safe to rerun.
- Keep SQL constraints close to the contract, especially the single pending task filtered unique index and permanent one-lifetime business object uniqueness.

</specifics>

<deferred>
## Deferred Ideas

- Full `WorkflowActionEnvelope` parsing and validation belongs to Phase 3.
- Real transition execution, task movement, idempotency replay behavior, audit writing, and refreshed workflow context belong to Phase 4.
- SLA overdue scan and notification outbox sender processing belong to Phase 5.
- Demo scripts, sample JSON, and README completion belong to Phase 6.

</deferred>

---

*Phase: 2-SQL Schema and Seed Data*
*Context gathered: 2026-05-07*
