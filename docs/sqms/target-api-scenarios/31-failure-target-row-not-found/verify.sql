SET NOCOUNT ON;

DECLARE @EntityId int = 999999;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);

IF EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId AND [IsDeleted] = 0)
    THROW 51000, 'Test prerequisite failed: active target Opportunity exists.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowInstance] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected workflow instance was created.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected workflow audit was created.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected idempotency record was created.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Unexpected notification was queued.', 1;

SELECT N'TargetNotFound side effects absent' AS [Verification];
