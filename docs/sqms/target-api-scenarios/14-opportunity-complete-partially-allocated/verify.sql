-- Read-only verifier; target values assume target-owned persistence committed.
DECLARE @w nvarchar(150) = N'WF-SQMS-OPP-10000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Opportunity] WHERE [ID]=10000 AND [Status]=N'COMPLETED' AND [WorkflowState]=N'COMPLETED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'COMPLETED' AND [Status]=N'Completed' AND [Version]=8 AND [CompletedAtUtc] IS NOT NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey],[PerformedByRoleKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'CompleteOpportunity' AND [FromStateKey]=N'PARTIALLY ALLOCATED' AND [ToStateKey]=N'COMPLETED';
SELECT [IdempotencyKey],[ActionKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:OPP:10000:7:CompleteOpportunity';
SELECT COUNT(*) UnexpectedOutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) SqmsTaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) SqmsSlaCount FROM [dbo].[WorkflowTaskSlaSchedule] sla JOIN [dbo].[WorkflowTask] task ON task.[Id]=sla.[WorkflowTaskId] WHERE task.[WorkflowInstanceId]=@w;
-- Compare non-workflow Opportunity columns with pre-request snapshot; adapter never writes them.
