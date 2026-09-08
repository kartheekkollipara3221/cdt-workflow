# F05 correction — completed locally, 2026-09-07

Changed: ProcessNotificationOutboxService records a failed row and continues through the selected pending rows. Failed messages retain Pending status, incremented RetryCount and LastError; processing claims are released. Cancellation still stops the run. The result retains the existing constructor and first Failure field, adds Failures, and supports a correct remaining-row count. Console output lists every failure and returns a nonzero exit code if any failed.

Why: one repeatedly failing message must not prevent unrelated healthy notifications from being delivered.

Tests run:
- RED: two SQL-backed regressions failed against the original implementation (sender failure and malformed snapshot blocking healthy rows).
- GREEN: all nine Phase5OutboxTests passed, covering ordering, multiple failures/console counts, repeated runs without resending successes, cancellation/claim release, concurrent claims and inactive SLA cancellation.
- Five SMTP integration tests passed against smtp4dev, including the new two-pass test: malformed mailbox remains Pending with RetryCount=2, two healthy messages are Sent and captured, malformed message is not captured.
- Both solution builds passed with `--no-restore -warnaserror --verbosity quiet`, zero warnings/errors.
- Full core suite: `dotnet test src/WorkflowEngine.sln --no-build --no-restore --verbosity quiet`, RUN_SMTP_TESTS=1: 484 passed, zero failed/skipped.
- API suite: `dotnet test workflow-api/WorkflowEngine.WorkflowApi.slnx --no-build --no-restore --verbosity quiet`: 68 passed, zero failed/skipped.
- Final source changes, including result compatibility fallback, are included in the full-suite runs. TRX files: results/f05-core.trx, results/f05-api.trx; earlier focused SMTP result: results/f05-smtp.trx.
- `git diff --check` passed; Git reported only line-ending conversion notices.

Seed verification run: passed through core Section21/FreshClone SQL tests.

Sample JSON executed: supported workflow branches and demo through existing core regressions. Both connection-string environment variables targeted private `(localdb)\WorkflowAudit_20260906`; no DOLLY application database or production initialization was touched. Fresh test databases are the only migration targets. The new SMTP test deletes its own workflowengine_f05_smtp_tests database after execution.

Files updated:
- src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs
- src/WorkflowEngine.Application/Notifications/IProcessNotificationOutboxService.cs
- src/WorkflowEngine.Application/Commands/ProcessNotificationOutboxCommandHandler.cs
- tests/WorkflowEngine.Tests/Phase5OutboxTests.cs
- tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs
- Audit presentation/checkpoint and quick-task/session handoffs. Prior F03 changes are preserved.

Open issues and limits:
- One attempt per selected row per processor invocation. No new retry delay, retry ceiling, quarantine or global relay-outage circuit breaker. A relay outage can still cause one failed attempt for each selected message.
- Failed state persistence can still stop the run if SQL itself is unavailable; it is not silently ignored.
- SMTP send/mark failure windows and at-least-once delivery remain; this change does not promise exactly-once delivery.
- Verification used private SQL Server 2025 LocalDB; no full-suite SQL Server 2022 rerun in this correction.
- No database schema changes, production data repair, deployment, commit or push performed. Source changes remain local.
- F04 accepted: target API protects deadline edits, per user confirmation; no engine change. Full source audit remains deferred. Next: discuss F06 after user finishes F05.

Retained test resource: smtp4dev process 25248, SMTP 127.0.0.1:3025, UI http://localhost:5000. It was started hidden because those ports had no listener, using isolated smtp4dev-data under this quick-task directory. Relay is disabled; IMAP/POP3 are disabled. Logs: smtp.out.log/smtp.err.log. Captures are synthetic example.test messages. The SMTP tool's own SQLite store is not used as evidence for workflow SQL Server behavior.
