-- Read-only: run before and after GET; all values/counts must remain identical.
DECLARE @w nvarchar(150) = N'WF-SQMS-OPP-10000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Opportunity] WHERE [ID]=10000;
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AuditCount FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) IdempotencyCount FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) OutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
