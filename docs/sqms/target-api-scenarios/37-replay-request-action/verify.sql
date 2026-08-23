SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);
DECLARE @IdempotencyKey nvarchar(200) = N'SQMS:OPP:10000:5:RequestSellerApproval';

IF (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [ActionKey] = N'RequestSellerApproval') <> 1
    THROW 51000, 'Expected exactly one transition audit after replay.', 1;
IF (SELECT COUNT(*) FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [IdempotencyKey] = @IdempotencyKey) <> 1
    THROW 51000, 'Expected exactly one idempotency record after replay.', 1;
IF (SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [NotificationType] = N'SqmsOpportunityPendingSellerApproval') <> 1
    THROW 51000, 'Expected exactly one seller-approval outbox row after replay.', 1;
IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId AND [Status] = N'PENDING SELLER APPROVAL' AND [WorkflowState] = N'PENDING SELLER APPROVAL')
    THROW 51000, 'Committed target projection does not match the replay result.', 1;

SELECT [WorkflowInstanceId], [CurrentStateKey], [Version] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
