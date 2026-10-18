# H01 bounded outbox fairness implementation plan

Goal: fix the user-authorized High finding without changing public contracts, schema, recipients, workflow behavior or unlimited-batch defaults.

Execution: inline GSD quick task with test-first debugging and verification. The user's instruction "fix this high" authorizes this focused implementation and its isolated regression tests. No production operations or remote push. Keep the existing branch and unrelated uncommitted documentation untouched.

## Design

Root cause: each bounded call sorts by immutable creation time, so an eligible failed batch repeatedly excludes later mail.

Use durable eligibility age for bounded selection: `NextAttemptAtUtc ?? CreatedAtUtc`, then RetryCount for equal timestamps, then CreatedAtUtc and Id. Existing unbounded selection remains CreatedAtUtc/Id. On a bounded zero-delay failure, write the current failure time as NextAttemptAtUtc; this still permits an immediate retry. Positive-delay timestamps retain their existing meaning and calculation. No schema change, new setting or process-local cursor.

Why this option: an older due retry can precede messages created after it became eligible, so continuous new work does not automatically take priority. A failed attempt moves its eligibility time forward, letting already-waiting messages progress. RetryCount resolves ties at a fixed/low-resolution clock. Progress assumes regular calls, a progressing clock and finite eligible work ahead of a given row; this is not a throughput guarantee under overload or clock corruption.

Rejected alternatives: increasing batch size only moves the failure threshold; always prioritizing new mail can starve retries; adding a last-attempt column is unnecessary for this scope. Recipient validation/dead-letter/purge policies remain unchanged.

## Tasks

- [x] Add SQL-backed regression tests in `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`: full failed batch with zero/positive delay, expiry across newly constructed processors, fixed-clock tie, due retry versus newer work, legacy order/null behavior and bounded ownership behavior. First run must expose H01 on the original processor.
- [x] Change only `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs` selection ordering and zero-delay failure timestamp persistence. Re-run focused tests, inspect SQL translation/claim behavior through actual service tests, and review source diff.
- [x] Run core non-SMTP and API suites sequentially against dedicated local test databases; builds with warnings as errors. Core suites include seed/sample/branch/demo/fresh-clone gates. Run local SMTP tests if capture infrastructure is available. Update `docs/OUTBOX_RETRY_ROLLOUT.md`, `docs/OPEN-ISSUES.md`, task SUMMARY, STATE and local handoff. Record exact evidence and limits. Commit only task-owned tracked source/tests/docs/planning changes after checks; no push.

Test commands use WORKFLOW_ENGINE_TEST_CONNECTION_STRING with explicit local server DOLLY. Existing test factories replace InitialCatalog with dedicated test database names; do not run production init-db.

Commands:

```powershell
dotnet test tests/WorkflowEngine.Tests/WorkflowEngine.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~Phase5OutboxTests
dotnet test src/WorkflowEngine.sln --no-restore -m:1 --filter 'Category!=Smtp'
dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -m:1
dotnet build src/WorkflowEngine.sln --no-restore -warnaserror -m:1
dotnet build workflow-api/WorkflowEngine.WorkflowApi.slnx --no-restore -warnaserror -m:1
```

Compatibility: existing constructors, results, status codes and retry eligibility checks remain unchanged. Only configured bounded processing changes order. For bounded zero-delay failures, a non-null immediate eligibility timestamp replaces null; no delay is introduced. Upgrade all bounded backlog workers for consistent fairness; older workers retain creation ordering. Rolling back binaries retains data but restores starvation risk. No active workflow closure required.
