# Project Reference and Solution Integration

The preferred migration path is to reference the existing reusable workflow projects from the external API solution instead of copying individual source files.

## Preferred Approach

Reference these projects:

- `src/WorkflowEngine.Application/WorkflowEngine.Application.csproj`
- `src/WorkflowEngine.Infrastructure/WorkflowEngine.Infrastructure.csproj`
- `src/WorkflowEngine.Domain/WorkflowEngine.Domain.csproj`

This preserves the tested boundaries and avoids source drift between the console POC and the API integration.

## Should The API Reference `WorkflowEngine.Console`?

Normally, no.

`WorkflowEngine.Console` is a CLI composition root. It contains `Program.cs`, console configuration loading, console command registration, `CommandRegistry.Dispatch`, and stdout/stderr behavior. A controller-style API should replace that with API `Program.cs`, controllers, model binding, authenticated actor resolution, and HTTP responses.

The console project can remain a separate operational/demo tool for `init-db`, `verify-seed`, sample execution, and demos during migration.

## Source Project Reference Table

| Source Project | Reference In API? | Reason | Caution |
|---|---|---|---|
| `WorkflowEngine.Application` | Yes | Contains contracts, helper services, service interfaces, command wrappers, and demo support. | Use contracts/helpers/interfaces. Do not expose command handlers as API runtime unless clearly console-only/admin reference. |
| `WorkflowEngine.Infrastructure` | Yes | Contains EF Core model, migrations, seed services, execution services, context reader, SLA, outbox, notification senders. | Requires `ConnectionStrings:DefaultConnection`; registers SQL Server and notification sender. |
| `WorkflowEngine.Domain` | Yes | Contains `WorkflowKeys.DefaultConnectionName`. | Small but should remain referenced because Application and Infrastructure depend on it. |
| `WorkflowEngine.Console` | Usually no | CLI tool only. | Do not copy `Program.cs`, `CommandRegistry`, or command-line parsing into the API. |
| `WorkflowEngine.Tests` | No runtime reference | Existing verification suite. | Use behavior as regression coverage inspiration for API tests. |

## Example `.csproj` Project References

Adjust the relative paths from `<ApiProjectPath>` to this repository's source projects.

```xml
<ItemGroup>
  <ProjectReference Include="..\path\to\cdt-workflow\src\WorkflowEngine.Application\WorkflowEngine.Application.csproj" />
  <ProjectReference Include="..\path\to\cdt-workflow\src\WorkflowEngine.Infrastructure\WorkflowEngine.Infrastructure.csproj" />
  <ProjectReference Include="..\path\to\cdt-workflow\src\WorkflowEngine.Domain\WorkflowEngine.Domain.csproj" />
</ItemGroup>
```

If the external API solution is moved into this repository later, prefer relative paths from the API project to:

```xml
<ItemGroup>
  <ProjectReference Include="..\WorkflowEngine.Application\WorkflowEngine.Application.csproj" />
  <ProjectReference Include="..\WorkflowEngine.Infrastructure\WorkflowEngine.Infrastructure.csproj" />
  <ProjectReference Include="..\WorkflowEngine.Domain\WorkflowEngine.Domain.csproj" />
</ItemGroup>
```

The exact paths must be adjusted because `<ApiProjectPath>` is unavailable here.

## Package Compatibility

Current package dependencies:

- `WorkflowEngine.Infrastructure` references `Microsoft.EntityFrameworkCore.SqlServer` version `10.0.7`.
- `WorkflowEngine.Infrastructure` references `Microsoft.Extensions.Configuration.Abstractions` version `10.0.7`.
- `WorkflowEngine.Infrastructure` references `MailKit` version `4.16.0`.
- `WorkflowEngine.Console` references `Microsoft.EntityFrameworkCore.Design` version `10.0.7`, but the API should not rely on the console project for runtime.
- `dotnet-tools.json` pins `dotnet-ef` version `10.0.7`.

The target API should use compatible .NET 10 and EF Core 10 package versions. Avoid adding new third-party packages unless explicitly approved. MailKit is already approved by `PROJECT.md`.

## Target Framework Compatibility

All current projects target `net10.0`. The external API must either:

- Target `net10.0`; or
- Be upgraded to target `net10.0` before referencing these projects.

Do not multi-target or downgrade the workflow projects without a deliberate compatibility decision.

## Directory.Build.props

The repository-level `Directory.Build.props` applies to projects under this repo and treats warnings as errors. If you reference these projects from an external solution without moving them, this file will still affect the workflow projects. Confirm the external API project has equivalent or compatible warning policy.

If you copy project directories into the API repo, also copy or recreate the relevant warning/nullable settings deliberately. Do not accidentally disable warnings-as-errors for workflow code.

## Avoid Copying Generated Files

Do not copy:

- `bin/`
- `obj/`
- `.vs/`
- test result output
- generated build artifacts

Reference source projects or copy source files only if project references are impossible.

## EF Core Design-Time Tooling

Current design-time factory:

- `src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContextFactory.cs`

It resolves `ConnectionStrings__DefaultConnection` first and falls back to:

```text
Server=DOLLY;Database=WorkflowEngineDesignTime;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

For the external API, prefer one of these:

- Keep the workflow migrations in `WorkflowEngine.Infrastructure` and run EF tooling with `--project` pointing there and `--startup-project` pointing at the API.
- Add EF design-time support in the API project that configures `WorkflowDbContext` the same way as production.
- Set `ConnectionStrings__DefaultConnection` before running `dotnet ef`.

## Migration Assembly Ownership

Current migrations are in `WorkflowEngine.Infrastructure/Migrations`.

Recommended default: keep workflow migrations in `WorkflowEngine.Infrastructure`. This isolates the workflow schema and avoids mixing workflow migrations into unrelated API DbContexts.

Only move migration ownership if the API already has a strict single-migration-assembly policy. If moved, document the decision and verify `WorkflowDbContext` still maps the same table names, keys, indexes, filtered indexes, foreign keys, and check constraints.

## Verification Commands

Current repository commands:

```bash
dotnet restore src/WorkflowEngine.sln
dotnet build src/WorkflowEngine.sln -warnaserror
dotnet test src/WorkflowEngine.sln
```

External API commands:

```bash
dotnet restore <ApiSolutionPath>
dotnet build <ApiSolutionPath> -warnaserror
```

If package restore fails after adding project references, first check relative paths, target framework, and package version conflicts.

