# SQMS Single-Lifecycle Live Acceptance

This retained run exercises one complete Opportunity and three Bids through compiled SQMS services, SQL Server, the notification outbox, SMTP, and smtp4dev.

## Retained evidence

```text
Server: DOLLY
Database: workflowengine_sqms_single_lifecycle_live_acceptance
Run marker: SQMS-SINGLE-LIVE-20260821
Opportunity ID: 10000
Bid IDs: 10000, 10001, 10002
smtp4dev SMTP: localhost:3026
smtp4dev UI: http://localhost:5001
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821
```

Do not rerun `scripts/run-sqms-single-lifecycle-live-acceptance.ps1`. It is a one-shot command and refuses to overwrite the retained database or mailbox. Use only the verification and inspection commands below.

## Verification-only environment

From the `sqms-console-mock` worktree:

```powershell
$env:SQMS_LIVE_ACCEPTANCE_DATABASE = 'workflowengine_sqms_single_lifecycle_live_acceptance'
$env:SQMS_LIVE_ACCEPTANCE_MARKER = 'SQMS-SINGLE-LIVE-20260821'
$env:SQMS_LIVE_ACCEPTANCE_OPENING_UTC = '2030-01-01T15:00:00Z'
$env:SQMS_LIVE_ACCEPTANCE_SCENARIO = 'single-lifecycle'
$env:SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL = 'http://localhost:5001'
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine_sqms_single_lifecycle_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$env:Sqms__Enabled = 'true'
$env:Sqms__Links__OpenBiddingDashboardUrl = 'https://sqms-acceptance.example.test/open-bidding?source=single-live'
$env:Sqms__Links__SellerApprovalBidsDashboardUrl = 'https://sqms-acceptance.example.test/bids?view=single-seller'
$env:Sqms__Links__BidFormUrl = 'https://sqms-acceptance.example.test/bid?source=single-live'
$env:Sqms__Scheduling__TimeZoneId = 'Eastern Standard Time'
$env:Sqms__Scheduling__BiddingDays = '15'
$env:Sqms__Scheduling__SellerResponseDays = '5'
$env:Notifications__Sender = 'Smtp'
$env:Smtp__Host = 'localhost'
$env:Smtp__Port = '3026'
$env:Smtp__From = 'workflow-sqms-single-live@example.test'
$env:Smtp__RecipientOverride = ''

dotnet run --no-build --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj -- --verify-only
```

Expected: `SQMS live acceptance verification-only PASS`.

## Independent SQL verification

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_single_lifecycle_live_acceptance -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
sqlcmd -S DOLLY -d workflowengine_sqms_single_lifecycle_live_acceptance -E -C -b -i sql/sqms/testing/007_verify_sqms_single_lifecycle_live_acceptance.sql
```

## SQMS target and workflow states

```sql
SELECT o.ID, o.Account, o.Status, o.WorkflowState,
       w.CurrentStateKey, w.Version,
       o.TotalBid, o.LargestBid, o.LowestBid
FROM SSQMS.Opportunity AS o
JOIN dbo.WorkflowInstance AS w
  ON w.WorkflowKey = N'SqmsOpportunity'
 AND w.BusinessObjectId = CONCAT(N'SQMS-OPP-', o.ID)
WHERE o.Account LIKE N'SQMS-SINGLE-LIVE-20260821%';

SELECT b.ID, b.OpportunityId, b.AccountNumber,
       b.CommitmentAmountRequested, b.BidExpressedAsFairValue,
       b.Status, b.WorkflowState,
       w.CurrentStateKey, w.Version
FROM SSQMS.Bid AS b
JOIN dbo.WorkflowInstance AS w
  ON w.WorkflowKey = N'SqmsBid'
 AND w.BusinessObjectId = CONCAT(N'SQMS-BID-', b.ID)
WHERE b.AccountNumber LIKE N'SQMS-SINGLE-LIVE-20260821%'
ORDER BY b.ID;
```

Expected:

- Opportunity 10000: `COMPLETED`, version 10.
- Bid 10000: `$40.00`, `FAIR VALUE`, `ACCEPTED`, version 7.
- Bid 10001: `$30.00`, `PREMIUM`, `PARTIALLY ACCEPTED`, version 5.
- Bid 10002: `$20.00`, `DISCOUNT`, `NOT ACCEPTED`, version 5.
- Target `Status`, target `WorkflowState`, and workflow `CurrentStateKey` match.
- Opportunity calculated-column sentinels remain `777`, `L-SENTINEL`, and `S-SENTINEL`.

## Email outbox

```sql
SELECT Id, NotificationType, NotificationRecipientsJson,
       Subject, Body, HtmlBody,
       Status, RetryCount, LastError
FROM dbo.NotificationOutbox
ORDER BY Id;

SELECT NotificationType, Status, RetryCount, COUNT(*) AS MessageCount
FROM dbo.NotificationOutbox
GROUP BY NotificationType, Status, RetryCount
ORDER BY NotificationType;
```

Expected: exactly one `Sent` row with `RetryCount = 0` for each route:

- `SqmsOpportunityOpen`
- `SqmsOpportunityReminder`
- `SqmsOpportunityPendingSellerApproval`
- `SqmsBidAccepted`
- `SqmsBidPartiallyAccepted`
- `SqmsBidNotAccepted`

The open/reminder link is static. Seller approval includes `opportunityId=10000`. The three bidder messages include `bidId=10000`, `bidId=10001`, and `bidId=10002` respectively.

## Audit, task, and SLA totals

```sql
SELECT COUNT(*) AS AuditCount
FROM dbo.WorkflowAudit
WHERE WorkflowInstanceId LIKE N'WF-SQMS-%';

SELECT COUNT(*) AS TaskCount
FROM dbo.WorkflowTask
WHERE WorkflowInstanceId LIKE N'WF-SQMS-%';

SELECT COUNT(*) AS SlaCount
FROM dbo.WorkflowTaskSlaSchedule AS schedule
JOIN dbo.WorkflowTask AS task ON task.Id = schedule.WorkflowTaskId
WHERE task.WorkflowInstanceId LIKE N'WF-SQMS-%';
```

Expected: 23 audits, zero tasks, and zero SLA schedules.

## smtp4dev inspection

Open [http://localhost:5001](http://localhost:5001). The mailbox contains exactly six messages. The PID recorded by the launcher is stored at:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-single-lifecycle-live-acceptance-20260821\smtp4dev.pid
```

Before stopping anything, resolve the current launcher and child process and verify their command lines contain this exact data directory and ports 3026/5001. Do not remove the retained data directory.
