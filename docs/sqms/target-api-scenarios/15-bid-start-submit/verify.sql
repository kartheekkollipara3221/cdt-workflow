-- Read-only verifier; target values assume target-owned persistence committed.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'SUBMITTED' AND [WorkflowState]=N'SUBMITTED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'SUBMITTED' AND [Status]=N'Active' AND [Version]=2 AND [CompletedAtUtc] IS NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'SubmitBid' AND [FromStateKey]=N'Draft' AND [ToStateKey]=N'SUBMITTED';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AS OutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AS TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
