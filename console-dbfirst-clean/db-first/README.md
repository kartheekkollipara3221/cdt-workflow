# Database-First Artifacts

This folder is the database-first source for the SQL Server schema and stored procedures used by the workflow engine.

Runtime workflow services use stored-procedure access through `DatabaseFirstWorkflowStore`. `init-db` no longer creates the schema by applying EF migrations. It creates the target database when missing, executes `sql/001-schema.sql`, executes `sql/002-stored-procedures.sql`, then applies the Portfolio Trade seed metadata.

Stored procedure artifacts:

- `sql/002-stored-procedures.sql` contains generic workflow get/insert/update procedures for metadata, workflow instances, tasks, section data, audit rows, notification outbox rows, idempotency records, workflow context, and demo runtime reset.
- The procedures are database artifacts and are applied to SQL Server by `init-db`; workflow runtime services call them through the database-first store. EF Core remains for the existing schema model, seed, verification, and test setup compatibility.

Local SQL target:

```text
Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Typical local run:

```powershell
dotnet run --project src\WorkflowEngine.Console -- init-db
dotnet run --project src\WorkflowEngine.Console -- verify-seed
dotnet run --project src\WorkflowEngine.Console -- demo
```

To refresh generated database-first C# reference files after a DBA schema change, first apply the SQL change to `burrito`, then run:

```powershell
dotnet tool restore
.\db-first\scaffold\scaffold-from-burrito.ps1
```

The scaffold output is a reference artifact under `db-first/scaffold/generated`; the production runtime model remains in `src/WorkflowEngine.Infrastructure` so existing application contracts, tests, and service behavior stay stable.
