# SQMS Live Acceptance Results — 2026-08-21

## Result

PASS. The compiled SQMS application services executed the complete retained acceptance run against SQL Server and smtp4dev. This was a live code exercise, not an xUnit simulation.

The existing `workflowengine` database was not changed.

## Retained evidence

```text
Server: DOLLY
Database: workflowengine_sqms_live_acceptance
Run marker: SQMS-LIVE-20260821
smtp4dev UI: http://localhost:5000
smtp4dev SMTP: localhost:3025
smtp4dev process: 27468 (Rnwood.Smtp4dev.exe at verification time)
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821
```

The database and smtp4dev data are intentionally retained. Process IDs can change after a restart; verify the current process before stopping it.

## Generated records and final states

| Opportunity | Target status | Workflow state | Version |
|---:|---|---|---:|
| 10000 | `COMPLETED` | `COMPLETED` | 10 |
| 10001 | `COMPLETED` | `COMPLETED` | 8 |
| 10002 | `NOT FULFILLED` | `NOT FULFILLED` | 7 |

| Bid IDs | Final outcome | Count |
|---|---|---:|
| 10000, 10004, 10005, 10008, 10010, 10013 | `ACCEPTED` | 6 |
| 10001, 10006, 10009, 10011 | `PARTIALLY ACCEPTED` | 4 |
| 10002, 10007, 10012, 10014 | `NOT ACCEPTED` | 4 |
| 10003 | `NOT ALLOCATED` | 1 |

For all 18 records, target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` match. The three calculated-column sentinels remain unchanged: `TotalBid=777`, `LargestBid=L-SENTINEL`, and `LowestBid=S-SENTINEL`.

## Workflow and email evidence

```text
Workflow instances: 18
Workflow audits: 82
Open SQMS tasks: 0
SQMS SLA schedules: 0
Sent outbox rows: 19
Non-Sent or retried outbox rows: 0
smtp4dev messages: 19
```

| Notification route | Sent |
|---|---:|
| `SqmsOpportunityOpen` | 1 |
| `SqmsOpportunityReminder` | 1 |
| `SqmsOpportunityPendingSellerApproval` | 3 |
| `SqmsBidAccepted` | 6 |
| `SqmsBidPartiallyAccepted` | 4 |
| `SqmsBidNotAccepted` | 4 |

The compiled verifier matched every outbox row to exactly one smtp4dev message using decoded subject and recipients, then checked the complete RFC822 body for all configured dashboard/form links and generated `opportunityId` or `bidId` parameters.

## Verification executed

- Compiled acceptance verifier: `SQMS live acceptance verification-only PASS`.
- Production package verifier: `SQMS deployment verification passed.`
- Dedicated acceptance SQL: `SQMS live acceptance database verification passed.`
- Dedicated SQL exact counts: 3 Opportunities, 15 Bids, 19 emails, 82 audits, 0 tasks, and 0 SLA schedules.
- Focused acceptance contract suite after verifier corrections: 14 passed, 0 failed.
- Acceptance tool build: 0 warnings, 0 errors.

Final backward-compatibility verification after all acceptance tooling changes:

- Core, Workflow API, and acceptance-tool warning-as-error builds: 0 warnings, 0 errors.
- Full core regression: 438 passed, 0 failed, 4 expected opt-in SMTP skips.
- Full Workflow API regression: 68 passed, 0 failed.
- Section 21 Checker-return and Trading-return sample branches: passed.
- Section 21 aggregate demo: all 14 steps passed.
- Typed public API gate: 26 passed, 0 failed.
- Sample and inspection gate: 24 passed, 0 failed.
- Disposable final-gate database `workflowengine_sqms_final_gate_20260821` was removed after verification; `DB_ID` returned null.

An initial full-suite invocation was accidentally duplicated while the first command was still running. The two suites reset the same fixed test databases concurrently and produced SQL login/reset failures. Both test processes were stopped, and the suite was rerun once in isolation with the passing result above. This did not touch the retained acceptance database.

During evidence verification, three verifier-only assumptions were corrected without replaying workflow operations: the actual task primary-key name, smtp4dev list pagination, and smtp4dev RFC822 body retrieval/MIME subject folding. The retained target rows, workflow rows, outbox rows, and messages were not rewritten or replayed.

## Inspection queries

Use the complete verifier and focused read-only queries in [SQMS-LIVE-ACCEPTANCE.md](SQMS-LIVE-ACCEPTANCE.md). The main independent command is:

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_live_acceptance -E -C -b -i sql/sqms/testing/005_verify_sqms_live_acceptance.sql
```

Open smtp4dev at [http://localhost:5000](http://localhost:5000) to inspect the retained messages.
