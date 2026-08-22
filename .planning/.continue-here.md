---
context: default
phase: SQMS-projection-ownership-correction
task: 8
total_tasks: 8
status: complete
last_updated: 2026-08-22T22:29:20Z
---

# SQMS Projection Ownership Correction Complete

<current_state>
Request-driven SQMS Start/Action operations return authoritative workflow state and context without changing any `SSQMS.Opportunity` or `SSQMS.Bid` column. Scheduled Opportunity close and Bid classification use `ISqmsScheduledTransitionService` and still project `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate` atomically with the workflow transition.
</current_state>

<completed_work>

- Added complete-row unchanged proofs for successful, failed, and idempotently replayed request operations.
- Added the scheduled-only execution interface and routed the scheduler through it.
- Preserved existing `ISqmsWorkflowService` signatures and one scoped concrete implementation.
- Updated end-to-end tests to simulate the target API's separate persistence of returned state.
- Corrected `docs/sqms/TARGET-API-INTEGRATION.md` with the required external target API migration.
- Ran the isolated projection-ownership acceptance against fresh SQL Server and smtp4dev evidence.
- Retained full results in `docs/sqms/SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE-RESULTS-20260822.md`.
</completed_work>

<verification>

- Main warning-as-error build: zero warnings/errors.
- Workflow API warning-as-error build: zero warnings/errors.
- Live tool warning-as-error build: zero warnings/errors.
- Main tests: 453 passed, 0 failed, 4 opt-in SMTP skips.
- Workflow API tests: 68 passed, 0 failed.
- Seed, SQMS deployment, and rollback verifiers: PASS.
- Live state: Opportunity 10000 `COMPLETED` v10; Bids 10000-10003 are `ACCEPTED` v7, `PARTIALLY ACCEPTED` v5, `NOT ACCEPTED` v5, `NOT ALLOCATED` v3.
- Ownership evidence: 20 request unchanged, 20 target-owner persisted, 5 scheduled projected.
- SMTP/outbox: six Sent messages, retry zero; zero SQMS tasks and SLA schedules.
</verification>

<infrastructure_state>

- Worktree: `C:\projects\cdt-workflow\.worktrees\sqms_projection_ownership`
- Branch: `sqms_projection_ownership`
- Verified implementation/results head before this continuity commit: `cbaf972`
- Database: `workflowengine_sqms_projection_ownership_live_acceptance`
- smtp4dev: launcher PID 9224, child PID 18996, SMTP 3027, UI 5002.
- Retained mailbox: `C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-projection-ownership-live-acceptance-20260822`
</infrastructure_state>

<remaining_work>

- No implementation work remains for this correction.
- Do not rerun the one-shot launcher or delete the retained database/mailbox.
- No merge, push, deployment, production SQL, scheduler hosting registration, or process shutdown has been performed.
- The external target API must adopt target-owned persistence after successful request Start/Action calls before taking this build.
</remaining_work>

<next_action>
Review and integrate branch `sqms_projection_ownership` when approved. Use verification-only mode and SQL verifier `008_verify_sqms_projection_ownership_live_acceptance.sql` for retained evidence; never replay the one-shot runner.
</next_action>
