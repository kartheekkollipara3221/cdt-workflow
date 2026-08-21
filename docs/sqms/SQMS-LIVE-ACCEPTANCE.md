# SQMS Live Acceptance Instructions

This acceptance exercise runs compiled application services against a retained local SQL database and smtp4dev. It is not an xUnit test and does not add an SQMS HTTP endpoint.

## Fixed safety boundary

```text
Server: DOLLY
Database: workflowengine_sqms_live_acceptance
Run marker: SQMS-LIVE-20260821
smtp4dev SMTP: localhost:3025
smtp4dev UI: http://localhost:5000
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821
```

The runner refuses any other database and refuses to overwrite an existing database, marker, smtp4dev data directory, or process using ports 3025/5000. It leaves all evidence intact. The existing `workflowengine` database is not changed.

## Execute once

From the `sqms-console-mock` worktree:

```powershell
& .\scripts\run-sqms-live-acceptance.ps1
```

The script builds first, creates the dedicated database, applies the target-table fixture plus production `001`-`003` package, verifies seed data, starts smtp4dev hidden, runs the compiled acceptance executable, and runs the independent SQL verifier. A successful run prints `SQMS LIVE ACCEPTANCE PASS` and `SQMS live acceptance database verification passed.`

An interrupted or failed run deliberately leaves partial evidence. Diagnose that evidence rather than rerunning the script over it.

## Expected scenarios

| Opportunity | Final state | Bid outcomes |
|---|---|---|
| A | `COMPLETED` through Awaiting Allocation | 2 Accepted, 1 Partially Accepted, 1 Not Accepted, 1 Not Allocated; Bid 01 also returns/resubmits |
| B | `COMPLETED` through Partially Allocated | 2 Accepted, 2 Partially Accepted, 1 Not Accepted |
| C | `NOT FULFILLED` | 2 Accepted, 1 Partially Accepted, 2 Not Accepted |

Expected final Bid totals are 6 Accepted, 4 Partially Accepted, 4 Not Accepted, and 1 Not Allocated.

## Expected email checklist

Open [http://localhost:5000](http://localhost:5000) after the run.

| Route | Expected messages | Link rule |
|---|---:|---|
| SQMS Opportunity open | 1 | Static open dashboard link; all 3 Opportunities |
| SQMS Opportunity reminder | 1 | Static open dashboard link; all 3 Opportunities |
| Pending Seller approval | 3 | Seller dashboard with generated `opportunityId` |
| Bid accepted | 6 | Bid form with generated `bidId` |
| Bid partially accepted | 4 | Bid form with generated `bidId` |
| Bid not accepted | 4 | Bid form with generated `bidId` |

Total: 19 messages. The Not Allocated Bid has no disposition email. Each outbox row must be `Sent`, use sender `Smtp`, and have retry count zero.

## Run the complete database verifier

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_live_acceptance -E -C -b -i sql/sqms/testing/005_verify_sqms_live_acceptance.sql
```

## Focused inspection queries

### Target Opportunities

```sql
SELECT ID, Account, Status, WorkflowState, TotalBid, LargestBid, LowestBid
FROM SSQMS.Opportunity
WHERE Account LIKE 'SQMS-LIVE-20260821%'
ORDER BY ID;
```

### Target Bids

```sql
SELECT OpportunityId, ID, AccountNumber, CommitmentAmountRequested,
       BidExpressedAsFairValue, Status, WorkflowState
FROM SSQMS.Bid
WHERE AccountNumber LIKE 'SQMS-LIVE-20260821%'
ORDER BY OpportunityId, ID;
```

### Workflow state and versions

```sql
SELECT WorkflowInstanceId, WorkflowKey, BusinessObjectId,
       CurrentStateKey, Version, Status, CompletedAtUtc
FROM dbo.WorkflowInstance
WHERE WorkflowKey IN ('SqmsOpportunity', 'SqmsBid')
ORDER BY WorkflowKey, BusinessObjectId;
```

### Ordered audit history

```sql
SELECT WorkflowInstanceId, Id, ActionKey, FromStateKey, ToStateKey,
       PerformedByUserId, PerformedByRoleKey, CreatedAtUtc
FROM dbo.WorkflowAudit
WHERE WorkflowInstanceId LIKE 'WF-SQMS-%'
ORDER BY WorkflowInstanceId, Id;
```

### Outbox and email routes

```sql
SELECT Id, WorkflowInstanceId, NotificationOccurrenceKey, NotificationType,
       NotificationRecipientsJson, Subject, Status, RetryCount, SentAtUtc, LastError
FROM dbo.NotificationOutbox
ORDER BY Id;
```

### Target/workflow mismatches

```sql
SELECT 'Opportunity' AS EntityType, opportunity.ID,
       opportunity.Status, opportunity.WorkflowState, instance.CurrentStateKey
FROM SSQMS.Opportunity AS opportunity
LEFT JOIN dbo.WorkflowInstance AS instance
  ON instance.WorkflowKey = 'SqmsOpportunity'
 AND instance.BusinessObjectId = CONCAT('SQMS-OPP-', opportunity.ID)
WHERE opportunity.Account LIKE 'SQMS-LIVE-20260821%'
  AND (instance.CurrentStateKey IS NULL OR opportunity.Status <> instance.CurrentStateKey
       OR opportunity.WorkflowState <> instance.CurrentStateKey)
UNION ALL
SELECT 'Bid', bid.ID, bid.Status, bid.WorkflowState, instance.CurrentStateKey
FROM SSQMS.Bid AS bid
LEFT JOIN dbo.WorkflowInstance AS instance
  ON instance.WorkflowKey = 'SqmsBid'
 AND instance.BusinessObjectId = CONCAT('SQMS-BID-', bid.ID)
WHERE bid.AccountNumber LIKE 'SQMS-LIVE-20260821%'
  AND (instance.CurrentStateKey IS NULL OR bid.Status <> instance.CurrentStateKey
       OR bid.WorkflowState <> instance.CurrentStateKey);
```

Expected: zero rows.

### Task and SLA absence

```sql
SELECT * FROM dbo.WorkflowTask WHERE WorkflowInstanceId LIKE 'WF-SQMS-%';

SELECT schedule.*
FROM dbo.WorkflowTaskSlaSchedule AS schedule
JOIN dbo.WorkflowTask AS task ON task.Id = schedule.WorkflowTaskId
WHERE task.WorkflowInstanceId LIKE 'WF-SQMS-%';
```

Expected: zero rows from both queries.

## smtp4dev process management

The PID is stored in:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-live-acceptance-20260821\smtp4dev.pid
```

To stop only this retained acceptance process, read the PID, verify its executable/process relationship, then use `Stop-Process -Id <verified-pid>`. Do not remove the data directory. To reopen the retained mailbox later, start the same smtp4dev launcher with the same `--baseappdatapath`, `--db=smtp4dev.db`, SMTP port, and UI URL after confirming those ports are unused.
