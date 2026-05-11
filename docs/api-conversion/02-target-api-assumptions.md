# Target API Assumptions

The real API project is external and was not available during this documentation pass. These assumptions and placeholders keep the migration instructions practical without pretending the external API was inspected.

## Known Target Facts

- The existing API project is external.
- The existing API is a controller-style ASP.NET Core API.
- The existing API uses the same .NET target framework as this console project.
- The existing API will manually receive the workflow engine capabilities.
- The API should not migrate the console entry point as-is.
- The API should call reusable workflow services directly.

## Required Placeholders

Use these placeholders consistently while applying this package:

- `<ApiProjectName>`
- `<ApiProjectPath>`
- `<ApiNamespace>`
- `<ControllerFolder>`
- `<ServiceFolder>`
- `<DtoFolder>`
- `<OptionsFolder>`
- `<HostedServiceFolder>`
- `<TestProjectName>`
- `<ApiSolutionPath>`

## Unknowns To Resolve

| Unknown | Why It Matters | How To Check In Existing API Project | What To Do With The Answer |
|---|---|---|---|
| API project name | Determines namespaces, `.csproj` edits, and test naming. | Inspect `<ApiSolutionPath>` and the API `.csproj`. | Replace `<ApiProjectName>` everywhere. |
| API project path | Determines relative `ProjectReference` paths. | Locate the `.csproj` that uses `Microsoft.NET.Sdk.Web`. | Replace `<ApiProjectPath>` and adjust all paths. |
| API namespace | Keeps controllers/services consistent with existing code. | Open existing controllers and `Program.cs`. | Replace `<ApiNamespace>`. |
| Existing folder structure | Avoids introducing alien layout. | Inspect controllers, services, options, hosted services, tests. | Map placeholders to local folders. |
| Existing `Program.cs` style | Determines registration location and startup validation pattern. | Read API `Program.cs`. | Add workflow registration in the established section. |
| Existing authentication setup | Actor resolution depends on it. | Inspect `AddAuthentication`, `AddAuthorization`, policies, middleware. | Implement `ClaimsWorkflowActorResolver` against the real claims model. |
| Existing claims/roles source | Workflow roles must come from trusted auth, not body JSON. | Inspect identity provider docs, claim names, group mapping, authorization handlers. | Map claims/groups to `Maker`, `Checker`, `TradingDesk`, `BDRMClientService`. |
| Existing error response convention | Determines whether to use `ProblemDetails` directly or a wrapper. | Inspect exception middleware, validation filters, controller examples. | Implement `WorkflowHttpResultMapper` consistently. |
| Existing logging convention | Controls correlation IDs and sensitive data policy. | Inspect logging middleware, enrichers, scopes, telemetry. | Add workflow logs using the same conventions. |
| Existing validation framework | Determines whether to use `System.ComponentModel.DataAnnotations`, FluentValidation, or custom filters. | Inspect request DTOs and validators. | Keep `WorkflowActionEnvelopeValidator` regardless; add model validation only for API wrappers. |
| Existing database strategy | Determines migration ownership and connection string reuse. | Inspect DbContexts, migration projects, deployment scripts. | Decide whether workflow migrations stay in `WorkflowEngine.Infrastructure`. |
| Existing CI/test setup | Determines how SQL-backed workflow tests run. | Inspect pipeline YAML and test projects. | Add API integration tests and SQL configuration. |
| Existing environment configuration style | Determines secrets and production config placement. | Inspect `appsettings*.json`, key vault, environment variable conventions. | Add workflow sections using existing secret management. |
| Existing background service/job strategy | Determines SLA/outbox processing approach. | Inspect hosted services, scheduler, worker services, infrastructure jobs. | Choose hosted service, external job, or admin-only operation. |

## Checklist Before Applying Migration

- Confirm `<ApiSolutionPath>`.
- Confirm `<ApiProjectPath>` and `<ApiProjectName>`.
- Confirm API target framework is `net10.0`.
- Confirm the API uses controllers and `MapControllers`.
- Confirm namespace conventions.
- Confirm `Program.cs` service registration style.
- Confirm authentication middleware is already present.
- Confirm authorization policies or role/group handlers.
- Identify trusted user ID claim.
- Identify trusted display name claim.
- Identify trusted group or role claims.
- Decide exact mapping to `Maker`, `Checker`, `TradingDesk`, and `BDRMClientService`.
- Confirm existing API response/error conventions.
- Confirm existing logging and correlation ID standard.
- Confirm database connection string key and SQL Server target.
- Confirm EF migration ownership.
- Confirm whether admin endpoints are allowed.
- Confirm whether background processing belongs in the API host.
- Confirm API integration test strategy and SQL test connection.

## Baseline Assumptions For This Package

Until replaced by facts from the real API:

- Controllers live under `<ControllerFolder>`.
- API-specific services live under `<ServiceFolder>`.
- API request/response DTOs live under `<DtoFolder>`.
- API options live under `<OptionsFolder>`.
- Hosted services live under `<HostedServiceFolder>`.
- The API uses standard ASP.NET Core dependency injection.
- The API has authentication before workflow endpoints are exposed.
- The API uses `ProblemDetails` or a closely related error model.
- SQL Server remains the workflow persistence store.
- `WorkflowEngine.Infrastructure` owns the workflow EF model unless the API team explicitly chooses otherwise.

