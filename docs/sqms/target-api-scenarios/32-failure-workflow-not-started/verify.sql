SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);

IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId AND [IsDeleted] = 0)
    THROW 51000, 'Test prerequisite failed: target Opportunity is absent.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Context or Action lazily started a workflow.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected workflow audit was created.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected idempotency record was created.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected notification was queued.', 1;

SELECT [ID], [Status], [WorkflowState] FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId;
