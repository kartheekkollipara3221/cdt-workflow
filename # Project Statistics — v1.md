# Project Statistics — v1.0 milestone

  ## Progress

  [███████░░░] 4/6 phases complete (67%)

  ## Plans

  8/8 execution plans complete (100%)

  ## Phases

  | Phase | Name | Plans | Completed | Status |
  |---|---|---:|---:|---|
  | 1 | Solution Skeleton | 1 | 1 | Complete |
  | 2 | SQL Schema and Seed Data | 1 | 1 | Complete |
  | 3 | Workflow Contracts and Console Actions | 2 | 2 | Complete |
  | 4 | Workflow Engine | 4 | 4 | Complete, UAT in progress |
  | 5 | SLA Monitor and Notification Senders | 0 | 0 | Not started |
  ## Requirements

  24/32 requirements complete.

  Remaining:

  - SLA-01 through SLA-04
  - DOC-01 through DOC-04

  ## Git

  - Commits: 25
  - Started: 2026-05-07
  - Last activity: 2026-05-07

  ## Timeline

  - Project age: 0 days
  - Current phase: Phase 5 ready to discuss
  - Active UAT: Phase 4, 3/8 checkpoints passed

  Note: worktree has uncommitted .planning/phases/04-workflow-engine/04-UAT.md progress and pre-existing untracked src/.vs/.

────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────


› summarize code completed so far so i can do a manual review


• Phase 4 code is implemented and committed. Main review targets:

  Workflow Execution

  - /C:/projects/cdt-workflow/src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
      - SQL-backed execute-json pipeline.
      - Idempotency preflight replay/conflict.
      - Persisted validation order: instance, version, completed, action, transition, concurrency, role/ambiguity.
      - Metadata-driven side effects: mock data, task close/create, state/version update, notification, audit, idempotency record.
      - Post-commit workflowContext refresh.
      - Admin override bypasses role/ambiguity only.

  Start Workflow / Reset

  - /C:/projects/cdt-workflow/src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs
      - start-workflow creates only WorkflowInstance in Draft/Active.
      - AlreadyExists read-before-write behavior.
      - --reset deletes runtime rows in bounded order and recreates fresh instance in one transaction.

  Console Commands

  - /C:/projects/cdt-workflow/src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs
      - Parses envelope files, overrides, --admin-override, --format json.
      - Delegates to execution service after envelope validation.
      - Readable output includes state/task/audit/notification/replay summary.
  - /C:/projects/cdt-workflow/src/WorkflowEngine.Application/Commands/StartWorkflowCommandHandler.cs
      - Parses start-workflow <workflowKey> <businessObjectId> --user <userId> [--reset] [--format json].

  Result / Hash Contract

  - /C:/projects/cdt-workflow/src/WorkflowEngine.Application/Contracts/WorkflowActionResult.cs
      - Stable result shape with expected, actual, failedGuard, diagnostics, workflowContext, existing*.
  - /C:/projects/cdt-workflow/src/WorkflowEngine.Application/Results/WorkflowActionResultBuilder.cs
      - Central result construction.
  - /C:/projects/cdt-workflow/src/WorkflowEngine.Application/Results/CanonicalWorkflowResultSerializer.cs
      - Canonical resultPhase 4 code is implemented through the workflow engine path.

  Completed code areas:

  - start-workflow
      - Creates deterministic WF-{businessObjectId} instances.
      - Supports --reset with bounded runtime-table cleanup.
      - Returns AlreadyExists for duplicate business objects.
  - execute-json
      - Parses envelopes, overrides business object/workflow instance, validates shape.
      - Computes canonical request hash from the effective envelope.
      - Supports --admin-override.
  - SQL-backed workflow execution
      - Loads workflow instance and definition version.
      - Enforces validation order: idempotency, instance existence, definition version, completed status, action/transition existence, concurrency
        guards, authorization/ambiguity.
      - Returns stable result codes/messages with expected, actual, failedGuard, and diagnostics.
  - Transition side effects
      - SaveDraft: writes mock section data + audit + idempotency only.
      - State-changing transitions: close task, move state, increment version, create next task, assign SLA due date, queue notification, write
        audit, persist idempotency.
      - Terminal transition marks workflow Completed.
  - Result contract
      - WorkflowActionResultEnvelope now has result plus top-level workflowContext.
      - Result includes diagnostics, expected/actual, failedGuard, existing-instance fields, side-effect summaries.
      - Canonical result serialization and ResultHash implemented.
  - Samples/tests
      - Added 10 sample envelopes under samples/workflow-actions.
      - Added tests for start/reset, validation/idempotency/concurrency/auth, transitions, result shapes, sample deserialization.
      - Latest full suite: 100 tests passed.

  Key files to review:

  - src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
  - src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs
  - src/WorkflowEngine.Application/Results/WorkflowActionResultBuilder.cs
  - src/WorkflowEngine.Application/Results/CanonicalWorkflowResultSerializer.cs
  - src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs
  - src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs
  - samples/workflow-actions/*.json
  - tests/WorkflowEngine.Tests/WorkflowTransitionTests.cs
  - tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
  - tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs

  Known caveats:

  - Phase 4 UAT is not complete: 3/8 checkpoints passed.
  - show, show-tasks, show-history, show-notifications, SLA monitor, and notification outbox processing are still later-phase work.
  - src/.vs/ is untracked/pre-existing and untouched.


› Improve documentation in @filename