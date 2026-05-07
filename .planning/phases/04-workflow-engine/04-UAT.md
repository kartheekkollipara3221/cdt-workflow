---
status: diagnosed
phase: 04-workflow-engine
source:
  - 04-01-SUMMARY.md
  - 04-02-SUMMARY.md
  - 04-03-SUMMARY.md
  - 04-04-SUMMARY.md
started: "2026-05-07T17:50:00Z"
updated: "2026-05-07T16:45:08Z"
---

## Current Test

[testing complete]

## Tests

### 1. Start And Context Smoke
expected: Run start-workflow with --reset for a PortfolioTrade business object. The command returns Success, shows reset counts, creates a fresh Draft/Active workflow at Version 1, and show-context for maker1/Maker shows currentTask = null, sla.status = NoSla, and available actions SaveDraft and SubmitForChecker.
result: pass

### 2. SaveDraft Behavior
expected: Execute 01-save-draft.json from Draft. The result is Success, state remains Draft, version stays 1, no task or notification is created, one mock section data row and one SaveDraft audit row are written, and workflowContext is returned.
result: pass

### 3. Submit For Checker Transition
expected: Execute 02-submit-for-checker.json from Draft. The result is Success, state moves Draft -> CheckerReview, version increments, CheckerReviewTask is created with SLA due date, WorkflowTaskAssigned notification is queued to Checker, audit is written, and refreshed workflowContext is returned.
result: pass

### 4. Checker Return To Maker Branch
expected: Execute 03-checker-return-to-maker.json from CheckerReview. The result is Success, CheckerReviewTask is closed, state moves to ReturnedToMaker, MakerReworkTask is created with no due date, WorkflowReturnedToMaker notification is queued to Maker, and audit records CheckerReturnToMaker.
result: issue
reported: "none of the show commands are working"
severity: major

### 5. Trading Desk Return To Maker Branch
expected: On a fresh workflow branch, execute SubmitForChecker, CheckerSendToTradingDesk, then TradingReturnToMaker with overrides. The result is Success, TradingDeskReviewTask is closed, state moves to ReturnedToMaker, MakerReworkTask is created, WorkflowReturnedToMaker notification is queued to Maker, and audit records TradingReturnToMaker.
result: pass

### 6. Validation, Concurrency, And Authorization Failures
expected: Invalid or stale execute-json requests return stable result codes and fields: ValidationFailed before SQL/hash work, WorkflowInstanceNotFound for missing instance, WorkflowDefinitionVersionMismatch with expected/actual and workflowContext, ConcurrencyConflict with first failedGuard, Forbidden for wrong role, AmbiguousRoleMatch for injected ambiguity, and InvalidTransition for completed or missing transitions.
result: pass

### 7. Idempotency Replay And Conflict
expected: Re-running the same idempotency key and matching request hash returns stored ResultJson as a replay without duplicate mock data, audit, tasks, notifications, or version increments. Reusing the key with a different request hash returns IdempotencyConflict with stored/current hashes.
result: pass

### 8. Result JSON And Samples
expected: --format json responses deserialize into one WorkflowActionResultEnvelope shape with result plus top-level workflowContext when applicable. Diagnostics, expected, actual, failedGuard, validationErrors, and existing* fields appear in the locked places. All 10 samples in samples/workflow-actions deserialize with default PT-1001/WF-PT-1001 identifiers and testExpectations.
result: pass

## Summary

total: 8
passed: 7
issues: 1
pending: 0
skipped: 0
blocked: 0

## Gaps

- truth: "Execute 03-checker-return-to-maker.json from CheckerReview. The result is Success, CheckerReviewTask is closed, state moves to ReturnedToMaker, MakerReworkTask is created with no due date, WorkflowReturnedToMaker notification is queued to Maker, and audit records CheckerReturnToMaker."
  status: failed
  reason: "User reported: none of the show commands are working"
  severity: major
  test: 4
  artifacts:
    - path: "src/WorkflowEngine.Application/Commands/CommandRegistry.cs"
      issue: "show, show-tasks, show-history, and show-notifications are recognized but fall through to the not-implemented response."
  missing:
    - "Implement handlers and DI registration for show, show-tasks, show-history, and show-notifications."
  root_cause: "Phase 4 implementation registered the command names in CommandRegistry.RequiredCommandNames but only dispatches show-context. The remaining read-only inspection commands fall through to CommandResult.Success(\"Command '<name>' is recognized but not implemented yet.\"), so UAT cannot inspect workflow state, task closure, notifications, or audit history through the documented demo commands."
  debug_session: "inline-diagnosis-2026-05-07"
