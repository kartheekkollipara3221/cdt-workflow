---
status: complete
date: 2026-10-17
quality_gate: passed
---

# C14 SLA batch processing; archive decision deferred

Changed: RunSlaService now pages due identifiers with default100 configurable via
Sla:BatchSize and uses a fresh context per page. Existing request/result/constructor
contracts remain; a new constructor overload accepts SlaProcessingOptions. Deferred
archive decisions now appear near the end of docs/OPEN-ISSUES.md.

Why: all due schedule identifiers previously materialized together and successful
iterations accumulated tracked entities in one context. Batches bound candidate
and new tracked-entity growth while preserving normal reminder generation.

## Implementation and compatibility

- Keyset cursor uses NextReminderAtUtc/Id, preserves ordering and advances past
  invalid/skipped rows. Run-start upper schedule ID avoids chasing newly created
  schedules. No OFFSET over rows removed from eligibility by successful processing.
- One short transaction per schedule, current-time/cadence rules, recipient snapshots,
  occurrence deduplication and C06 failure isolation are preserved.
- New contexts borrow the caller's current SQL connection without ownership;
  provider settings/interceptors and runtime command timeout are retained.
- Unchanged schedules already tracked by the caller are refreshed with current
  values and rowversion so later inspection/completion works. Unsaved caller edits
  are not saved or overwritten, including when automatic change detection is off.
- No schema, package, seed, public JSON, console command or workflow key changes.
  No active workflow closure, production operation or archive implementation.

## Tests and review

| Check | Evidence |
|---|---|
| Initial red test | 103 schedules used one context rather than two |
| Initial focused regression | Exposed stale caller values/rowversions; fixed targeted refresh and Unchanged state |
| Connection red tests | Reproduced owned-connection disposal and lost runtime connection settings |
| Caller-edit red test | Reproduced discarded schedule edit with AutoDetectChangesEnabled=false |
| Full core non-SMTP suite | 519 passed, zero failed/skipped |
| Final targeted SLA/options suite | 39 passed, zero failed/skipped |
| Full API suite | 70 passed, zero failed/skipped |
| Both warning-as-error builds | Passed, zero warnings/errors |
| Seed verification run | Passed through Section21QualityGateTests and fresh-clone walkthrough |
| Sample JSON executed | All ten samples; return branches, demo and fresh-clone checks passed |
| Read-only code review | Findings corrected; final review reports no remaining material source findings |

The full core run preceded the final two-line explicit-change-detection guard and
its additional regression test. The final 39-test focused suite includes that test
and reruns all Phase5SlaTests, RecurringSlaMonitorTests and SlaProcessingOptionsTests
after the guard. API tests and both builds ran after the guard. Counts overlap;
do not add full and focused suite counts as distinct tests. Twelve new cases were
added in total. No SMTP suite rerun: sending code is unchanged; queue creation,
recipient content and deduplication are covered by the core SLA regressions.

Coverage includes multi-page processing (103 schedules), tied ordering, batch sizes
1/2/3, future schedules, poison-row continuation, repeated scans, existing concurrent
occurrence deduplication, recurrence, cancellation/fatal propagation, caller edits,
runtime SetConnectionString/SetDbConnection, owned connections and command timeout.
Test contexts use dedicated SQL databases through WORKFLOW_ENGINE_TEST_CONNECTION_STRING.

Commands:

```text
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter Category!=Smtp
dotnet test tests/WorkflowEngine.Tests --no-restore -m:1 --filter FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~RecurringSlaMonitorTests|FullyQualifiedName~SlaProcessingOptionsTests
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror -m:1
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror -m:1
```

Evidence: ignored artifacts/c14/c14-core.trx, c14-focused-reviewed.trx, c14-api.trx
and earlier red/intermediate runs. The filter above must be shell-quoted when run.

Files updated: RunSlaService.cs, SlaProcessingOptions.cs, InfrastructureServiceCollectionExtensions.cs,
Phase5SlaTests.cs, SlaProcessingOptionsTests.cs, docs/SLA_BATCH_PROCESSING.md,
docs/OPEN-ISSUES.md, task PLAN/SUMMARY, STATE and local CODEX_HANDOFF.md.

Open issues: legacy result lists and total run work are not bounded; callers must
continue normal scoped lifetimes. Concurrent changes to ordering can defer a row
until the next scheduler invocation; this is not a point-in-time snapshot. Query
plans/index tuning and target whole-table lookups remain C14 follow-up work. No
production capacity claim. Archive schema, retention, historical reads, replay
protection and purge policy are deferred. No remote push or deployment performed.
