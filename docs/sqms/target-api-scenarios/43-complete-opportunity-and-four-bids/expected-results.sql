SET NOCOUNT ON;

-- Read-only target-team verifier. Change these IDs only to the newly saved rows in an
-- isolated acceptance database; do not point this file at the retained reference database.
DECLARE @OpportunityId int = 10000;
DECLARE @AcceptedBidId int = 10000;
DECLARE @PartiallyAcceptedBidId int = 10001;
DECLARE @NotAcceptedBidId int = 10002;
DECLARE @NotAllocatedBidId int = 10003;

IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] o JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-OPP-', o.[ID]) WHERE o.[ID] = @OpportunityId AND o.[Status] = N'COMPLETED' AND o.[WorkflowState] = N'COMPLETED' AND w.[CurrentStateKey] = N'COMPLETED' AND w.[Version] = 10)
    THROW 51000, 'Opportunity final state/version mismatch.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] b JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-BID-', b.[ID]) WHERE b.[ID] = @AcceptedBidId AND b.[Status] = N'ACCEPTED' AND b.[WorkflowState] = N'ACCEPTED' AND w.[CurrentStateKey] = N'ACCEPTED' AND w.[Version] = 7)
    THROW 51000, 'Accepted Bid final state/version mismatch.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] b JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-BID-', b.[ID]) WHERE b.[ID] = @PartiallyAcceptedBidId AND b.[Status] = N'PARTIALLY ACCEPTED' AND b.[WorkflowState] = N'PARTIALLY ACCEPTED' AND w.[CurrentStateKey] = N'PARTIALLY ACCEPTED' AND w.[Version] = 5)
    THROW 51000, 'Partially accepted Bid final state/version mismatch.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] b JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-BID-', b.[ID]) WHERE b.[ID] = @NotAcceptedBidId AND b.[Status] = N'NOT ACCEPTED' AND b.[WorkflowState] = N'NOT ACCEPTED' AND w.[CurrentStateKey] = N'NOT ACCEPTED' AND w.[Version] = 5)
    THROW 51000, 'Not accepted Bid final state/version mismatch.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] b JOIN [dbo].[WorkflowInstance] w ON w.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-BID-', b.[ID]) WHERE b.[ID] = @NotAllocatedBidId AND b.[Status] = N'NOT ALLOCATED' AND b.[WorkflowState] = N'NOT ALLOCATED' AND w.[CurrentStateKey] = N'NOT ALLOCATED' AND w.[Version] = 3)
    THROW 51000, 'Not allocated Bid final state/version mismatch.', 1;

IF (SELECT COUNT(*) FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))) <> 5
    THROW 51000, 'Expected five workflow instances.', 1;

IF (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))) <> 25
    THROW 51000, 'Expected 25 workflow audits.', 1;

IF (SELECT COUNT(*) FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))) <> 25
    THROW 51000, 'Expected 25 idempotency records.', 1;

IF (SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))) <> 0
    THROW 51000, 'Expected zero SQMS tasks.', 1;

IF (SELECT COUNT(*) FROM [dbo].[WorkflowTaskSlaSchedule] s
    JOIN [dbo].[WorkflowTask] t ON t.[Id] = s.[WorkflowTaskId]
    WHERE t.[WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))) <> 0
    THROW 51000, 'Expected zero SQMS SLA schedules.', 1;

IF EXISTS (
    SELECT expectedRoute.RouteKey
    FROM (VALUES
        (N'SqmsOpportunityOpen'),
        (N'SqmsOpportunityReminder'),
        (N'SqmsOpportunityPendingSellerApproval'),
        (N'SqmsBidAccepted'),
        (N'SqmsBidPartiallyAccepted'),
        (N'SqmsBidNotAccepted')) expectedRoute(RouteKey)
    OUTER APPLY (
        SELECT COUNT(*) AS RouteCount
        FROM [dbo].[NotificationOutbox] n
        WHERE n.[NotificationType] = expectedRoute.RouteKey
          AND n.[Status] = N'Sent'
          AND n.[RetryCount] = 0) actual
    WHERE actual.RouteCount <> 1)
    THROW 51000, 'Expected exactly one sent retry-zero message for each SQMS route.', 1;

SELECT [WorkflowInstanceId], [CurrentStateKey], [Status], [Version], [CompletedAtUtc]
FROM [dbo].[WorkflowInstance]
WHERE [WorkflowInstanceId] IN (CONCAT(N'WF-SQMS-OPP-', @OpportunityId), CONCAT(N'WF-SQMS-BID-', @AcceptedBidId), CONCAT(N'WF-SQMS-BID-', @PartiallyAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAcceptedBidId), CONCAT(N'WF-SQMS-BID-', @NotAllocatedBidId))
ORDER BY [WorkflowInstanceId];

SELECT [NotificationType], [Subject], [Status], [RetryCount], [SentAtUtc],
       [NotificationRecipientsJson]
FROM [dbo].[NotificationOutbox]
WHERE [NotificationType] IN (
    N'SqmsOpportunityOpen', N'SqmsOpportunityReminder',
    N'SqmsOpportunityPendingSellerApproval', N'SqmsBidAccepted',
    N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted')
ORDER BY [Id];

-- Retained-only ownership evidence was 20 RequestUnchanged, 20 TargetOwnerPersisted,
-- and 5 ScheduledProjected rows. A target team should capture equivalent before/after
-- snapshots in its own test harness; SqmsProjectionOwnershipEvidence is testing-only and
-- is intentionally not assumed to exist in a target database.
