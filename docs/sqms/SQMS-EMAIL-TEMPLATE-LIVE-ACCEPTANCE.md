# SQMS Corrected Email Template Live Acceptance

This run exercises the corrected SQMS subjects, plain-text bodies, HTML bodies, recipients, dates, and links through compiled services, SQL Server, the outbox processor, and smtp4dev.

## Fixed safety boundary

```text
Server: DOLLY
Database: workflowengine_sqms_email_live_acceptance
Run marker: SQMS-EMAIL-LIVE-20260821
smtp4dev SMTP: localhost:3025
smtp4dev UI: http://localhost:5000
smtp4dev data: C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821
```

The runner refuses to overwrite an existing database or smtp4dev data directory and refuses occupied ports. It does not alter either the existing `workflowengine` database or the earlier retained `workflowengine_sqms_live_acceptance` evidence.

## Execute once

From the `sqms-console-mock` worktree:

```powershell
& .\scripts\run-sqms-email-template-live-acceptance.ps1
```

Do not rerun this one-shot script after it creates evidence. Diagnose retained state and use the verification-only commands below.

## Verification-only environment

```powershell
$env:SQMS_LIVE_ACCEPTANCE_DATABASE = 'workflowengine_sqms_email_live_acceptance'
$env:SQMS_LIVE_ACCEPTANCE_MARKER = 'SQMS-EMAIL-LIVE-20260821'
$env:SQMS_LIVE_ACCEPTANCE_OPENING_UTC = '2030-01-01T15:00:00Z'
$env:ConnectionStrings__DefaultConnection = 'Server=DOLLY;Database=workflowengine_sqms_email_live_acceptance;Integrated Security=true;TrustServerCertificate=True;Encrypt=False'
$env:Sqms__Enabled = 'true'
$env:Sqms__Links__OpenBiddingDashboardUrl = 'https://sqms-acceptance.example.test/open-bidding?source=live'
$env:Sqms__Links__SellerApprovalBidsDashboardUrl = 'https://sqms-acceptance.example.test/bids?view=seller'
$env:Sqms__Links__BidFormUrl = 'https://sqms-acceptance.example.test/bid?source=live'
$env:Sqms__Scheduling__TimeZoneId = 'Eastern Standard Time'
$env:Sqms__Scheduling__BiddingDays = '15'
$env:Sqms__Scheduling__SellerResponseDays = '5'
$env:Notifications__Sender = 'Smtp'
$env:Smtp__Host = 'localhost'
$env:Smtp__Port = '3025'
$env:Smtp__From = 'workflow-sqms-live@example.test'
$env:Smtp__RecipientOverride = ''

dotnet run --project tools/WorkflowEngine.Sqms.LiveAcceptance/WorkflowEngine.Sqms.LiveAcceptance.csproj --no-build -- --verify-only
```

## Independent SQL verification

```powershell
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/003_verify_sqms_deployment.sql
sqlcmd -S DOLLY -d workflowengine_sqms_email_live_acceptance -E -C -b -i sql/sqms/testing/006_verify_sqms_email_template_live_acceptance.sql
```

## Focused inspection queries

### Target and workflow state

```sql
SELECT o.ID, o.Account, o.Status, o.WorkflowState,
       w.CurrentStateKey, w.Version,
       o.TotalBid, o.LargestBid, o.LowestBid
FROM SSQMS.Opportunity AS o
JOIN dbo.WorkflowInstance AS w
  ON w.BusinessObjectId = CONCAT('SQMS-OPP-', o.ID)
WHERE o.Account LIKE 'SQMS-EMAIL-LIVE-20260821%'
ORDER BY o.ID;

SELECT b.ID, b.OpportunityId, b.Status, b.WorkflowState,
       w.CurrentStateKey, w.Version
FROM SSQMS.Bid AS b
JOIN dbo.WorkflowInstance AS w
  ON w.BusinessObjectId = CONCAT('SQMS-BID-', b.ID)
WHERE b.AccountNumber LIKE 'SQMS-EMAIL-LIVE-20260821%'
ORDER BY b.ID;
```

### Corrected messages

```sql
SELECT Id, NotificationType, NotificationRecipientsJson,
       Subject, Body, HtmlBody, Status, RetryCount, LastError
FROM dbo.NotificationOutbox
ORDER BY Id;
```

Expected route counts are 1 open, 1 reminder, 3 Seller approval, 6 accepted, 4 partially accepted, and 4 not accepted. Every row must be `Sent` with retry count zero.

### Audit, task, and SLA totals

```sql
SELECT COUNT(*) AS AuditCount
FROM dbo.WorkflowAudit
WHERE WorkflowInstanceId LIKE 'WF-SQMS-%';

SELECT COUNT(*) AS TaskCount
FROM dbo.WorkflowTask
WHERE WorkflowInstanceId LIKE 'WF-SQMS-%';

SELECT COUNT(*) AS SlaCount
FROM dbo.WorkflowTaskSlaSchedule AS schedule
JOIN dbo.WorkflowTask AS task ON task.Id = schedule.WorkflowTaskId
WHERE task.WorkflowInstanceId LIKE 'WF-SQMS-%';
```

Expected: 82 audits, zero tasks, and zero SLA schedules.

## Retained smtp4dev process

The PID is stored at:

```text
C:\projects\cdt-workflow\.tools\smtp4dev\data\sqms-email-live-acceptance-20260821\smtp4dev.pid
```

Before stopping it, resolve the current PID and verify that its command line contains the corrected-email data directory. Stop only that process and do not delete the data directory.
