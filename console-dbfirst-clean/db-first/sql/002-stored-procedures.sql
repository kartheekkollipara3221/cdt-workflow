SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowDefinition_GetActiveVersion]
    @WorkflowKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        definition.[Id] AS [WorkflowDefinitionId],
        definition.[WorkflowKey],
        definition.[DisplayName],
        version.[Id] AS [WorkflowDefinitionVersionId],
        version.[VersionNumber],
        version.[CreatedAtUtc]
    FROM [dbo].[WorkflowDefinition] AS definition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.[WorkflowDefinitionId] = definition.[Id]
    WHERE definition.[WorkflowKey] = @WorkflowKey
        AND definition.[IsActive] = 1
        AND version.[IsActive] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowMetadata_GetForVersion]
    @WorkflowDefinitionVersionId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowStateDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [Id];

    SELECT *
    FROM [dbo].[WorkflowActionDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [SortOrder], [Id];

    SELECT *
    FROM [dbo].[WorkflowRoleDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [Id];

    SELECT *
    FROM [dbo].[WorkflowTaskDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [Id];

    SELECT *
    FROM [dbo].[WorkflowTransitionDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [FromStateKey], [ActionKey], [AllowedRoleKey];

    SELECT *
    FROM [dbo].[SlaPolicy]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [Id];

    SELECT *
    FROM [dbo].[NotificationRoute]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [IsActive] = 1
    ORDER BY [Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowInstance_Get]
    @WorkflowInstanceId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowInstance_GetByBusinessObject]
    @WorkflowKey nvarchar(100),
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowKey] = @WorkflowKey
        AND [BusinessObjectType] = @BusinessObjectType
        AND [BusinessObjectId] = @BusinessObjectId;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowInstance_Insert]
    @WorkflowInstanceId nvarchar(150),
    @WorkflowKey nvarchar(100),
    @WorkflowDefinitionVersionId bigint,
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150),
    @CurrentStateKey nvarchar(100),
    @Status nvarchar(30),
    @StartedAtUtc datetimeoffset,
    @StartedByUserId nvarchar(200),
    @CompletedAtUtc datetimeoffset = NULL,
    @Version int
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[WorkflowInstance] (
        [WorkflowInstanceId],
        [WorkflowKey],
        [WorkflowDefinitionVersionId],
        [BusinessObjectType],
        [BusinessObjectId],
        [CurrentStateKey],
        [Status],
        [StartedAtUtc],
        [StartedByUserId],
        [CompletedAtUtc],
        [Version])
    VALUES (
        @WorkflowInstanceId,
        @WorkflowKey,
        @WorkflowDefinitionVersionId,
        @BusinessObjectType,
        @BusinessObjectId,
        @CurrentStateKey,
        @Status,
        @StartedAtUtc,
        @StartedByUserId,
        @CompletedAtUtc,
        @Version);

    SELECT *
    FROM [dbo].[WorkflowInstance]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowInstance_UpdateState]
    @WorkflowInstanceId nvarchar(150),
    @ExpectedVersion int,
    @CurrentStateKey nvarchar(100),
    @Status nvarchar(30),
    @CompletedAtUtc datetimeoffset = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[WorkflowInstance]
    SET
        [CurrentStateKey] = @CurrentStateKey,
        [Status] = @Status,
        [CompletedAtUtc] = @CompletedAtUtc,
        [Version] = [Version] + 1
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
        AND [Version] = @ExpectedVersion;

    SELECT @@ROWCOUNT AS [RowsAffected];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTask_GetPending]
    @WorkflowInstanceId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1) *
    FROM [dbo].[WorkflowTask]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
        AND [Status] = N'Pending'
    ORDER BY [Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTask_Insert]
    @WorkflowInstanceId nvarchar(150),
    @TaskKey nvarchar(100),
    @AssignedRoleKey nvarchar(100),
    @AssignedUserId nvarchar(200) = NULL,
    @Status nvarchar(30),
    @CreatedAtUtc datetimeoffset,
    @DueAtUtc datetimeoffset = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[WorkflowTask] (
        [WorkflowInstanceId],
        [TaskKey],
        [AssignedRoleKey],
        [AssignedUserId],
        [Status],
        [CreatedAtUtc],
        [DueAtUtc],
        [CompletedAtUtc],
        [CompletedByUserId],
        [ClosedByActionKey])
    VALUES (
        @WorkflowInstanceId,
        @TaskKey,
        @AssignedRoleKey,
        @AssignedUserId,
        @Status,
        @CreatedAtUtc,
        @DueAtUtc,
        NULL,
        NULL,
        NULL);

    SELECT *
    FROM [dbo].[WorkflowTask]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTask_Complete]
    @WorkflowInstanceId nvarchar(150),
    @TaskKey nvarchar(100),
    @CompletedAtUtc datetimeoffset,
    @CompletedByUserId nvarchar(200),
    @ClosedByActionKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[WorkflowTask]
    SET
        [Status] = N'Completed',
        [CompletedAtUtc] = @CompletedAtUtc,
        [CompletedByUserId] = @CompletedByUserId,
        [ClosedByActionKey] = @ClosedByActionKey
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
        AND [TaskKey] = @TaskKey
        AND [Status] = N'Pending';

    SELECT @@ROWCOUNT AS [RowsAffected];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_MockSectionData_Insert]
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150),
    @SectionKey nvarchar(100),
    @PayloadJson nvarchar(max),
    @SavedByUserId nvarchar(200),
    @SavedAtUtc datetimeoffset,
    @CorrelationId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[MockSectionData] (
        [BusinessObjectType],
        [BusinessObjectId],
        [SectionKey],
        [PayloadJson],
        [SavedByUserId],
        [SavedAtUtc],
        [CorrelationId])
    VALUES (
        @BusinessObjectType,
        @BusinessObjectId,
        @SectionKey,
        @PayloadJson,
        @SavedByUserId,
        @SavedAtUtc,
        @CorrelationId);

    SELECT *
    FROM [dbo].[MockSectionData]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowAudit_Insert]
    @WorkflowInstanceId nvarchar(150),
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150),
    @ActionKey nvarchar(100),
    @FromStateKey nvarchar(100),
    @ToStateKey nvarchar(100),
    @PerformedByUserId nvarchar(200),
    @PerformedByRoleKey nvarchar(100),
    @Reason nvarchar(1000) = NULL,
    @CorrelationId nvarchar(150),
    @CreatedAtUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[WorkflowAudit] (
        [WorkflowInstanceId],
        [BusinessObjectType],
        [BusinessObjectId],
        [ActionKey],
        [FromStateKey],
        [ToStateKey],
        [PerformedByUserId],
        [PerformedByRoleKey],
        [Reason],
        [CorrelationId],
        [CreatedAtUtc])
    VALUES (
        @WorkflowInstanceId,
        @BusinessObjectType,
        @BusinessObjectId,
        @ActionKey,
        @FromStateKey,
        @ToStateKey,
        @PerformedByUserId,
        @PerformedByRoleKey,
        @Reason,
        @CorrelationId,
        @CreatedAtUtc);

    SELECT *
    FROM [dbo].[WorkflowAudit]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_Insert]
    @WorkflowInstanceId nvarchar(150),
    @WorkflowTaskId bigint = NULL,
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150),
    @NotificationType nvarchar(100),
    @RecipientRoleKey nvarchar(100) = NULL,
    @RecipientUserId nvarchar(200) = NULL,
    @Subject nvarchar(300),
    @PayloadJson nvarchar(max),
    @Status nvarchar(30),
    @CorrelationId nvarchar(150),
    @CreatedAtUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[NotificationOutbox] (
        [WorkflowInstanceId],
        [WorkflowTaskId],
        [BusinessObjectType],
        [BusinessObjectId],
        [NotificationType],
        [RecipientRoleKey],
        [RecipientUserId],
        [Subject],
        [PayloadJson],
        [Status],
        [CorrelationId],
        [CreatedAtUtc])
    VALUES (
        @WorkflowInstanceId,
        @WorkflowTaskId,
        @BusinessObjectType,
        @BusinessObjectId,
        @NotificationType,
        @RecipientRoleKey,
        @RecipientUserId,
        @Subject,
        @PayloadJson,
        @Status,
        @CorrelationId,
        @CreatedAtUtc);

    SELECT *
    FROM [dbo].[NotificationOutbox]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_IdempotencyRecord_GetByKey]
    @IdempotencyKey nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[IdempotencyRecord]
    WHERE [IdempotencyKey] = @IdempotencyKey;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_IdempotencyRecord_Insert]
    @IdempotencyKey nvarchar(200),
    @WorkflowInstanceId nvarchar(150),
    @ActionKey nvarchar(100),
    @RequestHash nvarchar(128),
    @ResultHash nvarchar(128),
    @ResultJson nvarchar(max),
    @CreatedAtUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[IdempotencyRecord] (
        [IdempotencyKey],
        [WorkflowInstanceId],
        [ActionKey],
        [RequestHash],
        [ResultHash],
        [ResultJson],
        [CreatedAtUtc])
    VALUES (
        @IdempotencyKey,
        @WorkflowInstanceId,
        @ActionKey,
        @RequestHash,
        @ResultHash,
        @ResultJson,
        @CreatedAtUtc);

    SELECT *
    FROM [dbo].[IdempotencyRecord]
    WHERE [Id] = CONVERT(bigint, SCOPE_IDENTITY());
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_RuntimeData_Reset]
    @WorkflowInstanceId nvarchar(150),
    @BusinessObjectType nvarchar(100),
    @BusinessObjectId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdempotencyRecordRows int = 0;
    DECLARE @NotificationOutboxRows int = 0;
    DECLARE @WorkflowAuditRows int = 0;
    DECLARE @MockSectionDataRows int = 0;
    DECLARE @WorkflowTaskRows int = 0;
    DECLARE @WorkflowInstanceRows int = 0;

    DELETE FROM [dbo].[IdempotencyRecord]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
    SET @IdempotencyRecordRows = @@ROWCOUNT;

    DELETE FROM [dbo].[NotificationOutbox]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
    SET @NotificationOutboxRows = @@ROWCOUNT;

    DELETE FROM [dbo].[WorkflowAudit]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
    SET @WorkflowAuditRows = @@ROWCOUNT;

    DELETE FROM [dbo].[MockSectionData]
    WHERE [BusinessObjectType] = @BusinessObjectType
        AND [BusinessObjectId] = @BusinessObjectId;
    SET @MockSectionDataRows = @@ROWCOUNT;

    DELETE FROM [dbo].[WorkflowTask]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
    SET @WorkflowTaskRows = @@ROWCOUNT;

    DELETE FROM [dbo].[WorkflowInstance]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId;
    SET @WorkflowInstanceRows = @@ROWCOUNT;

    SELECT
        @IdempotencyRecordRows AS [IdempotencyRecordRows],
        @NotificationOutboxRows AS [NotificationOutboxRows],
        @WorkflowAuditRows AS [WorkflowAuditRows],
        @MockSectionDataRows AS [MockSectionDataRows],
        @WorkflowTaskRows AS [WorkflowTaskRows],
        @WorkflowInstanceRows AS [WorkflowInstanceRows];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowDefinitionVersion_Get]
    @WorkflowDefinitionVersionId bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowDefinitionVersion]
    WHERE [Id] = @WorkflowDefinitionVersionId;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowActionDefinition_Get]
    @WorkflowDefinitionVersionId bigint,
    @ActionKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowActionDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [ActionKey] = @ActionKey
        AND [IsActive] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTransitionDefinition_ListForAction]
    @WorkflowDefinitionVersionId bigint,
    @FromStateKey nvarchar(100),
    @ActionKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowTransitionDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [FromStateKey] = @FromStateKey
        AND [ActionKey] = @ActionKey
        AND [IsActive] = 1
    ORDER BY [Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTaskDefinition_Get]
    @WorkflowDefinitionVersionId bigint,
    @TaskKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[WorkflowTaskDefinition]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [TaskKey] = @TaskKey
        AND [IsActive] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowTaskDefinition_HasSla]
    @WorkflowDefinitionVersionId bigint,
    @TaskKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM [dbo].[WorkflowTaskDefinition]
        WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
            AND [TaskKey] = @TaskKey
            AND [SlaPolicyKey] IS NOT NULL
            AND [SlaPolicyKey] <> N''
            AND [IsActive] = 1)
        THEN 1 ELSE 0 END AS [HasSla];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_SlaPolicy_GetDurationMinutes]
    @WorkflowDefinitionVersionId bigint,
    @SlaPolicyKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [DurationMinutes]
    FROM [dbo].[SlaPolicy]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [SlaPolicyKey] = @SlaPolicyKey
        AND [IsActive] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationRoute_GetByRouteKey]
    @WorkflowDefinitionVersionId bigint,
    @RouteKey nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[NotificationRoute]
    WHERE [WorkflowDefinitionVersionId] = @WorkflowDefinitionVersionId
        AND [RouteKey] = @RouteKey
        AND [IsActive] = 1;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowContext_Get]
    @WorkflowInstanceId nvarchar(150),
    @RoleKeys nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        instance.*,
        version.[VersionNumber] AS [WorkflowDefinitionVersionNumber],
        COALESCE(stateDefinition.[DisplayName], instance.[CurrentStateKey]) AS [CurrentStateLabel]
    FROM [dbo].[WorkflowInstance] AS instance
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.[Id] = instance.[WorkflowDefinitionVersionId]
    LEFT JOIN [dbo].[WorkflowStateDefinition] AS stateDefinition
        ON stateDefinition.[WorkflowDefinitionVersionId] = instance.[WorkflowDefinitionVersionId]
        AND stateDefinition.[StateKey] = instance.[CurrentStateKey]
        AND stateDefinition.[IsActive] = 1
    WHERE instance.[WorkflowInstanceId] = @WorkflowInstanceId;

    SELECT TOP (1)
        task.*,
        COALESCE(taskDefinition.[DisplayName], task.[TaskKey]) AS [TaskLabel]
    FROM [dbo].[WorkflowTask] AS task
    LEFT JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowInstanceId] = task.[WorkflowInstanceId]
    LEFT JOIN [dbo].[WorkflowTaskDefinition] AS taskDefinition
        ON taskDefinition.[WorkflowDefinitionVersionId] = instance.[WorkflowDefinitionVersionId]
        AND taskDefinition.[TaskKey] = task.[TaskKey]
        AND taskDefinition.[IsActive] = 1
    WHERE task.[WorkflowInstanceId] = @WorkflowInstanceId
        AND task.[Status] = N'Pending'
    ORDER BY task.[Id];

    SELECT
        actionDefinition.*
    FROM [dbo].[WorkflowInstance] AS instance
    INNER JOIN [dbo].[WorkflowTransitionDefinition] AS transitionDefinition
        ON transitionDefinition.[WorkflowDefinitionVersionId] = instance.[WorkflowDefinitionVersionId]
        AND transitionDefinition.[FromStateKey] = instance.[CurrentStateKey]
        AND transitionDefinition.[IsActive] = 1
    INNER JOIN [dbo].[WorkflowActionDefinition] AS actionDefinition
        ON actionDefinition.[WorkflowDefinitionVersionId] = transitionDefinition.[WorkflowDefinitionVersionId]
        AND actionDefinition.[ActionKey] = transitionDefinition.[ActionKey]
        AND actionDefinition.[IsActive] = 1
    INNER JOIN STRING_SPLIT(@RoleKeys, N',') AS roleKey
        ON LTRIM(RTRIM(roleKey.[value])) = transitionDefinition.[AllowedRoleKey]
    WHERE instance.[WorkflowInstanceId] = @WorkflowInstanceId
        AND instance.[Status] <> N'Completed'
    ORDER BY actionDefinition.[SortOrder], actionDefinition.[Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_WorkflowInspection_Get]
    @WorkflowInstanceId nvarchar(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        instance.*,
        version.[VersionNumber] AS [WorkflowDefinitionVersionNumber]
    FROM [dbo].[WorkflowInstance] AS instance
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.[Id] = instance.[WorkflowDefinitionVersionId]
    WHERE instance.[WorkflowInstanceId] = @WorkflowInstanceId;

    SELECT *
    FROM [dbo].[WorkflowTask]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
    ORDER BY [CreatedAtUtc], [Id];

    SELECT *
    FROM [dbo].[WorkflowAudit]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
    ORDER BY [CreatedAtUtc], [Id];

    SELECT *
    FROM [dbo].[NotificationOutbox]
    WHERE [WorkflowInstanceId] = @WorkflowInstanceId
    ORDER BY [CreatedAtUtc], [Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_Sla_GetScanSummary]
    @NowUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PendingSlaTasks AS (
        SELECT task.[Id]
        FROM [dbo].[WorkflowTask] AS task
        INNER JOIN [dbo].[WorkflowInstance] AS instance
            ON instance.[WorkflowInstanceId] = task.[WorkflowInstanceId]
        WHERE task.[Status] = N'Pending'
            AND task.[DueAtUtc] IS NOT NULL
            AND instance.[Status] = N'Active'
    )
    SELECT
        (SELECT COUNT(*) FROM PendingSlaTasks) AS [PendingTasksWithSla],
        (
            SELECT COUNT(*)
            FROM PendingSlaTasks AS task
            WHERE EXISTS (
                SELECT 1
                FROM [dbo].[NotificationOutbox] AS outbox
                WHERE outbox.[WorkflowTaskId] = task.[Id]
                    AND outbox.[NotificationType] = N'SlaTaskOverdue')
        ) AS [AlreadyQueued];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_Sla_GetOverdueCandidates]
    @NowUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        task.[Id] AS [WorkflowTaskId],
        instance.[WorkflowInstanceId],
        task.[TaskKey],
        task.[AssignedRoleKey],
        task.[DueAtUtc]
    FROM [dbo].[WorkflowTask] AS task
    INNER JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowInstanceId] = task.[WorkflowInstanceId]
    WHERE task.[Status] = N'Pending'
        AND task.[DueAtUtc] IS NOT NULL
        AND task.[DueAtUtc] < @NowUtc
        AND instance.[Status] = N'Active'
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[NotificationOutbox] AS outbox
            WHERE outbox.[WorkflowTaskId] = task.[Id]
                AND outbox.[NotificationType] = N'SlaTaskOverdue')
    ORDER BY task.[DueAtUtc], task.[Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_Sla_InsertOverdueNotification]
    @WorkflowTaskId bigint,
    @NowUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Inserted TABLE ([Id] bigint NOT NULL);

    INSERT INTO [dbo].[NotificationOutbox] (
        [WorkflowInstanceId],
        [WorkflowTaskId],
        [BusinessObjectType],
        [BusinessObjectId],
        [NotificationType],
        [RecipientRoleKey],
        [RecipientUserId],
        [Subject],
        [PayloadJson],
        [Status],
        [CorrelationId],
        [CreatedAtUtc])
    OUTPUT INSERTED.[Id] INTO @Inserted
    SELECT
        instance.[WorkflowInstanceId],
        task.[Id],
        instance.[BusinessObjectType],
        instance.[BusinessObjectId],
        route.[NotificationType],
        COALESCE(route.[RecipientRoleKey], task.[AssignedRoleKey]),
        route.[RecipientUserId],
        route.[SubjectTemplate],
        CONCAT(
            N'{"workflowInstanceId":"', STRING_ESCAPE(instance.[WorkflowInstanceId], 'json'),
            N'","taskKey":"', STRING_ESCAPE(task.[TaskKey], 'json'),
            N'","assignedRoleKey":"', STRING_ESCAPE(task.[AssignedRoleKey], 'json'),
            N'","dueAtUtc":"', CONVERT(nvarchar(40), task.[DueAtUtc], 127),
            N'","overdueAtUtc":"', CONVERT(nvarchar(40), @NowUtc, 127),
            N'"}'),
        N'Pending',
        CONCAT(N'sla-', task.[Id]),
        @NowUtc
    FROM [dbo].[WorkflowTask] AS task
    INNER JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowInstanceId] = task.[WorkflowInstanceId]
    INNER JOIN [dbo].[NotificationRoute] AS route
        ON route.[WorkflowDefinitionVersionId] = instance.[WorkflowDefinitionVersionId]
        AND route.[NotificationType] = N'SlaTaskOverdue'
        AND route.[IsActive] = 1
    WHERE task.[Id] = @WorkflowTaskId
        AND task.[Status] = N'Pending'
        AND task.[DueAtUtc] IS NOT NULL
        AND task.[DueAtUtc] < @NowUtc
        AND instance.[Status] = N'Active'
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[NotificationOutbox] AS existing
            WHERE existing.[WorkflowTaskId] = task.[Id]
                AND existing.[NotificationType] = N'SlaTaskOverdue');

    SELECT
        inserted.[Id] AS [NotificationOutboxId],
        task.[Id] AS [WorkflowTaskId],
        instance.[WorkflowInstanceId],
        task.[TaskKey],
        task.[AssignedRoleKey],
        task.[DueAtUtc]
    FROM @Inserted AS inserted
    INNER JOIN [dbo].[NotificationOutbox] AS outbox
        ON outbox.[Id] = inserted.[Id]
    INNER JOIN [dbo].[WorkflowTask] AS task
        ON task.[Id] = outbox.[WorkflowTaskId]
    INNER JOIN [dbo].[WorkflowInstance] AS instance
        ON instance.[WorkflowInstanceId] = task.[WorkflowInstanceId];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_GetClaimable]
    @LeaseCutoffUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[NotificationOutbox]
    WHERE [Status] = N'Pending'
        OR ([Status] = N'Processing'
            AND [ProcessingStartedAtUtc] IS NOT NULL
            AND [ProcessingStartedAtUtc] <= @LeaseCutoffUtc)
    ORDER BY [CreatedAtUtc], [Id];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_Claim]
    @Id bigint,
    @ProcessingToken nvarchar(100),
    @ProcessingStartedAtUtc datetimeoffset,
    @LeaseCutoffUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[NotificationOutbox]
    SET
        [Status] = N'Processing',
        [ProcessingToken] = @ProcessingToken,
        [ProcessingStartedAtUtc] = @ProcessingStartedAtUtc,
        [LastError] = NULL
    WHERE [Id] = @Id
        AND ([Status] = N'Pending'
            OR ([Status] = N'Processing'
                AND [ProcessingStartedAtUtc] IS NOT NULL
                AND [ProcessingStartedAtUtc] <= @LeaseCutoffUtc));

    SELECT @@ROWCOUNT AS [RowsAffected];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_GetByClaim]
    @Id bigint,
    @ProcessingToken nvarchar(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM [dbo].[NotificationOutbox]
    WHERE [Id] = @Id
        AND [ProcessingToken] = @ProcessingToken;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_MarkSent]
    @Id bigint,
    @ProcessingToken nvarchar(100),
    @SentAtUtc datetimeoffset
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[NotificationOutbox]
    SET
        [Status] = N'Sent',
        [SentAtUtc] = @SentAtUtc,
        [ProcessingToken] = NULL,
        [ProcessingStartedAtUtc] = NULL,
        [LastError] = NULL
    WHERE [Id] = @Id
        AND [Status] = N'Processing'
        AND [ProcessingToken] = @ProcessingToken;

    SELECT @@ROWCOUNT AS [RowsAffected];
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[usp_NotificationOutbox_ReleaseClaimAfterFailure]
    @Id bigint,
    @ProcessingToken nvarchar(100),
    @LastError nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[NotificationOutbox]
    SET
        [Status] = N'Pending',
        [ProcessingToken] = NULL,
        [ProcessingStartedAtUtc] = NULL,
        [RetryCount] = [RetryCount] + 1,
        [LastError] = @LastError
    WHERE [Id] = @Id
        AND [Status] = N'Processing'
        AND [ProcessingToken] = @ProcessingToken;

    SELECT @@ROWCOUNT AS [RowsAffected];
END;
GO
