-- Read-only verifier. Capture non-workflow columns before the scheduled run for comparison.
DECLARE @w nvarchar(150) = N'WF-SQMS-OPP-10000';
SELECT [ID], [Status], [WorkflowState], [ModifiedBy], [ModifiedDate]
FROM [SSQMS].[Opportunity]
WHERE [ID] = 10000
  AND [Status] = N'CLOSED TO BIDDING'
  AND [WorkflowState] = N'CLOSED TO BIDDING'
  AND [ModifiedBy] = N'system';

SELECT [CurrentStateKey], [Status], [Version], [CompletedAtUtc]
FROM [dbo].[WorkflowInstance]
WHERE [WorkflowInstanceId] = @w
  AND [CurrentStateKey] = N'CLOSED TO BIDDING'
  AND [Status] = N'Active'
  AND [Version] = 5
  AND [CompletedAtUtc] IS NULL;

SELECT [ActionKey], [FromStateKey], [ToStateKey], [PerformedByRoleKey]
FROM [dbo].[WorkflowAudit]
WHERE [WorkflowInstanceId] = @w
  AND [ActionKey] = N'CloseOpportunityBidding'
  AND [FromStateKey] = N'OPEN TO BIDDING'
  AND [ToStateKey] = N'CLOSED TO BIDDING'
  AND [PerformedByRoleKey] = N'System';

SELECT [IdempotencyKey], [ActionKey]
FROM [dbo].[IdempotencyRecord]
WHERE [WorkflowInstanceId] = @w
  AND [IdempotencyKey] = N'SQMS:OPP:10000:4:CloseOpportunityBidding';

SELECT COUNT(*) AS SqmsTaskCount FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] = @w;
SELECT COUNT(*) AS SqmsSlaCount
FROM [dbo].[WorkflowTaskSlaSchedule] AS sla
INNER JOIN [dbo].[WorkflowTask] AS task ON task.[Id] = sla.[WorkflowTaskId]
WHERE task.[WorkflowInstanceId] = @w;
