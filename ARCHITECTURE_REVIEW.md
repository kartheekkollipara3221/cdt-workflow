# Architectural Review: .NET Workflow Engine POC

Date: 2026-05-10

Scope: second-pass architecture review against `PROJECT.md`, especially sections 5, 8, 13, 15, 18, 22, and 28.

Verdict: Remediation complete in working tree.

## Architecture View

The intended architecture is still the right shape:

```text
Console command layer
  -> Application command handlers, validation, normalization, result contracts
  -> Infrastructure services
     -> EF Core SQL persistence
     -> metadata-driven workflow execution
     -> workflow context reader
     -> SLA scanner
     -> notification outbox sender abstraction
```

The reusable boundary is mostly intact. Portfolio Trade behavior is represented through seed metadata, samples, and tests; the execution service reasons over generic workflow concepts: instance, state, transition, action, role, task, SLA, notification route, audit, and idempotency.

The reusable layering remains sound. The original execution transaction-boundary defects have been fixed in the working tree: concurrency guards are rechecked inside the transaction, state-changing updates use a `WorkflowInstance.Version` predicate, and the idempotency replay record is inserted before the same transaction commits.

## Revisit Summary

Current status after remediation work:

| Severity | Status | Finding | Primary evidence | Required fix |
|---|---|---|---|---|
| Blocker | Fixed in working tree | Optimistic concurrency was not enforced at the database write boundary. | `WorkflowExecutionService.cs` | Guards are rechecked inside the transaction; state-changing updates use a `Version` predicate and return `ConcurrencyConflict` on stale data. |
| Major | Fixed in working tree | Idempotency replay data was persisted after side effects committed. | `WorkflowExecutionService.cs` | The replayable result and `IdempotencyRecord` are now inserted before the workflow transaction commits. |
| Major | Fixed in working tree | SMTP sender configuration validation was lazy, not guaranteed at startup. | `InfrastructureServiceCollectionExtensions.cs`, `SmtpNotificationSender.cs` | Infrastructure registration now validates the selected notification sender and SMTP host/from immediately. |
| Major | Fixed in working tree by formal manual prerequisite | Tier 2 SMTP tests depended on a manually running fake SMTP server without a clear contract. | `SmtpNotificationSenderIntegrationTests.cs`, `SmtpFactAttribute.cs`, test README | SMTP Tier 2 remains opt-in, but the manual smtp4dev prerequisite, ports, env var, and failure message are explicit. |
| Minor | Fixed in working tree | Several tests/design-time contexts hardcoded SQL Server connection strings. | `TestSqlConnectionFactory.cs`, SQL-backed tests, `WorkflowDbContextFactory.cs` | SQL-backed tests now use a centralized resolver honoring `ConnectionStrings__DefaultConnection` with the approved local fallback. |

## Fixed Execution-Boundary Defects

### Optimistic concurrency was checked too early

Before the fix, `WorkflowExecutionService.Execute` loaded the instance with `AsNoTracking()` and validated concurrency before opening the transaction. Later, `ExecuteSideEffects` opened a transaction and reloaded the instance by internal `Id` only:

```text
read instance outside transaction
check expected state/task/version
open transaction
reload by Id
mutate CurrentStateKey
increment Version
save changes
```

That did not satisfy the required section 15 pattern:

```sql
UPDATE WorkflowInstance
SET CurrentStateKey = @new,
    Version = Version + 1
WHERE Id = @id
  AND Version = @expected
```

A competing request could move the workflow after the pre-check but before the write. The second request could then close/create tasks or fail through SQL constraints instead of returning the contract result `ConcurrencyConflict`.

Implemented behavior:

1. Resolve idempotency replay first.
2. Resolve instance and metadata.
3. Open transaction.
4. Re-read current instance and pending task inside the transaction.
5. Validate all provided guards against current persisted state.
6. Apply a state-changing update with `Version` in the predicate.
7. Return `ConcurrencyConflict` with no side effects if guards fail or the update affects zero rows.
8. Commit section data, task changes, notification outbox, audit, workflow state, and idempotency record together.

Verification run after the fix:

```text
dotnet test src\WorkflowEngine.sln --filter "WorkflowTransitionTests|WorkflowExecutionValidationTests|WorkflowActionResultHashTests"
dotnet build src\WorkflowEngine.sln -warnaserror
dotnet test src\WorkflowEngine.sln
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
dotnet run --project src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\03-checker-return-to-maker.json
dotnet run --project src\WorkflowEngine.Console -- demo
```

## Architectural Passes

These areas still align with the project architecture:

| Area | Result | Notes |
|---|---|---|
| Reusable workflow model | Pass | Execution uses metadata rows rather than hardcoded Portfolio Trade transition methods. |
| Portfolio Trade isolation | Pass | Portfolio Trade appears in seed/sample/test data, not as a dedicated engine service. |
| Contracts | Pass | `WorkflowActionEnvelope`, `WorkflowContextResponse`, `AvailableWorkflowAction`, and `WorkflowActionResult` exist as separate concepts. |
| Single-active-state MVP | Pass | Filtered pending-task uniqueness and command behavior preserve one pending task. |
| Time handling | Pass | No direct `DateTime.UtcNow` usage found in source. |
| Prohibited frameworks | Pass | No CQRS, MediatR, event sourcing, BPMN, brokers, microservices, or plugin framework added. |
| Notification abstraction | Pass with startup caveat | `INotificationSender` exists with Console, NoOp, and MailKit SMTP implementations. |
| SQL-backed persistence | Pass | EF Core SQL Server is the runtime persistence model. |

## Priority

Fix in this order:

All accepted architecture defects from this review have working-tree fixes.

SMTP Tier 2 tests intentionally remain opt-in and require a local fake SMTP server. Default `dotnet test` still excludes them.
