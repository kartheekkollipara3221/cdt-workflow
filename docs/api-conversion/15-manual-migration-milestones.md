# Manual Migration Milestones

This is the main step-by-step migration plan for applying the workflow engine to the external controller-style ASP.NET Core API.

Use placeholders from `02-target-api-assumptions.md` until replaced with real API paths and namespaces.

## Milestone Summary

| Milestone | Goal | Source Files | Target API Files | Tests | Done When |
|---|---|---|---|---|---|
| 1 | Prepare API project and references | `.csproj` files, `src/WorkflowEngine.sln` | `<ApiProjectPath>/<ApiProjectName>.csproj` | Build restore | API references Application, Infrastructure, Domain. |
| 2 | Add workflow configuration | console `appsettings*.json`, DI extension | API `appsettings*.json`, secrets/env | Startup config tests | Required settings exist. |
| 3 | Register workflow services | `Program.cs`, `AddWorkflowInfrastructure` | API `Program.cs` | Startup test | DI resolves workflow services. |
| 4 | Add authenticated actor resolver | `WorkflowActor`, context request | `<ServiceFolder>/ClaimsWorkflowActorResolver.cs` | Actor resolver tests | Roles come from claims. |
| 5 | Add start workflow controller | `IStartWorkflowService` | `WorkflowInstancesController.cs`, DTO | Controller tests | Start succeeds and duplicate returns conflict. |
| 6 | Add workflow context controller | `IWorkflowContextReader` | `WorkflowContextController.cs` | Controller tests | Context uses claims roles. |
| 7 | Add execute workflow action controller | `IWorkflowExecutionService`, validator, normalizer, hasher | `WorkflowActionsController.cs` | Controller/integration tests | SubmitForChecker succeeds through API. |
| 8 | Add workflow result-to-HTTP mapper | result builder/tests | `WorkflowHttpResultMapper.cs` | Mapper tests | Result codes map consistently. |
| 9 | Add admin/inspection endpoints if needed | `IWorkflowInspectionReader`, seed/SLA/outbox services | Inspection/admin controllers | Authorization tests | Admin-only endpoints work or are intentionally omitted. |
| 10 | Add SLA/outbox processing | `IRunSlaService`, `IProcessNotificationOutboxService` | Hosted services or jobs | Hosted service tests | Background processing path chosen and verified. |
| 11 | Add migration/seed verification path | `WorkflowDatabaseInitializer`, `SeedVerificationService` | Deployment scripts/admin ops | SQL tests | Migrations applied and seed verifies. |
| 12 | Add API tests | `tests/WorkflowEngine.Tests` as behavior reference | `<TestProjectName>` | Full API tests | Required API cases pass. |
| 13 | Run full verification | README/PROJECT commands | CI/pipeline | Build/test/seed/API smoke | Current repo and API solution verified. |
| 14 | Cutover and cleanup | Docs and operational runbooks | API deployment/runbook | Smoke and rollback checks | API ready for controlled rollout. |

## Milestone 1: Prepare API Project And References

Objective: Reference reusable workflow projects from the API.

Source files to use:

- `src/WorkflowEngine.Application/WorkflowEngine.Application.csproj`
- `src/WorkflowEngine.Infrastructure/WorkflowEngine.Infrastructure.csproj`
- `src/WorkflowEngine.Domain/WorkflowEngine.Domain.csproj`
- `Directory.Build.props`

Target API files:

- `<ApiProjectPath>/<ApiProjectName>.csproj`
- `<ApiSolutionPath>`

Actions:

1. Confirm API target framework is `net10.0`.
2. Add project references to Application, Infrastructure, and Domain.
3. Do not add a reference to `WorkflowEngine.Console`.
4. Confirm package versions are compatible with EF Core 10 and MailKit 4.16.0.
5. Run restore/build.

Code-level notes:

- Adjust relative paths.
- Do not copy `bin` or `obj`.

Decisions to verify:

- Project references versus source copy.
- Migration assembly ownership.

Tests to add:

- Basic API solution build.

Verification:

```bash
dotnet restore <ApiSolutionPath>
dotnet build <ApiSolutionPath> -warnaserror
```

Rollback:

- Remove project references and restore prior `.csproj`.

Completion criteria:

- API builds with workflow projects referenced.

## Milestone 2: Add Workflow Configuration

Objective: Move required workflow configuration into API configuration.

Source files:

- `src/WorkflowEngine.Console/appsettings.json`
- `src/WorkflowEngine.Console/appsettings.Development.json`
- `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs`

Target files:

- API `appsettings.json`
- API `appsettings.Development.json`
- deployment environment/secrets

Actions:

1. Add `ConnectionStrings:DefaultConnection`.
2. Add `WorkflowEngine` section.
3. Add `Notifications:Sender`.
4. Add SMTP settings if using SMTP.
5. Use secrets/environment variables for production values.

Decisions:

- `NoOp`, `Console`, or `Smtp` per environment.
- SMTP role-email mapping source.

Tests:

- Missing connection string startup failure.
- Unknown sender startup failure.
- SMTP required setting failures.

Verification:

```bash
dotnet build <ApiSolutionPath> -warnaserror
```

Rollback:

- Remove workflow config sections and project references.

Completion:

- API has required settings and safe non-production sender.

## Milestone 3: Register Workflow Services In Program.cs

Objective: Register reusable services and API-only services.

Source files:

- `src/WorkflowEngine.Console/Program.cs`
- `src/WorkflowEngine.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

Target files:

- API `Program.cs`

Actions:

1. Register `TimeProvider.System`.
2. Register validator, normalizer, hasher, result builder, result serializer.
3. Call `AddWorkflowInfrastructure(builder.Configuration)`.
4. Register `IWorkflowActorResolver`.
5. Register `WorkflowHttpResultMapper`.
6. Do not register console command handlers.

Tests:

- DI can resolve `IWorkflowExecutionService`.
- DI can resolve `IWorkflowContextReader`.
- DI can resolve `IStartWorkflowService`.

Verification:

```bash
dotnet build <ApiSolutionPath> -warnaserror
```

Rollback:

- Remove registration block.

Completion:

- API starts with valid workflow configuration.

## Milestone 4: Add Authenticated Actor Resolver

Objective: Derive workflow actor from authenticated API user.

Source files:

- `src/WorkflowEngine.Application/Contracts/WorkflowActionEnvelope.cs`
- `src/WorkflowEngine.Application/Context/IWorkflowContextReader.cs`

Target files:

- `<ServiceFolder>/IWorkflowActorResolver.cs`
- `<ServiceFolder>/ClaimsWorkflowActorResolver.cs`

Actions:

1. Add `WorkflowActorSnapshot`.
2. Add `IWorkflowActorResolver`.
3. Implement claims-to-role mapping.
4. Register in DI.
5. Decide behavior when roles are missing.

Decisions:

- Claim names for user ID, display name, roles/groups.
- Mapping to `Maker`, `Checker`, `TradingDesk`, `BDRMClientService`.

Tests:

- Claims map correctly.
- Body actor is overwritten.
- Missing auth fails.

Verification:

```bash
dotnet test <ApiSolutionPath>
```

Rollback:

- Remove resolver and endpoint usage.

Completion:

- Actor data is trusted and server-stamped.

## Milestone 5: Add Start Workflow Controller

Objective: Expose workflow start without console reset behavior.

Source files:

- `src/WorkflowEngine.Application/WorkflowStart/IStartWorkflowService.cs`
- `src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs`

Target files:

- `<ControllerFolder>/WorkflowInstancesController.cs`
- `<DtoFolder>/StartWorkflowApiRequest.cs`

Actions:

1. Add `StartWorkflowApiRequest`.
2. Add `POST /api/workflows/{workflowKey}/instances`.
3. Resolve actor from claims.
4. Call `IStartWorkflowService.Start`.
5. Pass `Reset=false`.
6. Map `Success` and `AlreadyExists`.

Tests:

- Valid start workflow.
- Duplicate start workflow.
- Reset disallowed for normal user.

Completion:

- API can start `PortfolioTrade` workflow and returns existing instance conflict.

## Milestone 6: Add Workflow Context Controller

Objective: Expose form-open workflow context.

Source files:

- `src/WorkflowEngine.Application/Context/IWorkflowContextReader.cs`
- `src/WorkflowEngine.Infrastructure/WorkflowContext/WorkflowContextReader.cs`
- `src/WorkflowEngine.Application/Contracts/WorkflowContextResponse.cs`

Target files:

- `<ControllerFolder>/WorkflowContextController.cs`

Actions:

1. Add `GET /api/workflows/instances/{workflowInstanceId}/context`.
2. Resolve actor from claims.
3. Call `IWorkflowContextReader.Read`.
4. Return `WorkflowContextResponse`.
5. Map missing instance to `404`.

Tests:

- Valid context request.
- Context roles from claims.
- Wrong role sees no available actions.

Completion:

- API returns metadata-derived available actions.

## Milestone 7: Add Execute Workflow Action Controller

Objective: Execute a stamped workflow action envelope.

Source files:

- `WorkflowActionEnvelope`
- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `IWorkflowExecutionService`

Target files:

- `<ControllerFolder>/WorkflowActionsController.cs`

Actions:

1. Add `POST /api/workflows/instances/{workflowInstanceId}/actions`.
2. Bind `WorkflowActionEnvelope` or API wrapper.
3. Validate route/body `workflowInstanceId`.
4. Stamp actor from resolver.
5. Normalize.
6. Validate.
7. Hash.
8. Execute with `AdminOverride=false`.
9. Map result.

Tests:

- Execute valid `SubmitForChecker`.
- Body roles overridden by claims.
- Stale expected version returns `409`.
- Invalid transition returns `409`.
- Forbidden action returns `403`.
- Idempotent replay returns stored result.
- Idempotency conflict returns `409`.
- Route/body mismatch returns `400`.

Completion:

- API can execute the sample action path without console command handlers.

## Milestone 8: Add Workflow Result-To-HTTP Mapper

Objective: Centralize HTTP status code mapping.

Source files:

- `src/WorkflowEngine.Application/Results/WorkflowActionResultBuilder.cs`
- `tests/WorkflowEngine.Tests/WorkflowActionResultBuilderTests.cs`

Target files:

- `<ServiceFolder>/WorkflowHttpResultMapper.cs`

Actions:

1. Implement mapper table from document 11.
2. Use mapper in start and execute controllers.
3. Keep `WorkflowActionResultEnvelope` body for workflow outcomes.

Tests:

- All result-code mappings.

Completion:

- Controllers do not duplicate result mapping logic.

## Milestone 9: Add Admin/Inspection Endpoints If Needed

Objective: Expose operational read models and admin actions only if allowed.

Source files:

- `IWorkflowInspectionReader`
- `WorkflowInspectionReader`
- `ISeedVerificationService`

Target files:

- `<ControllerFolder>/WorkflowInspectionController.cs`
- `<ControllerFolder>/WorkflowAdminController.cs`

Actions:

1. Decide whether endpoints are allowed.
2. Add admin authorization policy.
3. Add inspection summary/tasks/history/notifications endpoints if useful.
4. Add seed verification endpoint only if operations approves.
5. Do not expose EF entities.

Tests:

- Admin can access.
- Normal user gets `403`.
- Missing workflow returns `404`.

Completion:

- Admin surface is explicit, limited, and authorized.

## Milestone 10: Add SLA And Notification Outbox Processing

Objective: Choose and implement background processing path.

Source files:

- `IRunSlaService`
- `IProcessNotificationOutboxService`
- `INotificationSender`

Target files:

- `<HostedServiceFolder>/WorkflowSlaHostedService.cs`
- `<HostedServiceFolder>/NotificationOutboxHostedService.cs`
- or external job project/scripts

Actions:

1. Choose hosted service, external job, or admin-only operation.
2. If hosted service, use `IServiceScopeFactory`.
3. Make intervals configurable.
4. Log failures.
5. Use `NoOp` in tests and `Smtp` only with valid config.

Tests:

- SLA scanner queues overdue notification once.
- Notification outbox processor uses scoped service.
- Sender failure is handled.

Completion:

- SLA/outbox processing runs outside normal user request paths.

## Milestone 11: Add Migration/Seed Verification Path

Objective: Apply schema and seed metadata safely.

Source files:

- `WorkflowDbContext`
- `WorkflowDatabaseInitializer`
- `WorkflowSeedService`
- `SeedVerificationService`
- migrations folder

Target files:

- Deployment scripts
- CI pipeline
- optional admin operation

Actions:

1. Decide migration assembly ownership.
2. Apply migrations to development SQL database.
3. Run seed.
4. Run seed verification.
5. Add deployment check.

Tests:

- Seed verification passes.
- Schema constraints exist.

Completion:

- API database has workflow schema and valid Portfolio Trade metadata.

## Milestone 12: Add API Tests

Objective: Prove API-specific behavior.

Source files:

- `tests/WorkflowEngine.Tests`
- `samples/workflow-actions`

Target files:

- `<TestProjectName>`

Actions:

1. Add controller/unit tests.
2. Add integration tests with test auth.
3. Add SQL-backed tests for core API flows.
4. Add actor resolver tests.
5. Add result mapper tests.
6. Add hosted service tests if applicable.

Completion:

- Tests listed in document 14 are implemented or explicitly deferred.

## Milestone 13: Run Full Verification

Objective: Verify both current repo and external API.

Current repo:

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
dotnet run --project src/WorkflowEngine.Console -- init-db
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

External API:

```bash
dotnet restore <ApiSolutionPath>
dotnet build <ApiSolutionPath> -warnaserror
dotnet test <ApiSolutionPath>
```

Completion:

- Build, tests, migration, seed verification, and HTTP smoke tests pass.

## Milestone 14: Cutover And Cleanup

Objective: Move controlled traffic to API workflow endpoints.

Actions:

1. Confirm rollback plan.
2. Confirm feature flags or route rollout strategy.
3. Confirm logs and dashboards.
4. Confirm admin operations access.
5. Confirm no public reset/admin override.
6. Confirm no raw payload logging.
7. Update API docs/runbook.

Completion:

- API workflow integration is operationally ready for controlled rollout, not merely compiling.

