SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunMarker varchar(100) = 'SQMS-LIVE-20260821';
DECLARE @MarkerPattern varchar(101) = @RunMarker + '%';
DECLARE @ExpectedOpportunityCount int = 3;
DECLARE @ExpectedBidCount int = 15;
DECLARE @ExpectedEmailCount int = 19;

-- 1. Target Opportunities and final states.
SELECT
    opportunity.[ID], opportunity.[Account], opportunity.[AccountNumber],
    opportunity.[Status], opportunity.[WorkflowState],
    opportunity.[TotalBid], opportunity.[LargestBid], opportunity.[LowestBid],
    opportunity.[ModifiedBy], opportunity.[ModifiedDate]
FROM [SSQMS].[Opportunity] AS opportunity
WHERE opportunity.[Account] LIKE @MarkerPattern
ORDER BY opportunity.[ID];

-- 2. Target Bids grouped by parent and final state.
SELECT
    bid.[OpportunityId], bid.[ID], bid.[AccountName], bid.[AccountNumber],
    bid.[CommitmentAmountRequested], bid.[BidExpressedAsFairValue],
    bid.[Status], bid.[WorkflowState], bid.[ModifiedBy], bid.[ModifiedDate]
FROM [SSQMS].[Bid] AS bid
WHERE bid.[AccountNumber] LIKE @MarkerPattern
ORDER BY bid.[OpportunityId], bid.[ID];

-- 3. Target/workflow state and version side by side.
SELECT
    N'Opportunity' AS EntityType, opportunity.[ID] AS TargetId,
    opportunity.[Status] AS TargetStatus,
    opportunity.[WorkflowState] AS TargetWorkflowState,
    instance.[WorkflowInstanceId], instance.[CurrentStateKey],
    instance.[Version], instance.[Status] AS RuntimeStatus,
    instance.[CompletedAtUtc]
FROM [SSQMS].[Opportunity] AS opportunity
LEFT JOIN [dbo].[WorkflowInstance] AS instance
    ON instance.[WorkflowKey] = N'SqmsOpportunity'
   AND instance.[BusinessObjectType] = N'SqmsOpportunity'
   AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
WHERE opportunity.[Account] LIKE @MarkerPattern
UNION ALL
SELECT
    N'Bid', bid.[ID], bid.[Status], bid.[WorkflowState],
    instance.[WorkflowInstanceId], instance.[CurrentStateKey],
    instance.[Version], instance.[Status], instance.[CompletedAtUtc]
FROM [SSQMS].[Bid] AS bid
LEFT JOIN [dbo].[WorkflowInstance] AS instance
    ON instance.[WorkflowKey] = N'SqmsBid'
   AND instance.[BusinessObjectType] = N'SqmsBid'
   AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
WHERE bid.[AccountNumber] LIKE @MarkerPattern
ORDER BY EntityType, TargetId;

-- 4. Ordered audit paths for all 18 workflows.
SELECT
    audit.[WorkflowInstanceId], audit.[Id], audit.[ActionKey],
    audit.[FromStateKey], audit.[ToStateKey],
    audit.[PerformedByUserId], audit.[PerformedByRoleKey], audit.[CreatedAtUtc]
FROM [dbo].[WorkflowAudit] AS audit
WHERE audit.[WorkflowInstanceId] IN
(
    SELECT CONCAT(N'WF-SQMS-OPP-', opportunity.[ID])
    FROM [SSQMS].[Opportunity] AS opportunity
    WHERE opportunity.[Account] LIKE @MarkerPattern
    UNION ALL
    SELECT CONCAT(N'WF-SQMS-BID-', bid.[ID])
    FROM [SSQMS].[Bid] AS bid
    WHERE bid.[AccountNumber] LIKE @MarkerPattern
)
ORDER BY audit.[WorkflowInstanceId], audit.[Id];

-- 5. State mismatches. Expected: zero rows.
SELECT *
FROM
(
    SELECT
        N'Opportunity' AS EntityType, opportunity.[ID] AS TargetId,
        opportunity.[Status], opportunity.[WorkflowState], instance.[CurrentStateKey]
    FROM [SSQMS].[Opportunity] AS opportunity
    LEFT JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowKey] = N'SqmsOpportunity'
       AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
    WHERE opportunity.[Account] LIKE @MarkerPattern
    UNION ALL
    SELECT
        N'Bid', bid.[ID], bid.[Status], bid.[WorkflowState], instance.[CurrentStateKey]
    FROM [SSQMS].[Bid] AS bid
    LEFT JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowKey] = N'SqmsBid'
       AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
    WHERE bid.[AccountNumber] LIKE @MarkerPattern
) AS states
WHERE states.[CurrentStateKey] IS NULL
   OR states.[Status] <> states.[CurrentStateKey]
   OR states.[WorkflowState] <> states.[CurrentStateKey]
ORDER BY states.EntityType, states.TargetId;

-- 6. SQMS tasks and SLA schedules. Expected: zero rows in both sections.
SELECT N'Task' AS EvidenceType, task.[Id] AS EvidenceId,
       task.[WorkflowInstanceId], task.[Status]
FROM [dbo].[WorkflowTask] AS task
WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%'
UNION ALL
SELECT N'SlaSchedule', schedule.[Id], task.[WorkflowInstanceId], schedule.[Status]
FROM [dbo].[WorkflowTaskSlaSchedule] AS schedule
INNER JOIN [dbo].[WorkflowTask] AS task ON task.[Id] = schedule.[WorkflowTaskId]
WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%'
ORDER BY EvidenceType, EvidenceId;

-- 7. Outbox routes, recipients, rendered content, links, and delivery evidence.
SELECT
    outbox.[Id], outbox.[WorkflowInstanceId], outbox.[NotificationOccurrenceKey],
    outbox.[NotificationType], outbox.[NotificationRecipientsJson],
    outbox.[Subject], outbox.[Body], outbox.[HtmlBody],
    outbox.[Status], outbox.[RetryCount], outbox.[LastError],
    outbox.[CreatedAtUtc], outbox.[SentAtUtc]
FROM [dbo].[NotificationOutbox] AS outbox
ORDER BY outbox.[Id];

-- 8. Scenario and route counts.
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

-- 9. Parent Opportunity final state evidence beside every Bid.
SELECT
    opportunity.[ID] AS OpportunityId,
    opportunity.[Status] AS OpportunityStatus,
    opportunity.[WorkflowState] AS OpportunityWorkflowState,
    bid.[ID] AS BidId, bid.[Status] AS BidStatus,
    bid.[WorkflowState] AS BidWorkflowState
FROM [SSQMS].[Opportunity] AS opportunity
INNER JOIN [SSQMS].[Bid] AS bid ON bid.[OpportunityId] = opportunity.[ID]
WHERE opportunity.[Account] LIKE @MarkerPattern
ORDER BY opportunity.[ID], bid.[ID];

-- 10. Calculated-column sentinels. Every row must remain unchanged.
SELECT
    opportunity.[ID], opportunity.[TotalBid],
    opportunity.[LargestBid], opportunity.[LowestBid],
    CASE WHEN opportunity.[TotalBid] = 777
              AND opportunity.[LargestBid] = 'L-SENTINEL'
              AND opportunity.[LowestBid] = 'S-SENTINEL'
         THEN N'PASS' ELSE N'FAIL' END AS Verdict
FROM [SSQMS].[Opportunity] AS opportunity
WHERE opportunity.[Account] LIKE @MarkerPattern
ORDER BY opportunity.[ID];

-- Human-readable expected-versus-actual summary.
SELECT N'ExpectedOpportunityCount' AS CheckName, @ExpectedOpportunityCount AS Expected,
       (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern) AS Actual
UNION ALL
SELECT N'ExpectedBidCount', @ExpectedBidCount,
       (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern)
UNION ALL
SELECT N'ExpectedEmailCount', @ExpectedEmailCount,
       (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] = N'Sent')
UNION ALL
SELECT N'ExpectedAuditCount', 82,
       (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%')
UNION ALL
SELECT N'ExpectedTaskCount', 0,
       (SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%')
UNION ALL
SELECT N'ExpectedSlaCount', 0,
       (SELECT COUNT(*) FROM [dbo].[WorkflowTaskSlaSchedule] AS schedule
        INNER JOIN [dbo].[WorkflowTask] AS task ON task.[Id] = schedule.[WorkflowTaskId]
        WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%');

IF (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern) <> @ExpectedOpportunityCount
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern) <> @ExpectedBidCount
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowInstance] WHERE [WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid')) <> 18
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 82
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 0
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] = N'Sent' AND [RetryCount] = 0) <> @ExpectedEmailCount
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
    THROW 51301, 'SQMS live acceptance verification failed.', 1;

PRINT 'SQMS live acceptance database verification passed.';
