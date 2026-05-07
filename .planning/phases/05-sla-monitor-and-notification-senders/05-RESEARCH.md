# Phase 05 Research: SLA Monitor and Notification Senders

## Research Goal

Plan Phase 5 so execution can add `run-sla`, `process-notification-outbox`, and sender selection without changing workflow contracts, workflow transition semantics, or Portfolio Trade engine behavior.

## Source Constraints

- `PROJECT.md` is authoritative.
- .NET 10, SQL Server, EF Core, warnings as errors.
- Use `TimeProvider`; do not introduce `DateTime.UtcNow`.
- Notification sending is configuration-driven through `INotificationSender`.
- MailKit is the only allowed new third-party package.
- No SQL-backed role-to-email mapping and no SMTP credentials in v1.
- Default dev/test sender must not send real email.

## Existing Implementation Baseline

- `WorkflowExecutionService` already creates workflow tasks with `DueAtUtc` based on seeded SLA policies.
- `WorkflowExecutionService` already creates `NotificationOutbox` rows for transition notifications.
- `WorkflowDbContext` already has `WorkflowTasks`, `WorkflowInstances`, `NotificationOutbox`, `SlaPolicies`, and `NotificationRoutes`.
- `NotificationOutbox` already has `WorkflowTaskId`, `NotificationType`, `RecipientRoleKey`, `Subject`, `PayloadJson`, `Status`, `CreatedAtUtc`, and `SentAtUtc`.
- `CommandRegistry` recognizes `run-sla` and `process-notification-outbox` but currently returns deferred stub output.
- `ShowNotificationsCommandHandler` can verify queued notifications during smoke tests.
- `SchemaConstraintTests` already verifies the filtered unique index for `SlaTaskOverdue` by `(WorkflowTaskId, NotificationType)`.

## Recommended Plan Shape

Use three small plans:

1. **SLA monitor command and service**: implement scan/query, `--now`, one-row transactions, duplicate suppression, output, and SQL-backed tests.
2. **Sender abstraction and configuration**: introduce `INotificationSender`, Console/NoOp/MailKit SMTP implementations, sender selection, config defaults, and selection tests.
3. **Outbox processor and SMTP test boundary**: implement durable pending-row processing, fail-fast sender exception behavior, concurrency warning, command output, Tier 1 tests, Tier 2 opt-in SMTP tests/docs.

This ordering lets `run-sla` prove SLA notification queueing before sender mechanics are fully exercised, while still establishing sender DI before the outbox processor consumes it.

## Planning Notes

- `run-sla` queues notifications only. It does not call a sender.
- `process-notification-outbox` sends only rows already in `NotificationOutbox` with `Status = 'Pending'`.
- `run-sla` may display the active sender name for operator visibility, but sender selection does not affect SLA queueing.
- Add application interfaces in `WorkflowEngine.Application` and SQL implementations in `WorkflowEngine.Infrastructure`.
- Keep command handlers in the application command layer and inject application-facing service interfaces.
- For tests that need SQL, follow the existing `WorkflowExecutionValidationTests.CreateSeededContext()` and `FixedTimeProvider` style.
- For sender tests, use a `RecordingNotificationSender` test double and avoid configuration mutation unless the test is specifically checking configuration binding.

## Risks and Guardrails

- **Duplicate SLA rows**: Guard in both query (`NOT EXISTS`) and database unique index; tests must include existing pending and sent `SlaTaskOverdue`.
- **Boundary bug at exact due time**: Use strict `< now`, not `<= now`.
- **Completed workflow notification**: Join to `WorkflowInstance` and filter `Status = 'Active'`.
- **No-SLA task notification**: Require `DueAtUtc IS NOT NULL`.
- **SMTP in default tests**: Configure Tier 2 tests so default `dotnet test` excludes `Category=Smtp`.
- **Real email in dev/test**: Default `Notifications:Sender` is `Console`; SMTP must require explicit configuration.
- **Role email lookup creep**: Resolve role emails from `Smtp:RoleEmailMap` configuration only.

## Verification Focus

- `dotnet build src\WorkflowEngine.sln -warnaserror -m:1`
- `dotnet test src\WorkflowEngine.sln -m:1`
- `dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"`
- `dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox`
- Optional Tier 2: `dotnet test src\WorkflowEngine.sln --filter Category=Smtp` after local fake SMTP setup.
