-- Read-only verifier.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20001';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Bid] WHERE [ID]=20001 AND [Status]=N'PARTIALLY ACCEPTED' AND [WorkflowState]=N'PARTIALLY ACCEPTED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'PARTIALLY ACCEPTED' AND [Status]=N'Completed' AND [Version]=5 AND [CompletedAtUtc] IS NOT NULL;
SELECT [ActionKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'PartiallyAcceptBid';
SELECT [IdempotencyKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:BID:20001:4:PartiallyAcceptBid';
SELECT [NotificationType],[Status],[NotificationRecipientsJson] FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w AND [NotificationType]=N'SqmsBidPartiallyAccepted';
SELECT COUNT(*) TaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
