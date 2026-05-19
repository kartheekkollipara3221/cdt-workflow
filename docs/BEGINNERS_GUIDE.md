# CDT Workflow App Beginner Setup Guide

This guide explains how to build and run the extracted CDT Workflow console app in:

```text
C:\CdtWorkflow\extracted
```

The extracted copy is configured to use this SQL Server database:

```text
Server=DOLLY;Database=burrito_unbundled;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

Do not point this extracted copy at the original `burrito_codefirst` database when following this guide.

## What this app is

This is a .NET 10 console proof of concept for a reusable workflow engine. It stores workflow metadata and runtime workflow state in SQL Server.

The sample workflow is `PortfolioTrade`. Portfolio Trade is sample seed data only. The reusable engine decides workflow movement from SQL metadata, not hardcoded Portfolio Trade code.

## Folder layout

Important folders and files:

```text
C:\CdtWorkflow\extracted
  BEGINNERS_GUIDE.md
  README.md
  PROJECT.md
  Directory.Build.props
  dotnet-tools.json
  samples\workflow-actions\*.json
  src\WorkflowEngine.sln
  src\WorkflowEngine.Console\appsettings.Development.json
  src\WorkflowEngine.Console\WorkflowEngine.Console.csproj
  tests\WorkflowEngine.Tests
```

The connection string is stored in:

```text
C:\CdtWorkflow\extracted\src\WorkflowEngine.Console\appsettings.Development.json
```

## Prerequisites

Install or confirm these first:

1. .NET 10 SDK.
2. SQL Server reachable as `DOLLY`.
3. Windows integrated security access that can create and update the `burrito_unbundled` database.
4. PowerShell.

Check .NET:

```powershell
dotnet --version
```

## Build the app

Open PowerShell and move to the extracted root:

```powershell
cd C:\CdtWorkflow\extracted
```

Restore packages:

```powershell
dotnet restore src\WorkflowEngine.sln
```

Build the solution:

```powershell
dotnet build src\WorkflowEngine.sln --no-restore
```

A successful build prints `Build succeeded` with `0 Warning(s)` and `0 Error(s)`.

## Initialize the database

Create or update the SQL schema and seed the Portfolio Trade workflow metadata:

```powershell
dotnet run --project src\WorkflowEngine.Console -- init-db
```

Expected result:

```text
Database initialized and Portfolio Trade seed data applied.
```

Verify the seed data:

```powershell
dotnet run --project src\WorkflowEngine.Console -- verify-seed
```

Expected result:

```text
Seed verification passed.
```

## Run the main workflow path

Start a fresh Portfolio Trade workflow for `PT-1001`:

```powershell
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
```

Save a draft:

```powershell
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/01-save-draft.json
```

Submit to Checker:

```powershell
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json
```

Show the workflow context for the Checker:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show-context WF-PT-1001 --user checker1 --roles Checker --format json
```

Show the workflow summary:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show WF-PT-1001
```

Show current and completed tasks:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-1001
```

Run the SLA monitor using a future time so the current task is overdue:

```powershell
dotnet run --project src\WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
```

Show queued notifications:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-1001
```

Process pending notifications with the default console sender:

```powershell
dotnet run --project src\WorkflowEngine.Console -- process-notification-outbox
```

Continue the happy path:

```powershell
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json
dotnet run --project src\WorkflowEngine.Console -- show-context WF-PT-1001 --user trader1 --roles TradingDesk --format json
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/07-trading-trade-executed.json
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/09-bdrm-trade-not-executed-properly.json
dotnet run --project src\WorkflowEngine.Console -- show-context WF-PT-1001 --user trader1 --roles TradingDesk --format json
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/10-trading-rework-trade-executed.json
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/08-bdrm-trade-fulfilled.json
```

Show the audit history:

```powershell
dotnet run --project src\WorkflowEngine.Console -- show-history WF-PT-1001
```

Expected final state for `WF-PT-1001`: `Completed`.

## Run the Checker return branch

This branch proves Checker can return work to Maker.

```powershell
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src\WorkflowEngine.Console -- show WF-PT-1002
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-1002
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-1002
```

Expected state after the return: `ReturnedToMaker` with a pending `MakerReworkTask`.

To prove Maker can resubmit after the return:

```powershell
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/04-maker-resubmit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-1002
```

Expected state after resubmit: `CheckerReview` with a pending `CheckerReviewTask`.

## Run the Trading Desk return branch

This branch proves Trading Desk can return work to Maker.

```powershell
dotnet run --project src\WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src\WorkflowEngine.Console -- execute-json ./samples/workflow-actions/06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src\WorkflowEngine.Console -- show WF-PT-1003
dotnet run --project src\WorkflowEngine.Console -- show-tasks WF-PT-1003
dotnet run --project src\WorkflowEngine.Console -- show-notifications WF-PT-1003
```

Expected state after the return: `ReturnedToMaker` with a pending `MakerReworkTask`.

## Run the built-in demo

The demo command runs the main happy path automatically using `PT-DEMO` and `WF-PT-DEMO`.

```powershell
dotnet run --project src\WorkflowEngine.Console -- demo
```

Expected result includes:

```text
Demo completed successfully.
```

## Run tests

The test suite needs a SQL connection source. Set it for the current PowerShell session:

```powershell
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING = "Server=DOLLY;Database=burrito_unbundled;Integrated Security=true;TrustServerCertificate=True;Encrypt=False"
```

Run the default test set:

```powershell
dotnet test src\WorkflowEngine.sln --no-build --filter "Category!=Smtp&Category!=FreshClone&Category!=Performance"
```

Expected result from this extracted copy:

```text
Passed!  - Failed: 0, Passed: 199, Skipped: 0, Total: 199
```

The SMTP, FreshClone, and Performance categories are opt-in and are not part of the default verification above.

## What each command does

- `init-db`: applies EF Core migrations and seeds Portfolio Trade workflow metadata.
- `verify-seed`: checks the required workflow metadata exists in SQL.
- `start-workflow`: creates a workflow instance for a business object.
- `execute-json`: processes one workflow action envelope from a sample JSON file.
- `show-context`: returns the form-open workflow context for a user and role.
- `show`: prints workflow instance summary.
- `show-tasks`: prints workflow tasks for one instance.
- `show-history`: prints audit history for one instance.
- `show-notifications`: prints notification outbox rows for one instance.
- `run-sla`: scans pending SLA-backed tasks and queues overdue notifications.
- `process-notification-outbox`: sends pending notifications through the configured sender.
- `demo`: runs the main demo path and validates expected workflow results.

## Troubleshooting

If `init-db` says the connection string is missing, confirm this file exists and contains `burrito_unbundled`:

```text
src\WorkflowEngine.Console\appsettings.Development.json
```

If SQL login fails, confirm your Windows account has access to SQL Server `DOLLY` and permission to create/update the `burrito_unbundled` database.

If sample JSON files cannot be found, confirm this folder exists:

```text
samples\workflow-actions
```

If tests fail with a missing connection string, set `WORKFLOW_ENGINE_TEST_CONNECTION_STRING` in the same PowerShell session before running `dotnet test`.

If `start-workflow` says a workflow already exists, either use a new business object id or rerun the command with `--reset` for demo purposes only.

## Verification completed for this extracted copy

The following checks were completed from `C:\CdtWorkflow\extracted`:

- `dotnet restore src\WorkflowEngine.sln`: passed.
- `dotnet build src\WorkflowEngine.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet run --project src\WorkflowEngine.Console -- init-db`: passed.
- `dotnet run --project src\WorkflowEngine.Console -- verify-seed`: passed.
- Every console command listed above was executed successfully.
- All ten sample JSON files in `samples\workflow-actions` were executed successfully across the main path and branch paths.
- `dotnet run --project src\WorkflowEngine.Console -- demo`: passed.
- Default regression tests passed: 199 passed, 0 failed.
