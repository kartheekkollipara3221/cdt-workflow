-- Read-only before/after verifier: these counts and version must remain identical.
DECLARE @w nvarchar(150)=N'WF-SQMS-BID-20000';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AS AuditCount FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AS IdempotencyCount FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) AS OutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
