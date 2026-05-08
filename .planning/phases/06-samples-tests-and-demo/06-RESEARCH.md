# Phase 6: Samples, Tests, and Demo - Research

**Researched:** 2026-05-08
**Status:** Ready for planning

## Research Question

What needs to be known to plan Phase 6 well without duplicating Phase 1-5 work?

## Findings

### Existing Command Surface

- `src/WorkflowEngine.Application/Commands/CommandRegistry.cs` lists all required command names, including `demo`.
- `demo` is currently a known but deferred command. `CommandRegistryTests.DeferredCommandsReturnIntentionalStubOutput(command: "demo")` confirms that behavior today.
- `start-workflow`, `execute-json`, `show-context`, `show`, `show-tasks`, `show-history`, `show-notifications`, `run-sla`, and `process-notification-outbox` already have real handlers and registry dispatch paths.
- Command parsing exists inside handlers as private nested records or private methods:
  - `StartWorkflowCommandHandler.StartWorkflowOptions.Parse`
  - `ExecuteJsonCommandHandler.ExecuteJsonOptions.Parse`
  - `ShowContextCommandHandler.ShowContextOptions.Parse`
  - `RunSlaCommandHandler.ParseNow`
  - Read-only inspection handlers use simple positional validation.
- Phase 6 README validation needs pure parser access. Plan for mechanical extraction to an application-layer parser helper without changing behavior.

### Existing Samples

- All 10 required `samples/workflow-actions/*.json` files exist.
- `WorkflowSamplesTests.AllRequiredWorkflowActionSamplesDeserializeAndCarryExpectations` already verifies count, default identifiers, deserializeability, and `testExpectations.ExpectedResult = Success`.
- Phase 6 still needs stronger expectation execution coverage: old/new state, closed/created task, notification, audit, and branch sequences.

### Existing SQL Test Patterns

- SQL-backed test helpers currently use local test databases such as:
  - `burrito_phase4_execution_tests`
  - `burrito_phase3_context_tests`
  - `burrito_phase4_start_tests`
- Helpers call `EnsureDeleted()`, `Migrate()`, and `WorkflowSeedService.SeedPortfolioTrade()` for clean seeded state.
- Collection attributes serialize related SQL tests:
  - `SqlServerWorkflowExecution`
  - `SqlServerWorkflowContext`
  - `SqlServerStartWorkflow`
- FreshClone tests are different: they must intentionally target the configured `burrito` database and be opt-in/destructive.

### Existing Coverage Inventory Signals

- `dotnet test src\WorkflowEngine.sln --list-tests --no-restore` succeeds outside the sandbox and lists existing test names.
- Many section 20 bullets are already covered by Phase 1-5 tests:
  - Seed matrix and schema: `SeedDataSmokeTests`, `SchemaConstraintTests`.
  - Envelope validation: `WorkflowActionEnvelopeValidationTests`.
  - Context: `WorkflowContextReaderTests`, `ShowContextCommandTests`, `WorkflowContextSerializationTests`.
  - Transition side effects: `WorkflowTransitionTests`, `WorkflowExecutionValidationTests`.
  - Start/reset: `StartWorkflowServiceTests`, `StartWorkflowCommandTests`.
  - SLA/outbox/senders: `Phase5SlaTests`, `Phase5OutboxTests`, `NotificationSenderSelectionTests`, `SmtpNotificationSenderTests`, SMTP integration tests.
- Phase 6-specific missing/likely missing areas:
  - `demo` implementation and control-flow tests.
  - Branch script fixture execution and README sync.
  - `tests/COVERAGE_AUDIT.md` and audit validation tests.
  - README creation plus command parser/spec-reference/result-code/config validation.
  - FreshClone opt-in tests and manual sign-off requirement.

### Demo Implementation Hooks

- Console composition root already registers command handlers and `CommandRegistry`; add demo handler and runner services using the same pattern.
- `WorkflowEngineDemoStepRunner` can dispatch command steps through `CommandRegistry`, but must avoid recursion if a step command is `demo`. The step fixture should contain only non-demo commands.
- `ProcessNotificationOutboxCommandHandler` writes a start banner directly to `Console.Out` before returning a `CommandResult`. Demo output handling should tolerate handlers that write directly and also return message text.
- Step expectation evaluation should reuse `WorkflowActionEnvelope.TestExpectations` and `WorkflowActionResult` JSON/readable result data where possible. For non-sample steps such as `show-context`, expectation can be command-success plus narrative checks.

### README Validation Hooks

- README does not exist yet.
- Validation should parse fenced markdown command lines only, ignoring prose.
- Branch script blocks should be delimited by HTML comments so tests can compare README blocks to fixture serialization.
- Capability coverage must compare README examples with the locked `PROJECT.md` section 10 command inventory.
- Reserved identifiers matter:
  - `PT-DEMO` belongs to `demo` examples and demo implementation only.
  - `PT-1001` belongs to default sample/happy-path examples.
  - `PT-1002` belongs to Checker Return contexts.
  - `PT-1003` belongs to Trading Return contexts.

### FreshClone Test Boundary

- Default `xunit.runner.json` currently excludes `Category=Smtp`.
- FreshClone should follow the same opt-in pattern by adding a second excluded trait for `Category=FreshClone`.
- FreshClone tests are destructive to the configured `burrito` database and run child processes. They should be explicitly filtered with `dotnet test --filter Category=FreshClone`, not default.

## Recommended Plan Shape

Use four plans:

1. **Coverage audit inventory** - read-only first pass that creates `tests/COVERAGE_AUDIT.md` and identifies Covered/Partial/Missing rows without writing tests.
2. **Demo and branch execution fixtures** - add reusable command fixtures, `IDemoStepRunner`, real `demo`, branch and sample expectation execution tests.
3. **README and parser validation** - add root README, parser extraction, README command/spec/result/config validation, and fixture-to-README sync tests.
4. **Coverage closure and final gates** - close remaining audit rows, add strong audit validation tests, add FreshClone opt-in tests, update audit to all Covered, and define final verification commands.

## Risks and Guardrails

- Do not let `demo` mutate `PT-1001`, `PT-1002`, or `PT-1003`; only `PT-DEMO`.
- Do not add shell/PowerShell script files.
- Do not let README validation execute SQL-backed commands; branch and quality-gate tests own semantic execution.
- Stop if the coverage audit finds an engine gap or spec contradiction.
- Keep command parser extraction behavior-preserving. Parser tests should lock existing usage messages and accepted flags before moving parser logic if needed.

## Research Complete

Phase 6 can be planned with the existing codebase structure. No external technical research is needed; this is primarily orchestration, traceability, documentation, and test-coverage closure.
