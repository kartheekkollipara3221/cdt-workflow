-- Read-only verifier.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'Draft' AND [WorkflowState]=N'Draft';
SELECT [CurrentStateKey],[Status],[Version] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'Draft' AND [Version]=3;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'ReturnBidToDraft';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:2:ReturnBidToDraft';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
