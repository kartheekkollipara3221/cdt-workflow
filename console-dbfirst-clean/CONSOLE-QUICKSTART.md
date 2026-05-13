# Console DB-First Quickstart

This folder is a clean copy of the console workflow POC only. It excludes the API project and keeps the database-first SQL scripts next to the console code.

## What to Use

- Console app: `src/WorkflowEngine.Console`
- Engine/application code: `src/WorkflowEngine.Application`, `src/WorkflowEngine.Domain`, `src/WorkflowEngine.Infrastructure`
- SQL database-first scripts: `db-first/sql`
- Stored procedures: `db-first/sql/002-stored-procedures.sql`
- Sample workflow JSON: `samples/workflow-actions`
- Workflow tests: `tests/WorkflowEngine.Tests`

## Database

The local development connection string is already set in `src/WorkflowEngine.Console/appsettings.Development.json`:

```text
Server=DOLLY;Database=burrito_codefirst;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Override it without editing files when needed:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=DOLLY;Database=burrito_codefirst;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
```

## First Commands

Run these from this folder:

```powershell
dotnet build .\WorkflowEngine.ConsoleDbFirst.slnx -warnaserror
dotnet run --project .\src\WorkflowEngine.Console -- init-db
dotnet run --project .\src\WorkflowEngine.Console -- verify-seed
dotnet run --project .\src\WorkflowEngine.Console -- demo
```

## Run a Button JSON

```powershell
dotnet run --project .\src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset
dotnet run --project .\src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project .\src\WorkflowEngine.Console -- execute-json .\samples\workflow-actions\03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002
```

## Tests

```powershell
dotnet test .\WorkflowEngine.ConsoleDbFirst.slnx --filter "Category!=FreshClone&Category!=Performance&Category!=Smtp"
```

Use the original repo root only when you need the API project. For console workflow work, this folder is the focused entry point.
