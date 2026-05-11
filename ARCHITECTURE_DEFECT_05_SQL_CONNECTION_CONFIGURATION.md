# Defect 05: SQL Connection Strings Are Hardcoded in Test and Design-Time Setup

Status: Fixed in working tree

Severity: Minor

Source review: `ARCHITECTURE_REVIEW.md`

## Summary

Runtime infrastructure reads SQL Server configuration from `ConnectionStrings:DefaultConnection`, but several test and design-time paths still hardcode local SQL Server connection strings.

This does not usually break workflow behavior at runtime, but it weakens portability and partially diverges from `PROJECT.md` section 4.1, which establishes `ConnectionStrings:DefaultConnection` as the configured source for SQL access.

## Real Scenario

A developer or CI agent checks out the project and configures the approved connection string through:

```text
ConnectionStrings__DefaultConnection
```

or through test configuration.

They expect SQL-backed tests and tooling to use that configured database.

Most runtime code does this correctly:

```text
configuration.GetConnectionString("DefaultConnection")
```

But some tests/design-time factories still point directly to machine-specific values such as:

```text
Server=DOLLY;Database=...
Server=(localdb)\MSSQLLocalDB;Database=...
```

On a different machine, those hardcoded values may not exist.

## Real-Time Impact

This is primarily a developer, CI, and release-verification problem:

- Tests may pass on the original developer machine and fail elsewhere.
- CI may need special machine-specific SQL aliases instead of normal configuration.
- A developer may set `ConnectionStrings__DefaultConnection` and still see tests use a different database.
- Test failures can be misleading: code may be fine, but the hardcoded SQL target is unavailable.
- Design-time EF tooling may point at an unexpected local database.
- The project drifts from the documented configuration model.

For this workflow engine, SQL-backed verification is important because the POC relies on real SQL Server behavior:

```text
EF Core migrations
SQL seed data
unique constraints
filtered pending-task index
idempotency uniqueness
runtime workflow state
notification outbox
audit history
```

If tests silently target hardcoded databases, verification becomes less reproducible.

## Code Evidence

Known examples from the architecture review and follow-up scan:

```text
src/WorkflowEngine.Infrastructure/Persistence/WorkflowDbContextFactory.cs
tests/WorkflowEngine.Tests/FreshCloneWalkthroughTests.cs
tests/WorkflowEngine.Tests/SchemaConstraintTests.cs
tests/WorkflowEngine.Tests/SeedDataSmokeTests.cs
tests/WorkflowEngine.Tests/StartWorkflowServiceTests.cs
tests/WorkflowEngine.Tests/WorkflowContextReaderTests.cs
tests/WorkflowEngine.Tests/WorkflowExecutionValidationTests.cs
```

Examples include direct `UseSqlServer(...)` calls with literal connection strings instead of consistently resolving:

```text
ConnectionStrings:DefaultConnection
```

## Required Fix

Centralize SQL connection string resolution for tests and design-time setup.

Preferred shape:

1. Create or extend a small test helper that resolves the SQL connection string in this order:
   - `ConnectionStrings__DefaultConnection`
   - test configuration value `ConnectionStrings:DefaultConnection`
   - documented local fallback only if the project intentionally keeps one
2. Replace direct hardcoded `UseSqlServer("...")` calls in tests with the helper.
3. Make design-time `WorkflowDbContextFactory` use configuration/environment where practical.
4. Keep runtime infrastructure behavior unchanged if it already uses `ConnectionStrings:DefaultConnection`.

The intent is not to remove every useful local default. The intent is to make configured connection strings authoritative and avoid machine-specific hardcoding scattered through tests.

## Important Constraints

- Do not switch SQL-backed tests to EF Core InMemory.
- Do not remove SQL Server verification of migrations, constraints, seed data, or persistence behavior.
- Do not commit passwords, tokens, or developer secrets.
- Do not introduce new third-party configuration packages unless already present.
- Do not change schema or workflow behavior as part of this fix.
- Do not make tests silently hit production databases.
- Keep the documented local integrated-security connection string acceptable as a fallback only if that is an intentional project decision.

## Acceptance Criteria

Add or update tests/helpers proving:

- Tests can resolve SQL from `ConnectionStrings__DefaultConnection`.
- Existing SQL-backed tests use the centralized resolver/helper instead of repeated literals.
- Runtime infrastructure still reads `ConnectionStrings:DefaultConnection`.
- Design-time factory no longer depends only on a hardcoded machine-specific SQL target, or clearly documents its fallback behavior.
- Default test behavior remains SQL-backed.
- No EF Core InMemory replacement is introduced.

## Suggested Verification Commands

Run the default test gate:

```bash
dotnet test src/WorkflowEngine.sln
```

Run with an explicit configured connection string:

```text
ConnectionStrings__DefaultConnection=Server=...;Database=...;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Then:

```bash
dotnet test src/WorkflowEngine.sln
```

The exact environment-variable syntax depends on shell:

PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=...;Database=...;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
dotnet test src/WorkflowEngine.sln
```
