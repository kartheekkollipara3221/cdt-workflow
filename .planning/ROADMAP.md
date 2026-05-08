# Roadmap: Reusable .NET Workflow Engine Console POC

**Source of truth:** Repository-root `PROJECT.md`, especially section 24.
**Planning mode:** Auto, coarse phases, parallel execution within milestone boundaries.

## Phase Overview

| # | Phase | Goal | Requirements | Status |
|---|-------|------|--------------|--------|
| 1 | Solution Skeleton | Establish solution structure, console routing, configuration, and EF wiring. | FND-01, FND-02, FND-03 | Complete - 2026-05-07 |
| 2 | SQL Schema and Seed Data | Persist workflow metadata/runtime tables and prove Portfolio Trade seed data. | SQL-01, SQL-02, SQL-03, SQL-04, PT-01 | Complete - 2026-05-07 |
| 3 | Workflow Contracts and Console Actions | Implement request/response DTOs plus `execute-json` and `show-context` parsing/output. | CON-01, CON-02, CON-03, CON-04 | Complete - 2026-05-07 |
| 4 | Workflow Engine | Execute metadata-driven workflow transitions with validation, tasks, audit, notifications, idempotency, concurrency, and refreshed context. | ENG-01, ENG-02, ENG-03, ENG-04, ENG-05, ENG-06, ENG-07, ENG-08, PT-02, PT-03, PT-04, PT-05 | Complete - 2026-05-07 |
| 5 | SLA Monitor and Notification Senders | Demonstrate SLA due-date scanning and notification outbox processing through configurable senders. | SLA-01, SLA-02, SLA-03, SLA-04 | Complete - 2026-05-07 |
| 6 | Samples, Tests, and Demo | Complete samples, automated tests, demo scripts, and README. | DOC-01, DOC-02, DOC-03, DOC-04 | Complete - 2026-05-08 |

## Phase Details

### Phase 1: Solution Skeleton

**Goal:** Solution and project structure exist. Console command routing dispatches a stub for every command in `PROJECT.md` section 10, including `show-context`. Configuration and EF Core wiring compile.

**Requirements:** FND-01, FND-02, FND-03

**Success criteria:**

1. `dotnet build -warnaserror` passes.
2. Solution contains the expected console, domain/application, infrastructure, and test structure.
3. Console command router recognizes required commands and returns clear stub output where implementation is deferred.
4. Configuration loads `ConnectionStrings:DefaultConnection` from supported sources.
5. No workflow logic hardcodes the local SQL connection string.

**Quality gate before Phase 2:** Run relevant build checks from `PROJECT.md` section 21 and resolve warnings/errors.

### Phase 2: SQL Schema and Seed Data

**Goal:** All entities and required unique constraints exist. `init-db` creates schema and seeds the Portfolio Trade definition. `verify-seed` passes against SQL Server.

**Requirements:** SQL-01, SQL-02, SQL-03, SQL-04, PT-01

**Success criteria:**

1. EF Core migrations create the required metadata and runtime tables.
2. Portfolio Trade states, roles, actions, task definitions, SLA policies, notifications, and transitions are seeded.
3. The transition matrix exactly matches `PROJECT.md` section 7, including both Return to Maker paths.
4. SQL constraints enforce definition-key uniqueness and at most one pending task per workflow instance.
5. `init-db` and `verify-seed` pass against configured SQL Server.

**Quality gate before Phase 3:** Run `dotnet build -warnaserror`, `dotnet test` where available, `init-db`, and `verify-seed`.

### Phase 3: Workflow Contracts and Console Actions

**Goal:** Workflow contract DTOs exist. `execute-json` parses sample files, saves mocked section data through the application path as available, and prints the demo output shape. `show-context` parses user/role arguments and prints workflow context JSON.

**Requirements:** CON-01, CON-02, CON-03, CON-04

**Success criteria:**

1. `WorkflowActionEnvelope` validation rejects malformed actor/action/workflow/guard combinations.
2. MakerPrimary checkbox consistency is validated before saving section data.
3. `WorkflowContextResponse`, `AvailableWorkflowAction`, and `WorkflowActionResult` shapes match `PROJECT.md`.
4. `execute-json` handles `--business-object` and `--workflow-instance` overrides deterministically.
5. `show-context` supports `--user`, `--roles`, and `--format json`.

**Quality gate before Phase 4:** Run build and focused DTO/command parsing tests.

### Phase 4: Workflow Engine

**Goal:** Metadata-driven transition execution is implemented end-to-end for tasks, state, audit, notifications, idempotency, concurrency, available actions, and refreshed context.

**Requirements:** ENG-01, ENG-02, ENG-03, ENG-04, ENG-05, ENG-06, ENG-07, ENG-08, PT-02, PT-03, PT-04, PT-05

**Success criteria:**

1. `SubmitForChecker`, `CheckerReturnToMaker`, and `TradingReturnToMaker` samples produce the expected state, task, audit, notification, and context results.
2. `SaveDraft` writes section data and lightweight audit only, without version increment or workflow movement.
3. Idempotent replay returns stored `ResultJson` and creates no duplicate side effects.
4. All supplied concurrency guards are enforced independently.
5. Wrong role, invalid transition, completed workflow, stale version, and definition-version mismatch cases return required result codes.
6. Available actions are derived from metadata and respect current actor roles.

**Quality gate before Phase 5:** Run build, tests, `init-db`, `verify-seed`, and required branch sample commands from `PROJECT.md` section 21.

### Phase 5: SLA Monitor and Notification Senders

**Goal:** `run-sla` and `process-notification-outbox` work. SLA durations are short enough to demo overdue behavior. `SlaTaskOverdue` is queued at most once per task, and notification delivery is selected by configuration.

**Requirements:** SLA-01, SLA-02, SLA-03, SLA-04

**Success criteria:**

1. Pending tasks with overdue `DueAtUtc` queue `SlaTaskOverdue` notifications.
2. Re-running SLA monitor does not create duplicate overdue notifications for the same task.
3. Completed tasks and completed workflows do not generate new SLA notifications.
4. Notification outbox processing command shows pending/processed records without sending real email by default.
5. `ConsoleNotificationSender`, `NoOpNotificationSender`, and MailKit-backed `SmtpNotificationSender` are selected by configuration and covered by appropriate tests.

**Quality gate before Phase 6:** Run build, tests, and an SLA demo command path. SMTP integration tests are opt-in only against a local fake SMTP server.

### Phase 6: Samples, Tests, and Demo

**Goal:** All sample JSON files exist and pass expectations. Workflow context tests and action-result tests pass. Demo scripts run end-to-end. README is written.

**Requirements:** DOC-01, DOC-02, DOC-03, DOC-04

**Success criteria:**

1. Required sample JSON files from `PROJECT.md` section 19 exist and execute successfully.
2. Required tests from `PROJECT.md` section 20 pass.
3. Demo scripts from `PROJECT.md` section 25 prove happy path, Checker Return to Maker, and Trading Desk Return to Maker branches.
4. README explains setup, SQL configuration, commands, workflow context, JSON envelopes, SLA, audit, notification sender configuration, and reusable engine boundaries.
5. Final quality gates from `PROJECT.md` section 21 pass or any unrun command is documented with the exact reason.

## Coverage

- v1 requirements: 32
- Mapped to phases: 32
- Unmapped: 0

---
*Roadmap created: May 7, 2026 from repository-root PROJECT.md section 24*
