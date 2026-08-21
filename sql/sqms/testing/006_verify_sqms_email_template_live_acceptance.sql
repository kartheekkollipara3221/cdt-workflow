SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunMarker varchar(100) = 'SQMS-EMAIL-LIVE-20260821';
DECLARE @MarkerPattern varchar(101) = @RunMarker + '%';

SELECT
    opportunity.[ID], opportunity.[Account], opportunity.[Status],
    opportunity.[WorkflowState], instance.[CurrentStateKey], instance.[Version],
    opportunity.[TotalBid], opportunity.[LargestBid], opportunity.[LowestBid]
FROM [SSQMS].[Opportunity] AS opportunity
LEFT JOIN [dbo].[WorkflowInstance] AS instance
  ON instance.[WorkflowKey] = N'SqmsOpportunity'
 AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
WHERE opportunity.[Account] LIKE @MarkerPattern
ORDER BY opportunity.[ID];

SELECT
    bid.[ID], bid.[OpportunityId], bid.[Status], bid.[WorkflowState],
    instance.[CurrentStateKey], instance.[Version]
FROM [SSQMS].[Bid] AS bid
LEFT JOIN [dbo].[WorkflowInstance] AS instance
  ON instance.[WorkflowKey] = N'SqmsBid'
 AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
WHERE bid.[AccountNumber] LIKE @MarkerPattern
ORDER BY bid.[ID];

SELECT
    outbox.[Id], outbox.[NotificationType], outbox.[NotificationRecipientsJson],
    outbox.[Subject], outbox.[Body], outbox.[HtmlBody],
    outbox.[Status], outbox.[RetryCount], outbox.[LastError]
FROM [dbo].[NotificationOutbox] AS outbox
ORDER BY outbox.[Id];

SELECT N'OpportunityState' AS CountType, opportunity.[Status] AS CountKey, COUNT(*) AS ActualCount
FROM [SSQMS].[Opportunity] AS opportunity
WHERE opportunity.[Account] LIKE @MarkerPattern
GROUP BY opportunity.[Status]
UNION ALL
SELECT N'BidState', bid.[Status], COUNT(*)
FROM [SSQMS].[Bid] AS bid
WHERE bid.[AccountNumber] LIKE @MarkerPattern
GROUP BY bid.[Status]
UNION ALL
SELECT N'NotificationRoute', outbox.[NotificationType], COUNT(*)
FROM [dbo].[NotificationOutbox] AS outbox
GROUP BY outbox.[NotificationType]
ORDER BY CountType, CountKey;

IF (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern) <> 3
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern) <> 15
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowInstance] WHERE [WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid')) <> 18
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 82
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 0
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowTaskSlaSchedule] AS schedule
        INNER JOIN [dbo].[WorkflowTask] AS task ON task.[Id] = schedule.[WorkflowTaskId]
        WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 0
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] = N'Sent' AND [RetryCount] = 0) <> 19
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] <> N'Sent' OR [RetryCount] <> 0) <> 0
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityOpen') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityReminder') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityPendingSellerApproval') <> 3
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidAccepted') <> 6
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidPartiallyAccepted') <> 4
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidNotAccepted') <> 4
    OR (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern AND [Status] = 'COMPLETED') <> 2
    OR (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern AND [Status] = 'NOT FULFILLED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = 'ACCEPTED') <> 6
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = 'PARTIALLY ACCEPTED') <> 4
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = 'NOT ACCEPTED') <> 4
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = 'NOT ALLOCATED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Opportunity]
        WHERE [Account] LIKE @MarkerPattern
          AND ([TotalBid] <> 777 OR [LargestBid] <> 'L-SENTINEL' OR [LowestBid] <> 'S-SENTINEL')) <> 0
    THROW 51401, 'SQMS corrected-email live acceptance structural verification failed.', 1;

IF EXISTS
(
    SELECT 1
    FROM
    (
        SELECT opportunity.[Status], opportunity.[WorkflowState], instance.[CurrentStateKey]
        FROM [SSQMS].[Opportunity] AS opportunity
        LEFT JOIN [dbo].[WorkflowInstance] AS instance
          ON instance.[WorkflowKey] = N'SqmsOpportunity'
         AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
        WHERE opportunity.[Account] LIKE @MarkerPattern
        UNION ALL
        SELECT bid.[Status], bid.[WorkflowState], instance.[CurrentStateKey]
        FROM [SSQMS].[Bid] AS bid
        LEFT JOIN [dbo].[WorkflowInstance] AS instance
          ON instance.[WorkflowKey] = N'SqmsBid'
         AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
        WHERE bid.[AccountNumber] LIKE @MarkerPattern
    ) AS states
    WHERE states.[CurrentStateKey] IS NULL
       OR states.[Status] <> states.[CurrentStateKey]
       OR states.[WorkflowState] <> states.[CurrentStateKey]
)
    THROW 51402, 'SQMS corrected-email target/workflow state mismatch.', 1;

IF EXISTS
(
    SELECT 1
    FROM [dbo].[NotificationOutbox]
    WHERE ([NotificationType] = N'SqmsOpportunityOpen'
           AND ([Subject] <> N'Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030'
                OR [Body] NOT LIKE N'%View all Opportunities open for Bidding%'))
       OR ([NotificationType] = N'SqmsOpportunityReminder'
           AND [Subject] <> N'REMINDER Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030')
       OR ([NotificationType] = N'SqmsOpportunityPendingSellerApproval'
           AND ([Subject] NOT LIKE N'Q1 2030 Secondary Sale Bids - %: Response Required by 01/22/2030'
                OR [Body] NOT LIKE N'%Link to approve or reject bids received:%'))
       OR ([NotificationType] = N'SqmsBidAccepted'
           AND [Body] NOT LIKE N'%has been accepted by the Seller for an effective date of 03/31/2030.%')
       OR ([NotificationType] = N'SqmsBidPartiallyAccepted'
           AND [Body] NOT LIKE N'%has been partially accepted by the Seller for an effective date of 03/31/2030.%')
       OR ([NotificationType] = N'SqmsBidNotAccepted'
           AND ([Body] NOT LIKE N'%has not been accepted by the Seller for an effective date of 03/31/2030.%'
                OR [Body] LIKE N'%Please confirm your buyer%'))
)
    THROW 51403, 'SQMS corrected-email content verification failed.', 1;

PRINT 'SQMS email-template live acceptance database verification passed.';
