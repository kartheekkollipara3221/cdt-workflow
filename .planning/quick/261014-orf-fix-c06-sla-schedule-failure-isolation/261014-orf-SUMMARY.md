---
status: complete
date: 2026-10-14
quality_gate: existing-clean-build-failures
code_commit: ad47084
---

# C06: isolate invalid SLA schedules

Changed: one schedule's known metadata/calendar failure now rolls back that
schedule, clears pending EF changes, adds a warning and continues the scan.
Why: missing/inactive routes, invalid schedule metadata, invalid time zones,
DST gaps and date overflow previously escaped the loop and blocked later reminders.

## Implementation and compatibility

Only `RunSlaService.cs` changes production behavior. Missing/inactive notification
routes and explicit metadata checks produce a private schedule-data exception.
Known calendar exceptions are translated only inside calculation boundaries.
The per-schedule transaction catches this private exception, rolls back, clears
tracking (including any staged outbox insert), and adds a warning containing
schedule ID, task ID, workflow ID and reason. `RunSlaResult.Warnings` and console
warning output already exist; no public contract addition was needed.

Cancellation, unexpected runtime failures, database outages and rollback failures
continue to propagate. Existing database concurrency and duplicate-occurrence
handlers remain in place. Next-reminder calculation now returns the calculated
value before assignment; its successful timing rules are unchanged.

No schema, migration, seed, package, DI, HTTP, workflow state, recipient snapshot,
email-content or valid reminder-cadence change. No workflows were closed.
The existing review reports and target snapshot were not edited.

## Verification executed

Test processes used the approved local SQL Server with the repository's dedicated
test database names. Notifications were NoOp/test doubles; SMTP tests were excluded.

| Check | Result |
|---|---|
| Initial C06 regression run before fix | 8 expected failures; 3 propagation tests passed |
| First focused SLA run after fix | 26 passed |
| Final core suite excluding SMTP | 489 passed, 2 failed, 0 skipped (491 total) |
| All new C06 cases within final core suite | 12 passed |
| API suite | 68 passed, 0 failed, 0 skipped |
| Core build, `--no-restore -warnaserror -m:1` | Passed, zero warnings/errors |
| API build, `--no-restore -warnaserror -m:1` | Passed, zero warnings/errors |
| Seed verification | Passed via Section21QualityGateTests and FreshClone VerifySeed |
| All ten public console sample JSON fixtures | Passed via SampleExpectationTests |
| Supported return/rework/completion paths and demo | Passed in core regression suite |
| Read-only independent code review | No material findings |
| Git diff whitespace check | Passed |

The two failures are the unchanged `FreshCloneWalkthroughTests.BuildSucceedsWithWarningsAsErrors`
and `QuickstartSequence_ProducesExpectedOutputs`. Isolated fresh restore/build
fails on NU1902/NU1903 for the existing `System.Security.Cryptography.Pkcs` 10.0.0
dependency, as recorded in the earlier cleanup. No audit suppression or dependency
change was applied. A fully passing clean-build gate is **not** claimed.

Final C06 cases cover duration/application metadata, unknown zone, missing/inactive
route, current/next DST gap, recurrence overflow, display-date overflow, and
cancellation/database/unexpected failures. They check unchanged persisted schedule,
task and instance; warning identity; no staged-outbox leak; healthy email contents;
repeated-scan deduplication; and recovery of the same active workflow after a
test-only metadata repair.

Commands:

```text
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~C06
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore -m:1 --filter "FullyQualifiedName~Phase5SlaTests|FullyQualifiedName~RecurringSlaMonitorTests"
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter Category!=Smtp
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror -m:1
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror -m:1
```

Local ignored TRX evidence: `artifacts/c06/c06-red.trx`, `c06-green.trx`,
`c06-core.trx`, and `c06-api.trx`. These logs do not travel with a clone.

## Target rollout (not executed)

1. Identify the actual deployed host that calls `IRunSlaService.RunAsync`, its
   engine build and target framework. The supplied target snapshot is incomplete;
   this local .NET 10 build is not proof of target deployment compatibility.
2. Integrate source commit `ad47084`, build/test in that target, and retain the
   previous deployable artifact for rollback. No database script or init-db is needed.
3. Pause scheduled SLA invocations and allow an in-flight scan to finish. Deploy
   the updated engine/host artifact, then resume the runner. Active workflows stay open.
4. Retain/report `RunSlaResult.Warnings`. Console callers already print `WARN:`.
   Check that healthy schedules queue reminders and identify any skipped schedule
   by its schedule/task/workflow IDs. This change does not install a runner or logger.
5. Review and repair malformed persisted schedules separately under the normal
   data-change process. Updating policy metadata alone does not rewrite existing
   schedule snapshots. Do not close workflows or reset their history to deploy this fix.
6. If rollback is necessary, stop the runner and restore the prior artifact.
   Schema/data rollback is unnecessary, but the previous C06 failure behavior returns.

## Completion summary

Changed: C06 isolated failure handling and 12 SQL-backed regression cases.
Why: one invalid schedule must not prevent healthy workflows receiving reminders.
Tests run: 557 passed across final core/API suites; 2 pre-existing clean-build failures.
Seed verification run: yes, isolated SQL-backed suite databases.
Sample JSON executed: all ten fixtures plus supported branch/demo checks.
Files updated: RunSlaService.cs, Phase5SlaTests.cs, task PLAN/SUMMARY,
.planning/STATE.md, CODEX_HANDOFF.md.
Open issues: existing dependency advisory gate; target deployment/runner verification;
repair of any actual malformed target schedules. Other review issues remain paused.

Code commit: `ad47084`. User authorized publication on 2026-10-14 to
`origin/console-code-first-0518` with full release notes in the publication commit.
The known clean-build limitations above remain disclosed. Deployment is not performed.
CODEX_HANDOFF.md also contains prior uncommitted review work and is intentionally
left outside this task's commits; its current C06 checkpoint is preserved locally.
