# H01 bounded outbox retry starvation - completed 2026-10-18

Changed: bounded processing now orders eligible rows by durable eligibility time, then retry count, creation time and ID. A bounded zero-delay failure records its failure time without adding a wait. Unbounded creation order and null retry timestamps remain unchanged.

Why: immutable creation order allowed a full failed batch to block every later healthy message on each poll. After a failure, the updated order lets already-waiting mail progress. Due retries can still precede mail created after their retry time. No schema, package, public contract, recipient, seed or workflow changes.

## Executed evidence

Actual SQL Server tests used dedicated local test databases on DOLLY. The test factory replaces the connection's database name. Production was not touched.

| Check | Result | Evidence under artifacts/h01 |
|---|---|---|
| New regressions before the fix | 4 failed as expected; 2 controls passed | h01-red.trx |
| Focused outbox/options suite after the fix | 33 passed | h01-focused.trx |
| Final core non-SMTP suite | 527 passed, 0 failed/skipped | h01-core.trx |
| API suite | 70 passed, 0 failed/skipped | h01-api.trx |
| Local capture-only SMTP suite | 5 passed, 0 failed/skipped | h01-smtp.trx |
| Core and API builds, warnings as errors | Both passed; 0 warnings/errors | core-build.log, api-build.log |

Final suites total 602 passing tests; the focused count overlaps the core count. The final core run includes the additional unbounded-null assertion added after the focused run. The task-owned SMTP capture process was stopped after verification. SMTP was sent only to the local capture server.

Seed verification run: SQL seed verification, fresh-clone init/verify-seed and section 21 quality gates passed in the core suite.

Sample JSON executed: all ten workflow-action samples passed their expected results through SampleExpectationTests; return branches, demo and fresh-clone quickstart tests also passed.

New coverage includes full failed batches of 1/100, zero/positive delays, a later poll after every retry is due, new contexts/processors, fixed-clock ties, due retries ahead of newer mail, unbounded ordering/null defaults, and bounded concurrent claim ownership. Red failures were healthy messages missing from Processed, confirming the defect through actual SQL/service execution.

Independent source review found no material issues; see [REVIEW.md](REVIEW.md). Review did not execute tests. Execution results above are separate evidence. No production performance or load claim is made.

## Files updated

- src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs
- tests/WorkflowEngine.Tests/Phase5OutboxTests.cs
- docs/OUTBOX_RETRY_ROLLOUT.md
- docs/OPEN-ISSUES.md
- .planning/STATE.md
- This task's PLAN, SUMMARY and REVIEW.
- CODEX_HANDOFF.md updated locally; excluded from the scoped commit because it contains earlier uncommitted work.

## Rollout and open issues

No additional migration beyond C13 and no active workflow closure or data reset are required. Update all bounded backlog workers for consistent ordering. Old null-timestamp failures rotate after their next failed attempt. Binary rollback preserves data but restores starvation risk. No push or deployment was performed.

Progress assumes continued polling, a progressing clock and finite work ahead. This does not guarantee latency under overload. Computed SQL sorting may need measured index/query tuning. Existing at-least-once delivery, lease duration and unlimited retry policy remain unchanged. Other audit findings and archive decisions remain outside this fix.

Historical audit reports and target snapshot were preserved. Of 608 protected audit-baseline paths, only the intended processor and test file changed. Git whitespace checks passed.
