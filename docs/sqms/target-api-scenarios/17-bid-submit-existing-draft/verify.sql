-- Read-only verifier.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'SUBMITTED' AND [WorkflowState]=N'SUBMITTED';
SELECT [CurrentStateKey],[Status],[Version] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'SUBMITTED' AND [Version]=4;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'SubmitBid' AND [FromStateKey]=N'Draft' AND [ToStateKey]=N'SUBMITTED';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:3:SubmitBid';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
