# D10: No ILogger, no correlation scope, no health checks, no metrics

| Field | Value |
|---|---|
| Defect ID | D10 (item 10 in `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_REVIEW.md`, "Top 10 problems") |
| Tag | **MAJOR** |
| Original severity | Medium |
| Status | Open, deferred for a later fix |
| Date tagged | 2026-10-11 |
| Owner decision | The user tagged this defect as important to look back at during the defect review |
| Related defects | D02 (SMTP in the HTTP request), D03 (HTTP 500 for business failures), D04 (outbox retry without limit), D06 (synchronous engine) |

## 1. Problem

The engine does not log. It writes one kind of warning to `Console.Error`. The API logs only unhandled exceptions, without a correlation ID or an instance ID. The API has no health endpoint. There are no engine metrics. Background job errors are only in SQL Server columns.

This defect does not change what the engine does. It changes what operators can see, and how fast they can find the cause of a problem.

## 2. Locations

| File | Line | Item |
|---|---|---|
| `src/WorkflowEngine.Infrastructure` (all services) | all | No `ILogger<T>` |
| `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` | 279-282 | `Console.Error.WriteLine(warning)` for `workflow.options` that do not agree with metadata |
| `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` | 512-530 | `BuildOptionWarnings` makes the warning text |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Infrastructure/GlobalExceptionHandler.cs` | 22-28 | Only log in the API; status, method, and path only |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Controllers/WorkflowActionController.cs` | 25-27 | Returns HTTP 500 without an exception; nothing is logged |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs` | 52 | Makes `CorrelationId`, but does not put it in a log scope |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Program.cs` | all | No `AddHealthChecks`, no `MapHealthChecks`, no metrics |
| `workflow-api/src/WorkflowEngine.WorkflowApi/appsettings.json` | 20-25 | `Microsoft.AspNetCore` at `Warning`; usual requests are not logged |
| `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs` | 145, 244 | Errors go to `LastError` only |
| `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs` | all | No log of SLA job results or failures |

## 3. Runtime impact

1. **Business failures with HTTP 500 are not logged.** `ConcurrencyConflict`, `Forbidden`, and `NotificationDeliveryFailed` return HTTP 500 without an exception. The API log shows nothing. The operator must query SQL Server.
2. **No connection between a log entry and an instance.** The exception log has no `CorrelationId`, `WorkflowInstanceId`, action key, or user ID. The response to the caller also has no correlation ID.
3. **Engine warnings are lost.** The text goes to stderr without a time stamp, level, or correlation ID. If the host does not collect stderr, the warning is lost.
4. **No health signal.** If the API loses SQL Server, the load balancer continues to send traffic. All requests fail with HTTP 500. Nothing removes the server automatically.
5. **Background job problems are not visible.** An SMTP outage or a stopped SLA job causes no log entry, no metric, and no alert. Users report the problem first.
6. **No trends.** There are no counters for actions, result codes, conflicts, duration, outbox backlog, or SLA reminders. The first signal of D06 thread-pool starvation is a user complaint.

## 4. Fix plan (backward compatible)

1. Add `ILogger<T>` to the engine services (`WorkflowExecutionService`, `StartWorkflowService`, `WorkflowContextReader`, `ProcessNotificationOutboxService`, `RunSlaService`, `WorkflowSeedService`).
   - Add constructor overloads. Keep the current constructors.
   - Use `NullLogger<T>.Instance` when no logger is given.
2. Replace `Console.Error.WriteLine(warning)` with `logger.LogWarning(...)`.
   - For the console host, keep the same text on stderr, so that current scripts see the same output.
3. In the API, start a log scope for each request with `CorrelationId`, `WorkflowInstanceId`, `WorkflowKey`, `ActionKey`, and `UserId`.
4. Log each business failure at `Warning` level with its `ResultCode`.
5. Log a summary at the end of each outbox run and each SLA run: rows selected, sent, failed, and skipped.
6. Add a health check:
   - `AddHealthChecks()` with a custom SQL Server check that opens a connection and runs `SELECT 1`.
   - Map it to a new route, for example `/health`.
   - Do not add `AspNetCore.HealthChecks.SqlServer`. `AGENTS.md` does not permit third-party packages.
7. Add metrics with `System.Diagnostics.Metrics` (part of .NET):
   - Counter: actions, by workflow key, action key, and result code.
   - Counter: concurrency conflicts.
   - Histogram: action duration.
   - Gauge: outbox rows by status, and age of the oldest `Pending` row.
   - Counter: SLA reminders and SLA breaches.
8. Keep `AddWorkflowInfrastructure(...)` unchanged. Register the new items inside it, or add a new optional extension method.

## 5. Decisions required before implementation

1. **Log destination.** Which system collects the logs in production (for example a file, Windows Event Log, or a central log service)? The fix uses only the built-in .NET providers unless a package is approved.
2. **Metrics exporter.** Which system collects the metrics? An exporter (for example OpenTelemetry) is a third-party package and needs approval. Without an exporter, the metrics are available only to `dotnet-counters`.
3. **Correlation ID in the response.** Add a response header, for example `X-Correlation-Id`, so that users can report it. This is an additive header. Confirm that no caller rejects unknown headers.
4. **Health route.** Confirm the route name and whether the proxy must permit it without actor headers.
5. **Personal data in logs.** Confirm that `UserId` can go in the log scope. Do not log email addresses or business payloads.

## 6. Backward compatibility check

| Item | Change |
|---|---|
| HTTP routes | No change; `/health` is a new route |
| Actor headers | No change |
| Request JSON | No change |
| Response JSON | No change |
| Response headers | Optional new `X-Correlation-Id` header (additive) |
| Status codes | No change |
| Console commands, arguments, and output | No change |
| `WorkflowActionEnvelope` fixtures and sample JSON | No change |
| Public .NET constructors and DI methods | Additive overloads only |
| Database schema | No change |
| NuGet packages | No new third-party package unless approved |

## 7. Quality gate for the fix

1. Build both solutions with warnings as errors:
   - `src/WorkflowEngine.sln`
   - `workflow-api/WorkflowEngine.WorkflowApi.slnx`
2. Run all current core tests and API tests. All must pass.
3. Add tests for the new behavior:
   - a. A business failure writes one `Warning` log entry with the result code and the correlation ID.
   - b. The log scope contains `CorrelationId` and `WorkflowInstanceId` for an action request.
   - c. A conflicting `workflow.options` value writes a `Warning` log entry.
   - d. The console still writes the warning text to stderr.
   - e. `/health` returns healthy when SQL Server is available, and unhealthy when the connection string is wrong.
   - f. The action counter increases by one for each action.
   - g. Current constructors work without a logger.
4. Run the seed verification and execute the current sample JSON (see `PROJECT.md` section 21).

## 8. Not in scope

- The HTTP 500 mapping (D03). This fix only logs the result.
- The outbox retry limit and dead-letter status (D04). This fix only makes the backlog visible.
- Alert rules and dashboards. They depend on the decisions in section 5.
