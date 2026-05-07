# Phase 1: Solution Skeleton - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 1 delivers only the buildable .NET solution skeleton: project structure, console command routing stubs, configuration loading, EF Core wiring, and test project baseline. It does not implement workflow transitions, SQL seed data, JSON execution behavior, SLA logic, notification processing, or demo samples beyond clear stubs and extension points needed by later phases.

</domain>

<decisions>
## Implementation Decisions

### Solution Shape
- **D-01:** Use a conventional multi-project .NET solution with clear boundaries: console entry point, application orchestration/contracts, domain workflow concepts, infrastructure persistence/configuration, and tests.
- **D-02:** Keep the skeleton generic from day one. Names may mention the workflow engine, but project boundaries and core abstractions must not be named around Portfolio Trade behavior.
- **D-03:** Enable nullable reference types and warnings-as-errors at the shared project level so later phases inherit strict compile behavior.

### Console Command Surface
- **D-04:** Create command routing for every command required by repository-root `PROJECT.md` section 10, including `init-db`, `verify-seed`, `start-workflow`, `execute-json`, `show-context`, `show`, `show-tasks`, `show-history`, `show-notifications`, `run-sla`, `process-notification-outbox`, and `demo`.
- **D-05:** Commands not implemented in Phase 1 should return intentional stub output with the command name and a concise "not implemented yet" message, rather than failing with unknown-command behavior.
- **D-06:** Keep CLI parsing simple and dependency-light in Phase 1. Do not add a command framework unless the code becomes materially clearer than a small local router.

### Configuration and EF Baseline
- **D-07:** Wire configuration through the standard .NET configuration stack with `ConnectionStrings:DefaultConnection`; do not embed the local SQL Server connection string in domain, application, or infrastructure workflow logic.
- **D-08:** Include `appsettings.json` and a local-development configuration path compatible with `appsettings.Development.json` and `ConnectionStrings__DefaultConnection`.
- **D-09:** Add EF Core wiring and a placeholder DbContext only far enough for the solution to compile and later migrations to attach cleanly. Do not design the full schema in Phase 1.

### Test Baseline
- **D-10:** Create test projects and at least smoke coverage for command routing/configuration behavior that Phase 1 introduces.
- **D-11:** Keep tests SQL-aware but do not require a live SQL database in Phase 1 unless the implemented behavior actually opens a connection.

### the agent's Discretion
The agent may choose exact project names, namespaces, and test framework details if they preserve the boundaries above and match the root `PROJECT.md` constraints. Prefer plain .NET conventions over extra packages.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` - authoritative full project requirements, transition matrix, console commands, quality gates, milestones, pitfall checklist, and success definition.
- `PROJECT.md` section 10 - required console command inventory.
- `PROJECT.md` section 21 - quality gates to run before marking work complete.
- `PROJECT.md` section 22 - stop-and-ask triggers.
- `PROJECT.md` section 24 - milestone structure that Phase 1 maps to.
- `PROJECT.md` section 28 - pitfall review and guardrails.

### GSD Planning
- `.planning/PROJECT.md` - project context and core value.
- `.planning/REQUIREMENTS.md` - Phase 1 requirement IDs `FND-01`, `FND-02`, and `FND-03`.
- `.planning/ROADMAP.md` - Phase 1 goal and success criteria.
- `.planning/STATE.md` - current project state and guardrails.
- `AGENTS.MD` - repo-local Codex/GSD instructions and required completion summary format.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None yet. The repository currently contains planning/project documentation and no implementation tree.

### Established Patterns
- `PROJECT.md` and `AGENTS.MD` establish milestone-driven execution, generic workflow-engine boundaries, warnings-as-errors, `TimeProvider`, SQL Server, EF Core, and no over-engineered frameworks.

### Integration Points
- New implementation should create the initial `src/` and test structure. Later phases will attach schema, workflow execution, SLA, notification, samples, and demo commands to the command router created in this phase.

</code_context>

<specifics>
## Specific Ideas

- Treat repository-root `PROJECT.md` as the contract when there is any conflict with generated planning artifacts.
- Phase 1 should stop after skeleton, routing, configuration, EF wiring, and tests compile. It should not pull Phase 2 schema/seed work forward.

</specifics>

<deferred>
## Deferred Ideas

- SQL schema, EF migrations, and seed data belong to Phase 2.
- JSON contract execution, `show-context` real output, and mocked section persistence belong to Phase 3 and Phase 4.
- SLA and notification sender behavior belongs to Phase 5.
- Samples, demo scripts, and README completion belong to Phase 6.

</deferred>

---

*Phase: 1-Solution Skeleton*
*Context gathered: 2026-05-07*
