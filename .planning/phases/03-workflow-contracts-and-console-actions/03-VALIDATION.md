---
phase: 03
slug: workflow-contracts-and-console-actions
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-07
updated: 2026-05-07
---

# Phase 03 - Validation Strategy

> Retroactive Nyquist validation for WorkflowActionEnvelope contracts, execute-json, and show-context.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit on .NET 10 |
| **Config file** | `tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj` |
| **Quick run command** | `dotnet test src\WorkflowEngine.sln -m:1 --filter "FullyQualifiedName~ExecuteJsonCommandTests|FullyQualifiedName~WorkflowActionEnvelopeValidationTests|FullyQualifiedName~WorkflowActionEnvelopeNormalizationTests|FullyQualifiedName~WorkflowContextReaderTests|FullyQualifiedName~ShowContextCommandTests"` |
| **Full suite command** | `dotnet test src\WorkflowEngine.sln -m:1` |
| **Estimated runtime** | ~20 seconds focused, ~40 seconds full |

## Sampling Rate

- **After every task commit:** Run the focused command for the touched contract or command area.
- **After every plan wave:** Run `dotnet test src\WorkflowEngine.sln -m:1`.
- **Before `$gsd-verify-work`:** Full suite must be green.
- **Max feedback latency:** ~40 seconds for the current suite.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | CON-01, CON-02 | N/A | DTOs preserve the required envelope/result contract and do not add forbidden fields. | unit | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~WorkflowActionEnvelopeSerializationTests` | yes | green |
| 03-01-02 | 01 | 1 | CON-01 | N/A | Invalid actor, role, data-save, payload, and MakerPrimary combinations return validation errors before execution. | unit | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~WorkflowActionEnvelopeValidationTests` | yes | green |
| 03-01-03 | 01 | 1 | CON-03 | N/A | Overrides are applied before hashing/result construction and effective idempotency is deterministic. | unit | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~WorkflowActionEnvelopeNormalizationTests` | yes | green |
| 03-01-04 | 01 | 1 | CON-03 | N/A | `execute-json` parses samples, emits result JSON, reports validation failures, and dispatches through the registry. | unit | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~ExecuteJsonCommandTests` | yes | green |
| 03-02-01 | 02 | 2 | CON-02 | N/A | Workflow context serializes the required form-open blocks and excludes forbidden conditional-action fields. | unit | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~WorkflowContextSerializationTests` | yes | green |
| 03-02-02 | 02 | 2 | CON-02, CON-04 | N/A | SQL-backed context returns metadata-derived actions, wrong-role read-only context, SLA status, and does not mutate runtime rows. | integration | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~WorkflowContextReaderTests` | yes | green |
| 03-02-03 | 02 | 2 | CON-04 | N/A | `show-context` argument parsing and JSON output work for default JSON, explicit JSON, wrong role, missing args, and missing instance. | integration | `dotnet test src\WorkflowEngine.sln -m:1 --filter FullyQualifiedName~ShowContextCommandTests` | yes | green |

## UAT Gap Closure

| UAT Item | Coverage | Status |
|----------|----------|--------|
| Valid Execute JSON Contract | `ExecuteJsonCommandTests.ExecuteJsonCanReturnWorkflowActionResultJson` plus `WorkflowActionEnvelopeSerializationTests` | green |
| Invalid Envelope Validation | `ExecuteJsonCommandTests.ExecuteJsonReturnsValidationFailedForInvalidEnvelope` and `WorkflowActionEnvelopeValidationTests` | green |
| Execute JSON Overrides | `ExecuteJsonCommandTests.ExecuteJsonAppliesOverridesBeforeResultConstruction` and `WorkflowActionEnvelopeNormalizationTests` | green |
| Checker Show Context | `WorkflowContextReaderTests.CheckerSeesSqlBackedActionsAndRequestDefaults` and `ShowContextCommandTests.ShowContextDefaultsToJsonAgainstSql` | green |
| Wrong-Role Show Context | `WorkflowContextReaderTests.WrongRoleSeesContextButNoActionsAndReaderDoesNotWriteRows` and `ShowContextCommandTests.ShowContextSupportsExplicitJsonFormatAndWrongRole` | green |
| Show Context Read-Only Behavior | `WorkflowContextReaderTests.WrongRoleSeesContextButNoActionsAndReaderDoesNotWriteRows` | green |

## Wave 0 Requirements

Existing xUnit infrastructure covers all Phase 3 requirements.

## Manual-Only Verifications

All Phase 3 behaviors have automated verification.

## Validation Audit 2026-05-07

| Metric | Count |
|--------|-------|
| Gaps found | 6 |
| Resolved | 6 |
| Escalated | 0 |

Notes:
- No new test files were required. Existing tests already cover the six skipped UAT checks.
- `execute-json` now returns a real Phase 4 execution result on valid requests; Phase 3's old deferred result code is superseded by the implemented execution path while preserving the contract-shaped JSON result.

## Validation Sign-Off

- [x] All tasks have automated verification.
- [x] Sampling continuity: no 3 consecutive tasks without automated verification.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency < 60s.
- [x] `nyquist_compliant: true` set in frontmatter.

**Approval:** approved 2026-05-07
