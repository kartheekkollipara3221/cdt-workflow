---
phase: 06-samples-tests-and-demo
plan: 02
subsystem: demo-and-samples
tags: [demo, tests, samples, console]
requires: [06-01]
provides:
  - src/WorkflowEngine.Application/Demo
  - src/WorkflowEngine.Application/Commands/DemoCommandHandler.cs
  - tests/WorkflowEngine.Tests/DemoCommandTests.cs
  - tests/WorkflowEngine.Tests/BranchScriptTests.cs
  - tests/WorkflowEngine.Tests/SampleExpectationTests.cs
  - tests/WorkflowEngine.Tests/Section21QualityGateTests.cs
affects: [console, application, tests]
tech-stack:
  added: []
  patterns: [injectable-runner, fixture-command-arrays]
key-files:
  created:
    - src/WorkflowEngine.Application/Demo/ConsoleCommand.cs
    - src/WorkflowEngine.Application/Demo/DemoStep.cs
    - src/WorkflowEngine.Application/Demo/IDemoStepRunner.cs
    - src/WorkflowEngine.Application/Demo/DemoScriptDefinitions.cs
    - src/WorkflowEngine.Application/Demo/WorkflowEngineDemoStepRunner.cs
    - src/WorkflowEngine.Application/Commands/IDemoCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/DemoCommandHandler.cs
    - tests/WorkflowEngine.Tests/BranchScriptDefinitions.cs
    - tests/WorkflowEngine.Tests/BranchScriptTests.cs
    - tests/WorkflowEngine.Tests/DemoCommandTests.cs
    - tests/WorkflowEngine.Tests/SampleExpectationTests.cs
    - tests/WorkflowEngine.Tests/Section21QualityGateTests.cs
    - tests/WorkflowEngine.Tests/TestConsoleHost.cs
  modified:
    - src/WorkflowEngine.Application/Commands/CommandRegistry.cs
    - src/WorkflowEngine.Console/Program.cs
    - tests/WorkflowEngine.Tests/CommandRegistryTests.cs
    - tests/COVERAGE_AUDIT.md
key-decisions:
  - "The demo command delegates execution through IDemoStepRunner and keeps orchestration separate from workflow logic."
  - "Branch scripts are represented as ConsoleCommand arrays in tests, not shell scripts."
requirements-completed:
  - DOC-01
  - DOC-02
  - DOC-03
duration: "22 min"
completed: 2026-05-08
---

# Phase 06 Plan 02: Demo and Branch Fixtures Summary

Implemented the real `demo` command and executable section 25 fixtures.

## Results

- Added the demo runner abstraction, production step runner, and demo command handler.
- Wired `demo` through `CommandRegistry` and console DI.
- `demo` now resets `PT-DEMO`, executes the happy path with overrides, asserts sample expectations, stops on first failure, prints final history only on success, and prints hardcoded branch guidance only on success.
- Added branch fixture arrays for sections 25.1, 25.2, and 25.3.
- Added deterministic demo failure tests with an injectable failing runner.
- Added SQL-backed real demo, branch script, sample expectation, and section 21 command-sequence tests.
- Updated coverage audit rows closed by this plan.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~Section21QualityGateTests|FullyQualifiedName~DemoCommandTests|FullyQualifiedName~BranchScriptTests|FullyQualifiedName~SampleExpectationTests|FullyQualifiedName~CommandRegistryTests"` passed.
- `dotnet run --project src\WorkflowEngine.Console -- init-db` passed.
- `dotnet run --project src\WorkflowEngine.Console -- demo` passed and printed branch guidance.
- `rg --files | rg "\.(ps1|sh|cmd|bat)$"` found no standalone script artifacts.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

The demo command and section 25 executable fixtures are in place and verified. Ready for Plan 06-03.
