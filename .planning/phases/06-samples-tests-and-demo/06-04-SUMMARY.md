---
phase: 06-samples-tests-and-demo
plan: 04
subsystem: final-audit-and-gates
tags: [coverage-audit, freshclone, quality-gates]
requires: [06-02, 06-03]
provides:
  - tests/WorkflowEngine.Tests/CoverageAuditTests.cs
  - tests/WorkflowEngine.Tests/FreshCloneWalkthroughTests.cs
  - tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs
affects: [engine, tests, audit]
tech-stack:
  added: []
  patterns: [machine-verified-audit, opt-in-destructive-test]
key-files:
  created:
    - tests/WorkflowEngine.Tests/CoverageAuditTests.cs
    - tests/WorkflowEngine.Tests/FreshCloneWalkthroughTests.cs
    - tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs
  modified:
    - src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
    - tests/COVERAGE_AUDIT.md
    - tests/WorkflowEngine.Tests/xunit.runner.json
key-decisions:
  - "workflow.options support is audit-closure only: compare advisory options to metadata, print stderr warnings for conflicts, and keep execution metadata-driven."
  - "FreshClone tests are destructive and opt-in through Category=FreshClone."
requirements-completed:
  - DOC-01
  - DOC-02
  - DOC-03
  - DOC-04
duration: "35 min"
completed: 2026-05-08
---

# Phase 06 Plan 04: Final Audit and Gates Summary

Closed the Phase 6 audit and added final quality gates.

## Changed:

- Added machine validation for `tests/COVERAGE_AUDIT.md`.
- Closed every §20 audit row to `Covered`.
- Added `workflow.options` advisory warning emission to stderr before side effects.
- Added focused tests for every approved `workflow.options` conflict case.
- Added opt-in `FreshCloneWalkthroughTests` and excluded `Category=FreshClone` from default test runs.

## Why:

Phase 6 requires the §20 test surface to be fully traced, machine-validated, and closed. The audit found missing `workflow.options` warning behavior from the locked §9 rule, so the fix implements only the warning comparison while preserving metadata-driven execution.

## Tests run:

- `dotnet restore src\WorkflowEngine.sln` — passed.
- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` — passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~CoverageAuditTests|FullyQualifiedName~WorkflowOptionsConflictTests"` — passed.
- `dotnet test src\WorkflowEngine.sln -m:1` — passed: 185 passed, 2 skipped SMTP infrastructure tests.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter Category=FreshClone` — passed: 6 passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter Category=Smtp` — discovered 2 tests, both skipped because the local SMTP integration guard did not find the required fake SMTP infrastructure.

## Seed verification run:

- `dotnet run --project src\WorkflowEngine.Console -- init-db` — passed.
- `dotnet run --project src\WorkflowEngine.Console -- verify-seed` — passed.

## Sample JSON executed:

- Section 25 Checker Return branch commands — passed end-to-end with `PT-1002`.
- Section 25 Trading Desk Return branch commands — passed end-to-end with `PT-1003`.
- `dotnet run --project src\WorkflowEngine.Console -- demo` — passed end-to-end with `PT-DEMO` and printed branch guidance.
- `SampleExpectationTests.ExecuteJson_SamplesProduceTheirTestExpectations` — passed and exercises all 10 sample JSON files.

## Files updated:

- `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs`
- `tests/COVERAGE_AUDIT.md`
- `tests/WorkflowEngine.Tests/CoverageAuditTests.cs`
- `tests/WorkflowEngine.Tests/FreshCloneWalkthroughTests.cs`
- `tests/WorkflowEngine.Tests/WorkflowOptionsConflictTests.cs`
- `tests/WorkflowEngine.Tests/xunit.runner.json`

## Fresh-clone walkthrough:

Automated current-workspace simulation passed through `dotnet test src\WorkflowEngine.sln -m:1 --filter Category=FreshClone`.

Manual true fresh-clone sign-off is still required from an operator before release acceptance:

`Verified by [operator name] on [date] from a fresh clone on [machine].`

## Open issues:

- Manual true fresh-clone walkthrough sign-off is pending. I did not populate the operator sign-off line because I did not perform a separate human-operated clone from a clean directory.
- Tier 2 SMTP integration tests were skipped by their local infrastructure guard; run again with `smtp4dev` or Papercut available.

## Deviations from Plan

- The plan requested a populated manual fresh-clone sign-off. Automated FreshClone passed, but the manual operator sign-off remains pending and is recorded above rather than fabricated.

## Self-Check: PASSED

All automated Phase 6 gates passed. Manual release sign-off remains the only non-automated acceptance item.
