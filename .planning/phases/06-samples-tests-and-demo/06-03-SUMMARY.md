---
phase: 06-samples-tests-and-demo
plan: 03
subsystem: readme-validation
tags: [docs, readme, command-parsing, validation]
requires: [06-02]
provides:
  - README.md
  - src/WorkflowEngine.Application/Commands/CommandArgumentParser.cs
  - tests/WorkflowEngine.Tests/ReadmeValidationTests.cs
affects: [docs, application, tests]
tech-stack:
  added: []
  patterns: [pure-command-parser, readme-fixture-sync]
key-files:
  created:
    - README.md
    - src/WorkflowEngine.Application/Commands/CommandArgumentParser.cs
    - tests/WorkflowEngine.Tests/ReadmeValidationTests.cs
  modified:
    - src/WorkflowEngine.Application/Commands/StartWorkflowCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ShowContextCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ShowCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ShowTasksCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ShowHistoryCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ShowNotificationsCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/RunSlaCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/ProcessNotificationOutboxCommandHandler.cs
    - src/WorkflowEngine.Application/Commands/DemoCommandHandler.cs
    - tests/WorkflowEngine.Tests/BranchScriptDefinitions.cs
key-decisions:
  - "README command validation uses production parser functions without dispatching side-effecting handlers."
  - "Tagged README branch blocks are synchronized to the executable fixture arrays."
requirements-completed:
  - DOC-02
  - DOC-03
  - DOC-04
duration: "18 min"
completed: 2026-05-08
---

# Phase 06 Plan 03: README and Validation Summary

Created the layered README and validation source for its command examples.

## Results

- Added root `README.md` with the locked quickstart and integration guide structure.
- Tagged the §25.1, §25.2, and §25.3 command blocks for fixture sync validation.
- Extracted command argument parsing to `CommandArgumentParser`.
- Updated production handlers to use the shared parser surface.
- Added README validation tests for known commands, parser acceptance, sample paths, override pairing, reserved identifiers, fixture sync, result codes, spec references, config JSON, and stop-and-ask guidance.

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~Readme"` passed.
- `dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~Readme|FullyQualifiedName~CommandRegistryTests|FullyQualifiedName~ExecuteJsonCommandTests|FullyQualifiedName~StartWorkflowCommandTests|FullyQualifiedName~ShowContextCommandTests|FullyQualifiedName~ShowInspectionCommandTests"` passed.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

README and command validation are implemented and verified. Ready for Plan 06-04.
