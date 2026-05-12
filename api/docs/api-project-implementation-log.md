# API Project Implementation Log

## 1. Clean API Folder Created

Files changed:

- `api/`
- `api/docs/`
- `api/src/WorkflowEngine.Api/`
- `api/tests/WorkflowEngine.Api.Tests/`

Summary:

- Created the required clean top-level API folder.
- Kept all API source, tests, options, services, and docs under `api/`.

Tests added:

- Covered by API project structure tests.

Verification command run:

- `git status --short`

Result:

- Only `api/` is new for this task. Pre-existing untracked `FULL_PROJECT_REVIEW.html` remains untouched.

Unresolved issue:

- None.

## 2. API Solution Created

Files changed:

- `api/WorkflowEngine.Api.sln`

Summary:

- Created a separate `.sln` solution file.
- The .NET 10 template initially created `.slnx`; it was replaced with the required `.sln`.

Tests added:

- Project structure tests validate the API project references and forbidden console reference.

Verification command run:

- `dotnet sln api\WorkflowEngine.Api.sln add api\src\WorkflowEngine.Api\WorkflowEngine.Api.csproj`
- `dotnet sln api\WorkflowEngine.Api.sln add api\tests\WorkflowEngine.Api.Tests\WorkflowEngine.Api.Tests.csproj`

Result:

- API solution contains API, API tests, and reusable library projects for developer convenience.

Unresolved issue:

- None.

## 3. API Project Created

Files changed:

- `api/src/WorkflowEngine.Api/WorkflowEngine.Api.csproj`
- `api/src/WorkflowEngine.Api/Program.cs`

Summary:

- Created a controller-style ASP.NET Core Web SDK project targeting `net10.0`.
- `Program.cs` calls `AddControllers()` and `MapControllers()`.
- No template or WeatherForecast endpoint was added.

Tests added:

- `ApiProjectStructureTests.ApiProgramDoesNotUseMinimalApiWorkflowRouteHandlersOrTemplateEndpoints`

Verification command run:

- `dotnet build api\WorkflowEngine.Api.sln -warnaserror`

Result:

- Passed.

Unresolved issue:

- None.

## 4. API Test Project Created

Files changed:

- `api/tests/WorkflowEngine.Api.Tests/WorkflowEngine.Api.Tests.csproj`

Summary:

- Created an xUnit test project under the API folder.
- Used package versions consistent with `tests/WorkflowEngine.Tests`.
- Added `Microsoft.AspNetCore.App` framework reference for controller and HTTP test types.

Tests added:

- API controller, service, startup, and structure tests.

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed. 38 tests.

Unresolved issue:

- None.

## 5. Project References Added

Files changed:

- `api/src/WorkflowEngine.Api/WorkflowEngine.Api.csproj`

Summary:

- API project references:
  - `WorkflowEngine.Application`
  - `WorkflowEngine.Infrastructure`
  - `WorkflowEngine.Domain`
- API project does not reference `WorkflowEngine.Console`.

Tests added:

- `ApiProjectStructureTests.ApiProjectDoesNotReferenceWorkflowEngineConsole`

Verification command run:

- `dotnet build api\WorkflowEngine.Api.sln -warnaserror`

Result:

- Passed.

Unresolved issue:

- None.

## 6. Configuration Added

Files changed:

- `api/src/WorkflowEngine.Api/appsettings.json`
- `api/src/WorkflowEngine.Api/appsettings.Development.json`
- `api/src/WorkflowEngine.Api/Options/WorkflowApiOptions.cs`
- `api/src/WorkflowEngine.Api/Options/WorkflowDatabaseStartupOptions.cs`

Summary:

- Added connection string, notifications, SMTP placeholders, `WorkflowEngine`, `WorkflowApi`, and `WorkflowDatabaseStartup` sections.
- Safe defaults:
  - `AllowDevelopmentHeaderActorResolver=false`
  - `DisablePublicReset=true`
  - `DisablePublicAdminOverride=true`
  - `ApplyMigrationsAndSeedOnStartup=false`
  - `VerifySeedOnStartup=false`
  - `FailStartupWhenSeedVerificationFails=true`

Tests added:

- `ApiOptionsTests.WorkflowApiOptionsDefaultsAreSafe`
- `ApiOptionsTests.WorkflowDatabaseStartupOptionsDefaultsAreSafe`

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 7. Database And Seed Readiness Service Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Services/WorkflowDatabaseReadinessService.cs`
- `api/src/WorkflowEngine.Api/Services/WorkflowDatabaseReadinessResult.cs`

Summary:

- Added API-side readiness service that reuses `IWorkflowDatabaseInitializer` and `ISeedVerificationService`.
- Does not duplicate seed data.
- Does not copy Portfolio Trade metadata into the API.
- Does not hardcode Portfolio Trade transitions.
- Does nothing destructive by default.

Tests added:

- Exercised through hosted-service startup tests.

Verification command run:

- `dotnet build api\WorkflowEngine.Api.sln -warnaserror`

Result:

- Passed.

Unresolved issue:

- Existing console `init-db` fails in this environment with EF Core `PendingModelChangesWarning`.

## 8. Database Startup Hosted Service Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Services/WorkflowDatabaseStartupHostedService.cs`

Summary:

- Runs once at application startup.
- Exits without creating a scope when database startup options are disabled.
- Uses `IServiceScopeFactory` when migration/seed or verification is enabled.
- Does not inject scoped workflow services directly into the singleton hosted service.

Tests added:

- `WorkflowDatabaseStartupTests.HostedServiceDoesNothingWhenStartupDatabaseOptionsAreDisabled`
- `WorkflowDatabaseStartupTests.HostedServiceUsesScopeWhenStartupDatabaseWorkIsEnabled`

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None in API code.

## 9. Database And Configuration Readiness Tests Added

Files changed:

- `api/tests/WorkflowEngine.Api.Tests/Startup/ApiOptionsTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Startup/ApiProjectStructureTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Services/WorkflowDatabaseStartupTests.cs`

Summary:

- Added tests for safe defaults, no console reference, DI registration, no forbidden public routes, and hosted-service scope behavior.

Tests added:

- See files above.

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- SQL-backed seed verification could not be claimed as passed because `verify-seed` failed with an SSPI error against the local integrated-security connection.

## 10. Program.cs And DI Registration Completed

Files changed:

- `api/src/WorkflowEngine.Api/Program.cs`

Summary:

- Registered controllers, TimeProvider, validators, normalizer, hasher, result builder, result serializer, infrastructure, API options, database startup service, actor resolver, and HTTP mapper.
- Did not register console command handlers, `CommandRegistry`, `CommandArgumentParser`, or demo runtime handlers.

Tests added:

- `ApiProjectStructureTests.ApiDependencyInjectionRegistersWorkflowInfrastructureServices`
- `ApiProjectStructureTests.ApiProgramDoesNotUseMinimalApiWorkflowRouteHandlersOrTemplateEndpoints`

Verification command run:

- `dotnet build api\WorkflowEngine.Api.sln -warnaserror`
- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 11. Actor Resolver Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Services/WorkflowActorSnapshot.cs`
- `api/src/WorkflowEngine.Api/Services/IWorkflowActorResolver.cs`
- `api/src/WorkflowEngine.Api/Services/ClaimsWorkflowActorResolver.cs`
- `api/src/WorkflowEngine.Api/Services/WorkflowActorResolutionException.cs`

Summary:

- Resolves actor user ID, display name, and roles from claims.
- Supports development headers only when explicitly enabled.
- Fails with 401 for missing actor identity and 403 for missing workflow roles.
- Does not read body actor data.

Tests added:

- `ClaimsWorkflowActorResolverTests`
- Execute-controller actor overwrite test.

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- Production authentication scheme is intentionally not configured in this conversion; caller integration must add the real auth provider.

## 12. HTTP Result Mapper Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Services/WorkflowHttpResultMapper.cs`

Summary:

- Maps real workflow result code strings to HTTP status codes.
- `AmbiguousRoleMatch` maps to 403 because it is an actor authorization ambiguity.

Tests added:

- `WorkflowHttpResultMapperTests`
- Execute-controller result-code mapping tests.

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 13. Start Workflow Endpoint Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Controllers/WorkflowInstancesController.cs`
- `api/src/WorkflowEngine.Api/Contracts/StartWorkflowApiRequest.cs`

Summary:

- Added `POST /api/workflows/{workflowKey}/instances`.
- Calls `IStartWorkflowService`.
- Uses actor resolver for `StartedByUserId`.
- Always passes `Reset=false`.
- DTO exposes only `BusinessObjectType` and `BusinessObjectId`.

Tests added:

- `WorkflowInstancesControllerTests`

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 14. Context Endpoint Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Controllers/WorkflowContextController.cs`

Summary:

- Added `GET /api/workflows/instances/{workflowInstanceId}/context`.
- Calls `IWorkflowContextReader`.
- Roles come from actor resolver, not query string or request body.
- Returns `WorkflowContextResponse`.

Tests added:

- `WorkflowContextControllerTests`

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 15. Execute Action Endpoint Implemented

Files changed:

- `api/src/WorkflowEngine.Api/Controllers/WorkflowActionsController.cs`

Summary:

- Added `POST /api/workflows/instances/{workflowInstanceId}/actions`.
- Accepts `WorkflowActionEnvelope`.
- Rejects null bodies and route/body workflow instance mismatches.
- Stamps or overwrites actor from resolver before normalization, validation, hashing, and execution.
- Calls `IWorkflowExecutionService` using `WorkflowExecutionRequest`.
- Does not allow admin override.
- Does not query `WorkflowDbContext`.
- Does not implement transition logic in the controller.

Tests added:

- `WorkflowActionsControllerTests`

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed.

Unresolved issue:

- None.

## 16. API Tests Added

Files changed:

- `api/tests/WorkflowEngine.Api.Tests/Controllers/WorkflowActionsControllerTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Controllers/WorkflowContextControllerTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Controllers/WorkflowInstancesControllerTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Services/ClaimsWorkflowActorResolverTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Services/WorkflowDatabaseStartupTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Services/WorkflowHttpResultMapperTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Startup/ApiOptionsTests.cs`
- `api/tests/WorkflowEngine.Api.Tests/Startup/ApiProjectStructureTests.cs`

Summary:

- Added 38 API tests.
- Used hand-written fakes instead of SQL-backed API integration tests.

Verification command run:

- `dotnet test api\WorkflowEngine.Api.sln`

Result:

- Passed. 38 passed, 0 failed.

Unresolved issue:

- None.

## 17. Build And Test Verification Completed

Files changed:

- No source changes during verification.

Summary:

- Ran API and existing solution verification commands.

Verification commands run:

- `dotnet restore api\WorkflowEngine.Api.sln`: passed.
- `dotnet build api\WorkflowEngine.Api.sln -warnaserror`: passed.
- `dotnet test api\WorkflowEngine.Api.sln`: passed, 38 tests.
- `dotnet restore src\WorkflowEngine.sln`: passed.
- `dotnet build src\WorkflowEngine.sln -warnaserror`: passed.
- `dotnet test src\WorkflowEngine.sln`: failed without SQL connection configuration.
- `dotnet test src\WorkflowEngine.sln --no-restore --no-build` with `ConnectionStrings__DefaultConnection=Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False`: exited successfully.
- `dotnet format src\WorkflowEngine.sln --verify-no-changes --no-restore`: passed.
- `dotnet format api\WorkflowEngine.Api.sln --verify-no-changes --no-restore`: passed.
- `dotnet list src\WorkflowEngine.sln package --vulnerable`: passed, no vulnerable packages reported.
- `dotnet list api\WorkflowEngine.Api.sln package --vulnerable`: passed, no vulnerable packages reported.
- `dotnet run --no-restore --no-build --project src\WorkflowEngine.Console -- init-db` with the same connection: failed with EF Core `PendingModelChangesWarning`.
- `dotnet run --no-restore --no-build --project src\WorkflowEngine.Console -- verify-seed` with the same connection: failed with SQL Server integrated-security error `Cannot generate SSPI context`.

Unresolved issue:

- Local/operator seed verification did not pass in this environment. Run the documented console commands in an environment with a valid SQL Server connection and resolved EF migration model state.

## 18. Final Self-Review Completed

Files changed:

- `api/docs/api-project-final-review.md`

Summary:

- Re-read prior API conversion analysis documents.
- Confirmed API implementation is isolated under `api/`.
- Confirmed `src/WorkflowEngine.sln` was not modified.
- Confirmed `WorkflowEngine.Api` does not reference `WorkflowEngine.Console`.
- Confirmed no public init-db, verify-seed, reset, admin override, run-sla, process-notification-outbox, demo, execute-json, show, show-tasks, show-history, or show-notifications route exists.
- Confirmed controllers do not query `WorkflowDbContext` or contain transition logic.
- Confirmed Portfolio Trade remains seed/metadata-driven.

Verification command run:

- `rg -n "WorkflowEngine.Console|CommandRegistry|CommandArgumentParser|MapGet\(|MapPost\(|WeatherForecast|run-sla|process-notification-outbox|verify-seed|init-db|admin override|admin|reset|DateTime\.UtcNow" api\src\WorkflowEngine.Api`

Result:

- No matches.

Unresolved issue:

- SQL seed verification remains blocked by the local SQL/EF issues noted above.
