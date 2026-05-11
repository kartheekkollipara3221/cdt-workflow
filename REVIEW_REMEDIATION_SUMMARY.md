# Review Remediation Summary

## Status

Overall remediation status: MOSTLY COMPLETE

Production readiness after fixes: NEAR READY

The actionable correctness, schema, configuration, CI, and formatting findings from `FULL_PROJECT_REVIEW.md` were addressed. Full SQL-backed verification is still blocked in this local environment by SQL Server integrated authentication failing with:

```text
The target principal name is incorrect. Cannot generate SSPI context.
```

## Fixes Implemented

| Review Finding | Status | Summary |
|---|---|---|
| Envelope identity can mismatch persisted workflow instance | Fixed | `execute-json` now validates request workflow key, business object type, and business object id against the persisted workflow instance before saving data or moving state. |
| Notification outbox can double-send under concurrent processors | Fixed | Outbox processing now claims rows first using `Processing`, a processing token, lease timestamp, retry count, and last error before sending. |
| Database integrity mostly enforced by application code | Fixed | Runtime FK constraints and check constraints were added for workflow instances, tasks, audits, outbox rows, and idempotency records. |
| `start-workflow` race between existence check and insert | Fixed | SQL unique violations are caught and mapped to the documented `AlreadyExists` result by re-reading the existing instance. |
| Action section echo not validated against metadata | Fixed | Resolved action metadata now validates UI section, data-save section, and button key before side effects. |
| Committed defaults tied to one developer SQL Server | Fixed | The committed development connection string was removed; SQL-backed tests now require explicit configuration. |
| No CI / important suites excluded by default | Fixed | GitHub Actions workflow added for build, format, default tests, vulnerability audit, and opt-in SMTP/extended integration jobs. |
| Formatting check fails | Fixed | `dotnet format` was run and format verification now passes. |

## Tests and Verification

Passed:

- `dotnet restore src\WorkflowEngine.sln -p:NuGetAudit=false`
- `dotnet build src\WorkflowEngine.sln --no-restore`
- `dotnet format src\WorkflowEngine.sln --verify-no-changes --no-restore`
- `dotnet list src\WorkflowEngine.sln package --vulnerable`
- Non-SQL focused test subset: 58 passed
- Schema constraint tests: 20 passed

Blocked by local SQL auth:

- `dotnet test src\WorkflowEngine.sln --no-build`
- `dotnet run --project src\WorkflowEngine.Console --no-build -- verify-seed`
- Sample JSON execution

## Remaining Items

- Run full SQL-backed tests and sample JSON once a working SQL connection string is available through `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` or `ConnectionStrings__DefaultConnection`.
- Production authentication/authorization remains a future API boundary decision and was not added to the console POC.
- Structured logging/metrics remain outside this remediation pass.
