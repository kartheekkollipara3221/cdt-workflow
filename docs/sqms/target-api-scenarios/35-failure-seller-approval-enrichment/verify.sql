SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [CurrentStateKey] = N'CLOSED TO BIDDING' AND [Version] = 5)
    THROW 51000, 'Seller-approval validation failure changed workflow state or version.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[WorkflowAudit]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [ActionKey] = N'RequestSellerApproval')
    THROW 51000, 'Seller-approval validation failure created an audit.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[IdempotencyRecord]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [IdempotencyKey] = N'SQMS:OPP:10000:5:RequestSellerApproval')
    THROW 51000, 'Seller-approval validation failure created an idempotency record.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[NotificationOutbox]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [NotificationType] = N'SqmsOpportunityPendingSellerApproval')
    THROW 51000, 'Seller-approval validation failure queued an email.', 1;

SELECT [ID], [Status], [WorkflowState], [EffectiveDateForSale]
FROM [SSQMS].[Opportunity]
WHERE [ID] = @EntityId;
