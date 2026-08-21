# SQMS Corrected Email Template Live Acceptance Results - 2026-08-21

## Result

PASS. The corrected SQMS notification metadata was exercised through the compiled application services, SQL Server outbox, SMTP sender, and smtp4dev. This was a live code exercise, not an xUnit simulation.

The existing `workflowengine` database and the earlier retained `workflowengine_sqms_live_acceptance` database and mailbox were not changed.

## Retained evidence

```text
Server: DOLLY
Database: workflowengine_sqms_email_live_acceptance
Run marker: SQMS-EMAIL-LIVE-20260821
smtp4dev UI: http://localhost:5000
smtp4dev SMTP: localhost:3025
smtp4dev launcher PID: 5928 at verification time
smtp4dev process PID: 32624 at verification time
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821
```

The database and smtp4dev data are intentionally retained. Process IDs can change after a restart; verify the current process and command line before stopping it.

## Generated records and final states

| Opportunity | Target status | Workflow state | Version |
|---:|---|---|---:|
| 10000 | `COMPLETED` | `COMPLETED` | 10 |
| 10001 | `COMPLETED` | `COMPLETED` | 8 |
| 10002 | `NOT FULFILLED` | `NOT FULFILLED` | 7 |

| Final bid outcome | Count |
|---|---:|
| `ACCEPTED` | 6 |
| `PARTIALLY ACCEPTED` | 4 |
| `NOT ACCEPTED` | 4 |
| `NOT ALLOCATED` | 1 |

For all 18 records, target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` match. The three UI-owned calculated-column sentinels remain unchanged: `TotalBid=777`, `LargestBid=L-SENTINEL`, and `LowestBid=S-SENTINEL`.

## Workflow and email evidence

```text
Workflow instances: 18
Workflow audits: 82
Open SQMS tasks: 0
SQMS SLA schedules: 0
Sent outbox rows: 19
Retried outbox rows: 0
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

The compiled verifier matched each outbox row to exactly one smtp4dev message using an exact decoded subject and the expected To/Cc recipients. It also checked each complete RFC822 message for its configured dashboard or form link and the generated `opportunityId` or `bidId` parameter.

The accepted, partially accepted, and not accepted messages use the approved wording and `MM/dd/yyyy` effective date. Seller-approval messages include the response deadline in the subject. Opening and reminder messages use the static dashboard link and retain the existing opened-date-relative schedule; no Effective Date scheduling rule was added.

## Verification executed

- Compiled verification-only run: `SQMS live acceptance verification-only PASS`.
- Production package verifier: `SQMS deployment verification passed.`
- Dedicated corrected-email SQL verifier: `SQMS email-template live acceptance database verification passed.`
- Exact database counts: 3 opportunities, 15 bids, 19 sent emails, 82 audits, 0 tasks, and 0 SLA schedules.
- Corrected-email acceptance contract suite after the verifier correction: 18 passed, 0 failed.
- Acceptance tool build after the verifier correction: 0 warnings, 0 errors.

The initial workflow execution completed once and produced all retained target, workflow, outbox, and SMTP evidence. Its first verification-only pass exposed a verifier defect: substring subject matching treated the reminder subject as a second match for the opening message. The verifier was changed to exact subject matching and covered by a regression test. Verification was then rerun without replaying or rewriting workflow operations, outbox rows, or email messages.

## Inspection

Use the verification-only environment and read-only queries in [SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE.md](SQMS-EMAIL-TEMPLATE-LIVE-ACCEPTANCE.md). The independent database command is:

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql
```

Open smtp4dev at [http://localhost:5000](http://localhost:5000) to inspect the retained corrected messages.
