---
status: complete
phase: 05-sla-monitor-and-notification-senders
source:
  - 05-01-SUMMARY.md
  - 05-02-SUMMARY.md
  - 05-03-SUMMARY.md
started: 2026-05-08T00:00:00Z
updated: 2026-05-08T00:25:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Run SLA Queues Overdue Tasks
expected: Running `dotnet run --project src\WorkflowEngine.Console -- run-sla --now 2030-01-15T16:00:00Z` prints `Command: RunSla`, `Sender: Console`, the fixed `Now`, scan counts, and queues `SlaTaskOverdue` rows only for overdue pending tasks with non-null due dates.
result: pass

### 2. Run SLA Is Idempotent
expected: Running `run-sla` a second time with the same `--now` reports already-queued overdue work and queues `0 SLA overdue notifications`, proving duplicate `SlaTaskOverdue` rows are suppressed.
result: pass

### 3. Process Notification Outbox Sends Pending Rows
expected: Running `dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox` prints `Command: ProcessNotificationOutbox`, `Sender: Console`, pending count, one console `[Sent]` line per pending row, and a final processed count; a rerun prints `Pending: 0` and `0 notifications processed via Console.`
result: pass

### 4. Sender Selection Is Configuration Driven
expected: The committed default configuration uses `Notifications:Sender = Console`; tests prove `Console`, `NoOp`, and `Smtp` values resolve the expected sender, whitespace/case are tolerated, and unknown values fail fast with the allowed-values message.
result: pass

### 5. SMTP Tests Are Opt-In Only
expected: Default `dotnet test src\WorkflowEngine.sln` does not attempt real SMTP and skips the `Category=Smtp` tests unless `RUN_SMTP_TESTS=1` is set with local smtp4dev; the test README documents the fake SMTP setup and warns never to point Tier 2 tests at a real relay.
result: pass

## Summary

total: 5
passed: 5
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none yet]
