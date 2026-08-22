SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunMarker varchar(100) = 'SQMS-PROJECTION-OWNERSHIP-LIVE-20260822';
DECLARE @MarkerPattern varchar(101) = @RunMarker + '%';

SELECT [Id], [EvidenceKind], [EntityType], [TargetId], [Operation],
       [ReturnedState], [ReturnedVersion], [Unchanged], [RecordedAtUtc]
FROM [dbo].[SqmsProjectionOwnershipEvidence]
ORDER BY [Id];

SELECT opportunity.[ID], opportunity.[Account], opportunity.[Status], opportunity.[WorkflowState],
       instance.[CurrentStateKey], instance.[Version],
       opportunity.[TotalBid], opportunity.[LargestBid], opportunity.[LowestBid]
FROM [SSQMS].[Opportunity] AS opportunity
LEFT JOIN [dbo].[WorkflowInstance] AS instance
  ON instance.[WorkflowKey] = N'SqmsOpportunity'
 AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
WHERE opportunity.[Account] LIKE @MarkerPattern;

SELECT bid.[ID], bid.[OpportunityId], bid.[AccountNumber], bid.[Status], bid.[WorkflowState],
       instance.[CurrentStateKey], instance.[Version]
FROM [SSQMS].[Bid] AS bid
LEFT JOIN [dbo].[WorkflowInstance] AS instance
  ON instance.[WorkflowKey] = N'SqmsBid'
 AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
WHERE bid.[AccountNumber] LIKE @MarkerPattern
ORDER BY bid.[ID];

SELECT [Id], [NotificationType], [NotificationRecipientsJson], [Subject], [Body], [HtmlBody],
       [Status], [RetryCount], [LastError]
FROM [dbo].[NotificationOutbox]
ORDER BY [Id];

IF (SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @MarkerPattern) <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @MarkerPattern) <> 4
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowInstance] WHERE [WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid')) <> 5
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 25
    OR (SELECT COUNT(*) FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 25
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 0
    OR (SELECT COUNT(*) FROM [dbo].[WorkflowTaskSlaSchedule] AS schedule
        INNER JOIN [dbo].[WorkflowTask] AS task ON task.[Id] = schedule.[WorkflowTaskId]
        WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%') <> 0
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] = N'Sent' AND [RetryCount] = 0) <> 6
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] <> N'Sent' OR [RetryCount] <> 0) <> 0
    THROW 51601, 'SQMS projection-ownership structural verification failed.', 1;

IF (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityOpen') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityReminder') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsOpportunityPendingSellerApproval') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidAccepted') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidPartiallyAccepted') <> 1
    OR (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = N'SqmsBidNotAccepted') <> 1
    THROW 51602, 'SQMS projection-ownership notification-route verification failed.', 1;

IF (SELECT COUNT(*) FROM [SSQMS].[Opportunity]
    WHERE [Account] LIKE @MarkerPattern AND [Status] = N'COMPLETED' AND [WorkflowState] = N'COMPLETED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid]
        WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = N'ACCEPTED' AND [WorkflowState] = N'ACCEPTED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid]
        WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = N'PARTIALLY ACCEPTED' AND [WorkflowState] = N'PARTIALLY ACCEPTED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid]
        WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = N'NOT ACCEPTED' AND [WorkflowState] = N'NOT ACCEPTED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Bid]
        WHERE [AccountNumber] LIKE @MarkerPattern AND [Status] = N'NOT ALLOCATED' AND [WorkflowState] = N'NOT ALLOCATED') <> 1
    OR (SELECT COUNT(*) FROM [SSQMS].[Opportunity]
        WHERE [Account] LIKE @MarkerPattern
          AND ([TotalBid] <> 777 OR [LargestBid] <> 'L-SENTINEL' OR [LowestBid] <> 'S-SENTINEL')) <> 0
    THROW 51603, 'SQMS projection-ownership target state or sentinel verification failed.', 1;

IF EXISTS
(
    SELECT 1
    FROM
    (
        SELECT opportunity.[Status], opportunity.[WorkflowState], instance.[CurrentStateKey],
               instance.[Version], 10 AS [ExpectedVersion]
        FROM [SSQMS].[Opportunity] AS opportunity
        LEFT JOIN [dbo].[WorkflowInstance] AS instance
          ON instance.[WorkflowKey] = N'SqmsOpportunity'
         AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
        WHERE opportunity.[Account] LIKE @MarkerPattern
        UNION ALL
        SELECT bid.[Status], bid.[WorkflowState], instance.[CurrentStateKey], instance.[Version],
               CASE
                   WHEN bid.[AccountNumber] LIKE '%-BID-01' THEN 7
                   WHEN bid.[AccountNumber] LIKE '%-BID-04' THEN 3
                   ELSE 5
               END
        FROM [SSQMS].[Bid] AS bid
        LEFT JOIN [dbo].[WorkflowInstance] AS instance
          ON instance.[WorkflowKey] = N'SqmsBid'
         AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
        WHERE bid.[AccountNumber] LIKE @MarkerPattern
    ) AS states
    WHERE states.[CurrentStateKey] IS NULL
       OR states.[Status] <> states.[CurrentStateKey]
       OR states.[WorkflowState] <> states.[CurrentStateKey]
       OR states.[Version] <> states.[ExpectedVersion]
)
    THROW 51604, 'SQMS projection-ownership target/workflow parity verification failed.', 1;

IF (SELECT COUNT(*) FROM [dbo].[SqmsProjectionOwnershipEvidence]
    WHERE [EvidenceKind] = N'RequestUnchanged'
      AND [Unchanged] = 1 AND [BeforeJson] = [AfterJson]) <> 20
    OR (SELECT COUNT(*) FROM [dbo].[SqmsProjectionOwnershipEvidence]
        WHERE [EvidenceKind] = N'TargetOwnerPersisted'
          AND [Unchanged] = 0 AND [BeforeJson] <> [AfterJson]) <> 20
    OR (SELECT COUNT(*) FROM [dbo].[SqmsProjectionOwnershipEvidence]
        WHERE [EvidenceKind] = N'ScheduledProjected'
          AND [Unchanged] = 0 AND [BeforeJson] <> [AfterJson]) <> 5
    THROW 51605, 'SQMS projection-ownership evidence counts failed.', 1;

IF EXISTS
(
    SELECT 1
    FROM [dbo].[SqmsProjectionOwnershipEvidence] AS requestProof
    WHERE requestProof.[EvidenceKind] = N'RequestUnchanged'
      AND NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[SqmsProjectionOwnershipEvidence] AS targetProof
          WHERE targetProof.[EvidenceKind] = N'TargetOwnerPersisted'
            AND targetProof.[EntityType] = requestProof.[EntityType]
            AND targetProof.[TargetId] = requestProof.[TargetId]
            AND targetProof.[Operation] = requestProof.[Operation]
            AND targetProof.[ReturnedState] = requestProof.[ReturnedState]
            AND targetProof.[ReturnedVersion] = requestProof.[ReturnedVersion]
            AND targetProof.[BeforeJson] = requestProof.[AfterJson]
      )
)
    THROW 51606, 'SQMS projection-ownership request proof is missing target-owner persistence.', 1;

PRINT 'SQMS projection-ownership live acceptance database verification passed.';
