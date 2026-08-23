SET NOCOUNT ON;

DECLARE @EntityId int = 10000;
DECLARE @WorkflowInstanceId nvarchar(150) = CONCAT(N'WF-SQMS-OPP-', @EntityId);

IF NOT EXISTS (
    SELECT 1 FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId AND [CurrentStateKey] = N'SUBMITTED' AND [Version] = 2)
    THROW 51000, 'Stale request changed workflow state or version.', 1;
IF (SELECT COUNT(*) FROM [dbo].[WorkflowAudit] WHERE [WorkflowInstanceId] = @WorkflowInstanceId) <> 1
    THROW 51000, 'Stale request changed audit count.', 1;
IF (SELECT COUNT(*) FROM [dbo].[IdempotencyRecord] WHERE [WorkflowInstanceId] = @WorkflowInstanceId) <> 1
    THROW 51000, 'Stale request changed idempotency count.', 1;
IF EXISTS (SELECT 1 FROM [dbo].[NotificationOutbox] WHERE [WorkflowInstanceId] = @WorkflowInstanceId)
    THROW 51000, 'Stale request queued a notification.', 1;

SELECT [ID], [Status], [WorkflowState] FROM [SSQMS].[Opportunity] WHERE [ID] = @EntityId;
