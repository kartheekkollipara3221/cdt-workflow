# Phase 6: Samples, Tests, and Demo - Context

**Gathered:** 2026-05-08T10:35:24.0910272-04:00
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 6 packages the already-built workflow engine into a runnable, reviewable POC: complete sample verification, demo orchestration, branch-demo validation, section 20 test traceability, README documentation, and final quality-gate evidence. It must not redesign workflow execution, change contract shapes, alter SQL schema, change the Portfolio Trade transition matrix, or hardcode Portfolio Trade behavior into the reusable engine. If the coverage audit exposes an engine gap or a spec contradiction, stop and ask before changing engine behavior.

</domain>

<decisions>
## Implementation Decisions

### Demo Command Behavior
- **D-01:** `demo` runs the `PROJECT.md` section 25.1 happy path automatically using `PT-DEMO` / `WF-PT-DEMO` only. It must not execute the Checker Return to Maker or Trading Desk Return to Maker branch flows automatically.
- **D-02:** Before running the happy path, `demo` resets `PT-DEMO` using the existing `start-workflow --reset` delete-then-create behavior. The command must not touch `PT-1001`, `PT-1002`, or `PT-1003`.
- **D-03:** Every happy-path sample step is executed with automatic `--business-object PT-DEMO --workflow-instance WF-PT-DEMO` overrides.
- **D-04:** Before each step, print `--- Step N: <step name> ---`. Between steps, print a 100ms pause indicator. The pause duration is fixed for v1.
- **D-05:** After each sample step, assert that step's `testExpectations` and print pass/fail. On the first failure, print `Demo failed at step N: [resultCode] [message]`, exit non-zero, stop later steps, and do not print final audit history or branch guidance.
- **D-06:** On full success, print `Demo completed successfully. N steps executed.`, print final audit history using the same output as `show-history WF-PT-DEMO`, then print the locked branch guidance block.
- **D-07:** Branch guidance is hardcoded in the demo command output and printed only after success. It contains copy-pasteable commands for the `PT-1002` Checker Return to Maker branch, the `PT-1003` Trading Desk Return to Maker branch, and SLA/outbox demonstration commands.
- **D-08:** Reject v1 flags `--no-final-history`, `--pause-between-steps`, `--continue-on-failure`, and `--skip-branch-guidance`. They add command-surface complexity without changing the required POC outcome.

### Demo Step Runner
- **D-09:** Implement `demo` around an internal application-layer `IDemoStepRunner` interface. Production registers a real runner such as `WorkflowEngineDemoStepRunner`; tests may register `FailAtStepDemoStepRunner`.
- **D-10:** The demo command contains no test-only branches, `if (testing)` checks, or `--simulate-failure` flag. Failure simulation is entirely controlled by DI registration.
- **D-11:** Use these conceptual records, with naming open to implementation fit: `DemoStep(Name, CommandName, Arguments, Expectations)` and `DemoStepResult(Success, ResultCode, Message, ExpectationOutcome)`.
- **D-12:** Demo control-flow tests use the failing runner for deterministic first-step, middle-step, last-step, and result-code-specific failure coverage. A separate production-path test runs `demo` against the real engine and SQL database.
- **D-13:** The same `ConsoleCommand`/demo-step data structure should be reusable for the section 25.1 happy path fixture and the production `demo` sequence to avoid a second representation of the happy path.

### README Structure
- **D-14:** Create one root `README.md` titled `# CDT Workflow Engine`. It has two clearly separated sections: a command-first Quickstart and a contract-first Integration Guide.
- **D-15:** The Quickstart is about 100 lines maximum, prose-minimal, and covers prerequisites, first run, manual branch commands, SLA/notifications, inspection commands, and troubleshooting.
- **D-16:** The Integration Guide is about 200-300 lines and covers engine boundary, `WorkflowContextResponse`, `WorkflowActionEnvelope`, `WorkflowActionResult`, result codes, idempotency/concurrency, SLA, notifications, configuration, production guidance, testing, extending the engine, and references.
- **D-17:** README explains and indexes the spec; it does not duplicate full field references. On conflict, repository-root `PROJECT.md` is authoritative.
- **D-18:** Use plain markdown only. No screenshots or diagrams in v1.
- **D-19:** Quickstart commands must be runnable shell lines. Integration JSON examples are abbreviated and point to `PROJECT.md` sections for full shapes. Configuration examples must be complete and copy-pasteable.
- **D-20:** README lists exactly these result codes unless the spec changes in the same work: `Success`, `ValidationFailed`, `Forbidden`, `InvalidTransition`, `ConcurrencyConflict`, `WorkflowDefinitionVersionMismatch`, `IdempotencyConflict`, `AmbiguousRoleMatch`, `ActorRolesMismatch`, `AlreadyExists`, and `WorkflowInstanceNotFound`.

### Demo Script Artifacts
- **D-21:** Do not add standalone `.sh`, `.ps1`, `.cmd`, or `.bat` scripts in v1.
- **D-22:** README contains the operator-facing command sequences for `PROJECT.md` sections 25.1, 25.2, and 25.3 in markdown code blocks.
- **D-23:** The authoritative executable definitions of those sequences live in test code as `ConsoleCommand` arrays, likely in `BranchScriptDefinitions`.
- **D-24:** Add tagged README blocks using HTML comments such as `<!-- BEGIN: HappyPath_Section25_1 -->` and `<!-- END: HappyPath_Section25_1 -->`. README sync tests serialize fixture arrays into `dotnet run --project src/WorkflowEngine.Console -- ...` lines and compare them to the tagged blocks after normalizing trailing whitespace and blank lines.
- **D-25:** `BranchScriptTests` and `Section21QualityGateTests` execute the fixture arrays end-to-end and assert the documented narratives. README displays those fixtures; it is not the only executable source.

### Coverage Audit
- **D-26:** Phase 6 starts with a read-only coverage audit. Do not write tests before inventorying existing test coverage against every `PROJECT.md` section 20 bullet.
- **D-27:** Create `tests/COVERAGE_AUDIT.md` as a checked-in traceability table with exactly four columns: `§20 bullet | Status | Test or action | Notes`.
- **D-28:** Each section 20 bullet is classified as `Covered`, `Partial`, `Missing`, `Engine gap`, or `Spec contradiction`.
- **D-29:** `Covered` means an existing test asserts the behavior; cite the fully-qualified test name and do not edit it. `Partial` means an existing test is close; tighten that existing test rather than creating a duplicate. `Missing` means no test exists; add a new test grouped by domain.
- **D-30:** If the audit finds a real engine behavior gap, record `Engine gap` and stop for approval. If a bullet contradicts the locked spec, record `Spec contradiction` and stop for approval.
- **D-31:** Phase 6 cannot complete until the final audit has exactly one row per section 20 bullet and every row's status is `Covered`.

### Coverage Audit Enforcement
- **D-32:** Add `CoverageAuditTests.AuditDocumentIsValidAndComplete` plus diagnostic sub-tests `AuditFile_Exists`, `Section20Bullets_AllPresent`, `AllRowsAreCovered`, and `AllCitedTestsExist`.
- **D-33:** The tests parse `PROJECT.md` section 20 bullets and require each bullet to appear exactly once in `tests/COVERAGE_AUDIT.md`. Matching is exact text after trimming and whitespace normalization; audit rows must not paraphrase bullets.
- **D-34:** The tests require all final statuses to be exactly `Covered`.
- **D-35:** The tests require cited test names to resolve to compiled tests, using reflection or `dotnet test --list-tests`. Rows may cite non-test actions only when the `Test or action` cell starts with `Manual:`, `Quality gate:`, or `README:`.

### README Validation
- **D-36:** Add README validation at argument-shape level, not SQL execution level.
- **D-37:** `ReadmeCommandExtraction` parses fenced markdown lines prefixed with `dotnet run --project src/WorkflowEngine.Console -- ` into `ParsedCommand(LineNumber, ReadmeSection, CommandName, Arguments, RawLine)`.
- **D-38:** `ReadmeCommandValidation` verifies known command names, pure production parser acceptance, JSON sample file existence under `samples/workflow-actions`, paired `--business-object`/`--workflow-instance` flags, reserved identifier scoping, and no unknown flags.
- **D-39:** `ReadmeCapabilityCoverage` verifies every `PROJECT.md` section 10.1 command has at least one README example and override-mode examples use only `PT-1002`, `PT-1003`, or `PT-DEMO`.
- **D-40:** If command argument parsing currently lives inline inside handlers, Phase 6 may mechanically extract pure production parsers such as `CommandArgumentParser.ParseStartWorkflow`, `ParseExecuteJson`, and `ParseShowContext`. This must not change behavior.

### Fresh-Clone Walkthrough
- **D-41:** Split fresh-clone verification into an automated current-workspace simulation and a required manual fresh-clone sign-off.
- **D-42:** Add `FreshCloneWalkthroughTests` marked with `Trait("Category", "FreshClone")` and excluded from default `dotnet test`. Run explicitly with `dotnet test --filter Category=FreshClone` before Phase 6 sign-off.
- **D-43:** Automated FreshClone tests drop/recreate the configured `burrito` database, run `dotnet build -warnaserror`, then run README quickstart commands (`init-db`, `verify-seed`, `demo`) as child processes and assert exit code zero plus key output strings.
- **D-44:** FreshClone tests are part of quality gates but not default tests because they are slow and destructive to the configured local database.
- **D-45:** The manual walkthrough starts from a true never-cloned directory. It includes `dotnet --version` SDK check, `git clone`, connection string verification, `dotnet build -warnaserror`, default tests, optional SMTP tests, FreshClone tests, README quickstart, `demo`, and one run of each return branch.
- **D-46:** Phase 6 completion summary must include a `Fresh-clone walkthrough` section with operator name, date, and machine identifier. Phase 6 cannot be marked complete without a populated sign-off line.
- **D-47:** `tests/COVERAGE_AUDIT.md` includes the manual walkthrough as a `Covered` row with a `Manual:` action so audit validation accepts it.

### the agent's Discretion
The agent may choose class/file names, fixture organization, parser extraction shape, output formatting details, and exact test grouping as long as the locked behavior, audit gates, README structure, and no-extra-script rule are preserved.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` section 4.1 - local SQL Server connection string and configuration behavior.
- `PROJECT.md` sections 8.1, 8.3, and 9 - workflow context, action result, action envelope, MakerPrimary, idempotency, and concurrency contracts.
- `PROJECT.md` section 10 - console command inventory, command flags, override behavior, `demo` command contract, and read-only inspection commands.
- `PROJECT.md` sections 16.2 and 16.3 - simplicity and surgical-change rules.
- `PROJECT.md` section 19 - required sample JSON files and default identifiers.
- `PROJECT.md` section 20 - required tests; this is the source for `tests/COVERAGE_AUDIT.md`.
- `PROJECT.md` section 21 - quality gates.
- `PROJECT.md` section 22 - stop-and-ask triggers.
- `PROJECT.md` sections 24, 25, and 26 - milestone 6 definition, demo scripts, and README guidance.
- `PROJECT.md` section 28 - pitfall checklist.

### GSD Planning
- `.planning/PROJECT.md` - project context and active documentation/sample/test requirements.
- `.planning/REQUIREMENTS.md` - Phase 6 requirements `DOC-01` through `DOC-04`.
- `.planning/ROADMAP.md` - Phase 6 goal and success criteria.
- `.planning/STATE.md` - current state, completed prior phases, and `-m:1` build/test note.
- `.planning/phases/03-workflow-contracts-and-console-actions/03-CONTEXT.md` - contract, validation, override, and context decisions.
- `.planning/phases/04-workflow-engine/04-CONTEXT.md` - execution, idempotency, result, samples, and refreshed context decisions.
- `.planning/phases/05-sla-monitor-and-notification-senders/05-CONTEXT.md` - SLA, outbox, sender, and SMTP test-boundary decisions.
- `AGENTS.MD` - repository-local GSD guardrails and completion summary format.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WorkflowEngine.Application/Commands/CommandRegistry.cs` - currently recognizes `demo` but returns the generic stub for unimplemented commands. Phase 6 should add real demo dispatch while keeping existing command registration patterns.
- `src/WorkflowEngine.Application/Commands/*CommandHandler.cs` - existing command parsing and execution paths for `start-workflow`, `execute-json`, `show-context`, `show`, `show-tasks`, `show-history`, `show-notifications`, `run-sla`, and `process-notification-outbox`.
- `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs` and `WorkflowActionResult.cs` - sample and expectation validation should reuse the existing DTO/result shapes.
- `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` - production sample execution path for expectation assertions.
- `src/WorkflowEngine.Infrastructure/WorkflowInspection/WorkflowInspectionReader.cs` - final audit-history and branch narrative checks should reuse the inspection path.
- `samples/workflow-actions/*.json` - all 10 required sample files already exist with `testExpectations`.
- `tests/WorkflowEngine.Tests/WorkflowSamplesTests.cs` - existing sample deserialization/default identifier coverage.
- `tests/WorkflowEngine.Tests/ShowInspectionCommandTests.cs` - existing inspection command tests and useful patterns for branch narrative assertions.
- `tests/WorkflowEngine.Tests/Phase5SlaTests.cs` and `Phase5OutboxTests.cs` - existing SLA/outbox test patterns.
- `tests/WorkflowEngine.Tests/SmtpFactAttribute.cs` and `xunit.runner.json` - existing pattern for excluding opt-in SMTP tests from default runs.

### Established Patterns
- Keep Portfolio Trade only in seed data, samples, README, and tests; reusable application/domain engine behavior remains generic.
- Use injected `TimeProvider`; no direct `DateTime.UtcNow` outside composition root.
- SQL-backed tests are acceptable for persistence and demo flows; use `-m:1` for reliable local .NET verification.
- Default `dotnet test` excludes opt-in, infrastructure-heavy categories such as SMTP; FreshClone follows the same opt-in pattern.
- Do not use `PT-DEMO` outside the demo command and demo-specific tests.

### Integration Points
- Add a real `demo` command handler and DI registration.
- Add demo step definitions, `IDemoStepRunner`, production runner, and test runner.
- Add `tests/COVERAGE_AUDIT.md` and tests that validate it.
- Add README validation helpers/tests and, if needed, pure command argument parsers.
- Add branch script fixture definitions and tests that execute them.
- Add root `README.md` and ensure its tagged command blocks sync with fixtures.

</code_context>

<specifics>
## Specific Ideas

- Branch guidance after successful `demo` should contain the exact locked command block supplied by the user for `PT-1002`, `PT-1003`, SLA overdue scan, and notification outbox processing.
- README quickstart branch blocks should be tagged so tests can compare them to fixture serialization.
- Coverage audit is a load-bearing artifact; future spec or test renames must update it in the same change.
- The manual fresh-clone sign-off is the only manual Phase 6 verification step and belongs in the final completion summary.

</specifics>

<deferred>
## Deferred Ideas

- `--dry-run` could be implemented later using the same `IDemoStepRunner` pattern, but it is out of scope for v1.
- Standalone shell/PowerShell script files are intentionally deferred/rejected for v1 because README plus fixture validation provides cross-platform discoverability without script drift.

</deferred>

---

*Phase: 6-Samples, Tests, and Demo*
*Context gathered: 2026-05-08T10:35:24.0910272-04:00*
