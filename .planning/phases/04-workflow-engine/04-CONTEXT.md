# Phase 4: Workflow Engine - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 4 implements the SQL-backed workflow engine for runtime actions. It replaces the Phase 3 non-executing `execute-json` path with metadata-driven execution: `start-workflow`, persisted validation, transition resolution, mock section data writes, task close/create, state/version updates, audit rows, notification outbox rows, idempotency replay/conflict handling, concurrency guards, admin override behavior, stable result JSON, and refreshed workflow context.

This phase must not implement SLA scanning or notification sending, which remain Phase 5. Portfolio Trade remains seed/sample metadata only.

</domain>

<decisions>
## Implementation Decisions

### Execution Transaction Semantics
- **D-01:** Every non-idempotent valid `execute-json` action uses one atomic transaction. Failed envelope validation, persisted validation, authorization, transition checks, concurrency checks, or idempotency conflicts write no mock data, audit, tasks, notifications, or idempotency record.
- **D-02:** `SaveDraft` saves one append-only `MockSectionData` row, writes one lightweight audit row, and writes one idempotency record. It does not move state, increment `WorkflowInstance.Version`, close/create tasks, evaluate SLA, or queue notifications.
- **D-03:** State-changing transitions execute in this order after all persisted validation passes: resolve metadata, save mock section data, close old task, move state and increment version, create new task, queue notification, write audit, write idempotency record.
- **D-04:** Readable `execute-json` output defaults to a concise transition summary: result code, workflow instance, old/new state, closed/created task, notification queued, audit written, and request hash.

### Validation and Result Codes
- **D-05:** Persisted validation uses this priority order and returns the first applicable failure:
  1. Envelope shape validation -> `ValidationFailed`
  2. Idempotency lookup -> stored replay or `IdempotencyConflict`
  3. Workflow instance loaded -> `WorkflowInstanceNotFound`
  4. Workflow definition version guard -> `WorkflowDefinitionVersionMismatch`
  5. Completed workflow guard -> `InvalidTransition` with message `Workflow is Completed; no actions are permitted.`
  6. Action exists in `WorkflowActionDefinition` -> `InvalidTransition`
  7. Transition exists for `(currentState, actionKey)` -> `InvalidTransition`
  8. State concurrency guard -> `ConcurrencyConflict`
  9. Task id concurrency guard -> `ConcurrencyConflict`
  10. Task key concurrency guard -> `ConcurrencyConflict`
  11. Instance version concurrency guard -> `ConcurrencyConflict`
  12. Role authorization -> `Forbidden`
  13. Ambiguous role match -> `AmbiguousRoleMatch`
  14. Execute
- **D-06:** Add `WorkflowInstanceNotFound` to the result-code model.
- **D-07:** Idempotency lookup is a SQL read before the main write transaction. A matching replay returns stored `ResultJson` before re-checking state, version, or authorization.
- **D-08:** Role authorization and ambiguity use one transition query filtered by workflow definition version, current state, action key, and `AllowedRoleKey IN actor.roles`.
  - Zero rows -> `Forbidden`
  - One row -> proceed
  - Multiple rows with distinct `AllowedRoleKey` -> `AmbiguousRoleMatch`, unless admin override is present
- **D-09:** Ambiguity means multiple distinct matching transition roles, not merely multiple roles in `actor.roles`. Portfolio Trade v1 should not naturally be ambiguous, but the reusable engine must implement the check and tests should add an ambiguous fixture.
- **D-10:** `WorkflowAudit.PerformedByRoleKey` is the matched transition `AllowedRoleKey`. With admin override, it is `AdminDemoOverride`.
- **D-11:** `--admin-override` bypasses only `Forbidden` and `AmbiguousRoleMatch`. It does not bypass envelope validation, instance existence, definition-version guard, completed status, action/transition existence, idempotency replay/conflict, or any concurrency guard.
- **D-12:** Successful admin override audit uses `PerformedByRoleKey = "AdminDemoOverride"` and prefixes `Reason` with `[ADMIN OVERRIDE] `. If the action reason is empty, use exactly `[ADMIN OVERRIDE]`.
- **D-13:** Failure messages include only concise guarded fields, are stable/testable English strings, and contain no unrelated state, payload contents, timestamps, or clock-dependent values.
- **D-14:** `WorkflowActionResult` carries structured `expected`, `actual`, and `failedGuard` fields so API consumers never need to regex messages.

### Idempotency and Concurrency
- **D-15:** `IdempotencyRecord.ResultJson` stores the canonical full `WorkflowActionResult` JSON returned by the original successful execution, including the original refreshed `workflowContext`.
- **D-16:** `ResultJson` reflects the original execution moment: original state, timestamps, task IDs, notification decisions, audit summary, and workflow context. It is never recomputed on replay.
- **D-17:** `ResultHash` is SHA-256 of canonical `ResultJson` in lowercase hex and is stored for diagnostics/integrity, not lookup.
- **D-18:** Idempotent replay deserializes and returns stored `ResultJson` as-is. It does not re-run validation, authorization, concurrency checks, side effects, or context refresh. Readable mode must include `Idempotent replay: Yes (original action executed at {originalCreatedAtUtc})`; JSON diagnostics must include `replay: true`.
- **D-19:** Reuse and harden the Phase 3 `CanonicalRequestHasher` as the single request hashing source of truth. Phase 4 must not create a second hasher.
- **D-20:** Request hash input follows the positive include-list from `PROJECT.md` section 9: workflow key, workflow instance id, workflow definition version, action key, action reason, actor user id, actor roles, business object type/id, all four concurrency guards, dataSave section key, and dataSave payload. Exclude request client action id, workflow idempotency key, request correlation id, and requested timestamp.
- **D-21:** Hash the effective envelope after console overrides. Do not hash raw file text.
- **D-22:** Canonicalization rules remain alphabetical recursive property ordering, arrays preserving original order, no whitespace, UTF-8, and SHA-256 lowercase hex.
- **D-23:** Concurrency guard failure reporting order is `expectedCurrentState`, `expectedCurrentTaskId`, `expectedCurrentTaskKey`, `expectedInstanceVersion`. Report the first mismatch and stop. Guards not supplied are skipped. All supplied guards must pass before execution.
- **D-24:** `WorkflowActionResult.failedGuard` is populated only for `ConcurrencyConflict`, using one of the four guard field names.
- **D-25:** Concurrent duplicate idempotency races use SQL uniqueness as the final arbiter. If the idempotency insert at the idempotency-write step fails because the key already exists, roll back the loser transaction, re-read the committed record outside any transaction with fresh state, and return replay if hashes match or `IdempotencyConflict` if hashes differ.
- **D-26:** The database-exception-to-result translation is scoped only to the idempotency-record unique violation at the idempotency write step. Unrelated SQL exceptions propagate and the command exits non-zero.

### Start Workflow and Reset
- **D-27:** `start-workflow` uses read-then-write duplicate detection. A pre-insert SELECT by `(WorkflowKey, BusinessObjectType, BusinessObjectId)` returns `AlreadyExists` with full existing instance details if a row exists. Do not attempt insert in that path.
- **D-28:** `AlreadyExists` includes workflow instance id, existing status, version, current state, started time, completed time, and a stable message explaining v1 one-lifetime behavior.
- **D-29:** Concurrent `start-workflow` unique violations are not translated to `AlreadyExists`; they propagate as unexpected operator-command failures.
- **D-30:** Fresh `start-workflow` inserts only one `WorkflowInstance` row in `Draft`. It writes no task, audit, notification, mock section data, or idempotency record.
- **D-31:** Fresh instance values are deterministic: `WorkflowInstanceId = WF-{businessObjectId}`, `CurrentStateKey = Draft`, `Status = Active`, `Version = 1`, `CompletedAtUtc = null`, `StartedAtUtc = TimeProvider.GetUtcNow()`, `StartedByUserId = --user`, and definition version resolves from the active workflow definition version.
- **D-32:** `start-workflow` is not an audited transition. Do not add `WorkflowStarted`, synthetic audit rows, seed actions, or nullable `FromStateKey`.
- **D-33:** `start-workflow --reset` deletes runtime rows for the target workflow instance/business object only, never seed metadata.
- **D-34:** Reset delete order is fixed and all deletes plus fresh insert happen in one transaction:
  1. `IdempotencyRecord` by `WorkflowInstanceId`
  2. `NotificationOutbox` by `WorkflowInstanceId`
  3. `WorkflowAudit` by `WorkflowInstanceId`
  4. `MockSectionData` by `BusinessObjectType` and `BusinessObjectId`
  5. `WorkflowTask` by `WorkflowInstanceId`
  6. `WorkflowInstance` by `WorkflowInstanceId`
- **D-35:** Reset deletes by column values, not by string-matching idempotency key text. Reset against a missing instance reports zero counts and creates a fresh instance.
- **D-36:** Readable reset output reports counts for all six runtime tables and the created fresh instance summary.

### Refreshed Context After Action
- **D-37:** Every successful non-replay `execute-json` returns refreshed `workflowContext`, including `SaveDraft`.
- **D-38:** Refreshed context is computed post-commit using the same service/code path as `show-context`, scoped to the submitting actor's `actor.userId` and `actor.roles`.
- **D-39:** Refreshed context is a separate committed-data read, not a query against an in-flight transaction context.
- **D-40:** The refreshed context uses the full `WorkflowContextResponse` shape from `PROJECT.md` section 8.1. Do not omit contract fields.
- **D-41:** The submitting actor's perspective is preserved. After Checker returns to Maker, the returned context is Checker's view of the now Maker-owned workflow, typically `canAct=false`, `availableActions=[]`, and a read-only reason showing the Maker assignment.
- **D-42:** Failure/replay context behavior is fixed:
  - `Success` non-replay: refreshed context
  - `Success` idempotent replay: stored original context from `ResultJson`, not refreshed
  - `ConcurrencyConflict`: refreshed actual context
  - `WorkflowDefinitionVersionMismatch`: refreshed actual context
  - `ValidationFailed`, `Forbidden`, `InvalidTransition`, `AmbiguousRoleMatch`, `IdempotencyConflict`, `WorkflowInstanceNotFound`: no refreshed context
- **D-43:** Readable `execute-json` output does not need to print refreshed `availableActions` by default. JSON output includes full context when the context rule says present.

### Error JSON Shape
- **D-44:** `WorkflowActionResultEnvelope` should expose a top-level `result` plus a top-level `workflowContext` peer. `workflowContext` is not nested inside `result`.
- **D-45:** Use one stable result shape for all result codes. Under `result`, include top-level `expected`, `actual`, `failedGuard`, and `diagnostics` fields.
- **D-46:** `result.success` is true only for `Success`, including idempotent replay. Every other result code is false.
- **D-47:** `result.expected` and `result.actual` are scalar values populated for failures with expected/actual pairs, including concurrency conflicts and workflow definition version mismatch. They are null otherwise.
- **D-48:** `result.failedGuard` is populated only for concurrency conflicts and null for all other result codes.
- **D-49:** Side-effect fields `oldState`, `newState`, `closedTask`, `createdTask`, `notification`, and `audit` are explicitly null on failure, not omitted.
- **D-50:** `result.diagnostics` is always present. It includes `requestHash`, `resultHash`, `replay`, and `originalCreatedAtUtc`.
  - `requestHash` is full 64-character lowercase hex in JSON, except `ValidationFailed` where it is null.
  - `resultHash` is SHA-256 of canonical `WorkflowActionResult` JSON excluding `diagnostics` to avoid recursion. It is populated on success and replay, null on failure.
  - `originalCreatedAtUtc` is populated only for replay.
- **D-51:** `ValidationFailed` uses `diagnostics.validationErrors` with field, rule, and message entries. `expected` and `actual` remain null.
- **D-52:** `AlreadyExists` uses flat `existing*` fields under `result`: existing status, version, current state, started time, and completed time. These fields are absent for other result codes.
- **D-53:** `IdempotencyConflict` uses `expected = stored hash`, `actual = current hash`, and diagnostics fields `storedRequestHash` and `storedActionKey`.
- **D-54:** Build results through one application-layer `WorkflowActionResultBuilder` with entry points per result code. Do not scatter `WorkflowActionResult` construction across services.

### the agent's Discretion
The agent may choose exact service/class names and task slicing, provided the above contracts are preserved. Mechanical details can be handled during planning without further user input: logging discipline, default test notification sender wiring, sample JSON fixture authoring from `PROJECT.md` section 19, and read-only command formatting derived from `PROJECT.md` section 10.5.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` sections 7-10 - Portfolio Trade transition matrix, context/action/result contracts, command behavior, admin override, start-workflow, reset, and read-only command output.
- `PROJECT.md` section 12 - SQL model and uniqueness requirements.
- `PROJECT.md` section 13 - SLA due-date and context computation rules.
- `PROJECT.md` section 14 - notification route/outbox behavior.
- `PROJECT.md` section 15 - transaction sequence, rollback behavior, READ COMMITTED isolation, and optimistic concurrency.
- `PROJECT.md` section 16.1 - logging severity, result codes, and failure audit rule.
- `PROJECT.md` sections 19-21 - sample JSON files, required tests, and quality gates.
- `PROJECT.md` section 22 - stop-and-ask triggers for contract, schema, return path, or workflow model changes.
- `PROJECT.md` section 28 - pitfall checklist for SaveDraft, idempotency, stale forms, reset, audit, SLA, and start-workflow behavior.

### GSD Planning
- `.planning/PROJECT.md` - validated project context and current active requirements.
- `.planning/REQUIREMENTS.md` - Phase 4 requirements `ENG-01` through `ENG-08` and `PT-02` through `PT-05`.
- `.planning/ROADMAP.md` - Phase 4 goal and success criteria.
- `.planning/STATE.md` - current project state and build note (`-m:1`).
- `.planning/phases/02-sql-schema-and-seed-data/02-CONTEXT.md` - SQL schema, seed, status, and reset boundaries.
- `.planning/phases/03-workflow-contracts-and-console-actions/03-CONTEXT.md` - Phase 3 DTO, validation, hash, override, and context decisions.
- `.planning/phases/03-workflow-contracts-and-console-actions/03-01-SUMMARY.md` - existing envelope validation, normalization, request hash, and `execute-json` command path.
- `.planning/phases/03-workflow-contracts-and-console-actions/03-02-SUMMARY.md` - existing SQL-backed `show-context` service and tests.
- `AGENTS.MD` - repository-local GSD guardrails and completion summary format.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs` - current Phase 3 non-executing handler. Phase 4 should replace the deferred result path with the engine execution path while preserving parsing/normalization/validation structure where useful.
- `src/WorkflowEngine.Application/Validation/WorkflowActionEnvelopeValidator.cs` - Phase 3 envelope validation for non-persisted rules.
- `src/WorkflowEngine.Application/Normalization/WorkflowActionEnvelopeNormalizer.cs` - override application and effective identity derivation.
- `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs` - single source of truth for request hashing.
- `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs` - button-click request DTO.
- `src/WorkflowEngine.Application/Contracts/WorkflowActionResult.cs` - result DTO to extend for Phase 4 fields.
- `src/WorkflowEngine.Application/Contracts/WorkflowContextResponse.cs` and `AvailableWorkflowAction.cs` - context/action DTOs used by `show-context` and refreshed results.
- `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs` - SQL-backed committed-read context builder to reuse after actions.
- `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` and `Entities/*.cs` - metadata/runtime tables for execution side effects.
- `src/WorkflowEngine.Infrastructure/Persistence/Seed/PortfolioTradeSeedData.cs` - authoritative seeded metadata keys for transition tests.

### Established Patterns
- Keep workflow behavior generic; Portfolio Trade appears only in seed data, samples, and tests.
- Use injected `TimeProvider` for all generated timestamps.
- Use `System.Text.Json` camelCase contract serialization with stable required blocks.
- SQL-backed tests are appropriate for persistence and concurrency behavior; local SQL Server may require elevated execution for Windows integrated authentication.
- Use `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` and `dotnet test src\WorkflowEngine.sln -m:1` for reliable verification.

### Integration Points
- Add application-layer orchestration services for workflow execution, result building, start/reset command behavior, and idempotency/concurrency handling.
- Add infrastructure-layer SQL services only where persistence specifics are required, keeping business workflow semantics in reusable application/domain code.
- Wire `start-workflow` and upgraded `execute-json` through `CommandRegistry` and console DI.
- Reuse `IWorkflowContextReader` for post-commit refresh rather than building a second context path.

</code_context>

<specifics>
## Specific Ideas

- Treat the error JSON shape as a public API contract. Tests should assert full field presence/null behavior, not just result codes.
- Keep `AlreadyExists`, replay, reset-count, and concise transition summaries useful for operators watching console demos.
- Sample JSON files needed for Phase 4 testing should be generated from the locked section 19 contract during the phase, even though overall sample/demo polish remains Phase 6.
- Supplementary read-only commands (`show`, `show-tasks`, `show-history`, `show-notifications`) are mechanical SQL-to-text/JSON work derived from `PROJECT.md` section 10.5; they do not need more discussion.

</specifics>

<deferred>
## Deferred Ideas

- SLA scanning and notification sending remain Phase 5.
- Full sample/demo script polish and README remain Phase 6, though Phase 4 may create fixtures required for its own tests.
- Logging discipline, test notification sender defaults, and optional readable-output flags are implementation details for planning, not separate discussion topics.

</deferred>

---

*Phase: 04-Workflow Engine*
*Context gathered: 2026-05-07*
