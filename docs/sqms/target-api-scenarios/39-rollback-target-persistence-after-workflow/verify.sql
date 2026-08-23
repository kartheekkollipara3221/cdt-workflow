SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId AND [Status] = N'CLOSED TO BIDDING' AND [WorkflowState] = N'CLOSED TO BIDDING')
    THROW 51000, 'Target projection survived the rolled-back request.', 1;
IF NOT EXISTS (SELECT 1 FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [CurrentStateKey] = N'CLOSED TO BIDDING' AND [Version] = 5)
    THROW 51000, 'Workflow transition survived the target failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [ActionKey] = N'RequestSellerApproval')
    THROW 51000, 'Audit survived the target failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [IdempotencyKey] = N'SQMS:OPP:10000:5:RequestSellerApproval')
    THROW 51000, 'Idempotency record survived the target failure.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [NotificationType] = N'SqmsOpportunityPendingSellerApproval')
    THROW 51000, 'Outbox row survived the target failure.', 1;
