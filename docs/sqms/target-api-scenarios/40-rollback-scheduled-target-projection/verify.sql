SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId AND [Status] = N'OPEN TO BIDDING' AND [WorkflowState] = N'OPEN TO BIDDING')
    THROW 51000, 'Scheduled projection changed the target row despite failure.', 1;
IF NOT EXISTS (SELECT 1 FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [CurrentStateKey] = N'OPEN TO BIDDING' AND [Version] = 4)
    THROW 51000, 'Scheduled workflow transition survived projection failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [ActionKey] = N'CloseOpportunityBidding')
    THROW 51000, 'Scheduled audit survived projection failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [IdempotencyKey] = N'SQMS:OPP:10000:4:CloseOpportunityBidding')
    THROW 51000, 'Scheduled idempotency record survived projection failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Scheduled transition outbox survived projection failure.', 1;
