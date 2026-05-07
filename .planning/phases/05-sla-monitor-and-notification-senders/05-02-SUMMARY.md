# 05-02 Summary: Notification Sender Selection

## What Changed

- Added `INotificationSender` and `NotificationDelivery`.
- Added `ConsoleNotificationSender`, `NoOpNotificationSender`, and MailKit-backed `SmtpNotificationSender`.
- Registered sender selection from `Notifications:Sender`, defaulting to `Console`, with trim/case-insensitive matching and fail-fast unknown values.
- Added committed `appsettings.json` default for `Notifications:Sender = Console`.
- Added SMTP configuration validation and unit tests for role mapping, recipient override, missing host, missing role mapping, and plain-text body formatting.

## Key Files

- `src/WorkflowEngine.Application/Notifications/INotificationSender.cs`
- `src/WorkflowEngine.Infrastructure/Notifications/ConsoleNotificationSender.cs`
- `src/WorkflowEngine.Infrastructure/Notifications/NoOpNotificationSender.cs`
- `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs`
- `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/WorkflowEngine.Console/appsettings.json`
- `tests/WorkflowEngine.Tests/NotificationSenderSelectionTests.cs`
- `tests/WorkflowEngine.Tests/SmtpNotificationSenderTests.cs`

## Verification

- `dotnet build src\WorkflowEngine.sln -warnaserror`
- `dotnet test src\WorkflowEngine.sln`
- Default DI resolves `ConsoleNotificationSender`.
- `Console`, `NoOp`, and `Smtp` configuration values resolve the expected implementations.
- Unknown sender and missing SMTP host fail at DI resolution/construction.
