-- Read-only verifier; inspect immutable recipients without deriving them from public JSON.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20000 AND [Status]=N'ACCEPTED' AND [WorkflowState]=N'ACCEPTED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'ACCEPTED' AND [Status]=N'Completed' AND [Version]=5 AND [CompletedAtUtc] IS NOT NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'AcceptBid';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20000:4:AcceptBid';
SELECT [NotificationType],[Status],[NotificationRecipientsJson] FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w AND [NotificationType]=N'SqmsBidAccepted';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
