# Defect 02: Idempotency Record Written After Side Effects Commit

Status: Fixed in working tree

Severity: Major

Source review: `ARCHITECTURE_REVIEW.md`

## Summary

`WorkflowExecutionService` commits workflow side effects before inserting the `IdempotencyRecord` that stores the replayable `ResultJson`.

This creates a durability gap: the workflow action can succeed and commit, but a crash, connection drop, timeout, or app recycle before the idempotency insert leaves no replay record. A retry of the same client action then cannot return the original success result and may instead report a stale-state failure.

## Production Scenario

A Maker submits a draft to Checker:

```text
WorkflowInstanceId: WF-PT-1001
Action: SubmitForChecker
Expected state: Draft
Expected version: 1
Idempotency key: submit-abc-123
```

The service performs and commits the business side effects:

```text
Saves Maker section data
Moves state Draft -> CheckerReview
Creates CheckerReviewTask
Queues WorkflowTaskAssigned notification to Checker
Writes audit
Increments workflow version to 2
Commits transaction
```

Then, before `IdempotencyRecord` is inserted, a production failure occurs:

```text
App process crashes
Database connection drops
App instance recycles during deploy
SQL timeout occurs
Request is interrupted after commit
```

The business action is committed, but the replay record is missing.

The browser, gateway, or client retries the same request with the same idempotency key. Because there is no stored replay result, the service treats the retry as new work. It now sees:

```text
Expected: Draft / version 1
Actual: CheckerReview / version 2
```

The user may receive `ConcurrencyConflict`, `InvalidTransition`, or another stale-state failure even though the original action succeeded.

## Real-Time Impact

This breaks the core purpose of idempotency under production conditions:

- The user sees an error for an action that actually succeeded.
- Maker may think the submission failed.
- Checker may already have a task.
- Notification may already be queued or sent.
- Audit already records the original action.
- Support has to reconcile user-reported failure against committed workflow state.
- Client retry policies, double-clicks, browser resubmits, and deployment recycles become confusing instead of safe.

The required retry behavior is:

```text
Same idempotency key + same request hash
  -> return stored original result
  -> mark diagnostics.replay = true
  -> perform no duplicate side effects
```

## Code Evidence

Primary file:

```text
src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
```

Relevant flow:

```text
Line 218: transaction begins
Line 223: MockSectionData is added
Line 239-246: task is closed
Line 256-257: state is changed and version incremented
Line 272-283: task is created and SaveChanges is called
Line 299-321: notification outbox row is added
Line 339-352: audit row is added
Line 354: side effects are saved
Line 355: transaction commits
Line 357-367: result is built and PersistIdempotencyRecord is called after commit
Line 398-407: IdempotencyRecord is added
Line 411: idempotency insert is saved separately
```

Current behavior:

```text
transaction 1:
  workflow side effects
  commit

transaction 2:
  insert IdempotencyRecord
  commit
```

Required behavior:

```text
single transaction:
  workflow side effects
  build replayable result
  insert IdempotencyRecord with ResultJson
  commit
```

## Required Fix

Persist the idempotency replay record in the same transaction as successful workflow side effects.

The fixed successful-action flow should be:

1. Check for existing idempotency record before doing work.
2. If the key exists with the same request hash, return the stored result as replay.
3. If the key exists with a different request hash, return `IdempotencyConflict`.
4. Begin the workflow transaction.
5. Validate current workflow state/task/version inside the transaction.
6. Apply successful side effects:
   - save `MockSectionData`
   - close task if metadata says to close one
   - move workflow state and increment version for non-`SaveDraft`
   - create next task if metadata says to create one
   - queue notification if metadata says to queue one
   - write audit
7. Build the exact `WorkflowActionResultEnvelope` to return.
8. Compute `ResultHash`.
9. Serialize the result to `ResultJson`.
10. Insert `IdempotencyRecord` in the same DbContext transaction.
11. Commit once.

After the commit, returning the already-built result is fine. The important guarantee is that committed side effects and the replay record are atomic.

## Important Constraints

- Do not insert idempotency records for failed validation, forbidden, invalid transition, concurrency conflict, or idempotency conflict paths.
- Do not make idempotency suppress concurrency checks for new requests with different keys.
- Do not change request hashing semantics unless required by the fix.
- Do not introduce new architecture frameworks.
- Do not hardcode Portfolio Trade behavior.
- Keep replay behavior compatible with existing `TryBuildIdempotencyReplay`.
- Keep `ResultHash`, `RequestHash`, `OriginalCreatedAtUtc`, and `Replay` diagnostics behavior intact.
- If this is fixed together with Defect 01, make sure stale requests still return `ConcurrencyConflict` with no side effects and no idempotency insert.

## Acceptance Criteria

Add or update tests proving:

- A successful workflow action inserts exactly one `IdempotencyRecord` in the same transaction as side effects.
- A retry with the same idempotency key and same request hash returns the original success result with `diagnostics.replay = true`.
- The retry performs no duplicate side effects:
  - no extra `MockSectionData`
  - no extra task close/create
  - no extra notification
  - no extra audit
- If workflow side effects roll back, no idempotency record remains.
- If idempotency insert fails, workflow side effects also roll back.
- Same idempotency key with different request hash still returns `IdempotencyConflict`.
- Failed action results still do not create idempotency records.

## Suggested Verification Commands

```bash
dotnet test src/WorkflowEngine.sln
```

If SQL Server-backed tests require the local configured database, use the project-approved connection string mechanism:

```text
ConnectionStrings__DefaultConnection
```

Do not hardcode a new connection string while fixing this defect.
