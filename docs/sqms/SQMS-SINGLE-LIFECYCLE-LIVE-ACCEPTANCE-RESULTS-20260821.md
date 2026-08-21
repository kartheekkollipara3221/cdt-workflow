# SQMS Single-Lifecycle Live Acceptance Results - 2026-08-21

## Result

PASS. One Opportunity and three related Bids completed their compiled SQMS lifecycle against SQL Server. All six applicable notifications were processed through SMTP and retained in smtp4dev for manual inspection.

## Retained successful evidence

```text
Database: DOLLY/workflowengine_sqms_single_lifecycle_live_acceptance
Run marker: SQMS-SINGLE-LIVE-20260821
smtp4dev UI: http://localhost:5001
smtp4dev SMTP: localhost:3026
smtp4dev launcher PID at verification: 2536
smtp4dev executable PID at verification: 16804
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821
```

## Final SQMS states

| Entity | ID | Amount/category | Target status | Workflow state | Version |
|---|---:|---|---|---|---:|
| Opportunity | 10000 | calculated sentinels unchanged | `COMPLETED` | `COMPLETED` | 10 |
| Bid | 10000 | `$40.00` / `FAIR VALUE` | `ACCEPTED` | `ACCEPTED` | 7 |
| Bid | 10001 | `$30.00` / `PREMIUM` | `PARTIALLY ACCEPTED` | `PARTIALLY ACCEPTED` | 5 |
| Bid | 10002 | `$20.00` / `DISCOUNT` | `NOT ACCEPTED` | `NOT ACCEPTED` | 5 |

For all four records, target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` match. `TotalBid=777`, `LargestBid=L-SENTINEL`, and `LowestBid=S-SENTINEL` were not modified.

## Workflow and notification evidence

```text
Workflow instances: 4
Workflow audits: 23
Open SQMS tasks: 0
SQMS SLA schedules: 0
Sent outbox rows: 6
Retried outbox rows: 0
smtp4dev messages: 6
```

| Route | Count |
|---|---:|
| `SqmsOpportunityOpen` | 1 |
| `SqmsOpportunityReminder` | 1 |
| `SqmsOpportunityPendingSellerApproval` | 1 |
| `SqmsBidAccepted` | 1 |
| `SqmsBidPartiallyAccepted` | 1 |
| `SqmsBidNotAccepted` | 1 |

The compiled verifier matched each outbox row to exactly one smtp4dev message by exact decoded subject and expected To/Cc recipients. It verified the static opening link, Seller `opportunityId=10000`, and Bid links for IDs 10000-10002.

## Verification passed

- Compiled verification-only mode: `SQMS live acceptance verification-only PASS`.
- Production package verifier: `SQMS deployment verification passed.`
- Independent single-lifecycle verifier: `SQMS single-lifecycle live acceptance database verification passed.`
- Acceptance tool warning-as-error build: zero warnings and errors.
- Focused SQMS preflight regression: 49 passed, 0 failed.
- Final core, Workflow API, and acceptance-tool warning-as-error builds: zero warnings and errors.
- Full core regression: 448 passed, 0 failed, 4 expected opt-in SMTP skips.
- Full Workflow API regression: 68 passed, 0 failed.

## Execution diagnostics

The first start stopped before lifecycle data was created because the new smtp4dev process inherited IMAP/POP ports already used by the retained instance. The launcher now explicitly disables IMAP and POP. Its empty mailbox was preserved as:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821-failed-pre-lifecycle
```

The next lifecycle attempt reached close and exposed invalid fixture assumptions: Fair Value `$60` plus Premium `$50` exceeded the `$100` sale amount, so the production classifier correctly marked the Discount Bid `NOT ALLOCATED`. No SMTP messages had been sent. The amounts were corrected test-first to `$40/$30/$20`, keeping the qualifying Fair Value plus Premium sum at `$70`. Its zero-message mailbox was preserved as:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821-failed-close
```

The partial failed-close SQL database was removed after its state was inspected. An attempted logical rename had retained the intended physical MDF/LDF filenames and prevented clean database creation, so the renamed partial database was dropped. No production/shared database or earlier retained SQMS evidence was affected.

## User inspection

Use [SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE.md](SQMS-SINGLE-LIFECYCLE-LIVE-ACCEPTANCE.md) for verification-only commands and copyable SQL queries. Open [http://localhost:5001](http://localhost:5001) to inspect the six successful messages.
