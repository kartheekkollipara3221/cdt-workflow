# Reusable .NET Workflow Engine Console POC

## What This Is

This project builds the reusable SQL-backed .NET workflow engine console proof of concept described by the repository-root `PROJECT.md`. The engine replaces SharePoint Designer workflow behavior for a sample Portfolio Trade process while remaining generic enough for other workflows.

The console POC owns workflow execution, task ownership, SLA tracking, notification outbox records, audit history, SQL seed verification, workflow context output, and JSON-driven action execution. The Portfolio Trade workflow is sample metadata only and must not be hardcoded into the reusable engine.

## Core Value

A user can run the console, pass a standard workflow action JSON envelope, and see SQL metadata drive workflow state, tasks, notifications, SLA, idempotency, concurrency, and audit without Portfolio Trade logic embedded in the engine.

## Requirements

### Validated

- Build the solution skeleton, console command routing, configuration loading, and EF Core wiring. - Validated in Phase 1: Solution Skeleton.
- Create SQL schema, EF entities, migrations, required constraints, and Portfolio Trade seed data. - Validated in Phase 2: SQL Schema and Seed Data.

### Active

- [ ] Implement stable `WorkflowActionEnvelope`, `WorkflowContextResponse`, `AvailableWorkflowAction`, and `WorkflowActionResult` contracts.
- [ ] Implement `execute-json` and `show-context` console commands.
- [ ] Implement a metadata-driven workflow engine for transitions, role/action/state validation, tasks, audit, notification outbox, SLA, idempotency, optimistic concurrency, and refreshed context.
- [ ] Implement `run-sla` and `process-notification-outbox` demo commands.
- [ ] Provide required sample JSON files, tests, demo scripts, and README coverage.
- [ ] Prove Checker Return to Maker and Trading Desk Return to Maker branch behavior.

### Out of Scope

- Web UI or InfoPath replacement UI - this project is the console/API-facing workflow proof of concept only.
- Full field-level form validation - mocked section data is enough for this POC.
- Sending real emails to real recipients during the POC - notification delivery is abstracted and real SMTP is configuration-driven only.
- Enterprise authentication/authorization - actor and role are simulated through console/API inputs.
- Parallel approvals, fan-out/fan-in, or multiple active workflow tasks - v1 is single-active-state only.
- Complex SharePoint migration tooling - this POC proves replacement workflow behavior, not migration automation.
- CQRS, MediatR, event sourcing, BPMN, saga frameworks, message brokers, plugin systems, microservices, or dynamic rule languages - explicitly excluded unless approved.

## Context

The authoritative requirements live in repository-root `PROJECT.md`. It defines the complete contract, including the Portfolio Trade transition matrix, request/response contracts, console commands, SQL model, sample JSON files, tests, quality gates, and implementation milestones.

The local SQL Server connection string approved for this POC is:

```text
Server=DOLLY;Database=burrito;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

The application and tests must read it from `ConnectionStrings:DefaultConnection`, preferably through `appsettings.Development.json` or `ConnectionStrings__DefaultConnection`. Do not hardcode connection strings inside domain, application, or infrastructure workflow logic.

The workflow engine must remain generic. It may understand workflow definitions, versions, states, actions, transitions, actors, roles, tasks, SLA, notifications, audit, idempotency, and optimistic concurrency. It must not understand Portfolio Trade form fields.

## Constraints

- **Source of truth**: Repository-root `PROJECT.md` wins over generated GSD artifacts if there is any conflict.
- **Milestone order**: Work milestone-by-milestone from `PROJECT.md` section 24. Do not skip milestone quality gates before starting the next milestone.
- **Transition authority**: SQL transition metadata decides next state. Request JSON passes `actionKey`, never authoritative `toState`.
- **Stable contracts**: `WorkflowContextResponse`, `WorkflowActionEnvelope`, and `WorkflowActionResult` shapes must remain stable unless the user approves changes.
- **SaveDraft semantics**: `SaveDraft` is only valid where seeded and must not move state, increment workflow version, create tasks, close tasks, evaluate SLA, or queue notifications.
- **Return paths**: Checker Return to Maker and Trading Desk Return to Maker are mandatory.
- **Single active task**: At most one pending task is allowed per workflow instance in v1.
- **Idempotency**: Idempotent replay returns stored `ResultJson` and must not re-execute side effects.
- **Concurrency**: If multiple concurrency guards are supplied, all must match.
- **Initial version**: `WorkflowInstance.Version` starts at 1 and increments only on state-changing transitions.
- **Time**: Use injected `TimeProvider`; do not call `DateTime.UtcNow` directly outside the composition root.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Use repository-root `PROJECT.md` as the project vision | The invocation provided `PROJECT.md` as the idea document and it contains the detailed project contract. | Pending |
| Use six GSD phases matching `PROJECT.md` section 24 milestones | The root project contract already defines implementation milestones and quality gates. | Pending |
| Use automatic GSD mode | The command was invoked with `--auto`, so planning artifacts are generated without interactive gates. | Pending |
| Use coarse granularity | The source document already defines six milestone-sized phases. | Pending |
| Allow parallel execution where plans are independent | Auto mode defaults to parallel execution, while phase order still follows the root milestone gates. | Pending |
| Commit planning docs | Auto mode keeps planning artifacts tracked in git. | Pending |
| Keep project research lightweight | GSD project subagents are not installed in this runtime, and the source requirements are already detailed. | Pending |
| Preserve existing `AGENTS.MD` guidance | It already contains project-specific GSD and Codex guardrails derived from the root requirements. | Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

After each phase transition:

1. Requirements invalidated? Move to Out of Scope with reason.
2. Requirements validated? Move to Validated with phase reference.
3. New requirements emerged? Add to Active.
4. Decisions to log? Add to Key Decisions.
5. "What This Is" still accurate? Update if drifted.

After each milestone:

1. Full review of all sections.
2. Core Value check - still the right priority?
3. Audit Out of Scope - reasons still valid?
4. Update Context with current state.

---
*Last updated: May 7, 2026 after Phase 1 completion*
