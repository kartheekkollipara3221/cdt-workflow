-- Read-only verifier; compare non-workflow Bid columns with a pre-run snapshot.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState],[ModifiedBy],[ModifiedDate] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'NOT ALLOCATED' AND [WorkflowState]=N'NOT ALLOCATED' AND [ModifiedBy]=N'system';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'NOT ALLOCATED' AND [Status]=N'Completed' AND [Version]=3 AND [CompletedAtUtc] IS NOT NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'MarkBidNotAllocated' AND [FromStateKey]=N'SUBMITTED' AND [ToStateKey]=N'NOT ALLOCATED';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:2:MarkBidNotAllocated';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) OutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
