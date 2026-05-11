# CDT Workflow Engine API Conversion Index

This package is a manual conversion guide for moving the CDT Workflow Engine from the current console proof of concept into an external controller-style ASP.NET Core API.

The CDT Workflow Engine is a reusable SQL-backed .NET workflow engine intended to replace SharePoint Designer workflow behavior. Portfolio Trade is the first sample workflow definition, but the engine is metadata-driven and must remain reusable for other workflows.

## Current Console App Summary

The current repository is a completed console-style .NET 10 proof of concept. The primary solution is `src/WorkflowEngine.sln`, with these source projects:

- `src/WorkflowEngine.Console`
- `src/WorkflowEngine.Application`
- `src/WorkflowEngine.Infrastructure`
- `src/WorkflowEngine.Domain`

The test project is `tests/WorkflowEngine.Tests`. The reusable engine contracts and services live mainly in `WorkflowEngine.Application`, `WorkflowEngine.Infrastructure`, and `WorkflowEngine.Domain`. The CLI runtime lives in `WorkflowEngine.Console` and should not become the API runtime.

## Target API Goal

The goal is to manually integrate the reusable workflow engine into an existing controller-style ASP.NET Core API project. The API should expose workflow capabilities through controllers, model binding, dependency injection, authenticated actor resolution, and HTTP responses.

The target API project is not available in this repository. This package does not claim to inspect it and does not implement the migration.

## Inputs Used

These instructions are based on:

- The live repository at `C:\projects\cdt-workflow`.
- `PROJECT.md`, which remains the source of truth.
- `docs/console-to-api-integration-analysis.html`.
- `README.md`, `AGENTS.MD`, `ARCHITECTURE_REVIEW.md`, `REVIEW_REMEDIATION_SUMMARY.md`, `FULL_PROJECT_REVIEW.md`, and the defect remediation documents.
- The actual source projects, EF model, migrations, seed data, samples, and tests.

If a prior analysis point disagrees with the current source, prefer the current source.

## Recommended Migration Strategy

Reference the existing reusable projects from the external API solution:

- Reference `WorkflowEngine.Application`.
- Reference `WorkflowEngine.Infrastructure`.
- Reference `WorkflowEngine.Domain`.
- Do not reference `WorkflowEngine.Console` for normal API runtime.

Then add API-specific controllers, DTOs only where needed, an authenticated actor resolver, a result-to-HTTP mapper, configuration, and optional background services in the external API project.

## Milestone Documents

| Milestone | Document | Purpose | When To Use |
|---|---|---|---|
| 00 | `00-index.md` | Navigation and strategy overview. | Start here. |
| 01 | `01-current-console-architecture.md` | Explains the current console architecture and reusable boundaries. | Before deciding what to copy or reference. |
| 02 | `02-target-api-assumptions.md` | Lists API unknowns and placeholders. | Before editing the real API project. |
| 03 | `03-project-reference-and-solution-integration.md` | Shows project references, framework compatibility, EF tooling, and solution integration. | When wiring the API solution. |
| 04 | `04-business-logic-extraction.md` | Classifies source code as reusable, wrapper-worthy, console-only, or test/demo-only. | Before creating API services. |
| 05 | `05-controller-api-surface.md` | Designs controller-style endpoints. | When creating controllers. |
| 06 | `06-request-response-contracts.md` | Describes request and response DTO strategy. | When modeling API JSON. |
| 07 | `07-authenticated-actor-resolution.md` | Designs authenticated actor/role stamping. | Before exposing execute or context endpoints. |
| 08 | `08-dependency-injection-and-program-cs.md` | Shows API DI registration. | When editing API `Program.cs`. |
| 09 | `09-configuration-and-appsettings.md` | Migrates console configuration to API configuration. | When editing `appsettings*.json` and deployment settings. |
| 10 | `10-database-migrations-seeding-and-verification.md` | Covers EF migrations, seed data, and verification. | Before applying database changes. |
| 11 | `11-error-handling-status-codes-and-validation.md` | Maps workflow results to HTTP behavior. | When implementing result mapping and exception handling. |
| 12 | `12-logging-observability-and-sensitive-data.md` | Replaces console output with API logging guidance. | When adding observability. |
| 13 | `13-sla-and-notification-outbox-processing.md` | Integrates SLA scanning and outbox processing. | When adding hosted services, jobs, or admin operations. |
| 14 | `14-testing-strategy.md` | Maps existing tests and proposes API tests. | When building API regression coverage. |
| 15 | `15-manual-migration-milestones.md` | Step-by-step migration plan. | Main execution guide. |
| 16 | `16-verification-cutover-and-rollback.md` | Verification, smoke testing, cutover, and rollback plan. | Before cutover and after each milestone. |
| 17 | `17-risks-open-questions-and-decisions.md` | Risk register and decision log starter. | Throughout migration. |
| Summary | `api-conversion-summary.html` | Standalone executive summary and roadmap. | For stakeholders and migration review. |

## Recommended Reading Order

1. `00-index.md`
2. `01-current-console-architecture.md`
3. `02-target-api-assumptions.md`
4. `03-project-reference-and-solution-integration.md`
5. `04-business-logic-extraction.md`
6. `07-authenticated-actor-resolution.md`
7. `08-dependency-injection-and-program-cs.md`
8. `05-controller-api-surface.md`
9. `06-request-response-contracts.md`
10. `10-database-migrations-seeding-and-verification.md`
11. `11-error-handling-status-codes-and-validation.md`
12. `13-sla-and-notification-outbox-processing.md`
13. `14-testing-strategy.md`
14. `15-manual-migration-milestones.md`
15. `16-verification-cutover-and-rollback.md`
16. `17-risks-open-questions-and-decisions.md`

## What To Do First In The Real API Repo

1. Confirm the real API project path and solution path.
2. Confirm the API targets `net10.0` or can reference `net10.0` libraries.
3. Confirm it uses controllers with `ControllerBase`, `[ApiController]`, and `MapControllers`.
4. Identify the authentication and authorization model.
5. Identify the claims or enterprise authorization source for workflow roles.
6. Decide whether routes should be generic workflow routes or domain-nested Portfolio Trade routes.
7. Decide who owns EF migrations and database deployment.
8. Add project references to the reusable workflow projects.
9. Add actor resolver and DI registration before exposing execute endpoints.

## Do Not Do

- Do not copy `WorkflowEngine.Console` as the API runtime.
- Do not expose CLI commands as public endpoints.
- Do not trust request body actor roles.
- Do not expose reset/admin override publicly.
- Do not expose EF entities as API DTOs.
- Do not hardcode Portfolio Trade transitions in controllers.

## Files Generated

- `docs/api-conversion/00-index.md`
- `docs/api-conversion/01-current-console-architecture.md`
- `docs/api-conversion/02-target-api-assumptions.md`
- `docs/api-conversion/03-project-reference-and-solution-integration.md`
- `docs/api-conversion/04-business-logic-extraction.md`
- `docs/api-conversion/05-controller-api-surface.md`
- `docs/api-conversion/06-request-response-contracts.md`
- `docs/api-conversion/07-authenticated-actor-resolution.md`
- `docs/api-conversion/08-dependency-injection-and-program-cs.md`
- `docs/api-conversion/09-configuration-and-appsettings.md`
- `docs/api-conversion/10-database-migrations-seeding-and-verification.md`
- `docs/api-conversion/11-error-handling-status-codes-and-validation.md`
- `docs/api-conversion/12-logging-observability-and-sensitive-data.md`
- `docs/api-conversion/13-sla-and-notification-outbox-processing.md`
- `docs/api-conversion/14-testing-strategy.md`
- `docs/api-conversion/15-manual-migration-milestones.md`
- `docs/api-conversion/16-verification-cutover-and-rollback.md`
- `docs/api-conversion/17-risks-open-questions-and-decisions.md`
- `docs/api-conversion/api-conversion-summary.html`

