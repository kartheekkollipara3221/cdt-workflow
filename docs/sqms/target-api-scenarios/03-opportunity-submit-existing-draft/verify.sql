-- Read-only; target values assume target-owned persistence committed.
DECLARE @w nvarchar(150) = N'WF-SQMS-OPP-10000';
SELECT [ID],[Status],[WorkflowState] FROM [SSQMS].[Opportunity] WHERE [ID]=10000 AND [Status]=N'SUBMITTED' AND [WorkflowState]=N'SUBMITTED';
SELECT [CurrentStateKey],[Status],[Version],[CompletedAtUtc] FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId]=@w AND [CurrentStateKey]=N'SUBMITTED' AND [Status]=N'Active' AND [Version]=2 AND [CompletedAtUtc] IS NULL;
SELECT [ActionKey],[FromStateKey],[ToStateKey],[PerformedByRoleKey] FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId]=@w AND [ActionKey]=N'SubmitOpportunity' AND [FromStateKey]=N'Draft' AND [ToStateKey]=N'SUBMITTED';
SELECT [IdempotencyKey],[ActionKey] FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId]=@w AND [IdempotencyKey]=N'SQMS:OPP:10000:1:SubmitOpportunity';
SELECT COUNT(*) UnexpectedOutboxCount FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) SqmsTaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId]=@w;
SELECT COUNT(*) SqmsSlaCount FROM [dbo].[WorkflowTaskSlaSchedule] sla JOIN [dbo].[WorkflowTask] task ON task.[Id]=sla.[WorkflowTaskId] WHERE task.[WorkflowInstanceId]=@w;
-- Compare non-workflow Opportunity columns with pre-request snapshot; adapter never writes them.
