# Defect 01: Optimistic Concurrency Checked Too Early

Status: Fixed in working tree

Severity: Blocker

Source review: `ARCHITECTURE_REVIEW.md`

## Summary

`WorkflowExecutionService` checks workflow concurrency guards before opening the transaction, but the later state-changing write does not require the database row to still have the expected `WorkflowInstance.Version`.

This means two concurrent submissions can both pass the initial stale-form check. The first request commits normally. The second request may continue into side-effect code using stale assumptions and fail through EF/SQL exceptions instead of returning the required `ConcurrencyConflict` result.

## Production Scenario

Two users open the same workflow item at nearly the same time:

```text
WorkflowInstanceId: WF-PT-1001
State: CheckerReview
Pending task: CheckerReviewTask
Version: 2
```

Both users submit actions with `expectedInstanceVersion = 2`.

Request A:

```text
checker1 -> CheckerSendToTradingDesk
```

Request B:

```text
checker2 -> CheckerReturnToMaker
```

Request A commits first:

```text
Closes CheckerReviewTask
Moves state to TradingDeskReview
Creates TradingDeskReviewTask
Queues notification
Writes audit
Increments version to 3
```

Request B has already passed the pre-transaction check. Inside its transaction, the service reloads the instance by internal `Id` only, not by `Id + Version = 2`. It can then attempt work based on the stale `CheckerReview` view.

## Real-Time Impact

Under production load, this turns a normal concurrent workflow race into unreliable behavior:

- The stale request may throw because the expected pending task has already been closed.
- The stale request may hit a SQL unique-index or EF exception instead of returning a business result.
- Users may see a 500-style error instead of a clear refresh-required message.
- Operators get noisy production errors for expected concurrency races.
- Retries or double-clicks increase pressure on the same workflow row.
- The workflow engine cannot reliably guarantee that exactly one real-time decision wins cleanly.

The required user-facing behavior is:

```text
One request wins.
Every stale competing request returns resultCode = ConcurrencyConflict.
No stale request saves section data, closes tasks, creates tasks, queues notifications, writes audit, or inserts idempotency records.
```

## Code Evidence

Primary file:

```text
src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
```

Relevant flow:

```text
Line 35-37: instance loaded AsNoTracking before transaction
Line 92-97: pending task read and CheckConcurrency called before transaction
Line 218: transaction begins later
Line 220: instance reloaded by Id only
Line 256-257: state changed and Version incremented without Version predicate
Line 354-355: SaveChanges and commit
```

Current write behavior is effectively:

```text
UPDATE WorkflowInstance
WHERE Id = @id
```

Required write behavior must be equivalent to:

```sql
UPDATE WorkflowInstance
SET CurrentStateKey = @newState,
    Version = Version + 1
WHERE Id = @id
  AND Version = @expectedInstanceVersion
```

If zero rows are affected, return `ConcurrencyConflict`.

## Required Fix

Move authoritative guard validation into the transaction and make the state change conditional on the expected persisted version.

The fixed flow should be:

1. Check idempotency replay before doing work.
2. Resolve instance and metadata enough to identify the workflow.
3. Open a transaction.
4. Re-read the current workflow instance and current pending task inside the transaction.
5. Re-run all supplied concurrency guards against current persisted data:
   - `expectedCurrentState`
   - `expectedCurrentTaskId`
   - `expectedCurrentTaskKey`
   - `expectedInstanceVersion`
6. If any guard fails, return `ConcurrencyConflict`.
7. Apply the state-changing workflow instance update with `Version` in the predicate.
8. If the update affects zero rows, return `ConcurrencyConflict`.
9. Only after the current persisted version/state/task are confirmed, apply side effects:
   - save `MockSectionData`
   - close task
   - create task
   - queue notification
   - write audit
   - persist idempotency result
10. Commit the transaction.

## Important Constraints

- Do not introduce CQRS, MediatR, event sourcing, BPMN, brokers, microservices, or plugin frameworks.
- Do not change Portfolio Trade metadata to solve this.
- Do not hardcode Portfolio Trade workflow behavior.
- Do not let `--admin-override` bypass concurrency.
- Do not save section data for failed concurrency checks.
- Keep `SaveDraft` semantics intact: `SaveDraft` does not increment `WorkflowInstance.Version`, but its supplied guards must still be checked against current persisted state before data/audit writes.
- Use `TimeProvider`; do not introduce direct `DateTime.UtcNow`.

## Acceptance Criteria

Add or update tests proving:

- Two stale submissions with the same `expectedInstanceVersion` cannot both perform side effects.
- If the persisted instance version changes between initial read and write, the service returns `ConcurrencyConflict`.
- A concurrency failure inserts no `MockSectionData`.
- A concurrency failure closes no task.
- A concurrency failure creates no task.
- A concurrency failure queues no notification.
- A concurrency failure writes no audit row.
- A concurrency failure inserts no idempotency record.
- The successful winning request still behaves normally.
- `SaveDraft` still does not increment version, but stale `SaveDraft` guards still return `ConcurrencyConflict` before section data is saved.

## Suggested Verification Commands

```bash
dotnet test src/WorkflowEngine.sln
```

If SQL Server-backed tests require the local configured database, use the project-approved connection string mechanism:

```text
ConnectionStrings__DefaultConnection
```

Do not hardcode a new connection string while fixing this defect.
