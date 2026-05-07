# Requirements: Reusable .NET Workflow Engine Console POC

**Defined:** May 7, 2026
**Core Value:** A user can run the console, pass a standard workflow action JSON envelope, and see SQL metadata drive workflow state, tasks, notifications, SLA, idempotency, concurrency, and audit without Portfolio Trade logic embedded in the engine.

The repository-root `PROJECT.md` is authoritative. This file converts that contract into checkable GSD requirement IDs for roadmap and phase tracking.

## v1 Requirements

### Foundation

- [ ] **FND-01**: Developer can build a .NET solution with console, application/domain/infrastructure structure, EF Core wiring, nullable enabled, and warnings treated as errors.
- [ ] **FND-02**: Developer can run a console command router with stubs for every command required by `PROJECT.md` section 10, including `show-context`.
- [ ] **FND-03**: Application and tests read `ConnectionStrings:DefaultConnection` from configuration sources, not hardcoded workflow logic.

### SQL Metadata and Runtime Persistence

- [ ] **SQL-01**: Developer can run `init-db` to apply migrations and seed the Portfolio Trade workflow definition into SQL Server.
- [ ] **SQL-02**: Developer can run `verify-seed` to prove required definition keys, transition rows, roles, states, actions, tasks, notifications, and constraints exist.
- [ ] **SQL-03**: SQL constraints enforce single pending task per workflow instance and required uniqueness for workflow definition keys.
- [ ] **SQL-04**: Runtime persistence supports workflow instances, tasks, SLA, notification outbox, audit, idempotency records, and append-only mocked section data.

### Workflow Contracts and Console Inputs

- [ ] **CON-01**: `WorkflowActionEnvelope` DTOs parse the required JSON request shape and validate actor, action, workflow, section data, MakerPrimary, idempotency, and concurrency rules.
- [ ] **CON-02**: `WorkflowContextResponse`, `AvailableWorkflowAction`, and `WorkflowActionResult` DTOs preserve the stable contract shapes defined by `PROJECT.md`.
- [ ] **CON-03**: `execute-json` reads sample files, applies command-line business object and workflow instance overrides deterministically, and prints the expected workflow action result shape.
- [ ] **CON-04**: `show-context` prints valid workflow context JSON for form-open scenarios using user, role, workflow instance, and format arguments.

### Metadata-Driven Workflow Engine

- [ ] **ENG-01**: Workflow transition resolution uses SQL metadata matching definition version, current state, action key, and allowed role.
- [ ] **ENG-02**: Request JSON never controls next state through `toState` or equivalent authority.
- [ ] **ENG-03**: Role, state, action, workflow definition version, task, and instance version validation return the required result codes.
- [ ] **ENG-04**: State-changing transitions close the configured task, create the configured task, queue metadata-required notifications, write audit, and increment instance version.
- [ ] **ENG-05**: `SaveDraft` is authorized through metadata, saves mocked section data, writes lightweight audit, and does not move state, touch tasks/SLA/notifications, or increment version.
- [ ] **ENG-06**: Idempotent replay returns stored `ResultJson` without duplicating mocked section data, audit, tasks, notifications, or SLA effects.
- [ ] **ENG-07**: Concurrency guards are independent and all supplied guards must match before saving section data or moving workflow.
- [ ] **ENG-08**: Workflow context resolves available actions from metadata and returns read-only/no-action behavior for wrong roles or completed workflows.

### Portfolio Trade Sample Metadata

- [ ] **PT-01**: Portfolio Trade states, roles, actions, tasks, and transition matrix exactly match `PROJECT.md` section 7.
- [ ] **PT-02**: Checker can return to Maker from `CheckerReview`, closing `CheckerReviewTask`, creating `MakerReworkTask`, notifying Maker, and writing audit.
- [ ] **PT-03**: Trading Desk can return to Maker from `TradingDeskReview`, closing `TradingDeskReviewTask`, creating `MakerReworkTask`, notifying Maker, and writing audit.
- [ ] **PT-04**: Maker can save draft or resubmit from `ReturnedToMaker`, and resubmission closes the existing `MakerReworkTask`.
- [ ] **PT-05**: BDRM can complete the workflow or route back to Trading Desk Rework according to seeded metadata.

### SLA and Notifications

- [ ] **SLA-01**: `run-sla` scans pending tasks and queues `SlaTaskOverdue` notifications when due dates are overdue.
- [ ] **SLA-02**: SLA overdue notifications are queued at most once per task.
- [ ] **SLA-03**: `process-notification-outbox` demonstrates notification outbox processing through configured senders without sending real email by default.
- [ ] **SLA-04**: SMTP notification delivery is implemented behind `INotificationSender` with MailKit, configuration-driven enablement, and fake/local SMTP tests only.

### Samples, Tests, and Documentation

- [ ] **DOC-01**: All required sample JSON files from `PROJECT.md` section 19 exist and include expected test expectations.
- [ ] **DOC-02**: Automated tests cover seed data, transitions, idempotency, concurrency, start workflow, mocked section data, envelope validation, SLA, notification senders, execute-json, show-context, workflow context, and workflow action results.
- [ ] **DOC-03**: Demo scripts prove happy path, Checker Return to Maker, and Trading Desk Return to Maker branch behavior.
- [ ] **DOC-04**: README explains the reusable workflow engine POC, console commands, workflow context, JSON envelopes, SQL setup, return paths, SLA, audit, notification sender configuration, and demo flow.

## v2 Requirements

Deferred unless the user explicitly starts a future milestone.

- **V2-01**: Add cancel/withdraw workflow paths.
- **V2-02**: Add business-day holiday calendars for SLA calculations.
- **V2-03**: Add dashboard/reporting UI.
- **V2-04**: Add real enterprise authentication and authorization.
- **V2-05**: Add SQL user-to-role mapping.
- **V2-06**: Add failure audit records for rejected or rolled-back actions.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Web UI or InfoPath replacement UI | The form/UI layer is handled separately; this POC proves workflow behavior. |
| Full field-level form validation | Mocked section data is enough for workflow execution. |
| Real email delivery in default dev/test settings | SMTP code path may exist, but real delivery is disabled by configuration in dev/test. |
| Parallel approvals or multiple pending tasks | v1 is single-active-state by requirement. |
| CQRS, MediatR, event sourcing, BPMN, saga frameworks, brokers, plugin systems, microservices, or dynamic rule languages | Explicitly excluded to keep the POC small and reusable. |
| Hardcoded Portfolio Trade engine logic | Portfolio Trade must remain sample metadata, not engine behavior. |
| SQL user-to-role lookup | v1 receives roles from console arguments or future auth claims. |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| FND-01 | Phase 1 | Pending |
| FND-02 | Phase 1 | Pending |
| FND-03 | Phase 1 | Pending |
| SQL-01 | Phase 2 | Pending |
| SQL-02 | Phase 2 | Pending |
| SQL-03 | Phase 2 | Pending |
| SQL-04 | Phase 2 | Pending |
| CON-01 | Phase 3 | Pending |
| CON-02 | Phase 3 | Pending |
| CON-03 | Phase 3 | Pending |
| CON-04 | Phase 3 | Pending |
| ENG-01 | Phase 4 | Pending |
| ENG-02 | Phase 4 | Pending |
| ENG-03 | Phase 4 | Pending |
| ENG-04 | Phase 4 | Pending |
| ENG-05 | Phase 4 | Pending |
| ENG-06 | Phase 4 | Pending |
| ENG-07 | Phase 4 | Pending |
| ENG-08 | Phase 4 | Pending |
| PT-01 | Phase 2 | Pending |
| PT-02 | Phase 4 | Pending |
| PT-03 | Phase 4 | Pending |
| PT-04 | Phase 4 | Pending |
| PT-05 | Phase 4 | Pending |
| SLA-01 | Phase 5 | Pending |
| SLA-02 | Phase 5 | Pending |
| SLA-03 | Phase 5 | Pending |
| SLA-04 | Phase 5 | Pending |
| DOC-01 | Phase 6 | Pending |
| DOC-02 | Phase 6 | Pending |
| DOC-03 | Phase 6 | Pending |
| DOC-04 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 32 total
- Mapped to phases: 32
- Unmapped: 0

---
*Requirements defined: May 7, 2026*
*Last updated: May 7, 2026 after auto initialization from repository-root PROJECT.md*
