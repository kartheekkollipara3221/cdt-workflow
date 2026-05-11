# SLA and Notification Outbox Processing

SLA scanning and notification delivery are not normal user request-path operations. They should run through hosted services, external scheduled jobs, or strict admin endpoints.

## Component Table

| Component | Current Console Command | API Integration Option | Recommended Option | Notes |
|---|---|---|---|---|
| `IRunSlaService` / `RunSlaService` | `run-sla [--now <iso8601-utc>]` | Hosted service, external job, admin endpoint | Hosted service or external job | Queues overdue notifications, does not send. |
| `IProcessNotificationOutboxService` / `ProcessNotificationOutboxService` | `process-notification-outbox` | Hosted service, external job, admin endpoint | Hosted service or external job | Claims rows before sending. |
| `INotificationSender` | Used by outbox command | Configured sender | Use `NoOp` for tests, `Smtp` for production when configured | Do not hardcode SMTP. |
| `ConsoleNotificationSender` | Default sender if configured/missing | Dev diagnostics | Avoid in production API | Writes to console. |
| `NoOpNotificationSender` | Configurable | Tests/dry run | Use in API tests | Does nothing. |
| `SmtpNotificationSender` | Configurable | Production SMTP | Use only with valid config | MailKit-based, maps roles from config. |

## Hosted Service Option

Use if the API host is allowed to run background work.

Pros:

- Integrated deployment.
- Uses the same DI/config.
- Good for simple polling.

Cons:

- Multiple API replicas can run the same worker. Current outbox claim logic handles duplicate processors, but SLA scan should still be monitored.
- Background failures share the API process.

## External Scheduled Job Option

Use if operations already has a scheduler, worker service, or batch platform.

Pros:

- Isolates background load from API requests.
- Easier operational ownership.
- Explicit schedule and retries.

Cons:

- Requires separate host wiring with the same workflow DI/config.

## Admin-Only Endpoint Option

Use for manual operations or diagnostics only.

Pros:

- Simple to invoke.
- Useful during migration.

Cons:

- Easy to misuse if exposed broadly.
- Not a substitute for reliable scheduling.

## Normal Request Path Rule

Do not process SLA or notification outbox inside normal user endpoints such as execute action or get context. User endpoints should return promptly and should not become responsible for batch processing.

## Hosted Service Scope Skeleton

```csharp
public sealed class WorkflowSlaHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;

    public WorkflowSlaHostedService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    {
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRunSlaService>();
            await service.RunAsync(new RunSlaRequest(timeProvider.GetUtcNow()), stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

```csharp
public sealed class NotificationOutboxHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;

    public NotificationOutboxHostedService(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IProcessNotificationOutboxService>();
            await service.ProcessAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

Make intervals configurable through API options if hosted services are used.

## Scoped Service Rule

`IRunSlaService` and `IProcessNotificationOutboxService` are scoped because they use `WorkflowDbContext`. Do not inject them directly into singleton hosted services. Always create a scope.

## Sender Failures

`ProcessNotificationOutboxService`:

- Claims a row by setting `Status = Processing`.
- Calls `INotificationSender.SendAsync`.
- Marks row `Sent` if successful.
- On exception, releases the claim, increments `RetryCount`, stores `LastError`, and returns a failure result.

API hosted service should log failures and retry on the next interval. Do not swallow repeated failures silently.

## SMTP Safety

`SmtpNotificationSender`:

- Requires `Smtp:Host`.
- Requires `Smtp:From`.
- Defaults `Smtp:Port` to `25`.
- Uses `Smtp:RecipientOverride` if present.
- Otherwise requires `Smtp:RoleEmailMap:<Role>`.

Do not add SMTP credentials or new third-party sender packages without approval. Current v1 assumes an on-prem/IP-restricted relay.

## Testing

Recommended tests:

- Hosted service creates a scope before resolving scoped workflow service.
- SLA scanner queues overdue notification once.
- Outbox processor sends pending rows in order.
- Outbox processor claim-before-send prevents duplicate sends.
- Sender failure keeps row pending and increments retry count.
- `NoOpNotificationSender` is used by default API integration tests.
- SMTP tests target fake local smtp4dev/Papercut only.

Current tests covering these areas include:

- `Phase5SlaTests`
- `Phase5OutboxTests`
- `NotificationSenderSelectionTests`
- `SmtpNotificationSenderTests`
- `SmtpNotificationSenderIntegrationTests` with `Category=Smtp`

