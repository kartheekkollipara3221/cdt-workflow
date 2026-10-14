---
status: complete
date: 2026-10-14
quality_gate: passed
---

# C13: bounded outbox processing and durable retry scheduling

Changed: optional validated `Notifications:Outbox` settings; lightweight bounded backlog selection and bounded claim warnings; durable exponential retry timing in background and API delivery paths; additive `NextAttemptAtUtc` column and eligibility index; tests and rollout guide.

Why: a large backlog previously materialized every full message and retried failed rows on every call. An API dispatcher could independently bypass any background retry schedule. Both paths now check persisted eligibility during selection and the atomic ownership update. Failed rows remain Pending, retain error/count evidence and are never silently discarded.

## Approved scope and compatibility

The user approved the implementation proposal, including additive schema changes, before code changes. GSD quick execution ran inline; milestones in PROJECT.md were not changed. A separate read-only review found no material issues.

- Legacy defaults: unlimited batch, immediate retry. Recommended opt-in: batch 100, initial delay 60 seconds, cap 3600 seconds.
- Preserve three-argument constructors, DI entry point, public request/response shapes, recipient snapshots, notification keys, workflow state and SLA semantics.
- API dispatch stays scoped to its action and is not truncated by the backlog batch setting. It rejects deferred delivery through the existing failure result.
- Upgraded consumers always honor future persisted timestamps, even with legacy options. Successful sends clear them; retry count is retained and saturates at Int32.MaxValue.
- A nullable column leaves old rows eligible without a backfill. Standalone target SQL is rerunnable and verifies existing column/index definitions; EF-managed databases have a migration and snapshot. Choose one schema-management path.
- No active workflow closure, production reset, deployment, package change or target-snapshot edit.

## Verification evidence

| Check | Result |
|---|---|
| Initial core regression tests | 2 expected failures: batch 2 processed 5; immediate retry repeated poison row |
| Initial API regression test | Expected failure: future retry row was delivered |
| First focused outbox suite | 16 passed |
| Final core non-SMTP suite | 508 passed, 0 failed/skipped |
| Final API suite | 70 passed, 0 failed/skipped |
| Final local SMTP suite | 5 passed, 0 failed/skipped |
| Core and API warning-as-error builds | Passed, 0 warnings/errors |
| EF/manual in-place upgrade | Passed with active workflow and queued row preserved; manual SQL executed twice |
| Seed verification run | Passed via Section21QualityGateTests and fresh-clone walkthrough |
| Sample JSON executed | All ten samples passed SampleExpectationTests; Checker/Trading return branches and demo passed |
| Diff whitespace validation | Passed |

Final test total: **583 passed**. Added 17 core cases and 2 API cases. Coverage includes delay boundaries, exponential cap, persisted retries across contexts, malformed-row fairness, batching/order, atomic claim eligibility races, existing concurrent ownership, bounded warning results, lease expiry, API/background cooperation and both upgrade paths. Uncertain SMTP acceptance is simulated with a sender that records acceptance then throws; it demonstrates possible repeat delivery, not a real crash/SMTP acknowledgement experiment.

Commands:

```text
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter Category!=Smtp
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-build --no-restore -m:1 --filter Category=Smtp
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror -m:1
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror -m:1
```

Tests used dedicated SQL database names under WORKFLOW_ENGINE_TEST_CONNECTION_STRING. SMTP used the existing local smtp4dev tool on loopback ports 3025/5000, isolated artifacts/c13/smtp4dev data and no user settings/relay. An initial helper launch supplied a directory instead of a database filename and failed before tests; the owned process was stopped and restarted with the correct base data path. HTTP 200 readiness was verified before all five SMTP tests passed. Final owned PID 17356 was stopped; no listener remained on those ports. Evidence: ignored artifacts/c13/c13-{red,api-red,outbox,core,api,smtp}.trx and helper logs.

Files updated: Infrastructure options/DI/processor/entity/model/migration/snapshot; API dispatcher; core/API regression tests; sql/upgrades/20261014_outbox_retry_scheduling.sql; docs/OUTBOX_RETRY_ROLLOUT.md; task PLAN/SUMMARY, STATE and OPEN-ISSUES. Local CODEX_HANDOFF.md updated while retaining unrelated earlier uncommitted notes.

Open issues: deployment must apply schema first and coordinate all consumers before enabling settings. SMTP remains at least once; 15-minute lease and possible duplicates after uncertain acceptance remain. Database claim-release failure can still abort a run. Maximum attempts/dead-letter policy is deliberately not introduced. Other consolidated-review findings remain outside this task. No push or production deployment performed.
