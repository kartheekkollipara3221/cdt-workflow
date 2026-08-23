-- Read-only verifier.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20002';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20002 AND [Status]=N'NOT ACCEPTED' AND [WorkflowState]=N'NOT ACCEPTED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'NOT ACCEPTED' AND [Status]=N'Completed' AND [Version]=5 AND [CompletedAtUtc] IS NOT NULL;
SELECT [ActionKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'RejectBid';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20002:4:RejectBid';
SELECT [NotificationType],[Status],[NotificationRecipientsJson] FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w AND [NotificationType]=N'SqmsBidNotAccepted';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
