# D06: Synchronous engine and API, no CancellationToken

| Field | Value |
|---|---|
| Defect ID | D06 (item 6 in `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_REVIEW.md`, "Top 10 problems") |
| Tag | **MAJOR** |
| Original severity | Medium |
| Status | Open, deferred for a later fix |
| Date tagged | 2026-10-11 |
| Owner decision | The user tagged this defect as major during the defect review |
| Related defects | D02 (synchronous SMTP in the HTTP request), "new idempotency key for each request" (lower-priority list) |

## 1. Problem

The API controllers, the API services, and the engine are synchronous. No method accepts a `CancellationToken`. Each HTTP request holds one thread-pool thread during all of its database and SMTP waits.

## 2. Locations

| File | Line | Item |
|---|---|---|
| `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs` | 18 | `ActionResult ExecuteWorkflowAction(...)` |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowStartController.cs` | 18 | `ActionResult StartWorkflow(...)` |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowContextController.cs` | 19 | `ActionResult GetWorkflowContext(...)` |
| `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` | 52 | `Execute(WorkflowExecutionRequest request)` |
| `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` | 68-894 | Approximately 20 to 25 synchronous database round trips for each action |
| `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs` | all | Synchronous start path |
| `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs` | all | Synchronous read path |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs` | 60-62 | `SendAsync(..., CancellationToken.None).GetAwaiter().GetResult()` |

## 3. Runtime impact

1. **Low load:** No visible problem. Tests and demos do not show the defect.
2. **High load (thread-pool starvation):** All threads wait for SQL Server or SMTP. New requests stay in a queue. .NET adds threads slowly. Response time increases quickly, but CPU usage stays low. All endpoints become slow.
3. **Work after the caller stops:** A caller timeout does not stop the engine. The engine can commit the state change after the caller received a timeout error.
4. **Retry after timeout:** The API makes a new idempotency key for each request. A retry of a state change gets a conflict error. A retry of a self-loop action runs the action two times.
5. **Overload becomes worse:** The API continues work for callers that stopped. Retries add more load. Recovery is slow.
6. **Slow SMTP:** D02 blocks threads during SMTP calls. A slow SMTP server can cause starvation, also when SQL Server is fast.

No effect on: `WorkflowEngine.Console` commands and SLA jobs, because they run one operation at a time. No data corruption, because the engine uses a transaction and a concurrency check.

## 4. Fix plan (backward compatible)

1. Add `ExecuteAsync(WorkflowExecutionRequest, CancellationToken)` to the engine. Keep `Execute(WorkflowExecutionRequest)`.
2. Add `StartAsync(..., CancellationToken)` to the start service. Keep the synchronous method.
3. Add `ReadAsync(..., CancellationToken)` to `IWorkflowContextReader`. Keep `Read(...)`.
4. Use the EF Core async methods in the new overloads (`SingleOrDefaultAsync`, `ToListAsync`, `AnyAsync`, `ExecuteUpdateAsync`, `SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`).
5. Share one implementation between the sync and the async paths, so that the business rules do not exist two times.
6. Change the three controllers to `async Task<ActionResult>`. Pass `HttpContext.RequestAborted`.
7. Change the API services (`IWorkflowActionService`, `IWorkflowStartService`, `IWorkflowContextQueryService`) to add async overloads. Keep the current methods.
8. Change `WorkflowActionNotificationDispatcher` to `await SendAsync(..., cancellationToken)`. Remove `GetAwaiter().GetResult()`.
9. Do not register new DI methods. Keep `AddWorkflowInfrastructure(...)` unchanged.

## 5. Decision required before implementation

**Cancellation point.** The engine must not stop after `Commit`. If it stops between `Commit` and the response, the caller sees a failure, but the state changed.

Proposed rule:

1. Read the token before each database call up to the start of the transaction.
2. Read the token one last time immediately before `Commit`.
3. After `Commit`, use `CancellationToken.None` for the context read and for the response.
4. For the notification dispatch after `Commit`, decide one of these options:
   - a. Use `CancellationToken.None`, so that the API tries to send the email although the caller stopped.
   - b. Use the request token, and let `process-notification-outbox` send the rows that the API did not send.

The owner must approve the rule and the option for step 4.

## 6. Backward compatibility check

| Item | Change |
|---|---|
| HTTP routes | No change |
| Actor headers | No change |
| Request JSON | No change |
| Response JSON | No change |
| Status codes | No change |
| Console commands and arguments | No change |
| `WorkflowActionEnvelope` fixtures and sample JSON | No change |
| Public .NET methods | Additive only (new async overloads) |
| Database schema | No change |

## 7. Quality gate for the fix

1. Build both solutions with warnings as errors:
   - `src/WorkflowEngine.sln`
   - `workflow-api/WorkflowEngine.WorkflowApi.slnx`
2. Run all current core tests and API tests. All must pass.
3. Add tests for the new behavior:
   - a. The async overloads give the same results as the sync methods for the Portfolio Trade sample actions.
   - b. A token that is cancelled before the transaction causes no state change.
   - c. A token that is cancelled after `Commit` does not change the result.
   - d. The controllers pass `HttpContext.RequestAborted`.
4. Execute the current sample JSON and the seed verification (see `PROJECT.md` section 21).
5. Optional: run a load test against the API with a slow SQL Server or a slow SMTP server. Compare the response time before and after the fix.

## 8. Not in scope

- The "new idempotency key for each request" problem. Track it separately. It makes Effects 3 and 4 worse.
- The HTTP 500 mapping (D03).
- The SMTP-in-request design (D02), except step 8 in section 4.
