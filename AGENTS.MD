# AGENTS.md

## Source of truth
This repo is governed by `PROJECT.md`.

Before planning or implementing, read `PROJECT.md`.
Do not summarize it from memory.
Do not replace its milestones with a new roadmap.

## GSD workflow
Use GSD end-to-end, but map GSD phases directly to `PROJECT.md` section 24:

- Phase 1 = Milestone 1: Solution skeleton
- Phase 2 = Milestone 2: SQL schema and seed data
- Phase 3 = Milestone 3: WorkflowActionEnvelope execution path
- Phase 4 = Milestone 4: Workflow engine
- Phase 5 = Milestone 5: SLA monitor and notification senders
- Phase 6 = Milestone 6: Samples, tests, and demo

Each phase must stop after its milestone is complete.
Do not start the next phase until the current phase passes its quality gate.

## Required architecture rules
- .NET 10.
- SQL Server.
- EF Core.
- Use `TimeProvider`; do not call `DateTime.UtcNow` outside the composition root.
- Keep workflow logic generic and metadata-driven.
- Portfolio Trade is seed/sample metadata only.
- Do not hardcode Portfolio Trade behavior inside the reusable engine.
- Do not let request JSON specify `toState`.
- Do not let `workflow.options` override metadata.
- Do not add CQRS, MediatR, event sourcing, BPMN, message brokers, microservices, or plugin frameworks.
- Do not add third-party packages except MailKit, which `PROJECT.md` pre-approves for SMTP.
- Do not create a DemoUser table or SQL user-to-role mapping.
- Treat warnings as errors.

## Backward compatibility

All future changes must be backward compatible by default.

- Existing public HTTP endpoints, trusted actor headers, request JSON, response JSON, status-code behavior, and field semantics must continue to work for existing callers.
- Public contract changes must be additive. New request fields must be optional unless introduced through a new explicitly versioned endpoint or contract.
- Existing console commands, command arguments, `WorkflowActionEnvelope` fixtures, sample JSON, and restore-bundle usage must remain executable.
- Database migrations must support in-place upgrades of existing databases and preserve existing data. Prefer additive nullable columns and new tables; do not rename, drop, or reinterpret persisted columns without an explicitly approved migration strategy.
- Existing workflow, state, action, task, role, SLA-policy, notification-route, and template keys are stable integration identifiers. Do not rename, remove, or change their meaning in place.
- Existing notification recipient snapshots, SLA behavior, email subject/body semantics, and target-application integration entry points must remain valid unless the user explicitly approves a versioned behavior change.
- Preserve existing dependency-injection registration methods and public .NET integration surfaces used by the target API. Add overloads or optional parameters instead of breaking existing call sites.
- Before completing a change, run regression coverage proving existing public samples and supported workflows still pass in addition to tests for the new behavior.
- If a requirement cannot be delivered compatibly, stop and ask before implementation. Document the breaking impact, affected callers/data, versioning or migration plan, rollback path, and required integration-team action.

## Stop-and-ask rules
Follow all stop-and-ask triggers in `PROJECT.md` section 22.

## Quality gates
Before marking a phase done, run the relevant commands from `PROJECT.md` section 21.

Always include this completion summary:

Changed:
Why:
Tests run:
Seed verification run:
Sample JSON executed:
Files updated:
Open issues:

## Long-running Codex work

For multi-step work, maintain `CODEX_HANDOFF.md`.

Update it before ending a session or when context is getting low. Include:

- Current goal
- Completed changes
- Changed files
- Build/test status
- Remaining tasks
- Known risks
- Next-session prompt

Do not rely only on chat history for project state.
