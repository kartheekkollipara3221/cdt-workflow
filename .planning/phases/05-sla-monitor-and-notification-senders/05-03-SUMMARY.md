# 05-03 Summary: Notification Outbox Processor and SMTP Test Boundary

## What Changed

- Added `IProcessNotificationOutboxService` result model.
- Implemented `ProcessNotificationOutboxService` that materializes pending rows ordered by `CreatedAtUtc, Id`, processes each row in a per-row transaction, calls `INotificationSender`, marks sent with `SentAtUtc`, aborts on sender exceptions, and leaves failed/current/later rows pending.
- Added `process-notification-outbox` command handler, DI wiring, and command registry dispatch.
- Added Tier 1 outbox tests with `RecordingNotificationSender`.
- Added opt-in `Category=Smtp` smtp4dev integration tests and documented fake SMTP setup.

## Key Files

- `src/WorkflowEngine.Application/Notifications/IProcessNotificationOutboxService.cs`
- `src/WorkflowEngine.Application/Commands/IProcessNotificationOutboxCommandHandler.cs`
- `src/WorkflowEngine.Application/Commands/ProcessNotificationOutboxCommandHandler.cs`
- `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs`
- `tests/WorkflowEngine.Tests/Phase5OutboxTests.cs`
- `tests/WorkflowEngine.Tests/RecordingNotificationSender.cs`
- `tests/WorkflowEngine.Tests/SmtpNotificationSenderIntegrationTests.cs`
- `tests/WorkflowEngine.Tests/README.md`
- `tests/WorkflowEngine.Tests/xunit.runner.json`

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror`
- `dotnet test src\WorkflowEngine.sln`
- `dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox`
- Empty rerun prints `0 notifications processed via Console.`
- Tier 2 SMTP tests are present but skipped by default unless `RUN_SMTP_TESTS=1` is set and `dotnet test --filter Category=Smtp` is run against local smtp4dev.
