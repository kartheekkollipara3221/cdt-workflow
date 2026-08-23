-- Read-only verifier; compare non-workflow Bid columns with a pre-run snapshot.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState],[ModifiedBy],[ModifiedDate] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'IPG REVIEW' AND [WorkflowState]=N'IPG REVIEW' AND [ModifiedBy]=N'system';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'IPG REVIEW' AND [Status]=N'Active' AND [Version]=3 AND [CompletedAtUtc] IS NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'MoveBidToIpgReview' AND [FromStateKey]=N'SUBMITTED' AND [ToStateKey]=N'IPG REVIEW';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:2:MoveBidToIpgReview';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) OutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
