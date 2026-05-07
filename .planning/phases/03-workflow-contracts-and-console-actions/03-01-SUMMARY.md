---
phase: 03-workflow-contracts-and-console-actions
plan: 01
subsystem: contracts
tags: [dotnet, system-text-json, cli, validation, hashing]
requires:
  - phase: 02-sql-schema-and-seed-data
    provides: SQL persistence baseline and command routing infrastructure
provides:
  - WorkflowActionEnvelope DTOs
  - WorkflowActionResult DTOs
  - Non-database envelope validation
  - Console override normalization
  - Canonical request hashing
  - Non-executing execute-json command path
affects: [phase-04-workflow-engine, idempotency, samples]
tech-stack:
  added: []
  patterns: [application-layer DTOs, command handler injection, canonical JSON hashing]
key-files:
  created:
    - src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs
    - src/WorkflowEngine.Application/Contracts/WorkflowActionResult.cs
    - src/WorkflowEngine.Application/Contracts/WorkflowJson.cs
    - src/WorkflowEngine.Application/Validation/WorkflowActionEnvelopeValidator.cs
    - src/WorkflowEngine.Application/Normalization/WorkflowActionEnvelopeNormalizer.cs
    - src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs
    - src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs
    - tests/WorkflowEngine.Tests/WorkflowActionEnvelopeSerializationTests.cs
    - tests/WorkflowEngine.Tests/WorkflowActionEnvelopeValidationTests.cs
    - tests/WorkflowEngine.Tests/WorkflowActionEnvelopeNormalizationTests.cs
    - tests/WorkflowEngine.Tests/ExecuteJsonCommandTests.cs
  modified:
    - src/WorkflowEngine.Application/Commands/CommandRegistry.cs
    - src/WorkflowEngine.Console/Program.cs
    - tests/WorkflowEngine.Tests/CommandRegistryTests.cs
key-decisions:
  - "execute-json validates and normalizes envelopes but does not execute transitions in Phase 3."
  - "Canonical request hashing is stable across JSON property order and excludes idempotency/correlation fields."
patterns-established:
  - "Command handlers are injected behind small application interfaces and invoked by CommandRegistry."
  - "Workflow contracts serialize with shared System.Text.Json camelCase options."
requirements-completed: [CON-01, CON-02, CON-03]
duration: 35 min
completed: 2026-05-07
---

# Phase 3 Plan 01: Workflow Action Contract Summary

**Workflow action envelopes now parse, validate, normalize override targets, and return non-executing WorkflowActionResult output.**

## Performance

- **Duration:** 35 min
- **Started:** 2026-05-07T15:25:00Z
- **Completed:** 2026-05-07T16:00:00Z
- **Tasks:** 4
- **Files modified:** 17

## Accomplishments

- Added `WorkflowActionEnvelope` and `WorkflowActionResult` DTOs with stable camelCase JSON serialization.
- Added full non-database envelope validation, including MakerPrimary `saveAsDraft` consistency.
- Added deterministic console override normalization and canonical request hashing for Phase 4 idempotency reuse.
- Replaced the `execute-json` stub with a command handler that emits readable default output or JSON via `--format json`.

## Task Commits

Task commits are included in the plan implementation commit:

1. **Task 1: Add workflow action contract DTOs and JSON options** - pending plan commit
2. **Task 2: Implement non-database envelope validation** - pending plan commit
3. **Task 3: Implement override normalization and canonical request hashing** - pending plan commit
4. **Task 4: Wire execute-json command output without workflow execution** - pending plan commit

## Files Created/Modified

- `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs` - Button-click request DTOs.
- `src/WorkflowEngine.Application/Contracts/WorkflowActionResult.cs` - Button-click result DTOs.
- `src/WorkflowEngine.Application/Contracts/WorkflowJson.cs` - Shared JSON options.
- `src/WorkflowEngine.Application/Validation/WorkflowActionEnvelopeValidator.cs` - Non-database validation rules.
- `src/WorkflowEngine.Application/Normalization/WorkflowActionEnvelopeNormalizer.cs` - Console override and effective key derivation.
- `src/WorkflowEngine.Application/Normalization/CanonicalRequestHasher.cs` - Stable request hash generation.
- `src/WorkflowEngine.Application/Commands/ExecuteJsonCommandHandler.cs` - Non-executing command implementation.
- `tests/WorkflowEngine.Tests/*Envelope*Tests.cs` and `ExecuteJsonCommandTests.cs` - Contract, validation, normalization, and command coverage.

## Decisions Made

- Valid envelopes return `AcceptedForPhase3Validation` to make the Phase 3 execution boundary explicit.
- Valid `execute-json` output includes `notification.queued=false` and `audit.written=false` so the result shape is present without claiming side effects occurred.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Initial nullable warnings and invalid-envelope hashing null handling were fixed before completion.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

Plan 03-02 can build `show-context` on the shared contract serialization patterns and command handler injection style.

---
*Phase: 03-workflow-contracts-and-console-actions*
*Completed: 2026-05-07*

