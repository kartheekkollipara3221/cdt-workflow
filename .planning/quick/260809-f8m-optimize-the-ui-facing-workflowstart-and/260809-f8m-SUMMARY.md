---
phase: quick-260809-f8m
plan: 01
status: complete
subsystem: workflow-api
tags: [dotnet-10, aspnet-core, sql-server, ef-core, workflow, notifications, sla]

requires:
  - phase: milestone-6
    provides: completed generic workflow engine, Workflow API, SLA, notifications, samples, and demo
provides:
  - Optimized five-field public Workflow Start and Action DTOs
  - Metadata-derived business-object type and ordered recipient rules
  - Immutable workflow participant capture for Original Maker
  - Named request recipient inputs with deterministic To/Cc resolution
  - Fixed task/outbox recipient snapshots reused by SLA reminders
affects: [workflow-api, ui-integration, notification-outbox, sla, cdt, sqms]

tech-stack:
  added: []
  patterns:
    - Public DTO separated from the internal console WorkflowActionEnvelope
    - Definition-owned values derived from active metadata and persistence
    - Immutable participant capture plus route-scoped request inputs
    - One final message-level recipient snapshot per created task

key-files:
  created:
    - workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowNotificationRequest.cs
    - src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowParticipantDefinition.cs
    - src/WorkflowEngine.Infrastructure/Persistence/Entities/WorkflowParticipant.cs
    - src/WorkflowEngine.Infrastructure/Persistence/Entities/NotificationRecipientRule.cs
    - src/WorkflowEngine.Infrastructure/Migrations/20260809150000_OptimizeWorkflowApiEnvelope.cs
  modified:
    - workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowStartRequest.cs
    - workflow-api/src/WorkflowEngine.WorkflowApi/Models/WorkflowActionRequest.cs
    - workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs
    - workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs
    - src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
    - src/WorkflowEngine.Infrastructure/WorkflowStart/StartWorkflowService.cs
    - PROJECT.md
    - workflow-api/WORKFLOW_API_BOUNDARIES.md

key-decisions:
  - "Start derives businessObjectType from WorkflowDefinition metadata and public Start/Action use the same actionKey name."
  - "Original Maker is a required immutable Start participant; later actions cannot replace it."
  - "The UI may send only named recipient sources declared by route metadata; metadata owns To/Cc placement."
  - "Address strings are opaque and are not syntax, directory, mailbox, SMTP, trim, or rewrite validated before storage."
  - "Final recipients use route/source order, case-insensitive duplicate comparison, first-value retention, and To-over-Cc precedence."
  - "SLA reminders copy the task recipient snapshot unchanged instead of re-resolving current metadata or participants."

requirements-completed: [QUICK-260809-F8M]
duration: 2026-08-09
completed: 2026-08-09
---

# Quick Task 260809-f8m: Optimized Workflow API Envelope Summary

**The UI-facing Start and Action bodies now contain only application-owned workflow inputs while the engine derives definition, state, identity, recipient placement, and SLA behavior from trusted metadata and persistence.**

## Accomplishments

- Reduced each public Start/Action DTO to exactly five root properties and rejected obsolete or unknown root/nested JSON fields.
- Added generic schema, migration, seed, and verification for definition business-object type, workflow participants, and ordered recipient rules.
- Captured Original Maker once on Start and enforced participant requiredness/cardinality/immutability.
- Added route-scoped request recipient inputs and generic StaticAddress, WorkflowParticipant, and RequestInput resolution.
- Included notification input in canonical request hashing while preserving input-list order.
- Integrated final recipient resolution before workflow side effects and retained the internal console direct-snapshot path.
- Proved task/outbox snapshot identity and byte-for-byte SLA reuse after recipient metadata and participant changes.
- Added public samples, current README guidance, and consolidated frontend/beginner/target integration handoffs.

## Task Commits

1. `d4d72ca` - design optimized Workflow API envelope
2. `ad89a82` - plan Workflow API envelope optimization
3. `1a7516d` - align authoritative API boundaries
4. `24e4b09` - add metadata recipient rules and participants
5. `302cd6f` - resolve recipients from workflow metadata
6. `879d728` - integrate metadata recipients into workflow execution
7. `c5df601` - simplify public Workflow API payloads
8. `220590a` - prove SLA recipient snapshot reuse
9. `373a689` - publish optimized public examples
10. `b817711` - align UI integration handoffs
11. `9317013` - map public notification template data into rendering context

## Deviations and Auto-fixed Issues

### Existing recurring-SLA migration failed against earlier duration rows

- The live database had rows created before `CalculationMode` existed. The earlier migration added a blank default and then immediately applied a valid-mode check constraint.
- Added a deterministic update to `DurationFromTaskCreation` before the constraint and a migration-script regression assertion.
- Rebuilt the console before retrying because the first retry used stale `--no-build` output.

### Live HTTP Action exposed a missing adapter mapping

- The optimized Action DTO created `WorkflowNotificationInput`, but template data was not copied into the existing internal rendering context.
- A live Checker Return failed validation before side effects, proving the gap.
- Added a failing service regression assertion, mapped template data into the internal context, reran the focused suite, then reran live Start/Action successfully with SQL evidence.

## Verification Evidence

- Core warnings-as-errors build: 0 warnings, 0 errors.
- Core full SQL-backed suite: 308 passed, 0 failed, 4 smtp4dev-only skipped.
- Workflow API warnings-as-errors build: 0 warnings, 0 errors.
- Workflow API full suite: 66 passed, 0 failed, 0 skipped.
- Database initialization: passed against `cdt_workflow_api_20260527`.
- Seed verification: passed.
- Checker Return and Trading Desk Return PROJECT.md branch commands: passed.
- Aggregate demo: 14 steps passed.
- Public JSON sample deserialization/contract tests: passed.
- Live HTTP Start: succeeded and moved to CheckerReview.
- Live HTTP Checker Return: succeeded and moved to ReturnedToMaker.
- SQL evidence preserved opaque whitespace, captured Original Maker once, and showed each task recipient JSON equal to its immediate outbox JSON.
- Dedicated SQL test proved SLA reminder JSON remains byte-for-byte equal after metadata and participant changes.
- Negative source scans found removed public properties absent from API models and dormant legacy recipient route fields only in historical migrations.
- Port 5058 was available after stopping the verification API process.

## Open Issues

- Four smtp4dev integration tests remain opt-in/skipped because a fake SMTP service was not part of this verification; sender unit coverage and message-level retry tests pass.
- The migration intentionally clears workflow runtime rows because the user approved no backward compatibility requirement.
- The local verification database contains console/demo/live-HTTP evidence rows.

## Required Completion Summary

Changed: Optimized public Start/Action DTOs; added generic participant and recipient-rule metadata, resolution, snapshots, migration/seed, tests, public samples, and integration documentation.

Why: The UI should send only application-owned inputs while the reusable engine derives workflow definition, transition, identity, final recipient placement, and SLA behavior generically.

Tests run: Core 308 passed with 4 smtp4dev-only skips; Workflow API 66 passed; both warnings-as-errors builds passed with zero warnings/errors; focused and live HTTP regression checks passed.

Seed verification run: Yes. `init-db` and `verify-seed` passed against the configured local SQL Server database.

Sample JSON executed: Yes. Required console return branches and the 14-step demo passed; public Workflow API samples passed live request-model contract tests, and equivalent optimized Start/Action bodies passed live HTTP execution.

Files updated: Public API models/services/tests; application notification contracts/resolver; infrastructure entities/configuration/migration/seed/execution/Start/SLA tests; PROJECT/README/boundary/design/plan/handoff docs; public samples.

Open issues: Four smtp4dev-only tests remain skipped; migration runtime disposal is intentional; local SQL retains verification rows.

---
*Phase: quick-260809-f8m*
*Completed: 2026-08-09*
