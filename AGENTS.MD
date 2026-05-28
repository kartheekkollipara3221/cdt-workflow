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