-- Read-only verifier.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'UNDER REVIEW BY SELLER' AND [WorkflowState]=N'UNDER REVIEW BY SELLER';
SELECT [CurrentStateKey],[Status],[Version] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'UNDER REVIEW BY SELLER' AND [Version]=4;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'SendBidToSellerReview';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:3:SendBidToSellerReview';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
