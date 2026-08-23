SET NOCOUNT ON;

DECLARE @EntityId int = 20000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-BID-', @EntityId);

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [CurrentStateKey] = N'UNDER REVIEW BY SELLER' AND [Version] = 4)
    THROW 51000, 'Bid-disposition validation failure changed workflow state or version.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[WorkflowAudit]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [ActionKey] IN (N'AcceptBid', N'PartiallyAcceptBid', N'RejectBid'))
    THROW 51000, 'Bid-disposition validation failure created an audit.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[IdempotencyRecord]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
      AND [IdempotencyKey] IN (
          N'SQMS:BID:20000:4:AcceptBid',
          N'SQMS:BID:20000:4:PartiallyAcceptBid',
          N'SQMS:BID:20000:4:RejectBid'))
    THROW 51000, 'Bid-disposition validation failure created an idempotency record.', 1;
IF EXISTS (
    SELECT 1 FROM [dbo].[NotificationOutbox]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
      AND [NotificationType] IN (N'SqmsBidAccepted', N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted'))
    THROW 51000, 'Bid-disposition validation failure queued an email.', 1;

SELECT [ID], [Status], [WorkflowState], [CommitmentAmountRequested]
FROM [SSQMS].[Bid]
WHERE [ID] = @EntityId;
