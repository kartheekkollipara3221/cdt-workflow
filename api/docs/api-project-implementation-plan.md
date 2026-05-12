# API Project Implementation Plan

## API Folder Structure

The ASP.NET Core API will live entirely under the new top-level `api/` folder:

- `api/WorkflowEngine.Api.sln`
- `api/src/WorkflowEngine.Api`
- `api/tests/WorkflowEngine.Api.Tests`
- `api/docs`

No API controllers, API services, API options, API tests, or API docs will be added under the existing `src/WorkflowEngine.*` projects or `tests/WorkflowEngine.Tests`.

## API Solution And Project Files

Create a separate solution at `api/WorkflowEngine.Api.sln`.

Create the web project at `api/src/WorkflowEngine.Api/WorkflowEngine.Api.csproj` using `Microsoft.NET.Sdk.Web`, targeting `net10.0`, with warnings treated as errors through the repository `Directory.Build.props`.

Create the test project at `api/tests/WorkflowEngine.Api.Tests/WorkflowEngine.Api.Tests.csproj`, targeting `net10.0`, using xUnit package versions consistent with `tests/WorkflowEngine.Tests`.

## Project Reference Plan

`WorkflowEngine.Api.csproj` will reference:

- `../../../src/WorkflowEngine.Application/WorkflowEngine.Application.csproj`
- `../../../src/WorkflowEngine.Infrastructure/WorkflowEngine.Infrastructure.csproj`
- `../../../src/WorkflowEngine.Domain/WorkflowEngine.Domain.csproj`

The API project will not reference `WorkflowEngine.Console`.

The test project will reference the API project and use direct hand-written fakes for controller/service unit tests.

## Database And Seed Readiness Plan

Database startup work will be API-internal only. The API will reuse:

- `IWorkflowDatabaseInitializer`
- `ISeedVerificationService`
- `WorkflowDbContext` through `AddWorkflowInfrastructure(builder.Configuration)`

API startup migration/seed and seed verification will be controlled by `WorkflowDatabaseStartupOptions`:

- `ApplyMigrationsAndSeedOnStartup=false`
- `VerifySeedOnStartup=false`
- `FailStartupWhenSeedVerificationFails=true`

`WorkflowDatabaseReadinessService` will call the existing initializer and verifier when explicitly enabled. `WorkflowDatabaseStartupHostedService` will run once at host startup, create a scope through `IServiceScopeFactory`, and never run per request.

## Endpoint List

Implement exactly three public workflow endpoint groups:

- `POST /api/workflows/{workflowKey}/instances`
- `GET /api/workflows/instances/{workflowInstanceId}/context`
- `POST /api/workflows/instances/{workflowInstanceId}/actions`

Do not expose public routes for demo, reset, admin override, command registry, execute-json file processing, init-db, verify-seed, run-sla, process-notification-outbox, show, show-tasks, show-history, or show-notifications.

## Controller Design

Controllers will derive from `ControllerBase`, use `[ApiController]`, receive dependencies through constructor injection, and call application services directly.

Controllers will not contain transition logic, will not query `WorkflowDbContext`, will not expose EF entities, and will not hardcode Portfolio Trade transitions.

## Actor Resolution Design

`ClaimsWorkflowActorResolver` will resolve user identity and workflow roles from `HttpContext.User` claims. It will support optional development headers only when `WorkflowApiOptions.AllowDevelopmentHeaderActorResolver=true`:

- `X-Workflow-UserId`
- `X-Workflow-DisplayName`
- `X-Workflow-Roles`

Request body actor data will never be trusted. The execute controller will overwrite `WorkflowActionEnvelope.Actor` before normalization, validation, hashing, and execution.

## Dependency Injection Plan

`Program.cs` will register:

- MVC controllers
- `TimeProvider.System`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowActionResultBuilder`
- `CanonicalWorkflowResultSerializer`
- `AddWorkflowInfrastructure(builder.Configuration)`
- `WorkflowApiOptions`
- `WorkflowDatabaseStartupOptions`
- `WorkflowDatabaseReadinessService`
- `WorkflowDatabaseStartupHostedService`
- `IWorkflowActorResolver` / `ClaimsWorkflowActorResolver`
- `WorkflowHttpResultMapper`

It will not register `CommandRegistry`, `CommandArgumentParser`, console command handlers, or demo command handlers.

## Configuration Plan

Add `appsettings.json` and `appsettings.Development.json` with:

- `ConnectionStrings:DefaultConnection`
- `Notifications:Sender`
- `Smtp` placeholders used by the existing infrastructure when SMTP is enabled
- `WorkflowEngine`
- `WorkflowApi`
- `WorkflowDatabaseStartup`

Production-safe defaults stay disabled for public reset/admin override and database startup work.

## HTTP Result Mapping Plan

`WorkflowHttpResultMapper` will map workflow result codes to HTTP status codes:

- `Success` -> 200
- `ValidationFailed` -> 400
- `WorkflowInstanceNotFound` -> 404
- `Forbidden` -> 403
- `AmbiguousRoleMatch` -> 403
- `InvalidTransition` -> 409
- `ConcurrencyConflict` -> 409
- `WorkflowDefinitionVersionMismatch` -> 409
- `IdempotencyConflict` -> 409
- `AlreadyExists` -> 409
- unknown -> 500

Ambiguous role matches are mapped to 403 because the caller's authenticated role set is not authorized in a single unambiguous way; it is not a resource state conflict.

## Test Plan

Add focused tests under `api/tests/WorkflowEngine.Api.Tests` covering:

- project references and forbidden console reference
- safe option defaults
- database startup hosted-service gating and scoped execution
- DI registration with workflow infrastructure configuration
- absence of public admin/demo/console routes
- actor resolution from claims and gated development headers
- HTTP result status mapping
- start workflow controller request shaping and reset suppression
- context controller actor role forwarding
- execute controller null body, route/body mismatch, server-stamped actor, validation failure, and result-code HTTP mapping

SQL-backed seed verification will remain an operator/local command because this environment may not have SQL Server available.

## Verification Commands

Run the relevant safe gates:

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
dotnet restore api/WorkflowEngine.Api.sln
dotnet build api/WorkflowEngine.Api.sln -warnaserror
dotnet test api/WorkflowEngine.Api.sln
```

Document operator commands for SQL-backed seed readiness:

```bash
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```
