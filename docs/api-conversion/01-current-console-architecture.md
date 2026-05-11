# Current Console Architecture

This document explains the current CDT Workflow Engine repository as it exists now. It separates console-only runtime code from reusable workflow logic, infrastructure/database logic, and test/demo/sample assets.

## Repository Structure

Actual repository map:

```text
C:\projects\cdt-workflow
|-- PROJECT.md
|-- README.md
|-- AGENTS.MD
|-- Directory.Build.props
|-- dotnet-tools.json
|-- ARCHITECTURE_REVIEW.md
|-- REVIEW_REMEDIATION_SUMMARY.md
|-- FULL_PROJECT_REVIEW.md
|-- ARCHITECTURE_DEFECT_01_CONCURRENCY.md
|-- ARCHITECTURE_DEFECT_02_IDEMPOTENCY.md
|-- ARCHITECTURE_DEFECT_03_SMTP_STARTUP_VALIDATION.md
|-- ARCHITECTURE_DEFECT_04_SMTP_TEST_LIFECYCLE.md
|-- ARCHITECTURE_DEFECT_05_SQL_CONNECTION_CONFIGURATION.md
|-- docs
|   `-- console-to-api-integration-analysis.html
|-- samples
|   `-- workflow-actions
|       |-- 01-save-draft.json
|       |-- 02-submit-for-checker.json
|       |-- 03-checker-return-to-maker.json
|       |-- 04-maker-resubmit-for-checker.json
|       |-- 05-checker-send-to-trading-desk.json
|       |-- 06-trading-return-to-maker.json
|       |-- 07-trading-trade-executed.json
|       |-- 08-bdrm-trade-fulfilled.json
|       |-- 09-bdrm-trade-not-executed-properly.json
|       `-- 10-trading-rework-trade-executed.json
|-- src
|   |-- WorkflowEngine.sln
|   |-- WorkflowEngine.Console
|   |-- WorkflowEngine.Application
|   |-- WorkflowEngine.Infrastructure
|   `-- WorkflowEngine.Domain
`-- tests
    |-- COVERAGE_AUDIT.md
    `-- WorkflowEngine.Tests
```

## Project Type And Target Framework

The solution is a console application plus class libraries. It is not an ASP.NET Core API project. A source search found no `Microsoft.NET.Sdk.Web`, `ControllerBase`, `WebApplication`, `AddControllers`, or `MapControllers` in source projects.

The primary solution path is `src/WorkflowEngine.sln`.

All source and test projects target `net10.0`. `Directory.Build.props` sets:

- `LangVersion` = `latest`
- `Nullable` = `enable`
- `ImplicitUsings` = `enable`
- `TreatWarningsAsErrors` = `true`

## Main Entry Point

The console entry point is `src/WorkflowEngine.Console/Program.cs`.

It performs these composition-root steps:

1. Builds configuration from `appsettings.json`, `appsettings.Development.json`, environment variables, and command-line arguments.
2. Registers `IConfiguration`.
3. Registers `TimeProvider.System`.
4. Registers application helpers:
   - `WorkflowActionEnvelopeValidator`
   - `WorkflowActionEnvelopeNormalizer`
   - `CanonicalRequestHasher`
   - `WorkflowActionResultBuilder`
   - `CanonicalWorkflowResultSerializer`
5. Registers all console command handlers and `CommandRegistry`.
6. Calls `AddWorkflowInfrastructure(configuration)`.
7. Creates a scope, resolves `CommandRegistry`, and dispatches CLI arguments.
8. Writes success to `Console.Out` and failures to `Console.Error`.

## Main Execution Flow

```text
Program.cs
  |
  |-- ConfigurationBuilder
  |     |-- appsettings.json
  |     |-- appsettings.Development.json
  |     |-- environment variables
  |     `-- command-line args
  |
  |-- ServiceCollection
  |     |-- TimeProvider.System
  |     |-- validators/normalizers/hashers/result builders
  |     |-- console command handlers
  |     `-- AddWorkflowInfrastructure(configuration)
  |
  `-- CommandRegistry.Dispatch(args)
        |
        |-- init-db
        |     `-- IWorkflowDatabaseInitializer.Initialize
        |
        |-- verify-seed
        |     `-- ISeedVerificationService.Verify
        |
        |-- start-workflow
        |     `-- StartWorkflowCommandHandler
        |           `-- IStartWorkflowService.Start
        |
        |-- execute-json
        |     `-- ExecuteJsonCommandHandler
        |           |-- read JSON file
        |           |-- WorkflowActionEnvelopeNormalizer.Normalize
        |           |-- WorkflowActionEnvelopeValidator.Validate
        |           |-- CanonicalRequestHasher.ComputeHash
        |           `-- IWorkflowExecutionService.Execute
        |
        |-- show-context
        |     `-- ShowContextCommandHandler
        |           `-- IWorkflowContextReader.Read
        |
        |-- show / show-tasks / show-history / show-notifications
        |     `-- show command handlers
        |           `-- IWorkflowInspectionReader.Read
        |
        |-- run-sla
        |     `-- RunSlaCommandHandler
        |           `-- IRunSlaService.RunAsync
        |
        |-- process-notification-outbox
        |     `-- ProcessNotificationOutboxCommandHandler
        |           `-- IProcessNotificationOutboxService.ProcessAsync
        |
        `-- demo
              `-- DemoCommandHandler
                    `-- WorkflowEngineDemoStepRunner
```

## Console Command Layer Overview

Console commands live in `src/WorkflowEngine.Application/Commands`. This layer is an adapter around the reusable services.

| File / Module | Purpose | Key Classes / Methods | Inputs | Outputs | Dependencies | API Migration Role |
|---|---|---|---|---|---|---|
| `src/WorkflowEngine.Console/Program.cs` | Console composition root. | Top-level program. | CLI args and config files. | Exit code and console text. | `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, workflow services. | Behavior reference only. Replace with API `Program.cs`. |
| `CommandRegistry.cs` | Dispatches CLI command names. | `CommandRegistry.Dispatch`. | `IReadOnlyList<string>` args. | `CommandResult`. | Command handlers and database initializer/verifier. | Console-only. Do not expose as API runtime. |
| `CommandArgumentParser.cs` | Parses CLI flags. | `ParseStartWorkflow`, `ParseExecuteJson`, `ParseShowContext`, `ParseRunSla`. | CLI strings. | Parsed argument records. | None. | Console-only. Translate flags into routes/body/query/admin policy. |
| `ExecuteJsonCommandHandler.cs` | Reads JSON file and executes workflow. | `Execute`. | File path, overrides, `--admin-override`, `--format json`. | Readable text or JSON result envelope. | Validator, normalizer, hasher, result builder, `IWorkflowExecutionService`. | Use order as reference. API should bind JSON body, stamp actor, then call the same helpers/services. |
| `StartWorkflowCommandHandler.cs` | Starts workflow instance. | `Execute`. | `workflowKey`, `businessObjectId`, `--user`, `--reset`. | Readable text or `WorkflowActionResultEnvelope`. | `IStartWorkflowService`. | Use service directly. Do not expose reset to normal users. |
| `ShowContextCommandHandler.cs` | Returns form-open workflow context. | `Execute`. | `workflowInstanceId`, `--user`, `--roles`. | `WorkflowContextResponse` JSON. | `IWorkflowContextReader`. | Use context reader directly with authenticated actor. |
| `ShowCommandHandler.cs` | Prints inspection summary. | `Execute`. | `workflowInstanceId`. | Text summary. | `IWorkflowInspectionReader`. | Optional admin/inspection API behavior reference. |
| `ShowTasksCommandHandler.cs` | Prints task inspection rows. | `Execute`. | `workflowInstanceId`. | Text. | `IWorkflowInspectionReader`. | Optional admin/inspection API behavior reference. |
| `ShowHistoryCommandHandler.cs` | Prints audit rows. | `Execute`. | `workflowInstanceId`. | Text. | `IWorkflowInspectionReader`. | Optional admin/inspection API behavior reference. |
| `ShowNotificationsCommandHandler.cs` | Prints notification rows. | `Execute`. | `workflowInstanceId`. | Text. | `IWorkflowInspectionReader`. | Optional admin/inspection API behavior reference. |
| `RunSlaCommandHandler.cs` | CLI wrapper for SLA scan. | `RunAsync`. | Optional `--now`. | Text summary. | `IRunSlaService`, `TimeProvider`, `INotificationSender`. | Replace with hosted service, scheduled job, or admin-only endpoint. |
| `ProcessNotificationOutboxCommandHandler.cs` | CLI wrapper for outbox processing. | `RunAsync`. | No args. | Text summary or failure. | `IProcessNotificationOutboxService`, `INotificationSender`. | Replace with hosted service, scheduled job, or admin-only endpoint. |
| `DemoCommandHandler.cs` | Runs scripted demo. | `Execute`. | No args. | Demo transcript. | `IDemoStepRunner`, `IShowHistoryCommandHandler`. | Demo-only. Do not expose in production API. |

## Reusable Application Layer Overview

Reusable contracts and helper services live in `src/WorkflowEngine.Application`.

Important API-safe contracts:

- `WorkflowActionEnvelope`
- `WorkflowActionResultEnvelope`
- `WorkflowContextResponse`
- `AvailableWorkflowAction`
- `StartWorkflowRequest`
- `StartWorkflowResult`
- `WorkflowContextReadRequest`
- `WorkflowInspectionSnapshot`
- `RunSlaRequest`
- `RunSlaResult`
- `ProcessNotificationOutboxResult`
- `NotificationDelivery`

Important reusable helper services:

- `WorkflowActionEnvelopeValidator`
- `WorkflowActionEnvelopeNormalizer`
- `CanonicalRequestHasher`
- `WorkflowActionResultBuilder`
- `CanonicalWorkflowResultSerializer`

Important application interfaces:

- `IWorkflowExecutionService`
- `IStartWorkflowService`
- `IWorkflowContextReader`
- `IWorkflowInspectionReader`
- `IRunSlaService`
- `IProcessNotificationOutboxService`
- `INotificationSender`
- `IWorkflowDatabaseInitializer`
- `ISeedVerificationService`

## Infrastructure Layer Overview

Reusable infrastructure lives in `src/WorkflowEngine.Infrastructure`.

Key components:

- `WorkflowDbContext`
- EF Core migrations:
  - `20260507145558_InitialWorkflowSchema`
  - `20260510120000_HardenRuntimeConstraints`
- `WorkflowDbContextFactory`
- `InfrastructureServiceCollectionExtensions.AddWorkflowInfrastructure`
- `WorkflowExecutionService`
- `StartWorkflowService`
- `WorkflowContextReader`
- `WorkflowInspectionReader`
- `RunSlaService`
- `ProcessNotificationOutboxService`
- `ConsoleNotificationSender`
- `NoOpNotificationSender`
- `SmtpNotificationSender`
- `WorkflowDatabaseInitializer`
- `WorkflowSeedService`
- `SeedVerificationService`
- `PortfolioTradeSeedData`

`AddWorkflowInfrastructure(configuration)` requires `ConnectionStrings:DefaultConnection`. It validates `Notifications:Sender`, registers `WorkflowDbContext` with SQL Server, registers scoped infrastructure services, and registers a singleton `INotificationSender`.

## Domain Layer Overview

The current domain project is small. It contains `WorkflowKeys.DefaultConnectionName = "DefaultConnection"` in `src/WorkflowEngine.Domain/Workflow/WorkflowKeys.cs`.

Reference it rather than duplicating the connection string key.

## Test Project Overview

The test project is `tests/WorkflowEngine.Tests`. It targets `net10.0` and references Application, Infrastructure, and Domain.

Important test areas:

- Command dispatch and CLI parsing behavior.
- Envelope validation, serialization, normalization, hashing.
- Workflow transitions and result envelopes.
- SQL-backed seed verification and schema constraints.
- Start workflow duplicate handling and reset behavior.
- Context reader behavior.
- SLA scanning.
- Notification outbox claim-before-send behavior.
- SMTP sender unit tests and opt-in SMTP integration tests.
- Sample envelope expectations.
- Branch scripts and demo command behavior.

`tests/WorkflowEngine.Tests/xunit.runner.json` excludes `Category=Smtp`, `Category=FreshClone`, and `Category=Performance` by default.

## Sample JSON Overview

The sample envelope files under `samples/workflow-actions` are API regression fixtures. They include console-only `testExpectations`.

| File | Action | Expected Old State | Expected New State | Notes |
|---|---|---|---|---|
| `01-save-draft.json` | `SaveDraft` | `Draft` | `Draft` | Saves Maker data and writes audit without moving workflow. |
| `02-submit-for-checker.json` | `SubmitForChecker` | `Draft` | `CheckerReview` | Creates `CheckerReviewTask` and queues `WorkflowTaskAssigned`. |
| `03-checker-return-to-maker.json` | `CheckerReturnToMaker` | `CheckerReview` | `ReturnedToMaker` | Closes Checker task, creates `MakerReworkTask`, queues returned notification. |
| `04-maker-resubmit-for-checker.json` | `ResubmitForChecker` | `ReturnedToMaker` | `CheckerReview` | Closes Maker rework task and creates Checker task. |
| `05-checker-send-to-trading-desk.json` | `CheckerSendToTradingDesk` | `CheckerReview` | `TradingDeskReview` | Creates Trading Desk review task. |
| `06-trading-return-to-maker.json` | `TradingReturnToMaker` | `TradingDeskReview` | `ReturnedToMaker` | Returns from Trading Desk to Maker. |
| `07-trading-trade-executed.json` | `TradingTradeExecuted` | `TradingDeskReview` | `BdrmClientServiceReview` | Creates BDRM review task. |
| `08-bdrm-trade-fulfilled.json` | `BdrmTradeFulfilled` | `BdrmClientServiceReview` | `Completed` | Completes the workflow. |
| `09-bdrm-trade-not-executed-properly.json` | `BdrmTradeNotExecutedProperly` | `BdrmClientServiceReview` | `TradingDeskRework` | Creates Trading Desk rework task. |
| `10-trading-rework-trade-executed.json` | `TradingTradeExecuted` | `TradingDeskRework` | `BdrmClientServiceReview` | Returns rework to BDRM review. |

## Current Console-Specific Assumptions

- Actor identity is supplied by `--user` and JSON `actor.userId`.
- Workflow roles are supplied by `--roles` and JSON `actor.roles`.
- `execute-json` reads files instead of HTTP request bodies.
- `start-workflow --reset` is allowed for demos.
- `execute-json --admin-override` is allowed for demos.
- Command output is text or JSON written to stdout/stderr.
- `ConsoleNotificationSender` writes delivery lines to the console.
- Demo scripts use sample files and override business object/workflow instance IDs.
- `MockSectionData` is POC storage for opaque section payloads.

In the API, replace these assumptions with authenticated actor resolution, model binding, API authorization policies, HTTP responses, structured logging, and production form-section persistence decisions.

