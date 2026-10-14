# D02: Email sent inside the HTTP request (load defect)

| Field | Value |
|---|---|
| Defect ID | D02 (item 2 in `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_REVIEW.md`), same root cause as audit F06 |
| Tag | High, **confirmed by load test** (proposed Major; owner to confirm) |
| Status | Open, deferred for a later fix |
| Date logged | 2026-10-11 |
| Evidence | `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/.planning/audits/2026-10-11-final/load-results.md`, `C:/projects/cdt-workflow-archive/20261014-cleanup/snapshot/WORKFLOW_ENGINE_LOAD_REVIEW.html` |
| Related | D06 (sync I/O), L1 (new idempotency key per request), D04 (outbox retry), F06 |

## 1. Problem

After the engine commits, the API sends each outbox email in the same HTTP request. It blocks the thread with `SendAsync(..., CancellationToken.None).GetAwaiter().GetResult()` (sync over async). A send failure sets `Success = false` and HTTP 500 after the state change is saved.

## 2. Locations

| File | Line | Item |
|---|---|---|
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionNotificationDispatcher.cs` | 60-62 | Blocking SMTP send |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs` | 132, 139-172 | Dispatch after commit; overwrites result to `NotificationDeliveryFailed` |
| `workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs` | 58-98 | Same for initial action |
| `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs` | 44-48 | New SMTP connection for each email |
| `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs` | 20-29 | Reads all pending rows; no batch size; no index on `Status` |

## 3. Measured runtime impact (2026-10-11, 16 cores, local SQL Server, smtp4dev)

| Condition | Result |
|---|---|
| No email (`NoOp`), 256 callers | 424 requests/s, p95 0.75 s, 0 failures |
| SMTP, 32 callers | 64 requests/s, p95 0.56 s |
| SMTP, 128 callers | 20 requests/s, p95 48 s |
| SMTP, 256 callers | 19 requests/s, p95 78 s |
| SMTP, 256 writers + 4 readers | API threads 90 -> 349; read p95 22 s; 486 of 2,560 writes timed out; **338 workflows saved and emailed after the caller timeout** |

## 4. Fix plan (backward compatible)

1. Add setting `Notifications:DispatchMode` with values `Inline` (default, current behavior) and `Outbox`.
2. In `Outbox` mode, the API does not send email. It returns the committed result. The outbox job sends the rows.
3. In `Inline` mode, replace `GetAwaiter().GetResult()` with an awaited call when the async API path exists (D06). Until then, keep current behavior.
4. Optional, additive: add a notification status field to the response (for example `Notification.DeliveryStatus = Queued | Sent | Failed`) so callers can see delivery separately from the action result. Do not change existing fields.
5. Reuse one SMTP connection for each outbox batch.
6. Add an outbox batch size (default keeps current behavior if unset) and an index on `NotificationOutbox (Status, CreatedAtUtc)`.

## 5. Decisions required before implementation

1. Default for new deployments: keep `Inline`, or document `Outbox` as recommended.
2. Required outbox job interval when `Outbox` is used (email delay = job interval).
3. Whether to add the additive `DeliveryStatus` response field.
4. Batch size default.

## 6. Backward compatibility check

| Item | Change |
|---|---|
| Routes, headers, request JSON | No change |
| Response JSON | No change; optional additive field only if decision 3 = yes |
| Status codes | No change in `Inline` mode |
| Console commands | No change |
| Database | Additive index only |
| Email subject, body, recipients | No change |

## 7. Quality gate

1. Both solutions build with warnings as errors.
2. Full core, SMTP (`RUN_SMTP_TESTS=1`), and API suites pass.
3. New tests: `Outbox` mode returns success without SMTP; outbox job then sends the row; `Inline` mode is unchanged; batch size limits rows; SMTP connection reuse sends the same MIME content.
4. Re-run `load-probe.cs.txt` with SMTP in `Outbox` mode. Target: near the `NoOp` result (about 400 requests/s on the reference machine), 0 timeouts.
5. Seed verification and sample JSON (PROJECT.md section 21).
