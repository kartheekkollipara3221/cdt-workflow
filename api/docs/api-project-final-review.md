# API Project Final Review

## Endpoints Implemented

| Method | Route | Controller | Purpose |
|---|---|---|---|
| `POST` | `/api/workflows/{workflowKey}/instances` | `WorkflowInstancesController` | Start a workflow instance for a business object. |
| `GET` | `/api/workflows/instances/{workflowInstanceId}/context` | `WorkflowContextController` | Return persisted workflow context for form-open behavior. |
| `POST` | `/api/workflows/instances/{workflowInstanceId}/actions` | `WorkflowActionsController` | Execute one workflow action envelope. |

## Endpoints Intentionally Not Implemented

The API does not expose public endpoints for:

- demo
- reset
- admin override
- command registry
- command argument parsing
- execute-json file processing
- init-db
- verify-seed
- run-sla
- process-notification-outbox
- show
- show-tasks
- show-history
- show-notifications

These were excluded because they are console/operator/demo capabilities or operational jobs, not public workflow API features. Exposing them would risk destructive reset behavior, role bypass, metadata initialization from public traffic, or accidental coupling to the console adapter.

## Database And Seed Readiness Behavior

The API includes:

- `WorkflowDatabaseReadinessService`
- `WorkflowDatabaseReadinessResult`
- `WorkflowDatabaseStartupHostedService`
- `WorkflowDatabaseStartupOptions`

Startup migration/seed and verification are disabled by default:

```json
{
  "WorkflowDatabaseStartup": {
    "ApplyMigrationsAndSeedOnStartup": false,
    "VerifySeedOnStartup": false,
    "FailStartupWhenSeedVerificationFails": true
  }
}
```

This is disabled by default because applying migrations and seed data is an operational deployment action. It should not happen accidentally every time the web host starts in production.

For development or controlled environments, enable one or both:

```json
{
  "WorkflowDatabaseStartup": {
    "ApplyMigrationsAndSeedOnStartup": true,
    "VerifySeedOnStartup": true,
    "FailStartupWhenSeedVerificationFails": true
  }
}
```

The startup hosted service runs once at application startup and uses a scope to resolve the readiness service. It never runs per request and does not expose public routes.

## Operator Commands

The existing console remains the operator tool for local database initialization and seed verification:

```bash
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

In this environment:

- `init-db` failed with EF Core `PendingModelChangesWarning`.
- `verify-seed` failed with SQL Server integrated-security error `Cannot generate SSPI context`.

Because of that, SQL seed verification is not claimed as passed here.

## How To Run The API

Configure `ConnectionStrings:DefaultConnection`, then run:

```bash
dotnet run --project api/src/WorkflowEngine.Api/WorkflowEngine.Api.csproj
```

For local development without a real authentication provider, `appsettings.Development.json` enables the development header actor resolver:

- `X-Workflow-UserId`
- `X-Workflow-DisplayName`
- `X-Workflow-Roles`

Production should provide real authentication and claims instead.

## Required Configuration

Required or relevant keys:

- `ConnectionStrings:DefaultConnection`
- `Notifications:Sender`
- `Smtp:Host`
- `Smtp:Port`
- `Smtp:From`
- `Smtp:RecipientOverride`
- `Smtp:RoleEmailMap`
- `WorkflowApi:AllowDevelopmentHeaderActorResolver`
- `WorkflowApi:DisablePublicReset`
- `WorkflowApi:DisablePublicAdminOverride`
- `WorkflowDatabaseStartup:ApplyMigrationsAndSeedOnStartup`
- `WorkflowDatabaseStartup:VerifySeedOnStartup`
- `WorkflowDatabaseStartup:FailStartupWhenSeedVerificationFails`

Do not put production passwords, tokens, or SMTP credentials in committed configuration.

## Security Notes

- Actor identity and workflow roles come from claims.
- Development headers are ignored unless explicitly enabled.
- The execute endpoint overwrites request body `actor` before validation, hashing, and execution.
- Body actor roles are not trusted.
- Public reset and admin override are not exposed.
- Controllers do not log raw `dataSave.payload`.
- No SQL user-role table was added.

## Existing Workflow Services Reused

The API reuses:

- `IStartWorkflowService`
- `StartWorkflowService`
- `IWorkflowContextReader`
- `WorkflowContextReader`
- `IWorkflowExecutionService`
- `WorkflowExecutionService`
- `IWorkflowDatabaseInitializer`
- `WorkflowDatabaseInitializer`
- `ISeedVerificationService`
- `SeedVerificationService`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowActionResultBuilder`
- `CanonicalWorkflowResultSerializer`
- `WorkflowDbContext`
- `AddWorkflowInfrastructure(builder.Configuration)`

Operational services such as `IRunSlaService`, `IProcessNotificationOutboxService`, and notification senders remain registered by infrastructure but are not public API endpoints.

## Test Commands

API verification:

```bash
dotnet restore api/WorkflowEngine.Api.sln
dotnet build api/WorkflowEngine.Api.sln -warnaserror
dotnet test api/WorkflowEngine.Api.sln
dotnet format api/WorkflowEngine.Api.sln --verify-no-changes --no-restore
dotnet list api/WorkflowEngine.Api.sln package --vulnerable
```

Existing solution verification:

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
dotnet format src/WorkflowEngine.sln --verify-no-changes --no-restore
dotnet list src/WorkflowEngine.sln package --vulnerable
```

SQL-backed tests and console seed verification require a valid SQL Server connection string, for example:

```bash
set ConnectionStrings__DefaultConnection=Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

## Remaining Risks

- Production authentication is not configured in the new API project; the project currently provides the claims-based adapter point and a gated development header fallback.
- Local SQL seed verification did not pass in this environment due EF pending model changes and SSPI connectivity errors.
- SLA scan and notification outbox processing still need a production operational host or scheduled job. They are intentionally not public endpoints in this first conversion.

## Final Checklist

- API is fully contained under `api/`.
- `src/WorkflowEngine.sln` was not modified.
- No API files were added under existing `src/WorkflowEngine.*` projects.
- `WorkflowEngine.Api` does not reference `WorkflowEngine.Console`.
- No console command API runtime was added.
- No Minimal API workflow endpoints were added.
- Only start workflow, get context, and execute action public endpoint groups were implemented.
- Database readiness is internal and startup-gated.
- No public init-db endpoint exists.
- No public verify-seed endpoint exists.
- No public reset endpoint exists.
- No public admin override exists.
- No public run-sla endpoint exists.
- No public process-notification-outbox endpoint exists.
- Body actor roles are not trusted.
- Controllers do not contain transition logic.
- Controllers do not query `WorkflowDbContext` directly.
- Portfolio Trade remains metadata and seed driven.
