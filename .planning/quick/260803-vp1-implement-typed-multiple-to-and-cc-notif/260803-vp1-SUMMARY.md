---
phase: quick-260803-vp1
plan: 01
subsystem: notifications
tags: [dotnet-10, sql-server, ef-core, mailkit, outbox, sla, aspnet-core]

requires:
  - phase: milestone-5
    provides: SLA monitor, notification outbox, and notification senders
provides:
  - Caller-supplied ordered multiple-To and multiple-Cc notification contract
  - Durable task and outbox recipient snapshots reused by SLA reminders
  - One-message MailKit delivery with message-level retry and override diagnostics
  - Workflow API, console samples, inspection, and handoff documentation for the typed contract
affects: [CDT, SQMS, workflow-api, notification-outbox, sla]

tech-stack:
  added: []
  patterns:
    - Caller-owned address selection and validation
    - Exact ordered recipient snapshot per task and outbox message
    - Message-level delivery status and retry for all To and Cc recipients

key-files:
  created:
    - src/WorkflowEngine.Application/Contracts/NotificationRecipients.cs
    - src/WorkflowEngine.Infrastructure/Migrations/20260803120000_AddNotificationRecipientSnapshots.cs
  modified:
    - src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs
    - src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs
    - src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs
    - src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs
    - workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowActionService.cs
    - workflow-api/src/WorkflowEngine.WorkflowApi/Services/WorkflowStartService.cs
    - PROJECT.md

key-decisions:
  - "CDT and SQMS supply and validate concrete addresses; the generic engine does not validate, trim, normalize, deduplicate, resolve, or look up recipients."
  - "One outbox row represents one email with all ordered To and Cc recipients and one status/retry lifecycle."
  - "SLA reminders copy the task recipient snapshot unchanged instead of re-resolving recipients."
  - "No backward-compatible singular, delimited, payload, or SMTP role-map runtime fallback is retained."

patterns-established:
  - "Recipient snapshot: serialize notificationRecipients separately from template-only notificationContext."
  - "Malformed snapshot/address: release the complete outbox claim, increment retry, persist LastError, and leave the row Pending."

requirements-completed: [QUICK-260803-VP1]

duration: multi-session, 2026-08-03 through 2026-08-05
completed: 2026-08-05
---

# Quick Task 260803-vp1: Multiple Notification Recipients Summary

**Typed ordered To/Cc recipients now flow through workflow start/action APIs, SQL task/outbox snapshots, SLA reminders, inspection, and one-message MailKit delivery.**

## Performance

- **Duration:** Multi-session, 2026-08-03 through 2026-08-05
- **Tasks:** 3 planned tasks completed
- **Files modified:** 67

## Accomplishments

- Added typed `notificationRecipients.toEmailAddresses` and `ccEmailAddresses` arrays to start/action contracts and idempotency hashing.
- Added required SQL JSON snapshots to `WorkflowTask` and `NotificationOutbox`; the migration discards approved disposable task/outbox rows and removes outbox scalar recipient columns.
- Persisted the same initial snapshot on task and outbox, copied it unchanged into SLA reminders, and delivered all addresses as one message.
- Removed runtime payload/singular/delimited/role-map fallback while retaining `Smtp:RecipientOverride` with intended-recipient diagnostics.
- Updated Workflow API mapping, console inspection, nine notification-producing samples, PROJECT.md, active handoff documentation, and target deployment instructions.
- Added regression coverage for ordering, To/Cc placement, hashing, explicit null arrays, malformed snapshots, malformed MailKit addresses, override behavior, dispatcher persistence, and API mapping.

## Task Commits

1. **Core contract, schema, runtime, SLA, outbox, and SMTP** - `8089ffd`
2. **Snapshot diagnostics and malformed-address coverage** - `ce0cca1`
3. **Workflow API, samples, and documentation** - `db46e0c`
4. **Concrete API dispatcher and deployment coverage** - `4bc34e4`
5. **Explicit-null start snapshot preservation** - `790569c`
6. **SQL recipient integration assertion corrections** - `bd106a0`
7. **FreshClone self-build artifact isolation** - `172b8f3`

**Plan metadata:** `dc0c8fb`

## Files Created/Modified

- `src/WorkflowEngine.Application/Contracts/NotificationRecipients.cs` - Typed mutable JSON contract with ordered To/Cc lists.
- `src/WorkflowEngine.Infrastructure/Migrations/20260803120000_AddNotificationRecipientSnapshots.cs` - Required task/outbox snapshot columns and approved disposable-data transition.
- `src/WorkflowEngine.Infrastructure/WorkflowExecution/WorkflowExecutionService.cs` - Exact initial task/outbox snapshot persistence.
- `src/WorkflowEngine.Infrastructure/Sla/RunSlaService.cs` - Unchanged task-snapshot reuse for overdue reminders.
- `src/WorkflowEngine.Infrastructure/Notifications/ProcessNotificationOutboxService.cs` - Snapshot materialization and complete-row retry behavior.
- `src/WorkflowEngine.Infrastructure/Notifications/SmtpNotificationSender.cs` - Multiple To/Cc MailKit message construction and override suppression.
- `workflow-api/src/WorkflowEngine.WorkflowApi` - Typed request models and unchanged recipient mapping for start/actions.
- `samples/workflow-actions/02-10-*.json` - Concrete 2-To/2-Cc examples outside template context.
- `PROJECT.md`, `README.md`, `context.md`, and active API/handoff docs - Current ownership and runtime semantics.
- Engine and Workflow API test projects - Contract, persistence, SLA, outbox, SMTP, inspection, and dispatcher regressions.

## Decisions Made

- Recipient syntax validation remains in CDT/SQMS; the engine deliberately preserves caller values and order without extra validation overhead.
- Explicit malformed shapes, including null arrays, are preserved until the delivery boundary and then fail/retry the whole message instead of being silently replaced.
- `NotificationRoute.RecipientRoleKey` and `RecipientUserId` remain dormant metadata/schema history only; runtime delivery does not read them.
- The dedicated verification database was `cdt_workflow_multiple_recipient_verify`; the existing application database was not modified by the manual walkthrough.

## Deviations from Plan

### Auto-fixed Issues

**1. Explicit null arrays crashed initial-action start**
- **Found during:** Whole-feature review
- **Issue:** Collection spread threw before the malformed snapshot could reach the intended outbox retry boundary.
- **Fix:** Clone only non-null lists while preserving explicit null arrays unchanged.
- **Verification:** Focused start tests and full SQL-backed suite pass.
- **Committed in:** `790569c`

**2. New SQL tests mutated an unsaved row through a database query**
- **Found during:** Final unfiltered SQL suite
- **Issue:** `DbSet.Single()` queried SQL before the added notification was saved, so no row existed in the database.
- **Fix:** Retained and mutated the tracked entity returned by the test helper; corrected return-branch assertions to the samples' exact recipient lists.
- **Verification:** Five formerly failing integration tests passed, followed by the full suite.
- **Committed in:** `bd106a0`

**3. FreshClone tests rebuilt into assemblies loaded by testhost**
- **Found during:** Final unfiltered SQL suite
- **Issue:** Nested `dotnet build` attempted to overwrite DLLs locked by the parent Windows testhost.
- **Fix:** Directed nested build output to a unique temporary `--artifacts-path` and removed it after completion.
- **Verification:** FreshClone 6/6 passed and the unfiltered engine suite passed 252/252 non-SMTP tests.
- **Committed in:** `172b8f3`

---

**Total deviations:** 3 auto-fixed correctness/test-infrastructure issues.
**Impact on plan:** Recipient behavior stayed within the approved design; no packages or product scope were added.

## Verification Evidence

- Engine restore: passed.
- Engine build: passed with 0 warnings and 0 errors.
- Workflow API build: passed with 0 warnings and 0 errors using existing restored assets.
- Engine SQL-backed tests: 252 passed, 0 failed, 3 skipped smtp4dev-only integration tests.
- Workflow API SQL-enabled tests: 50 passed, 0 failed, 0 skipped.
- `init-db`: passed against `cdt_workflow_multiple_recipient_verify`.
- `verify-seed`: passed.
- Sample `02-submit-for-checker.json`: passed and queued one row with ordered 2-To/2-Cc recipients.
- `run-sla --now "2030-01-15T16:00:00Z"`: queued one overdue row with the identical task snapshot.
- `process-notification-outbox` with NoOp: processed both rows; inspection showed both `Sent` with identical ordered recipients.

## Issues Encountered

- Normal Workflow API restore is blocked by existing transitive `Microsoft.OpenApi 2.4.1` advisory `NU1903` from `Swashbuckle.AspNetCore 10.1.7`. The feature added no packages; builds and 50 API tests passed from the existing restored assets. Package remediation remains separate work because dependency changes were outside this approved recipient implementation.
- Three SMTP integration tests remain skipped unless the configured local smtp4dev server/test gate is enabled. MailKit construction and malformed-address behavior are covered by passing automated tests.

## User Setup Required

None for the implementation. Production callers must send validated concrete To/Cc arrays. A local smtp4dev instance is required only to run the opt-in SMTP integration category.

## Next Phase Readiness

- Implementation is complete on `feature/multiple-notification-recipients` in `.worktrees/multiple-notification-recipients`.
- The branch is ready for integration after choosing merge/rebase/PR handling around the original checkout's unrelated user changes.

## Required Completion Summary

Changed: Added exact typed multiple-To/multiple-Cc handling across contracts, SQL snapshots, SLA, outbox, SMTP, API, samples, inspection, and documentation.

Why: CDT and SQMS need multiple To/Cc recipients on the original and SLA reminder emails without business-specific recipient resolution in the generic engine.

Tests run: Engine 252 passed and 3 smtp4dev-gated skipped; Workflow API 50 passed; both solutions built with zero warnings/errors.

Seed verification run: Yes, `init-db` and `verify-seed` passed against the dedicated local verification database.

Sample JSON executed: Yes, `02-submit-for-checker.json`; initial and SLA outbox rows retained identical ordered 2-To/2-Cc snapshots and both reached Sent through NoOp.

Files updated: 67 files across application contracts, infrastructure, EF migration, console/API, samples, tests, and documentation.

Open issues: Workflow API normal restore reports existing transitive `Microsoft.OpenApi 2.4.1` `NU1903`; smtp4dev-only tests require the local fake SMTP service.

---
*Phase: quick-260803-vp1*
*Completed: 2026-08-05*
