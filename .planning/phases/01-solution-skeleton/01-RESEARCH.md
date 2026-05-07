# Phase 1: Solution Skeleton - Research

## RESEARCH COMPLETE

**Phase:** 01 - Solution Skeleton
**Date:** 2026-05-07
**Source:** Inline fallback because GSD phase researcher agents are not installed in this runtime.

## What Matters for Planning

Phase 1 is a foundation phase. The main risk is not technical uncertainty; it is accidentally pulling later workflow behavior into the skeleton. The plan should create just enough structure that later phases can add schema, contracts, workflow execution, SLA, notifications, samples, and documentation without reshuffling the solution.

## Recommended Skeleton

- Use a `.sln` at `src/WorkflowEngine.sln`.
- Use conventional projects:
  - `src/WorkflowEngine.Console` - command-line entry point and command router.
  - `src/WorkflowEngine.Application` - application contracts, configuration helpers, and orchestration extension points.
  - `src/WorkflowEngine.Domain` - generic workflow concepts and domain boundaries.
  - `src/WorkflowEngine.Infrastructure` - EF Core DbContext and persistence registration.
  - `tests/WorkflowEngine.Tests` - Phase 1 smoke tests.
- Keep Portfolio Trade out of project names, namespaces, and service names.
- Put shared compile strictness in `Directory.Build.props` so all projects inherit nullable and warnings-as-errors.

## Command Routing Notes

Phase 1 should recognize the required command names from `PROJECT.md` section 10 but return stub results for behavior owned by later phases. This keeps the CLI contract visible early while preventing fake implementations from becoming accidental behavior.

Required command names for the router:

- `init-db`
- `verify-seed`
- `start-workflow`
- `execute-json`
- `show-context`
- `show`
- `show-tasks`
- `show-history`
- `show-notifications`
- `run-sla`
- `process-notification-outbox`
- `demo`

## Configuration and EF Notes

- Configuration must read `ConnectionStrings:DefaultConnection`.
- The local SQL Server connection string from root `PROJECT.md` may appear in `appsettings.Development.json` for this POC, but not in domain/application/infrastructure workflow logic.
- EF Core wiring in Phase 1 should stop at a compile-ready DbContext and service registration extension. Entity sets and migrations belong to Phase 2.
- Use `TimeProvider` registration at the composition root; future code should consume it through dependency injection.

## Testing Notes

Phase 1 tests should prove:

- Known commands are recognized.
- Unknown commands return a clear error code/message.
- Configuration reads the expected connection string key.
- The solution builds with warnings treated as errors.

These tests should not require a live SQL Server connection yet.

## Pitfalls to Avoid

- Do not hardcode Portfolio Trade workflow actions into the engine skeleton.
- Do not implement SQL schema or seed rows in Phase 1.
- Do not add CQRS, MediatR, event sourcing, BPMN, brokers, microservices, plugin frameworks, or dynamic rule languages.
- Do not make `show-context` or `execute-json` return fake success data that later tests might mistake for real behavior.
- Do not call `DateTime.UtcNow` in services; register and use `TimeProvider`.
