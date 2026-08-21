# SQMS Live Acceptance Design

**Date:** 2026-08-21

**Status:** Approved interactively on 2026-08-21; implementation planning pending written-spec review

**Target branch:** `sqms_clean_slate`

## 1. Purpose

Run the complete SQMS Opportunity and Bid workflow through compiled application code, a retained SQL Server database, and local smtp4dev. This is an operational acceptance exercise, not an xUnit test. It must leave inspectable target rows, workflow state, audit history, notification outbox records, and received messages.

The run covers three new Opportunities and fifteen new Bids across every approved Opportunity and Bid state/action branch, scheduled processing, applicable email routes, target/workflow state parity, and the calculated-column exclusion.

## 2. Isolation and ownership

The acceptance run uses only:

```text
Server=DOLLY
Database=workflowengine_sqms_live_acceptance
```

It must not modify or reset the existing `workflowengine` database. The dedicated database and all acceptance records remain after the run.

The target API is not present as an SQMS HTTP surface in this repository. A dedicated non-production console project therefore acts as the target-application acceptance driver. It inserts target fixtures and calls the same production `ISqmsWorkflowService`, `ISqmsScheduledService`, notification outbox processor, SQL store, and generic workflow services used by the integration. It does not add an HTTP endpoint or a command to the production console.

## 3. Components

### 3.1 Acceptance runner

Add `tools/WorkflowEngine.Sqms.LiveAcceptance`, a .NET 10 console project with project references only to existing production projects. It introduces no third-party package and is not registered in production dependency injection.

The runner:

- requires the exact database name `workflowengine_sqms_live_acceptance`;
- requires a unique run marker, fixed for this acceptance as `SQMS-LIVE-20260821`;
- refuses to start if target rows containing that marker already exist;
- inserts three Opportunity and fifteen Bid fixtures using explicit SQL and captures their generated identity IDs;
- invokes production SQMS services with trusted acceptance actors and recipients;
- uses an acceptance-only controllable `TimeProvider` to exercise calendar events without editing audit timestamps or waiting sixteen days;
- prints every input, expected state/version, authoritative result, target projection, and failure;
- exits nonzero at the first unexpected operation result or at final evidence mismatch;
- never cleans up committed acceptance data.

### 3.2 Setup orchestration

Add a PowerShell acceptance script that performs the setup in this exact order:

1. Refuse to continue if any selected database name differs from `workflowengine_sqms_live_acceptance`.
2. Create the dedicated database through the existing local `init-db` command.
3. Apply `sql/sqms/testing/000_create_sqms_test_tables.sql` to supply the approved target table shapes in this isolated database.
4. Apply production `001_sqms_notification_outbox_ddl.sql`.
5. Apply production `002_sqms_workflow_metadata_dml.sql` with acceptance-only recipient arrays.
6. Run production `003_verify_sqms_deployment.sql` and `verify-seed`.
7. Start the dedicated smtp4dev process.
8. Build and run the acceptance runner.
9. Run the read-only SQL verifier.

The script must not use `--reset`, drop a database, delete target rows, or silently reuse an existing acceptance marker. If the dedicated database already exists, setup stops and reports the manual review required; it does not overwrite retained evidence.

### 3.3 smtp4dev

Use the checked-in local tool launcher:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\smtp4dev.cmd
```

Start it hidden with:

- SMTP endpoint `localhost:3025`;
- web UI `http://localhost:5000`;
- a new database under `C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821`;
- dedicated stdout/stderr logs in the same directory;
- no relay and no real recipients.

The acceptance application uses `Notifications:Sender=Smtp`, `Smtp:Host=localhost`, `Smtp:Port=3025`, and `Smtp:From=workflow-sqms-live@example.test`. No recipient override is used because recipient placement itself is under acceptance.

### 3.4 Read-only verification SQL

Add `sql/sqms/testing/005_verify_sqms_live_acceptance.sql`. It accepts or embeds the fixed run marker and returns human-readable evidence without changing data.

Its result sets cover:

1. the three target Opportunities and final states;
2. the fifteen target Bids grouped by parent and final state;
3. target rows beside workflow instance state, version, and runtime status;
4. ordered audit history for all eighteen workflows;
5. target/workflow state mismatches, requiring zero rows;
6. SQMS task and SLA rows, requiring zero rows;
7. outbox route, To/Cc snapshot, subject, links, sender, delivery state, retry count, and timestamps;
8. expected-versus-actual scenario and email counts;
9. parent Opportunity state evidence across Bid disposition processing;
10. the seeded sentinel values in `TotalBid`, `LargestBid`, and `LowestBid`.

## 4. Acceptance identities and configuration

Use stable acceptance actors:

| Role | User ID | Display name |
|---|---|---|
| MFO | `sqms-live-mfo` | `SQMS Live MFO` |
| IPG | `sqms-live-ipg` | `SQMS Live IPG` |
| Seller | `sqms-live-seller` | `SQMS Live Seller` |
| System | `sqms-live-system` | `SQMS Live Scheduler` |

Use acceptance-only static recipient metadata:

| Input | Value |
|---|---|
| Open To | `sqms-open.live@example.test` |
| Open Cc | `sqms-audit.live@example.test` |
| Seller-approval Cc | `sqms-audit.live@example.test` |
| Bid-disposition Cc | `sqms-audit.live@example.test` |

Use target-resolved addresses that remain distinguishable in smtp4dev:

- Relationship Managers: `rm-a.live@example.test`, `rm-b.live@example.test`, and `rm-c.live@example.test`.
- Seller teams: `seller-a.live@example.test`, `seller-b.live@example.test`, and `seller-c.live@example.test`.
- Buyer relationship managers and buyer teams use unique `bid01` through `bid15` addresses.

Use acceptance URLs:

```text
OpenBiddingDashboardUrl=https://sqms-acceptance.example.test/open-bidding?source=live
SellerApprovalBidsDashboardUrl=https://sqms-acceptance.example.test/bids?view=seller
BidFormUrl=https://sqms-acceptance.example.test/bid?source=live
```

The open link remains unchanged. Seller approval appends URL-encoded `opportunityId`; Bid disposition appends URL-encoded `bidId`. Existing query values must remain intact.

## 5. Scenario matrix

Every target row contains `SQMS-LIVE-20260821` in a searchable account/reference field. The runner captures generated IDs rather than assuming identity values.

### 5.1 Opportunities

| Scenario | Workflow path | Expected terminal target state | Expected runtime status |
|---|---|---|---|
| A: full allocation | Submit, Return to Draft, Resubmit, Pending, Open, scheduled Close, Pending Seller Approval, Awaiting Allocation, Complete | `COMPLETED` | `Completed` |
| B: partial allocation | Submit, Pending, Open, scheduled Close, Pending Seller Approval, Partially Allocated, Complete | `COMPLETED` | `Completed` |
| C: unfulfilled | Submit, Pending, Open, scheduled Close, Pending Seller Approval, Not Fulfilled | `NOT FULFILLED` | `Completed` |

Expected final workflow versions are 10 for A, 8 for B, and 7 for C.

### 5.2 Bids

| Parent | Bid | Special path | Terminal state |
|---|---:|---|---|
| A | 01 | Return to Draft, Resubmit, IPG Review, Seller Review, Accept | `ACCEPTED` |
| A | 02 | IPG Review, Seller Review, Partially Accept | `PARTIALLY ACCEPTED` |
| A | 03 | IPG Review, Seller Review, Reject | `NOT ACCEPTED` |
| A | 04 | Discount Bid excluded by strict greater-than classification | `NOT ALLOCATED` |
| A | 05 | IPG Review, Seller Review, Accept | `ACCEPTED` |
| B | 06 | IPG Review, Seller Review, Accept | `ACCEPTED` |
| B | 07 | IPG Review, Seller Review, Partially Accept | `PARTIALLY ACCEPTED` |
| B | 08 | IPG Review, Seller Review, Reject | `NOT ACCEPTED` |
| B | 09 | IPG Review, Seller Review, Accept | `ACCEPTED` |
| B | 10 | IPG Review, Seller Review, Partially Accept | `PARTIALLY ACCEPTED` |
| C | 11 | IPG Review, Seller Review, Accept | `ACCEPTED` |
| C | 12 | IPG Review, Seller Review, Partially Accept | `PARTIALLY ACCEPTED` |
| C | 13 | IPG Review, Seller Review, Reject | `NOT ACCEPTED` |
| C | 14 | IPG Review, Seller Review, Accept | `ACCEPTED` |
| C | 15 | IPG Review, Seller Review, Reject | `NOT ACCEPTED` |

Opportunity A fixture amounts make the FAIR VALUE plus PREMIUM requested total strictly greater than its sale amount and mark only Bid 04, a DISCOUNT Bid, `NOT ALLOCATED`. Opportunities B and C use totals less than or equal to their sale amounts, so every submitted Bid moves to `IPG REVIEW`.

Final Bid totals are:

- 6 `ACCEPTED`;
- 4 `PARTIALLY ACCEPTED`;
- 4 `NOT ACCEPTED`;
- 1 `NOT ALLOCATED`.

## 6. Scheduled execution

All three Opportunities open on the same acceptance-local date. The acceptance `TimeProvider` advances through three separate invocations of the production scheduled service:

1. opening audit local date +1: queue one grouped open email containing all three Opportunities;
2. opening audit local date +14: queue one grouped reminder email containing all three Opportunities;
3. opening audit local date +16: close all due Opportunities first, classify eligible Bids second, and queue no obsolete reminder.

The runner validates each scheduled summary, stable occurrence key, and absence of duplicate occurrence rows. It then performs the manual Seller-review and allocation actions.

## 7. Email acceptance

Expected delivered message count is exactly 19:

| Route | Count |
|---|---:|
| Grouped Opportunity open | 1 |
| Grouped Opportunity reminder | 1 |
| Opportunity pending Seller approval | 3 |
| Bid accepted | 6 |
| Bid partially accepted | 4 |
| Bid not accepted | 4 |

`NOT ALLOCATED` has no disposition email. Return, resubmit, pending, close, IPG review, Seller review, allocation, and completion actions have no additional route.

The acceptance runner processes all pending rows through the production `ProcessNotificationOutboxService` and SMTP sender. It validates every applicable message for:

- notification route/type;
- subject plus plain-text and HTML bodies;
- immutable To/Cc snapshots;
- expected Opportunity or Bid reference;
- unchanged open-dashboard URL or correctly appended target ID;
- `Sent` outbox status;
- sender name `Smtp`;
- retry count zero;
- a matching smtp4dev message.

The smtp4dev message database remains available through `http://localhost:5000` after the run.

## 8. Console evidence

Before each workflow call, print:

```text
sequence
entity type and generated target ID
production service method
actionKey, when applicable
expected workflow version
trusted actor ID, display name, and roles
resolved recipient inputs, when applicable
expected state
```

After each call, print the structured success/error result, authoritative workflow state/version, target `Status`, target `WorkflowState`, and parity verdict. Scheduled and outbox runs print candidate counts, occurrence keys, queued/sent counts, and every failure.

The final console summary lists the marker, database, generated Opportunity/Bid IDs, state totals, audit totals, expected and actual email totals, smtp4dev URL, SQL verification command, and overall PASS/FAIL.

## 9. Failure handling and retained state

- Setup SQL uses explicit transactions and `sqlcmd -b`.
- Fixture insertion uses one explicit transaction before workflow execution begins.
- Every workflow call retains its normal production transaction boundary.
- The runner stops on an unexpected action result, state/version mismatch, scheduled failure, outbox failure, SMTP mismatch, or final verification mismatch.
- A failure produces a nonzero exit and leaves all already committed target, workflow, audit, outbox, log, and smtp4dev evidence intact.
- There is no automatic cleanup, retry that hides the first failure, state rewrite, or direct next-state selection.
- Re-running with the same marker is rejected. A later rerun requires an explicitly different marker or separately approved cleanup.

## 10. Acceptance criteria

The live run passes only when:

- compiled application code, not test discovery, drives every workflow operation;
- exactly 3 marked Opportunities and 15 marked Bids exist;
- every target `Status` and `WorkflowState` exactly equals its authoritative workflow state;
- the three Opportunity and four Bid terminal categories match the scenario matrix;
- all expected state transitions and versions appear in ordered audit history;
- Bid disposition leaves each parent Opportunity unchanged;
- no SQMS workflow task or SLA row exists;
- `TotalBid`, `LargestBid`, and `LowestBid` retain their initial sentinel values;
- exactly 19 applicable outbox rows are `Sent` with zero retries;
- exactly 19 matching smtp4dev messages exist with expected recipients, content, and links;
- both grouped occurrence keys are unique and replay-safe;
- the production `003` database verifier and generic `verify-seed` still pass;
- all independent mismatch queries return zero rows;
- the database and smtp4dev evidence remain available for manual inspection.

## 11. Deliverables

- `tools/WorkflowEngine.Sqms.LiveAcceptance/` acceptance console project;
- a PowerShell setup/run script using the fixed safe database boundary;
- `sql/sqms/testing/005_verify_sqms_live_acceptance.sql` read-only verifier;
- a scenario-to-email checklist and generated-ID manifest;
- updated `CODEX_HANDOFF.md` with build, run, database, SMTP, and open-issue evidence;
- user-facing commands for SQL verification and reopening smtp4dev.

No production deployment, production database change, push, merge, or Windows Task Scheduler enablement is part of this acceptance exercise.
