# Database Migrations, Seeding, and Verification

The workflow engine uses SQL Server through EF Core. The current workflow schema, migrations, seed data, runtime state, SLA, notification outbox, audit, idempotency, and mock section storage are all in `WorkflowEngine.Infrastructure`.

## Database Component Table

| Database Component | Current Role | API Integration Recommendation | Risk |
|---|---|---|---|
| `WorkflowDbContext` | EF Core DbContext for workflow metadata and runtime tables. | Register via `AddWorkflowInfrastructure`. | DbContext lifetime misuse if injected into singletons. |
| `Migrations/20260507145558_InitialWorkflowSchema` | Initial workflow schema. | Keep in `WorkflowEngine.Infrastructure` by default. | Migration history conflict if moved carelessly. |
| `Migrations/20260510120000_HardenRuntimeConstraints` | Adds runtime hardening constraints/columns. | Keep and apply. | Missing constraints can reintroduce fixed defects. |
| `WorkflowDbContextFactory` | EF design-time factory. | Use with `ConnectionStrings__DefaultConnection` or API startup project. | Fallback points at `Server=DOLLY`; avoid relying on it. |
| `WorkflowDatabaseInitializer` | Applies migrations and seed. | Deployment/admin operation only. | Public init endpoint can mutate production schema/data. |
| `WorkflowSeedService` | Upserts Portfolio Trade seed metadata. | Use via initializer. | Hardcoding seed data into API controllers would break reuse. |
| `SeedVerificationService` | Verifies Portfolio Trade metadata and constraints. | Deployment check or admin-only endpoint. | Skipping it can hide broken metadata. |
| `PortfolioTradeSeedData` | Internal seed matrix. | Keep internal; use through seed service. | Treating it as API business logic hardcodes sample workflow. |
| `MockSectionData` | Append-only POC section payload storage. | Keep only if accepted as demo storage; otherwise plan replacement. | Production form data may need a real persistence model. |
| `NotificationOutbox` | Pending/processing/sent notification rows. | Keep. Process by hosted service/job/admin op. | Duplicate sends if processor is bypassed. |
| `IdempotencyRecord` | Stores replayable action results. | Keep. | Idempotency breaks if API bypasses execution service. |

## Keep Workflow Migrations In Infrastructure

Recommended default: keep workflow migrations in `WorkflowEngine.Infrastructure`.

Reasons:

- The current migrations match `WorkflowDbContext`.
- Tests validate schema constraints.
- The workflow schema is reusable and separate from the external API's domain schema.
- It avoids mixing workflow metadata/runtime tables into an unrelated API DbContext.

## Applying Migrations From The API Solution

If using EF tooling from the external API solution:

```bash
dotnet ef database update --project path/to/WorkflowEngine.Infrastructure --startup-project <ApiProjectPath>
```

If using the existing console tool during migration:

```bash
dotnet run --project src/WorkflowEngine.Console -- init-db
```

If `dotnet ef` is unavailable:

```bash
dotnet tool restore
dotnet ef database update --project src/WorkflowEngine.Infrastructure --startup-project src/WorkflowEngine.Console
```

Adjust paths for the external API.

## Avoid Migration History Conflicts

Before applying migrations in a database already used by the API:

1. Confirm whether the API already uses EF Core migrations.
2. Confirm whether it uses the same `__EFMigrationsHistory` table.
3. Confirm workflow migrations have not already been applied manually.
4. Decide whether workflow migrations remain in `WorkflowEngine.Infrastructure`.
5. Document the migration assembly decision.

Do not copy migrations into another project and rename them without understanding EF migration history.

## Seed Portfolio Trade Metadata

Current seed service upserts:

- Workflow definition `PortfolioTrade`.
- Version `1`.
- States: `Draft`, `ReturnedToMaker`, `CheckerReview`, `TradingDeskReview`, `TradingDeskRework`, `BdrmClientServiceReview`, `Completed`.
- Roles: `Maker`, `Checker`, `TradingDesk`, `BDRMClientService`.
- Actions: `SaveDraft`, `SubmitForChecker`, `ResubmitForChecker`, `CheckerReturnToMaker`, `CheckerSendToTradingDesk`, `TradingReturnToMaker`, `TradingTradeExecuted`, `BdrmTradeFulfilled`, `BdrmTradeNotExecutedProperly`.
- SLA policies.
- Task definitions.
- Notification routes.
- Eleven transition rows.

Use `WorkflowDatabaseInitializer.Initialize()` or deployment scripts to apply seed.

## Seed Verification

Current command:

```bash
dotnet run --project src/WorkflowEngine.Console -- verify-seed
```

The service behind it is `ISeedVerificationService.Verify()`.

Seed verification checks:

- Required states, roles, actions, tasks.
- No `AdminDemoOverride` role/transition.
- Action presentation hints.
- Task role/SLA references.
- `MakerReworkTask` has no SLA.
- Notification routes.
- Transition matrix and references.
- No duplicate transitions.

## Should `init-db` Become An Endpoint?

Recommended: no.

Migrations and seed should run in deployment, migration tooling, or a restricted operations process. Public schema/data mutation endpoints are risky.

If the API team requires an endpoint:

- Put it under admin-only route.
- Gate by environment.
- Require explicit deployment/operator policy.
- Log every call.
- Never call it from normal user workflows.

## Deployment-Time Database Setup

Recommended deployment sequence:

1. Deploy binaries/config.
2. Apply workflow migrations.
3. Seed Portfolio Trade metadata.
4. Run seed verification.
5. Run API startup smoke tests.
6. Enable traffic.

## Schema Constraints To Preserve

Do not lose:

- Single pending task filtered unique index.
- SLA overdue notification uniqueness.
- Runtime status check constraints.
- Runtime foreign keys to workflow instance.
- Unique definition/version/action/role/task/transition keys.
- Unique idempotency key.
- Unique workflow lifetime per business object.

Existing tests:

- `SchemaConstraintTests`
- `SeedDataSmokeTests`

## Test Databases

Current tests require SQL Server and use:

- `WORKFLOW_ENGINE_TEST_CONNECTION_STRING`
- `ConnectionStrings__DefaultConnection`

They do not use EF Core InMemory for persistence verification. Keep this rule for API integration tests.

## Recommended Commands

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

EF tooling with placeholders:

```bash
dotnet ef database update --project <PathToWorkflowEngine.Infrastructure.csproj> --startup-project <ApiProjectPath>
```

