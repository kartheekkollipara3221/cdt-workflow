# Phase 3: Workflow Contracts and Console Actions - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 3 delivers the workflow contract layer and console command parsing/output for form-open and button-click scenarios. It should add `WorkflowActionEnvelope`, `WorkflowContextResponse`, `AvailableWorkflowAction`, and `WorkflowActionResult` DTOs; implement full non-database envelope validation; apply console override normalization deterministically; make `show-context` read persisted SQL workflow state/task/metadata to return a useful contract JSON response; and make `execute-json` parse, validate, normalize, and print contract-shaped results without executing workflow transitions or writing side effects that belong to Phase 4.

</domain>

<decisions>
## Implementation Decisions

### Command Behavior Boundary
- **D-01:** `show-context` should be useful in Phase 3: it reads SQL-backed `WorkflowInstance`, current pending `WorkflowTask`, workflow metadata, actor arguments, and SLA status to return a real `WorkflowContextResponse`.
- **D-02:** `execute-json` should not execute transitions in Phase 3. It parses, applies overrides, validates, normalizes, and returns a contract-shaped result, but task close/create, state movement, audit, notification outbox writes, idempotency replay persistence, and persisted concurrency checks stay in Phase 4.
- **D-03:** `show-context` remains read-only. It must not move workflow state, create/close tasks, write audit rows, save mocked section data, or queue notifications.

### DTO Validation Strictness
- **D-04:** Implement full contract validation now for rules that do not require workflow execution or persisted state checks.
- **D-05:** Phase 3 validation includes required actor/action/workflow/business object/dataSave/idempotency/concurrency fields, non-empty `actor.userId`, non-empty `actor.roles`, required `dataSave.sectionKey`, required `dataSave.payload`, and structured `ValidationFailed` results.
- **D-06:** Implement MakerPrimary consistency validation in Phase 3: `SaveDraft` requires `uiAction.saveAsDraft = true`; `SubmitForChecker` and `ResubmitForChecker` require `uiAction.saveAsDraft = false`.
- **D-07:** Defer authorization, transition validity, action existence against current state, role ambiguity, persisted workflow definition version checks, persisted task/state/version concurrency checks, and all side-effect rollback behavior to Phase 4.

### Console Output Style
- **D-08:** Use mixed output defaults. `show-context` defaults to JSON because it is the form-open/API contract object.
- **D-09:** `execute-json` defaults to readable console text for operator ergonomics, with `--format json` available to print the contract-shaped `WorkflowActionResult`.
- **D-10:** JSON output should be stable enough for tests: predictable property names, no omitted required contract blocks, and `TimeProvider` for generated timestamps.

### Override and Idempotency Normalization
- **D-11:** Implement full deterministic normalization now for console overrides and request hashing.
- **D-12:** Apply `--business-object` and `--workflow-instance` overrides after parsing but before validation, idempotency key derivation, request hashing, or result construction.
- **D-13:** Derive effective `clientActionId`, `idempotencyKey`, and `correlationId` consistently after overrides so reused sample files can target different business objects without collisions.
- **D-14:** Compute a stable canonical request hash from the effective request in Phase 3 and expose it for Phase 4 idempotency persistence. The hash logic must avoid property-order instability and must not treat pre-override and post-override envelopes as equivalent.

### the agent's Discretion
The agent may choose exact namespace/file organization, validator class names, and JSON serialization helpers, provided the public DTO shapes match `PROJECT.md` and existing generic boundaries are preserved.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` section 8 - `WorkflowContextResponse`, `AvailableWorkflowAction`, MakerPrimary grouping, actor access, SLA block, and request defaults.
- `PROJECT.md` section 9 - `WorkflowActionEnvelope`, validation rules, actor handling, concurrency guards, dataSave requirements, idempotency semantics, and canonical request hashing.
- `PROJECT.md` section 10 - console command definitions for `execute-json`, `show-context`, flags, override behavior, and output expectations.
- `PROJECT.md` section 12 - SQL model consumed by `show-context` and later execution.
- `PROJECT.md` section 13 - SLA block behavior and `TimeProvider` requirement for current time.
- `PROJECT.md` section 15 - transaction/concurrency notes that define what Phase 3 should model but not execute.
- `PROJECT.md` section 20 - required tests for envelope validation, workflow context, command parsing, and output shapes.
- `PROJECT.md` section 21 - quality gates.
- `PROJECT.md` section 22 - stop-and-ask triggers for contract shape changes.
- `PROJECT.md` section 28 - pitfall checklist for contract confusion, MakerPrimary, stale forms, overrides, and display name behavior.

### GSD Planning
- `.planning/PROJECT.md` - validated project context and phase history.
- `.planning/REQUIREMENTS.md` - Phase 3 requirements `CON-01`, `CON-02`, `CON-03`, and `CON-04`.
- `.planning/ROADMAP.md` - Phase 3 goal and success criteria.
- `.planning/STATE.md` - current project state and build note (`-m:1`).
- `.planning/phases/01-solution-skeleton/01-01-SUMMARY.md` - command-routing and configuration baseline.
- `.planning/phases/02-sql-schema-and-seed-data/02-01-SUMMARY.md` - SQL schema, seed, and verification baseline.
- `AGENTS.MD` - repository-local completion format and GSD guardrails.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WorkflowEngine.Application/Commands/CommandRegistry.cs` - current command dispatch point; `execute-json` and `show-context` are recognized but still return stubs.
- `src/WorkflowEngine.Application/Commands/CommandResult.cs` - simple result wrapper for command exit codes/messages.
- `src/WorkflowEngine.Console/Program.cs` - composition root with configuration, `TimeProvider.System`, command registry, and infrastructure services.
- `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` - SQL-backed metadata/runtime DbSets used by `show-context`.
- `src/WorkflowEngine.Infrastructure/Persistence/Entities/*.cs` - generic runtime and metadata entity model created in Phase 2.
- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` - canonical seeded metadata keys useful for tests and expected context behavior.
- `tests/WorkflowEngine.Tests` - xUnit test project with command, configuration, schema, and SQL-backed seed tests.

### Established Patterns
- Keep application/domain boundaries generic; Portfolio Trade belongs in seed data, samples, and tests only.
- Use `TimeProvider` for current timestamps; do not introduce direct `DateTime.UtcNow` in services.
- SQL-backed tests are valid for persistence checks, but local SQL Server access may require running outside the default sandbox.
- Use `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` and `dotnet test src\WorkflowEngine.sln -m:1` for reliable local verification.

### Integration Points
- Phase 3 should add DTOs and validation in the application layer, SQL read/query services in infrastructure where needed, and console argument parsing in the command path.
- `show-context` should query `WorkflowDbContext` read-only.
- `execute-json` should parse files and flags through the command path, then call reusable validation/normalization services.

</code_context>

<specifics>
## Specific Ideas

- Treat `WorkflowContextResponse` as the canonical form-open object; do not confuse it with the button-click `WorkflowActionEnvelope`.
- `show-context` JSON should include actor access and SLA blocks even when no action is available or no SLA exists.
- `actorAccess.displayName` defaults to the `--user` value; do not add a user lookup table or `--display-name` flag.
- `execute-json` can return a `WorkflowActionResult` shape that clearly says execution is deferred when the envelope is valid but Phase 4 transition execution is not implemented yet.
- Hash/normalization code should be reusable by Phase 4 rather than replaced.

</specifics>

<deferred>
## Deferred Ideas

- Transition resolution, authorization against current state/role metadata, task close/create, state movement, audit writing, notification outbox writes, persisted idempotency replay behavior, and persisted concurrency checks belong to Phase 4.
- SLA overdue scanning and notification sending belong to Phase 5.
- Required sample JSON files and end-to-end demo flow belong to Phase 6 unless needed as focused Phase 3 parsing fixtures.

</deferred>

---

*Phase: 3-Workflow Contracts and Console Actions*
*Context gathered: 2026-05-07*

