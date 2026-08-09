# Codex Handoff

Last updated: 2026-08-07T08:35:00-04:00

## 2026-08-07 Application-Anchored Recurring SLA Implementation

### Current goal

- Application-anchored recurring SLA implementation and live acceptance are complete on `feature/application-anchored-recurring-sla`.
- Leave smtp4dev and the Workflow API running for user review on ports 5000/3025 and 5058.

### Completed changes

- Added typed `slaInput.targetDate` to core/API start and action contracts; SLA configuration fields are rejected as unmapped input.
- Added `DurationFromTaskCreation` and `ApplicationTargetDate` calculations using `TimeProvider` and independent `TimeZoneInfo` conversion for each calendar occurrence.
- Added versioned `WorkflowTaskSlaSchedule` persistence and occurrence-level outbox identity/cancellation fields with clean-schema EF migration `20260807032848_AddApplicationAnchoredRecurringSla`.
- Seeded Trading Desk review for configurable 09:00 `America/New_York`, due-today on target date, overdue target + 1 calendar day, and daily calendar recurrence. Other SLA tasks retain configured two-minute duration mode.
- Integrated schedule create/no-op/reschedule/complete behavior into the existing workflow transaction. Changed dates supersede schedules and cancel undelivered old occurrences while preserving sent history.
- Added actionless SLA synchronization through the existing `POST /v1/WorkflowAction`; no second API, state movement, workflow version increment, or workflow audit.
- Replaced task-level one-time scanning with schedule/date/kind occurrences, local-time eligibility, weekend recurrence, current-only catch-up, and completion stop behavior.
- Added post-claim inactive-schedule suppression so stale outbox rows become `Cancelled` before sender invocation.
- Fixed the pre-existing fresh-clone test harness pipe hang by disabling child MSBuild reuse and draining stdout/stderr concurrently.
- Fixed the actionless reschedule response so `actionResult.BusinessObject` is populated from persisted workflow context instead of returning empty strings.
- Updated the sample contract, `PROJECT.md`, README, API boundary, HTML handoffs, and coverage audit for the implemented behavior.

### Changed files

- Core contracts/application: `src/WorkflowEngine.Application/**`
- Persistence/SLA/notification runtime: `src/WorkflowEngine.Infrastructure/**`
- API models/services/tests: `workflow-api/**`
- Migration and seed: `src/WorkflowEngine.Infrastructure/Persistence/Migrations/**`, `Persistence/Seed/**`
- Samples/tests/docs: `samples/**`, `tests/**`, `PROJECT.md`, `README.md`, `docs/**`, this handoff.

### Build/test status

- Both solution restores passed and both warning-as-error builds passed with 0 warnings/errors.
- Full core suite: 287 passed, 4 optional SMTP skipped, 0 failed, 291 total, in 1m16s.
- Full Workflow API suite after the final response-only regression fix: 51 passed, 0 skipped/failed. Its focused red/green test first failed with the empty business object, then passed after the fix.
- Focused recurring SLA/outbox suite: 14 passed.
- Documentation/coverage/sample contract suite: 24 passed.
- EF `has-pending-model-changes`: no model changes since the latest migration.
- Opt-in SMTP suite: 4 passed, including proof that an inactive-schedule row is not delivered.
- Clean database `cdt_workflow_sla_live_20260807`: `init-db` and `verify-seed` passed without changing the existing application database.

### Live acceptance evidence

- Actual HTTP instance: `WF-PT-SLA-LIVE-20260807`, business object `PT-SLA-LIVE-20260807`.
- `CheckerSendToTradingDesk` created task 2 and schedule 2/version 1 for target `2030-01-20`; assignment email was sent immediately.
- Fast-forwarded `run-sla` sent due-today on `2030-01-20` and overdue on `2030-01-21`; a repeated Jan 21 scan queued zero duplicates.
- Jan 22 overdue occurrence 5 was deliberately left pending. Actionless `POST /v1/WorkflowAction` changed the target to `2030-01-25`, kept workflow state/version at `TradingDeskReview`/3 and audit count at 2, superseded schedule 2, cancelled occurrence 5 with `SlaScheduleSuperseded`, and created schedule 3/version 2.
- Revised due-today was sent on Jan 25. `TradingReturnToMaker` completed schedule 3; a Jan 26 scan found zero active SLA schedules and queued zero reminders.
- smtp4dev contains six acceptance messages: two assignment messages, original due-today, first overdue, revised due-today, and return-to-maker. The error log is empty.
- Review services are running against the preserved acceptance database: Workflow API `http://localhost:5058` on PID 7640; smtp4dev UI `http://localhost:5000` and SMTP 3025 on PID 10496.

### Full-path live acceptance rerun

- Actual HTTP instance `WF-PT-FULL-LIVE-20260807-01` completed this route: Maker submit, Checker return/reject, Maker resubmit, Checker approve/send to Trading Desk, Trader execute, and BDRM fulfill/approve.
- The application supplied original target `2030-03-10` on Maker submit and revised target `2030-03-15` on Maker resubmit. Duration-mode Checker schedules correctly ignored target-date authority; Checker approval forwarded the current revised date and created the Trading Desk `ApplicationTargetDate` schedule for `2030-03-15`.
- The final instance is `Completed` at version 7. All five tasks and all four SLA schedules are completed. The Trading Desk schedule persisted due-today `2030-03-15T13:00:00Z` and overdue start `2030-03-16T13:00:00Z`, reflecting 09:00 Eastern daylight time.
- Six audit rows record the exact route and six outbox rows (IDs 8-13) are `Sent` with no errors. smtp4dev received exactly six new messages, increasing from 27 to 33; API and smtp4dev error logs remain empty.
- A post-completion `run-sla --now 2030-03-17T13:00:00Z` found zero pending tasks with active SLA and queued zero reminder occurrences.

### Remaining tasks

- None for the approved SLA scope. The feature branch is ready for review/integration; runtime log files remain untracked under `.runtime/` while the API is running.

### Known risks

- The migration intentionally has no legacy SLA backfill and requires a clean latest-schema application database.
- SQL Integrated Security commands require the normal Windows token; sandbox identity produces SSPI failures.
- The original checkout has user-owned `.tools/`, `SQMS/`, `tareget-api-integration/`, and handoff changes. Feature work remains isolated in `.worktrees/application-anchored-recurring-sla`.

### Next-session prompt

```text
Review or integrate the completed application-anchored recurring SLA in C:\projects\cdt-workflow\.worktrees\application-anchored-recurring-sla on feature/application-anchored-recurring-sla. Read AGENTS.MD, PROJECT.md, and the top handoff section. Use the dedicated database cdt_workflow_sla_live_20260807 for preserved acceptance evidence; do not destructively recreate the existing application database.
```

## 2026-08-06 Session Pause Status

### Active branch and remote state

- Active checkout: `C:\projects\cdt-workflow`
- Active branch: `console-code-first-0518`
- Latest pushed product/bundle commit: `92c42de chore: update portable repository bundle`.
- This pause handoff is committed locally after `92c42de`; unless separately pushed, the active branch will be one handoff-only commit ahead of `origin/console-code-first-0518`.
- The completed feature worktree still exists at `.worktrees/multiple-notification-recipients` on `feature/multiple-notification-recipients` at `f60f40e`; its commits are already merged into the active branch, so it is retained only as an optional cleanup item.

### Work completed today

- Completed and verified typed multiple-`To`/multiple-`Cc` recipient support across workflow start/action contracts, task and outbox snapshots, SLA reminder processing, console inspection, SMTP delivery, and Workflow API dispatch.
- Preserved one outbox row and one message-level retry lifecycle for all recipients; per-recipient delivery tracking was intentionally not added.
- Kept recipient selection and email-address validation in CDT/SQMS/calling applications. The generic engine preserves the supplied ordered arrays and does not normalize, deduplicate, resolve, or validate them.
- Merged `feature/multiple-notification-recipients` into `console-code-first-0518` as `f55470d merge: multiple notification recipients`.
- Validated `restore-repo.ps1`: PowerShell syntax passed, 312 files restored, and SHA-256 `f0582eb80a27b08ab462d443ba9613959789ff60eee644839ea1a66d00827968` matched.
- Committed and pushed the bundle as `92c42de chore: update portable repository bundle`.
- Post-merge engine and Workflow API builds passed with 0 warnings and 0 errors.

### SQMS integration status

- SQMS integration is **not implemented yet**. There are no SQMS-specific engine code, seed, migration, contract, sample, or test changes.
- All five source files are present under the untracked `SQMS/` folder and were previously reviewed as business-rule evidence.
- The current source-backed direction is two metadata-driven workflow definitions, `SqmsOpportunity` and `SqmsBid`, plus a thin coordinator for timed close, submitted-bid classification, aggregation, and normal `System` workflow actions.
- The existing SQMS application remains responsible for opportunity/bid business records, parent-child queries, field validation, contextual authorization, Seller delegation, allocation-state selection, and recipient validation.
- The generic engine remains responsible for metadata transitions, context, tasks/SLA, outbox, audit, idempotency, and concurrency.
- The detailed Opportunity/Bid transition flow still needs explicit user review/confirmation before an SQMS implementation plan or code work begins.
- The multiple-recipient work completed today is a generic prerequisite SQMS can use later; it is not the SQMS integration itself.

### Verification and workspace state at pause

- Latest verified feature suites before merge: engine 252 passed, 3 smtp4dev-gated skipped; SQL-enabled Workflow API 50 passed.
- Latest post-merge builds: both solutions passed with 0 warnings/errors.
- No `dotnet` or `testhost` process is running at pause.
- Local uncommitted/untracked material to preserve: `SQMS/` and `tareget-api-integration/`.
- Known separate dependency issue: normal audited Workflow API restore reports transitive `Microsoft.OpenApi 2.4.1` advisory `NU1903` through `Swashbuckle.AspNetCore 10.1.7`.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD and PROJECT.md first, then CODEX_HANDOFF.md and .planning/.continue-here.md. The multiple-To/multiple-Cc feature and base64 bundle are merged and pushed through 92c42de; a later local commit may contain only this pause handoff. Do not redo the recipient feature. SQMS-specific integration has not started. Review the source-derived SqmsOpportunity/SqmsBid transition flow and thin-coordinator boundary recorded in CODEX_HANDOFF.md with the user. Do not create SQMS code, seeds, migrations, contracts, samples, tests, or an implementation plan until the user explicitly confirms the detailed flow and authorizes implementation. Preserve the untracked SQMS/ and tareget-api-integration/ folders.
```

## 2026-08-05 Multiple-Recipient Implementation Status

### Current goal

The approved typed multiple-To/multiple-Cc notification design has now been implemented and verified. The work is isolated from the original checkout on:

- Branch: `feature/multiple-notification-recipients`
- Worktree: `C:\projects\cdt-workflow\.worktrees\multiple-notification-recipients`
- GSD summary: `.planning/quick/260803-vp1-implement-typed-multiple-to-and-cc-notif/260803-vp1-SUMMARY.md`

The original `console-code-first-0518` checkout still contains the user's unrelated `CODEX_HANDOFF.md` edits and untracked `SQMS/` and `tareget-api-integration/` material. Do not discard or overwrite those changes when integrating the feature branch.

### Completed changes

- Added typed ordered `notificationRecipients.toEmailAddresses` and `ccEmailAddresses` arrays to workflow start/action contracts and the canonical idempotency hash.
- Added required `NotificationRecipientsJson` snapshots to `WorkflowTask` and `NotificationOutbox` with migration `20260803120000_AddNotificationRecipientSnapshots`.
- Stored one exact snapshot on the created task and initial outbox row; `run-sla` copies the task snapshot unchanged into its reminder row.
- Removed runtime singular/delimited/payload/SMTP-role-map recipient fallback. CDT/SQMS own address selection and validation.
- Updated MailKit delivery to add every To/Cc address to one message with one outbox status/retry lifecycle. `Smtp:RecipientOverride` suppresses original recipients and records intended lists diagnostically.
- Updated Workflow API models/services/dispatcher, console inspection, samples 02-10, PROJECT.md, README/context, target/API handoffs, migration deployment guidance, and regression tests.
- Fixed explicit-null recipient arrays so malformed caller shapes reach message-level retry rather than crashing workflow start.
- Fixed the FreshClone test harness to build into isolated temporary artifacts instead of overwriting DLLs loaded by Windows testhost.

### Changed files and commits

The implementation changes 67 files. Commits after the plan commit `dc0c8fb`:

```text
8089ffd feat: support multiple notification recipients
ce0cca1 test: strengthen recipient snapshot diagnostics
db46e0c feat: expose multiple recipients through API
4bc34e4 test: cover API notification dispatch
790569c fix: preserve malformed recipient snapshots on start
bd106a0 test: correct recipient integration assertions
172b8f3 test: isolate fresh clone build artifacts
f60f40e docs: complete multiple recipient quick task
```

### Build and test status

- `dotnet restore src\WorkflowEngine.sln -m:1`: passed.
- `dotnet build src\WorkflowEngine.sln -m:1 --no-restore`: passed, 0 warnings/errors.
- `dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 --no-restore`: passed, 0 warnings/errors.
- Full SQL-backed engine suite: 252 passed, 0 failed, 3 skipped smtp4dev-gated tests.
- SQL-enabled Workflow API suite: 50 passed, 0 failed, 0 skipped.
- Both formerly failing SQL recipient tests and both return-branch recipient assertions pass.
- Normal Workflow API restore reports existing transitive `Microsoft.OpenApi 2.4.1` advisory `NU1903` through `Swashbuckle.AspNetCore 10.1.7`. This feature added no packages; dependency remediation remains separate work.

### Seed and sample verification

Used dedicated database `cdt_workflow_multiple_recipient_verify`, leaving the existing application database unchanged:

- `init-db`: passed.
- `verify-seed`: passed.
- Started `WF-PT-1001` and executed `samples\workflow-actions\02-submit-for-checker.json`: passed.
- Initial `WorkflowTaskAssigned` row persisted two ordered To and two ordered Cc addresses.
- `run-sla --now "2030-01-15T16:00:00Z"` queued one `SlaTaskOverdue` row with the identical task snapshot.
- `process-notification-outbox` with `Notifications__Sender=NoOp` processed both messages.
- Final inspection showed both rows `Sent` with the same ordered recipient arrays.

### Remaining tasks and known risks

- Choose how to integrate `feature/multiple-notification-recipients` into `console-code-first-0518` without disturbing the original checkout's user changes.
- Remediate the transitive `Microsoft.OpenApi 2.4.1` advisory separately if a clean audited Workflow API restore is required.
- Run the three opt-in `Category=Smtp` tests only when the local smtp4dev gate is enabled; automated MailKit construction, override, and malformed-address retry tests already pass.
- Before deploying the target API, apply the new EF migration; it intentionally deletes disposable `NotificationOutbox` and `WorkflowTask` runtime rows as previously approved.

### Next-session prompt

```text
Continue in C:\projects\cdt-workflow. Read AGENTS.MD and PROJECT.md first, then this handoff. The typed multiple-To/multiple-Cc notification implementation is complete on feature/multiple-notification-recipients in .worktrees/multiple-notification-recipients, with summary f60f40e. Engine tests are 252 passed/3 smtp4dev-gated skipped; SQL-enabled API tests are 50 passed. Decide how to integrate the feature branch while preserving the original checkout's modified CODEX_HANDOFF.md and untracked SQMS/ and tareget-api-integration/ content. Do not discard those user changes. The only verification issue is the pre-existing transitive Microsoft.OpenApi 2.4.1 NU1903 on normal Workflow API restore.
```

## Current Goal

Target API integration is complete. The overall objective is to integrate `SQMS` behavior into the existing generic workflow engine, adding only the thin layers genuinely needed around it. SQMS is not a new engine or standalone platform. No SQMS implementation is approved yet.

Approved generic notification recipient design:

- The approved specification is `docs/superpowers/specs/2026-08-03-multiple-notification-recipients-design.md`.
- CDT, SQMS, and future calling applications supply and validate concrete recipient lists.
- One notification supports multiple `To` and multiple `Cc` addresses in a single outbox message.
- The engine stores the exact recipient snapshot on the created task and reuses it for the initial notification and later SLA reminder.
- The SLA monitor does not refresh recipients from CDT, SQMS, a directory, or another external API.
- The engine does not validate, normalize, deduplicate, resolve, or look up recipient addresses.
- Per-recipient delivery tracking is not required; delivery and retry status remain message-level.
- No backward compatibility or legacy runtime-data migration is required for singular recipients, delimited CC strings, or SMTP role-map resolution.
- Existing notification and in-flight task data may be discarded when the future implementation changes the contract/schema.
- The design was subsequently authorized, implemented, and verified on `feature/multiple-notification-recipients`; the status section above supersedes the earlier design-only gate.

Current SQMS state:

- All five current files under `SQMS/` were reread on 2026-07-29.
- `SQMS/Opportunity status.txt`, `SQMS/BidWorkflow.cs.txt`, and `SQMS/Bidstatus change.txt` are business-rule evidence for the discovery.
- `SQMS/Advantage data info.txt` and `SQMS/Opportunity setup.txt` remain explicitly excluded from workflow scope.
- `docs/sqms-workflow-questionnaire.html` is a discovery reference, not a requirement that the user answer all 33 questions. On 2026-07-29 the user clarified that the supplied SharePoint source and status flow should answer the design wherever they provide evidence.
- Derive the workflow from those sources first. Ask only about a contradiction or a rule that materially changes the transition matrix and is genuinely absent from the supplied evidence.
- Do not make code, seed, migration, contract, or implementation-plan changes until the source-derived transition matrices are reviewed and approved.
- Prefer an integration-only slice: SQMS metadata definitions, existing generic API usage, scheduled coordination where the legacy behavior is time-driven, samples, and tests.
- Add or generalize engine capabilities only when a source-backed SQMS requirement cannot be expressed through the existing generic metadata and execution contracts. Any such change must remain workflow-generic.
- Keep SQMS business records, field validation, record ownership, and opportunity-to-bid relationships in the existing application unless the supplied behavior proves the workflow engine must own something.
- Do not create SQMS-specific branches inside the reusable transition engine.

Current source classification:

- The files are legacy SharePoint/batch snippets with syntax and OCR defects. Treat them as business-rule evidence, not copyable implementation.
- `Opportunity status.txt` is the strongest status source. It covers the Pending Seller Approval notification path, open-to-bidding notifications/reminders, automatic close to bidding, and automatic bid classification to `NOT ALLOCATED` or `IPG REVIEW`.
- `BidWorkflow.cs.txt` performs bid/person synchronization and aggregate calculations; it is background processing rather than a user workflow transition.
- `Bidstatus change.txt` sends bid-disposition email and refreshes opportunity bid statistics. It does not establish the full authorization, reversibility, or opportunity-allocation rules.
- Scheduled reminders, automatic close, classification, and aggregation must remain distinct from human actions.
- The current reusable engine can support another workflow definition through metadata, but the current seed and verification services are still PortfolioTrade-specific.

Current discussion state:

- Confirmed integration architecture: add two metadata-driven workflow definitions, `SqmsOpportunity` and `SqmsBid`, to the existing generic engine, plus a thin SQMS coordinator for time-driven parent/child processing.
- The generic engine continues to own transition resolution, workflow context, tasks/SLA, notification outbox, audit, idempotency, and concurrency. The coordinator uses normal workflow actions; it does not update workflow state directly.
- The existing application remains authoritative for SQMS opportunity/bid records, parent-child lookup, contextual MFO/IPG/Seller authorization, field validation, and form behavior.
- Allocation-state selection is application/IPG-owned. The application validates its allocation data and submits the appropriate metadata action (`MarkAwaitingAllocation`, `MarkPartiallyAllocated`, or `MarkNotFulfilled`); neither the generic engine nor the SQMS coordinator calculates this outcome.
- The committed questionnaire over-expanded the discovery into 33 questions. Do not walk the user through it as an interview.
- Build a compact, source-derived Opportunity and Bid flow from the supplied status information and legacy code.
- Resolve project-level defaults from `PROJECT.md` and treat unsupported optional capabilities as out of scope rather than asking about them.
- Map each SQMS behavior onto an existing generic engine capability: definition metadata, start, context, execute action, task/SLA, notification outbox, audit, or scheduled coordinator.
- The architecture and ownership split looked good to the user. The detailed flow below was then presented for confirmation. No code changes are authorized yet.

## SQMS Flow Presented for Confirmation

### Opportunity workflow

```text
Draft
  -- MFO / SubmitOpportunity --> Submitted

Submitted
  -- IPG / ReturnToDraft --> Draft
  -- IPG / MarkPending --> Pending

Pending
  -- IPG / OpenBidding --> OpenToBidding

OpenToBidding
  -- System / CloseBidding after 15 calendar days --> ClosedToBidding

ClosedToBidding
  -- IPG / BeginSellerApproval --> PendingSellerApprovalIpg

PendingSellerApprovalIpg
  -- IPG via existing app / MarkAwaitingAllocation --> AwaitingAllocation
  -- IPG via existing app / MarkPartiallyAllocated --> PartiallyAllocated
  -- IPG via existing app / MarkNotFulfilled --> NotFulfilled

AwaitingAllocation, PartiallyAllocated, or NotFulfilled
  -- IPG / CompleteOpportunity --> Completed
```

- The existing application validates SQMS fields and sends only `actionKey`; metadata resolves the destination state.
- `ReturnToDraft` completes the IPG task, creates an MFO correction task, and queues a notification. Resubmission returns the opportunity to `Submitted`.
- The existing application/IPG owns the allocation outcome. Neither the workflow engine nor the SQMS coordinator calculates whether the opportunity is awaiting allocation, partially allocated, or not fulfilled.

### Bid workflow

```text
Draft
  -- MFO / SaveBid --> Draft
  -- MFO / SubmitBid --> Submitted

Submitted
  -- System during parent close / MarkNotAllocated --> NotAllocated
  -- System during parent close / MarkIpgReview --> IpgReview

IpgReview
  -- IPG / SendToSeller --> UnderReviewBySeller

UnderReviewBySeller
  -- Seller / AcceptBid --> Accepted
  -- Seller / PartiallyAcceptBid --> PartiallyAccepted
  -- Seller / RejectBid --> NotAccepted
```

- Each bid is an independent workflow instance linked to its opportunity through the existing application.
- Bid edits do not change workflow state. The submitting MFO/team can edit while the parent opportunity is open.
- When the parent closes, unsubmitted `Draft` bids stay `Draft`, become read-only in the existing application, and are excluded from automatic classification.
- Seller disposition is independent per bid. The existing application validates disposition data such as a partial-acceptance amount before invoking the action.

### Automatic close coordinator

1. Find `OpenToBidding` opportunities whose 15-calendar-day deadline has passed.
2. Read the application-owned opportunity and child-bid data.
3. Validate the committed amount, bid amounts, and classifications before moving workflow state.
4. Execute `CloseBidding` as `System` through the normal workflow action path.
5. Classify only `Submitted` bids.
6. Sum fair-value and premium bid amounts.
7. If that total is strictly greater than the committed sale amount, execute `MarkNotAllocated` for discount bids.
8. Execute `MarkIpgReview` for every submitted bid not marked `NotAllocated`.
9. Leave `Draft` bids unchanged.
10. Queue the close summary through the notification outbox.

The coordinator must use deterministic idempotency keys and normal workflow actions. It must never update workflow state tables directly. If the parent close succeeds and some child actions fail, the opportunity remains closed and a retry processes only unfinished child actions.

### Notifications and ownership

- Action-created notifications are persisted to the existing outbox; the coordinator does not send email directly.
- The original open-to-bidding notice and reminder are scheduled notifications, not workflow transitions.
- `BeginSellerApproval` queues the Seller notification. Seller disposition queues the bid-owner/team notification.
- The existing engine owns transition resolution, context, tasks/SLA, outbox, audit, idempotency, and concurrency.
- The existing application owns SQMS records, parent-child lookup, form behavior, field validation, contextual authorization, Seller delegation, and allocation-state selection.
- The thin SQMS coordinator owns only timed close/classification orchestration and invokes metadata-defined `System` actions.
- `SQMS/Advantage data info.txt` and `SQMS/Opportunity setup.txt` remain excluded.

### Current gate

- No material business-rule question remains identified from the supplied sources and decisions.
- The detailed flow above is awaiting explicit confirmation after being explained to the user.
- Do not write a design spec, implementation plan, seed data, migrations, contracts, tests, or code until the user confirms the flow and authorizes the next artifact.

Boundary for the next session:

- Keep SQMS metadata-driven.
- Do not hardcode SQMS business behavior inside the reusable workflow engine.
- Separate true user workflow steps/actions from scheduled jobs such as daily notifications or automatic close-to-bidding processing.

## Required Reading

1. `AGENTS.MD`
2. `PROJECT.md`
3. `context.md`
4. `CODEX_HANDOFF.md`
5. `NEXT_CODEX_PROMPT.md`
6. `SQMS/`
7. `docs/sqms-workflow-questionnaire.html`
8. `workflow-api/WORKFLOW_API_BOUNDARIES.md` if API contract questions come up
9. `TODO_WORKFLOW_API.md` when continuing API migration or operational follow-up

## Active Repo Shape

- `src/WorkflowEngine.sln`: reusable workflow engine, infrastructure, console/demo host, EF migrations, seed data, SLA, notification outbox, and SMTP sender.
- `workflow-api/WorkflowEngine.WorkflowApi.slnx`: active ASP.NET Core workflow API host.
- `tests/WorkflowEngine.Tests`: shared engine/infrastructure tests.
- `workflow-api/tests/WorkflowEngine.WorkflowApi.Tests`: active API host tests.
- `samples/workflow-actions`: Portfolio Trade sample action JSON.
- `docs/superpowers`: active design and implementation plans/specs.
- `.tools/smtp4dev`: retained local SMTP test tool folder.
- `SQMS/`: currently untracked legacy SQMS reference/source material; all five current files were reviewed on 2026-07-29.
- `docs/sqms-workflow-questionnaire.html`: committed requirements-discovery questionnaire and current decision surface.

The old tracked `api/` host has been removed from the working tree by user approval. Do not bring it back unless the user explicitly asks.

## Completed Cleanup

- Removed local/generated folders unrelated to the current source shape: `.dotnet/`, `.dotnet_home/`, `.nuget/`, `.templateengine/`, `.tmp/`, `.vs/`, `.\~`, and `console-dbfirst-clean/`.
- Preserved `.tools/smtp4dev`.
- Removed the old tracked `api/` host.
- Removed stale generated/historical docs that described the deleted host:
  - `docs/api-conversion/`
  - `docs/workflow-map/`
  - `docs/workflow-visualization.html`
  - `docs/console-to-api-integration-analysis.html`
- Removed stale root/extracted-copy artifacts:
  - `FULL_PROJECT_REVIEW.html`
  - `docs/BEGINNERS_GUIDE.md`
  - `cdt-src-bundle.ps1`
  - `cdt-src-bundle.sh`
  - `restore-repo.ps1`
- Updated `README.md` to describe the current `src/` plus `workflow-api/` shape and current local verification DB.
- Updated `PROJECT.md` to remove stale notification-delivery wording, align the local SQL database name, and match the current SMTP/outbox implementation shape.
- Rewrote `CODEX_HANDOFF.md`, `NEXT_CODEX_PROMPT.md`, and `context.md` around the current shape.
- Added `docs/README.md` to describe the remaining documentation surface.

## Beginner Handoff Guide

Added `docs/beginners-api-console-handoff.html` as the single-file beginner handoff requested for:

- current API and console project layout
- local console/API smoke-test commands
- workflow-only API request/response shapes
- target API integration steps
- action-created notification dispatch
- Task Scheduler setup for `run-sla` and `process-notification-outbox`
- database/migration and SMTP configuration notes

The guide intentionally distinguishes the local `workflow-api/` reference host from the external target API so future work does not drift back to the removed `api/` host.

## Latest Completed Changes

### SQMS discovery resumed

No workflow implementation code was changed for SQMS.

The current untracked SQMS source folder contains:

```text
SQMS/Advantage data info.txt
SQMS/Bidstatus change.txt
SQMS/BidWorkflow.cs.txt
SQMS/Opportunity setup.txt
SQMS/Opportunity status.txt
```

The full source pass is complete. The questionnaire is now a reference checklist only. Derive the flow from the supplied statuses and code, apply repository defaults, and surface only material rules that cannot be established from either source.

### Frontend workflow API handoff

Added `docs/frontend-workflow-api-handoff.html` as a frontend-focused handoff for the current reference API in `workflow-api/WorkflowEngine.WorkflowApi.slnx`.

The guide documents:

- `POST /v1/WorkflowStart`, `GET /v1/WorkflowContext`, and `POST /v1/WorkflowAction`.
- Required trusted actor headers.
- Current API JSON casing: lower-case wrapper properties with PascalCase model fields.
- Input and output parameter tables.
- The approved full happy path: Maker submit, Checker reject, Maker resubmit, Checker approve, Trading Desk reject, Maker save draft, Maker submit again, Checker approve, Trading Desk approve, BDRM approve/complete.
- The reference API boundary that `WorkflowAction` is workflow-only and does not save form payload data.

Updated `docs/README.md` so the new frontend handoff is discoverable.

### Per-record SMTP recipient resolution

The Maker recipient changes per business record. The code supports that without adding a SQL user table or changing public request DTOs:

- `SmtpNotificationSender` resolves recipients in this order: `Smtp:RecipientOverride`, then payload `recipientEmail`, then `Smtp:RoleEmailMap:{RecipientRoleKey}`.
- `SmtpNotificationSender` adds SMTP CC recipients from payload `ccEmailAddresses`, split by comma or semicolon.
- Override-mode bodies preserve `OriginalRecipientRoleKey`, `OriginalRecipientEmail`, and `OriginalCcEmailAddresses` when available.
- `Smtp:RecipientOverride` suppresses real CC delivery during smoke tests.
- Return-to-Maker persistence is covered by `WorkflowExecutionValidationTests.ReturnToMakerPersistsRecipientEmailFromNotificationContext`.
- SMTP recipient and CC behavior is covered by `SmtpNotificationSenderTests`.
- SLA CC persistence is covered by `Phase5SlaTests.RunSlaQueuesOnlyOverduePendingTasksWithoutExistingOverdueNotification`.
- `ConsoleNotificationSenderTests` and `tests/COVERAGE_AUDIT.md` were updated because PROJECT.md section 20 coverage audit is enforced by tests.

The target API should populate `workflow.notificationContext.recipientEmail` for Return-to-Maker and any other business-record-specific recipient. If the UI supplies copied recipients, the target API should pass them through as `workflow.notificationContext.ccEmailAddresses` so action-created and SLA overdue emails share the same CC list.

### HTML notification email rendering

The email format change is implemented in schema-backed metadata, not hardcoded Portfolio Trade logic:

- `NotificationRoute` now has `HtmlBodyTemplate`.
- `NotificationOutbox` now has rendered `HtmlBody`.
- Migration `20260620130000_AddHtmlNotificationBodies` adds both columns under schema `dbo`.
- The renderer has `RenderHtml(...)`, which HTML-escapes token values while preserving template-owned markup.
- Seeded notification templates include plain text and HTML versions for action-created and SLA overdue routes.
- Important UI-provided values are wrapped with `<strong>...</strong>` in seeded HTML templates, and `tradeLink` renders as `<a href="...">Open trade</a>`.
- `SmtpNotificationSender` sends `multipart/alternative` email when `HtmlBody` is present, with text/plain and text/html parts.
- Override-mode SMTP still suppresses real CC delivery and records original recipient/CC metadata in the test email body.

Pushed commits:

- `95c0d4a feat: support per-notification SMTP recipients`
- `cb0d65b test: verify per-record SMTP recipient with smtp4dev`

### Documentation updates

`PROJECT.md`, `README.md`, `docs/beginners-api-console-handoff.html`, `context.md`, and this file now document the recipient resolution order and target API handoff guidance.

Added `docs/target-api-notification-changes-handoff.html` as the precise target API integration checklist for the last two notification changes: per-record SMTP recipients/copied recipients and persisted HTML notification bodies. It lists target API files to update, corresponding source files in this repo, SQL column changes, request DTO shape, dispatch pattern, SMTP configuration, and verification steps.

### API JSON body and console real-usage handoff

Added `docs/api-json-bodies-and-console-real-usage.html` as the current one-file reference for:

- all public workflow API JSON body shapes for `POST /v1/WorkflowStart` and `POST /v1/WorkflowAction`;
- the no-body shape for `GET /v1/WorkflowContext`;
- trusted actor headers;
- the Portfolio Trade sample action flow and dynamic trading desk role keys;
- action-specific `notificationContext` examples;
- fields the target API must not send, including `toState`, form payload, UI echo fields, expected state/task/version guards, and generated IDs;
- console commands classified as real operational use, support diagnostics, or demo-only.

Updated `docs/README.md` so the new API JSON body handoff is discoverable.

### Restore bundle

`restore-repo.ps1` is the portable base64 PowerShell bundle for target-api handoff. It restores the repository source/handoff content without `.git`, `bin`, `obj`, `.tools`, local caches, logs, or the bundle script itself.

## Notification Template Feature State

Latest feature commit before cleanup: `a0e5640 docs: record notification template verification`.

The generic notification-template feature is implemented and verified:

- Notification subject/body templates live in SQL metadata through `NotificationRoute`.
- Portfolio Trade/Directed Trade wording is seed/sample metadata only.
- The reusable engine stays generic and renders `{{tokenName}}` placeholders from caller-provided notification context.
- Workflow action and workflow start API requests accept optional `notificationContext`.
- SLA overdue notifications reuse the route/context snapshotted on the created task and prefix the subject with metadata, currently `Overdue: `.
- `returnReason` was removed from all seeded templates.
- SMTP sends persisted outbox body content.

## Current Operational Notes

- Local API and console testing use:

```text
Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False
```

- Trusted local actor headers for the workflow API remain:

```text
X-Workflow-UserId
X-Workflow-Roles
X-Workflow-DisplayName
```

- `POST /v1/WorkflowAction` commits workflow/task/audit/outbox data before SMTP delivery. If delivery fails, the retry path is `process-notification-outbox`.
- Normal action notifications are API-owned: after the workflow action execution returns success and queues notification rows, the target API should dispatch the rows created by that action.
- The action dispatcher should select by `WorkflowInstanceId` plus `CorrelationId`, matching the existing `WorkflowActionNotificationDispatcher` pattern in `workflow-api/src/WorkflowEngine.WorkflowApi/Services/`.
- The hosted/scheduled SLA service should call `IRunSlaService.RunAsync(...)` against the workflow DB. It should not call an external API to rebuild overdue email context; the task snapshot carries the notification route and context.
- Task Scheduler is available on the target machine. Preferred processor shape:

```powershell
dotnet .\WorkflowEngine.Console.dll run-sla
dotnet .\WorkflowEngine.Console.dll process-notification-outbox
```

- Keep `RecipientOverride` configured during live SMTP smoke testing so real users do not receive test email.

## Current Worktree Expectations

Expected local state after this pause:

- `SQMS/` may be untracked while the user is adding remaining source files.
- No SQMS implementation edits should exist yet.
- Handoff/context files may contain this SQMS pause state.

Run before further edits:

```powershell
git status --short --untracked-files=normal
git diff --stat
git diff --name-only
```

## Verification Status

Latest bundle and push-prep verification on 2026-06-22:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath .\.bundle-test\restore-20260622-225258 -Force
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false
git diff --check
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "Category!=FreshClone" -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
```

Results:

- `restore-repo.ps1` regenerated as a base64 ZIP PowerShell restore bundle.
- Bundle selected 294 files and excluded `.git`, `.tools`, `.bundle-test`, `.bundle-work`, build outputs, caches, and the previous restore script.
- Bundle SHA-256 verified: `d6ca44471252e6af0a7a6753dc7f8a6521531ce61795fe0fe11c118b0005d85b`.
- Restored file count matched: 294 files.
- Required target API handoff/source files were present in the restored tree; forbidden paths were absent.
- Restored `workflow-api\WorkflowEngine.WorkflowApi.slnx` warnings-as-errors build passed with 0 warnings/errors.
- Main tree `git diff --check` was clean except Git line-ending warnings.
- Main tree shared solution warnings-as-errors build passed with 0 warnings/errors.
- Main tree workflow API warnings-as-errors build passed with 0 warnings/errors.
- Workflow API tests passed: 41 passed.
- Core tests excluding known long `FreshClone` walkthrough category passed: 229 passed, 3 skipped SMTP integration tests.
- Full unfiltered core test command timed out under the tool after 5 minutes and left stale test processes; those PIDs were stopped before rerunning the filtered core suite.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.

Latest retest of the last two notification changes on 2026-06-22:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "NotificationTemplateRendererTests|SmtpNotificationSenderTests|CoverageAuditTests" -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "SeedDataSmokeTests|WorkflowTransitionTests|Phase5SlaTests" -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- init-db
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
git diff --check
```

Results:

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed when run outside the sandbox with normal Windows credentials.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and Portfolio Trade seed data.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.
- `git diff --check` was clean except Git line-ending warnings.
- Note: SQL-backed commands fail under the sandbox identity `dolly\codexsandboxoffline` with `Cannot generate SSPI context`; run SQL Integrated Security gates outside the sandbox.

Latest verification after the HTML notification email change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "NotificationTemplateRendererTests|SmtpNotificationSenderTests|CoverageAuditTests" -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter "SeedDataSmokeTests|WorkflowTransitionTests|Phase5SlaTests" -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- init-db
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- start-workflow PortfolioTrade PT-1001 --user maker1 --reset
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- execute-json .\samples\workflow-actions\02-submit-for-checker.json
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
```

Results:

- Focused renderer/SMTP/audit tests passed: 19 passed.
- SQL-backed seed/transition/SLA tests passed: 19 passed.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `init-db` applied the schema and seed.
- `verify-seed` passed.
- Sample JSON `samples/workflow-actions/02-submit-for-checker.json` executed successfully and queued a notification.
- Full core test project passed: 235 passed, 3 skipped SMTP integration tests.

Latest verification for the frontend handoff documentation slice:

- `dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore` passed: 41 passed, 0 failed, 0 skipped.
- `git diff --check` reported only Git line-ending warnings for edited markdown files, with no whitespace errors.

Latest verification for the API JSON body and console real-usage documentation slice:

- Targeted text check confirmed the new HTML contains the current endpoints, key action names, dynamic Trading Desk role key, and console outbox command.
- `git diff --check` passed with no whitespace errors; it reported only Git line-ending warnings for edited files.

Latest verification after the per-record recipient change:

```powershell
dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter SmtpNotificationSenderTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter WorkflowExecutionValidationTests -m:1 /p:UseAppHost=false --no-restore
$env:WORKFLOW_ENGINE_TEST_CONNECTION_STRING='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj -m:1 /p:UseAppHost=false --no-restore
dotnet build src\WorkflowEngine.sln -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet build workflow-api\WorkflowEngine.WorkflowApi.slnx -warnaserror -m:1 /p:UseAppHost=false --no-restore
dotnet test workflow-api\WorkflowEngine.WorkflowApi.slnx -m:1 /p:UseAppHost=false --no-restore
$env:ConnectionStrings__DefaultConnection='Server=DOLLY;Database=cdt_workflow_api_20260527;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'; dotnet run --project src\WorkflowEngine.Console --no-restore -- verify-seed
git diff --check
```

Results:

- `SmtpNotificationSenderTests` passed: 7 passed.
- `WorkflowExecutionValidationTests` passed: 19 passed.
- Full core test project passed: 231 passed, 2 skipped SMTP integration tests.
- Shared solution warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API warnings-as-errors build passed with 0 warnings/errors.
- Active workflow API tests passed: 41 passed.
- `verify-seed` passed.
- `git diff --check` was clean except Git line-ending warnings.

Latest smtp4dev verification:

```powershell
RUN_SMTP_TESTS=1 dotnet test tests\WorkflowEngine.Tests\WorkflowEngine.Tests.csproj --filter Category=Smtp -m:1 /p:UseAppHost=false --no-restore
```

Result: 3 passed, 0 failed, including per-record `recipientEmail` delivery to `maker.one@example.test`.

Latest restore-bundle verification:

- `restore-repo.ps1` was generated from commit `cb0d65b`.
- Restored into a temp folder using `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\restore-repo.ps1 -DestinationPath <temp> -Force`.
- Restored file count matched source-selected count.
- Source/restored SHA-256 comparison found no mismatches.
- Required target-api handoff files were present.
- Forbidden paths were absent: `.git`, `bin`, `obj`, `.tools`, local caches, logs, temp files, and `restore-repo.ps1`.

## Remaining Work

- Obtain explicit user confirmation or corrections for the SQMS flow recorded above.
- After confirmation, update the written SQMS design/decision artifact only if the user authorizes that documentation step.
- Do not create an implementation plan or touch code, seed data, migrations, contracts, samples, or tests until separately authorized.
- For the target API integration, ensure Return-to-Maker request construction includes `notificationContext.recipientEmail`.
- Keep `Smtp:RecipientOverride` configured during live SMTP smoke testing, then remove it only when ready to send to real per-record or role-map recipients.
- For target-machine operations, publish/copy the workflow console processor, configure DB/SMTP settings, and schedule `run-sla` followed by `process-notification-outbox` in Windows Task Scheduler.
- Replace trusted-header actor resolution with the target auth/claims resolver when integrating into the real host.
- Integrate EF migrations into the target deployment process.

## Next Session Prompt

```text
Continue the SQMS design discussion in C:\projects\cdt-workflow on branch console-code-first-0518. Read AGENTS.MD and PROJECT.md first, then CODEX_HANDOFF.md. The supplied SQMS sources have already been reviewed; do not restart the 33-question questionnaire or re-derive settled decisions unless a source conflict appears. The approved direction is integration-only: two metadata-driven definitions (`SqmsOpportunity` and `SqmsBid`) plus a thin timed-close coordinator around the existing generic engine. The existing application/IPG owns allocation-state selection. Review the exact opportunity, bid, automatic-close, notification, and ownership flow in `CODEX_HANDOFF.md` with me. Make no code, seed, migration, contract, test, sample, or implementation-plan changes unless I explicitly authorize them.
```

## 2026-08-09 Optimized UI Workflow API Envelope

### Current goal

- Completed the approved UI-facing `WorkflowStart` and `WorkflowAction` payload optimization on `console-code-first-0518`.
- Kept the reusable engine generic and metadata-driven while moving definition-owned and server-derived values out of the browser body.

### Completed changes

- Public Start now exposes exactly `workflowKey`, `businessObjectId`, optional `actionKey`, optional flat `slaTargetDate`, and optional `notification`.
- Public Action now exposes exactly `workflowInstanceId`, optional `actionKey`, optional `assignedRoleKey`, optional flat `slaTargetDate`, and optional `notification`.
- Unknown root/nested fields are rejected; obsolete body fields such as business-object type, body idempotency, nested SLA input, legacy notification context, and final recipient arrays are no longer accepted publicly.
- Added definition metadata for business-object type, workflow participant definitions/snapshots, and ordered notification recipient rules.
- Start derives business-object type and captures the required immutable `originalMaker` participant once.
- Metadata resolves StaticAddress, WorkflowParticipant, and RequestInput sources into final To/Cc snapshots. Resolution uses rule/source order, case-insensitive duplicate comparison, first opaque value retention, and To-over-Cc precedence.
- Opaque address strings are stored without syntax, directory, mailbox, SMTP, trimming, or rewriting validation.
- Immediate outbox rows and tasks store the same final snapshot; SLA reminders reuse the task snapshot byte-for-byte.
- The API generates client-action, correlation, and idempotency identity inside the trusted boundary.
- Preserved the console-only `WorkflowActionEnvelope` and existing console sample fixtures.
- Added current public examples under `workflow-api/samples` and replaced stale HTML integration handoffs.

### Changed files

- Public API DTOs/services/tests under `workflow-api/src` and `workflow-api/tests`.
- Application notification contracts, participant capture, canonical hashing, and recipient resolver.
- Workflow execution and Start integration.
- EF entities/configuration/migration/seed verification for definition type, participants, and recipient rules.
- SQL-backed Start/Action/SLA regression tests.
- `PROJECT.md`, `README.md`, `workflow-api/WORKFLOW_API_BOUNDARIES.md`, current design/plan artifacts, and four integration handoff HTML files.

### Build and test status

- Core warnings-as-errors build: passed with 0 warnings/errors.
- Core full SQL-backed suite: 308 passed, 0 failed, 4 smtp4dev-only tests skipped.
- Workflow API warnings-as-errors build: passed with 0 warnings/errors.
- Workflow API full suite: 66 passed, 0 failed, 0 skipped.
- `init-db` and `verify-seed`: passed against `cdt_workflow_api_20260527`.
- Required Checker Return, Trading Desk Return, SLA, notification inspection, and 14-step demo commands: passed.
- Live NoOp HTTP verification: Start moved to CheckerReview; Checker Return moved to ReturnedToMaker. SQL showed the captured Maker address and selected addresses retained surrounding spaces, with each task snapshot matching its immediate Sent outbox row.
- The live API process was stopped and port 5058 was verified available.

### Known risks and open items

- The migration intentionally clears workflow runtime rows because backward compatibility was explicitly not required; metadata and mock business data remain.
- Four smtp4dev integration tests remain opt-in/skipped because no live fake SMTP service was requested for this verification. Unit and SQL tests cover message-level retry behavior.
- The local verification database now contains the final console/demo/live-HTTP evidence rows.
- Existing unrelated untracked `.tools`, `SQMS`, and `tareget-api-integration` content was preserved.

### Next-session prompt

```text
Continue UI integration from the optimized Workflow API contract on console-code-first-0518. Read AGENTS.MD, PROJECT.md, workflow-api/WORKFLOW_API_BOUNDARIES.md, and the 2026-08-09 handoff section. Use workflow-api/samples for public bodies; do not use samples/workflow-actions as HTTP payloads. Start must send the resolved immutable originalMaker. Actions may send only metadata-declared recipient input keys such as selectedCcUsers. The engine owns final To/Cc placement, state transition, SLA configuration, correlation, and idempotency identity.
```
