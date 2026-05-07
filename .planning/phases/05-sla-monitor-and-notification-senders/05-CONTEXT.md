# Phase 5: SLA Monitor and Notification Senders - Context

**Gathered:** 2026-05-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 5 implements `run-sla`, `process-notification-outbox`, and configurable notification sender wiring. It proves task-driven SLA overdue scanning, one `SlaTaskOverdue` outbox row per task ever, durable outbox processing, and sender selection through configuration. It does not change workflow transitions, contract DTO shapes, Portfolio Trade metadata beyond existing seed requirements, or the Phase 4 execution path except where necessary to wire senders.

</domain>

<decisions>
## Implementation Decisions

### SLA Scan Behavior
- **D-01:** `run-sla` scans pending workflow tasks only. The SQL shape is task-driven: join `WorkflowTask` to `WorkflowInstance`, filter `WorkflowTask.Status = 'Pending'`, `WorkflowTask.DueAtUtc IS NOT NULL`, `WorkflowTask.DueAtUtc < now`, `WorkflowInstance.Status = 'Active'`, and exclude any task that already has a `NotificationOutbox` row with `WorkflowTaskId = task.Id` and `NotificationType = 'SlaTaskOverdue'`.
- **D-02:** The overdue cutoff uses strict less-than. A task due exactly at `now` is not overdue.
- **D-03:** Existing `SlaTaskOverdue` rows suppress new SLA notifications forever, regardless of whether the existing row is `Pending` or `Sent`.
- **D-04:** `MakerReworkTask` and any other no-SLA task are excluded by `DueAtUtc IS NULL`. Do not invent or inherit due dates.
- **D-05:** `run-sla --now <iso8601>` registers a fixed-time `TimeProvider` for command duration. Without `--now`, use the injected system `TimeProvider`.
- **D-06:** The scan runs outside a transaction and materializes all result rows before processing. Each overdue task row is then processed in its own per-row transaction. No pagination is required for the POC.
- **D-07:** Result ordering is deterministic: `DueAtUtc ASC, Id ASC`.
- **D-08:** Console output reports scan-level counts: pending tasks with SLA, already-queued skipped count, overdue-to-process count, then per-task processing output and final summary.
- **D-09:** Two consecutive `run-sla` invocations are idempotent at the outbox level: the first queues missing overdue rows, the second finds zero rows to queue.

### Outbox Processing
- **D-10:** `process-notification-outbox` loads all `NotificationOutbox` rows where `Status = 'Pending'`, ordered by `CreatedAtUtc ASC, Id ASC`, and materializes the result set before processing.
- **D-11:** Each row is processed in order with its own per-row transaction. The command calls `INotificationSender.SendAsync(row, cancellationToken)`. On success it updates the row to `Status = 'Sent'` and sets `SentAtUtc = TimeProvider.GetUtcNow()`, then commits.
- **D-12:** Any exception from `INotificationSender.SendAsync` aborts the command. The current row transaction rolls back, the row remains `Pending`, later rows are not attempted, and the command exits non-zero with the exception surfaced. Earlier rows that already committed remain `Sent`.
- **D-13:** v1 does not classify sender exceptions and does not implement `Failed`, retry policies, dead-letter handling, or application-level attempt tracking.
- **D-14:** The `UPDATE` that marks a row sent includes `WHERE Status = 'Pending'`. If zero rows are affected, assume another invocation already marked the row `Sent`; log `WARN: row {Id} was already marked Sent by another process` and continue.
- **D-15:** Concurrent runs may both load and send the same pending row before the status update. This is accepted for v1. Database state remains correct, but duplicate external sends are possible under concurrent SMTP invocations; operators must avoid concurrent live SMTP runs.
- **D-16:** `ConsoleNotificationSender.SendAsync` prints per-row summaries. `process-notification-outbox` prints only start and final summary lines. `NoOpNotificationSender` and `SmtpNotificationSender` print no per-row output from `SendAsync`.
- **D-17:** Safe rerun behavior is defined by durable status: after a successful run, a rerun finds zero pending rows; after a failed run, a rerun attempts the still-pending failed and unattempted rows.

### Sender Configuration
- **D-18:** `INotificationSender` is selected only from configuration at application startup. `Notifications:Sender` allowed values are `Console`, `NoOp`, and `Smtp`, matched case-insensitively after trimming. Default is `Console`.
- **D-19:** Unknown `Notifications:Sender` values throw `InvalidOperationException` at startup with an allowed-values message. Do not add a command flag override or environment-name hardcoding.
- **D-20:** Configuration source precedence follows .NET configuration conventions: environment variable `Notifications__Sender`, then `appsettings.{Environment}.json`, then `appsettings.json`.
- **D-21:** Committed `appsettings.json` defaults to `"Console"`. Production enables SMTP by configuration only, not code change.
- **D-22:** Tests that need a custom sender replace DI after normal service configuration, for example by removing `INotificationSender` and registering `RecordingNotificationSender`. Tests do not mutate configuration to change sender behavior.
- **D-23:** `SmtpNotificationSender` validates required SMTP configuration at construction/DI resolution time. If `Notifications:Sender = "Smtp"` and `Smtp:Host` is missing or empty, throw `InvalidOperationException` before any send attempt.
- **D-24:** `run-sla` and `process-notification-outbox` start banners print the active sender name from `INotificationSender.SenderName`, even though `run-sla` only queues rows and does not send them.

### SMTP Test Boundary
- **D-25:** Phase 5 owns Tier 1 and Tier 2 SMTP coverage only. Tier 3 live relay smoke testing is manual and out of automated test scope.
- **D-26:** Tier 1 tests run in default `dotnet test` using fakes such as `RecordingNotificationSender`. They assert queueing, processing order, sender calls, and exception behavior without file I/O, network, or real SMTP.
- **D-27:** Tier 2 tests exercise `SmtpNotificationSender` against a local fake SMTP server such as `smtp4dev` or Papercut. Mark them with xUnit `Trait("Category", "Smtp")` or the equivalent framework category.
- **D-28:** Default `dotnet test` must not run Tier 2. Configure exclusion cleanly through the test framework runner config or test-project properties. Explicit opt-in command is `dotnet test --filter Category=Smtp`.
- **D-29:** Tier 2 tests must never point at a real SMTP relay. They verify `From`, resolved `To`, subject, body content, recipient override behavior, missing role mapping failure, and per-send connection lifecycle against the fake server.
- **D-30:** The Phase 5 implementer may choose `smtp4dev` or Papercut. `smtp4dev` is preferred for portability if Docker is acceptable; Papercut is acceptable for Windows-native setups. Document whichever is chosen.

### the agent's Discretion
The agent may choose exact service and command handler names, internal DTO names for sender input, and whether to use runner config or test-project properties for SMTP test exclusion, provided the locked behavior above and `PROJECT.md` stop-and-ask rules are preserved.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Contract
- `PROJECT.md` section 13 - SLA due-date behavior, `run-sla`, task-driven overdue detection, and no-SLA states.
- `PROJECT.md` section 14 - notification outbox records, sender abstraction, Console/NoOp/SMTP behavior, MailKit requirement, and SMTP configuration.
- `PROJECT.md` section 20 - required tests for SLA duplicate suppression, notification processing, and sender coverage.
- `PROJECT.md` section 21 - quality gates, including SLA demo command path and SMTP opt-in boundary.
- `PROJECT.md` section 22 - stop-and-ask triggers, especially no extra packages except MailKit, no SQL role-email mapping, no SMTP credentials, and no real SMTP default.
- `PROJECT.md` section 28 - pitfall checklist for SLA duplicates, sender configuration, role-to-email mapping, SMTP defaults, and Tier 2 test exclusion.

### GSD Planning
- `.planning/PROJECT.md` - validated project context and current active requirements.
- `.planning/REQUIREMENTS.md` - Phase 5 requirements `SLA-01` through `SLA-04`.
- `.planning/ROADMAP.md` - Phase 5 goal and success criteria.
- `.planning/STATE.md` - current project state and build note (`-m:1`).
- `.planning/phases/04-workflow-engine/04-CONTEXT.md` - Phase 4 execution, notification outbox, due-date, and deferred Phase 5 boundaries.
- `.planning/phases/04-workflow-engine/04-04-SUMMARY.md` - existing workflow transitions create task due dates and notification outbox rows for Phase 5 to consume.
- `.planning/phases/04-workflow-engine/04-05-SUMMARY.md` - inspection commands for showing workflow, tasks, history, and notifications.
- `AGENTS.MD` - repository-local GSD guardrails and completion summary format.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContext.cs` - contains `WorkflowTasks`, `WorkflowInstances`, `NotificationOutbox`, `NotificationRoutes`, and `SlaPolicies`.
- `src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowTask.cs` - task status and `DueAtUtc` source for SLA scanning.
- `src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationOutbox.cs` - durable outbox row to queue and mark sent.
- `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` - already sets task `DueAtUtc` from SLA policies and queues metadata-driven workflow notifications.
- `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs` - computes SLA context from task due dates using `TimeProvider`.
- `src/WorkflowEngine.Application/Commands/CommandRegistry.cs` - already recognizes `run-sla` and `process-notification-outbox` as required commands.
- `src/WorkflowEngine.Application/Commands/ShowNotificationsCommandHandler.cs` - existing read-only notification output useful for smoke verification.
- `tests/WorkflowEngine.Tests/SchemaConstraintTests.cs` - already verifies the filtered unique index for `SlaTaskOverdue` by `WorkflowTaskId` and notification type.

### Established Patterns
- Use injected `TimeProvider` for all generated timestamps and `--now` fixed-time command behavior.
- Keep workflow logic metadata-driven and generic; Portfolio Trade belongs in seed/sample metadata and tests only.
- Use SQL-backed tests for persistence behavior; local SQL Server may require running tests outside the sandbox because of Windows integrated authentication.
- Use `dotnet build src\WorkflowEngine.sln -warnaserror -m:1` and `dotnet test src\WorkflowEngine.sln -m:1` for reliable verification.
- Default committed configuration must be safe for dev/test and must not send real email.

### Integration Points
- Add application command handlers for `run-sla` and `process-notification-outbox`.
- Add infrastructure services for SQL-backed SLA scanning and outbox processing.
- Add `INotificationSender` plus `ConsoleNotificationSender`, `NoOpNotificationSender`, and MailKit-backed `SmtpNotificationSender`.
- Wire sender selection in the console composition root from `Notifications:Sender`.
- Add Tier 1 tests with a recording sender and Tier 2 SMTP tests behind an explicit `Category=Smtp` filter.

</code_context>

<specifics>
## Specific Ideas

- `run-sla` output should show `Command: RunSla`, the effective `Now`, scan counts, and processing counts.
- `process-notification-outbox` output should show `Command: ProcessNotificationOutbox`, `Sender: <name>`, pending count, and final processed count.
- `ConsoleNotificationSender` per-row format should be compact: notification type, recipient, subject, and workflow instance id.
- `RecipientOverride` for SMTP should annotate the original recipient in the message body.
- SMTP body output should be plain text key/value lines, not raw JSON.

</specifics>

<deferred>
## Deferred Ideas

- v2 may add `Failed` notification status, retry policies, dead-letter handling, and application-level attempt tracking.
- v2 may add database locking or skip-locked style processing to prevent duplicate sends under concurrent SMTP runs.
- Tier 3 live SMTP relay smoke testing is manual and belongs to UI integration or production readiness, not Phase 5 automated tests.

</deferred>

---

*Phase: 5-SLA Monitor and Notification Senders*
*Context gathered: 2026-05-07*
