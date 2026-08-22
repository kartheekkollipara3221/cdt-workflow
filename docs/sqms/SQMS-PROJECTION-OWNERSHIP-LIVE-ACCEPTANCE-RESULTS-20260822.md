# SQMS Projection-Ownership Live Acceptance Results — 2026-08-22

Result: PASS at 2026-08-22 18:29 EDT.

## Retained evidence

```text
Server: DOLLY
Database: workflowengine_sqms_projection_ownership_live_acceptance
Marker: SQMS-PROJECTION-OWNERSHIP-LIVE-20260822
Opportunity ID: 10000
Bid IDs: 10000, 10001, 10002, 10003
smtp4dev SMTP: localhost:3027
smtp4dev UI: http://localhost:5002
smtp4dev launcher PID: 9224
smtp4dev process PID: 18996
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-projection-ownership-live-acceptance-20260822
```

Do not replay `scripts/run-sqms-projection-ownership-live-acceptance.ps1`. The database and mailbox are retained, and the launcher intentionally refuses to overwrite them.

## Ownership proof

The live runner captured the complete target row before and after every request-driven Start/Action call.

| Evidence | Count | Result |
|---|---:|---|
| `RequestUnchanged` | 20 | Before/after JSON identical; thin layer did not update the target row |
| `TargetOwnerPersisted` | 20 | Separate target-owner writer persisted the returned state |
| `ScheduledProjected` | 5 | Scheduled Opportunity close and four Bid classifications directly updated target rows |

The request and target-owner records pair by entity, target ID, operation, returned state, and returned version. The scheduled records cover Opportunity `CLOSED TO BIDDING`, three Bids moved to `IPG REVIEW`, and one Bid moved to `NOT ALLOCATED` before the remaining manual lifecycle steps.

## Final workflow and target state

| Entity | ID | Final state | Version |
|---|---:|---|---:|
| Opportunity | 10000 | `COMPLETED` | 10 |
| Bid | 10000 | `ACCEPTED` | 7 |
| Bid | 10001 | `PARTIALLY ACCEPTED` | 5 |
| Bid | 10002 | `NOT ACCEPTED` | 5 |
| Bid | 10003 | `NOT ALLOCATED` | 3 |

For every row, target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` matched. Opportunity sentinels remained `777`, `L-SENTINEL`, and `S-SENTINEL`.

Runtime totals:

- Workflow instances: 5
- Workflow audits: 25
- Idempotency records: 25
- Workflow tasks: 0
- SQMS SLA schedules: 0
- Sent outbox rows with retry zero: 6

## smtp4dev proof

smtp4dev returned exactly six messages, one per route:

- `SqmsOpportunityOpen`: `Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030`
- `SqmsOpportunityReminder`: `REMINDER Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030`
- `SqmsOpportunityPendingSellerApproval`: `Q1 2030 Secondary Sale Bids - SQMS Live Vehicle A/SQMS-PROJECTION-OWNERSHIP-LIVE-20260822-A - SQMS Live Acceptance Opportunity A: Response Required by 01/22/2030`
- `SqmsBidAccepted`: subject contains `...-BID-01 - ... Bid 01`
- `SqmsBidPartiallyAccepted`: subject contains `...-BID-02 - ... Bid 02`
- `SqmsBidNotAccepted`: subject contains `...-BID-03 - ... Bid 03`

The compiled verifier matched every Sent outbox row to exactly one smtp4dev message, including expected recipients and rendered links.

## Verification executed

- Main solution warning-as-error build: PASS, zero warnings/errors.
- Workflow API warning-as-error build: PASS, zero warnings/errors.
- Live acceptance tool warning-as-error build: PASS, zero warnings/errors.
- Main solution tests: 453 passed, 0 failed, 4 opt-in SMTP tests skipped.
- Workflow API tests: 68 passed, 0 failed, 0 skipped.
- Fresh database initialization: PASS.
- Generic seed verification: PASS.
- SQMS deployment verification: PASS.
- SQMS package rollback verification: PASS.
- One-shot compiled live acceptance: PASS; six messages processed through SMTP.
- Verification-only compiled runner: PASS without replay.
- Independent SQL verifier `008_verify_sqms_projection_ownership_live_acceptance.sql`: PASS both during orchestration and afterward.

The first main-solution `--no-restore` build attempt reported a missing Console assets file in the new worktree. After the required restore, all warning-as-error builds passed. The implementation result was unaffected.
