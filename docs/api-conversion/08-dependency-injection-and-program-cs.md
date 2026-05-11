# Dependency Injection and Program.cs Integration

This document explains how to register workflow services in the external API `Program.cs`.

## Current Console Registration

`src/WorkflowEngine.Console/Program.cs` registers:

- `IConfiguration`
- `TimeProvider.System`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowActionResultBuilder`
- `CanonicalWorkflowResultSerializer`
- all console command handlers
- `CommandRegistry`
- `AddWorkflowInfrastructure(configuration)`

The API should keep the reusable registrations and omit console command registrations.

## Service Registration Table

| Service / Class | Interface | Lifetime | Register In API? | Registration Source | Reason |
|---|---|---|---|---|---|
| `TimeProvider.System` | `TimeProvider` | Singleton | Yes | API `Program.cs` | Required by start/context/execution/SLA/outbox services. |
| `WorkflowActionEnvelopeValidator` | Concrete | Scoped | Yes | API `Program.cs` | Execute action validation. |
| `WorkflowActionEnvelopeNormalizer` | Concrete | Scoped | Yes | API `Program.cs` | Effective IDs and idempotency key normalization. |
| `CanonicalRequestHasher` | Concrete | Scoped | Yes | API `Program.cs` | Request hash before execution. |
| `WorkflowActionResultBuilder` | Concrete | Scoped | Yes | API `Program.cs` | Validation/start/execution result envelopes. |
| `CanonicalWorkflowResultSerializer` | Concrete | Scoped | Yes | API `Program.cs` | Result hash and stored replay JSON. |
| `WorkflowDbContext` | Concrete | Scoped | Yes | `AddWorkflowInfrastructure` | SQL Server persistence. |
| `WorkflowSeedService` | Concrete | Scoped | Deployment/admin only | `AddWorkflowInfrastructure` | Seed data upsert. |
| `IWorkflowDatabaseInitializer` | Interface | Scoped | Deployment/admin only | `AddWorkflowInfrastructure` | Applies migrations and seed. |
| `ISeedVerificationService` | Interface | Scoped | Admin/deployment check | `AddWorkflowInfrastructure` | Verifies seed metadata. |
| `IWorkflowContextReader` | Interface | Scoped | Yes | `AddWorkflowInfrastructure` | Context endpoint. |
| `IWorkflowExecutionService` | Interface | Scoped | Yes | `AddWorkflowInfrastructure` | Execute action endpoint. |
| `IWorkflowInspectionReader` | Interface | Scoped | Optional | `AddWorkflowInfrastructure` | Admin/inspection endpoints. |
| `IStartWorkflowService` | Interface | Scoped | Yes | `AddWorkflowInfrastructure` | Start workflow endpoint. |
| `IRunSlaService` | Interface | Scoped | Yes if SLA is enabled | `AddWorkflowInfrastructure` | SLA scan. |
| `IProcessNotificationOutboxService` | Interface | Scoped | Yes if notifications enabled | `AddWorkflowInfrastructure` | Outbox processing. |
| `INotificationSender` | Interface | Singleton | Yes | `AddWorkflowInfrastructure` | Console/NoOp/Smtp by configuration. |
| `IWorkflowActorResolver` | Interface | Scoped or Singleton | Yes | API project | Authenticated actor mapping. |
| `ClaimsWorkflowActorResolver` | `IWorkflowActorResolver` | Scoped or Singleton | Yes | API project | Depends on claims mapping. |
| `WorkflowHttpResultMapper` | Concrete | Singleton | Yes | API project | Maps result codes to HTTP responses. |
| SLA hosted service | `IHostedService` | Singleton | Optional | API project | Scheduled SLA scan. Use scopes internally. |
| Outbox hosted service | `IHostedService` | Singleton | Optional | API project | Scheduled notification processing. Use scopes internally. |

## API Program.cs Registration Block

```csharp
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.Validation;
using WorkflowEngine.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthentication(/* existing API auth */);
builder.Services.AddAuthorization(/* existing API policies */);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<WorkflowActionEnvelopeValidator>();
builder.Services.AddScoped<WorkflowActionEnvelopeNormalizer>();
builder.Services.AddScoped<CanonicalRequestHasher>();
builder.Services.AddScoped<WorkflowActionResultBuilder>();
builder.Services.AddScoped<CanonicalWorkflowResultSerializer>();

builder.Services.AddWorkflowInfrastructure(builder.Configuration);

builder.Services.AddScoped<IWorkflowActorResolver, ClaimsWorkflowActorResolver>();
builder.Services.AddSingleton<WorkflowHttpResultMapper>();

// Optional background processing:
// builder.Services.AddHostedService<WorkflowSlaHostedService>();
// builder.Services.AddHostedService<NotificationOutboxHostedService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

Place this inside the existing API `Program.cs` conventions rather than replacing the file wholesale.

## Services Already Registered By `AddWorkflowInfrastructure`

`AddWorkflowInfrastructure(builder.Configuration)` registers:

- `WorkflowDbContext` using SQL Server and `ConnectionStrings:DefaultConnection`
- `WorkflowSeedService`
- `WorkflowDatabaseInitializer`
- `SeedVerificationService`
- `WorkflowContextReader`
- `WorkflowExecutionService`
- `WorkflowInspectionReader`
- `StartWorkflowService`
- `RunSlaService`
- `ProcessNotificationOutboxService`
- interface mappings for the above
- singleton `INotificationSender`

It also validates `Notifications:Sender`. Unknown sender values fail startup. SMTP mode validates `Smtp:Host` and `Smtp:From`.

## Services To Register Manually

Register manually in the API:

- `TimeProvider.System`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowActionResultBuilder`
- `CanonicalWorkflowResultSerializer`
- `IWorkflowActorResolver`
- `WorkflowHttpResultMapper`
- optional hosted services

## Services Not To Register For Normal API Runtime

Do not register these for normal API runtime:

- `CommandRegistry`
- `CommandArgumentParser`
- `IStartWorkflowCommandHandler` / `StartWorkflowCommandHandler`
- `IExecuteJsonCommandHandler` / `ExecuteJsonCommandHandler`
- `IShowContextCommandHandler` / `ShowContextCommandHandler`
- `IShowCommandHandler` / `ShowCommandHandler`
- `IShowTasksCommandHandler` / `ShowTasksCommandHandler`
- `IShowHistoryCommandHandler` / `ShowHistoryCommandHandler`
- `IShowNotificationsCommandHandler` / `ShowNotificationsCommandHandler`
- `IRunSlaCommandHandler` / `RunSlaCommandHandler`
- `IProcessNotificationOutboxCommandHandler` / `ProcessNotificationOutboxCommandHandler`
- `IDemoCommandHandler` / `DemoCommandHandler`
- `IDemoStepRunner` / `WorkflowEngineDemoStepRunner`

Those are console adapters.

## Lifetimes And State

- `WorkflowDbContext` is scoped.
- Workflow services are scoped because they use `WorkflowDbContext`.
- Helper services can be scoped for consistency.
- `INotificationSender` is singleton in current infrastructure registration.
- Actor resolver can be scoped if it depends on scoped API services; otherwise singleton is fine.
- Hosted services are singletons and must create scopes to use scoped workflow services.

## Scoped DbContext Rules

Do not inject `WorkflowDbContext` into singleton services. Do not store it in static fields. Do not manually construct it in controllers.

If a controller needs workflow behavior, inject a service interface. If an admin endpoint needs data not exposed by an existing reader, add a scoped application/API service and use `WorkflowDbContext` there.

## Hosted Service Scope Rules

Hosted services are singleton by default. Use `IServiceScopeFactory`:

```csharp
public sealed class WorkflowSlaHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;

    public WorkflowSlaHostedService(IServiceScopeFactory scopeFactory)
    {
        this.scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sla = scope.ServiceProvider.GetRequiredService<IRunSlaService>();
        await sla.RunAsync(new RunSlaRequest(DateTimeOffset.UtcNow), stoppingToken);
    }
}
```

Replace `DateTimeOffset.UtcNow` with injected `TimeProvider` in real code. This snippet is only a shape example; the project rule is to use `TimeProvider`.

## Notification Sender Selection

Configured by `Notifications:Sender`:

- Missing or `Console` -> `ConsoleNotificationSender`
- `NoOp` -> `NoOpNotificationSender`
- `Smtp` -> `SmtpNotificationSender`

For production API, prefer `Smtp` only after configuration is valid. For tests, use `NoOp` or an API test double.

