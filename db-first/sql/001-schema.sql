SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'[dbo].[WorkflowDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowDefinitionVersion]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowDefinitionVersion] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionId] bigint NOT NULL,
        [VersionNumber] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WorkflowDefinitionVersion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkflowDefinitionVersion_WorkflowDefinition_WorkflowDefinitionId]
            FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [dbo].[WorkflowDefinition] ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowStateDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowStateDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [StateKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsTerminal] bit NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowStateDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowActionDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowActionDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [ActionKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [SectionKey] nvarchar(100) NULL,
        [ButtonKey] nvarchar(100) NULL,
        [ButtonLabel] nvarchar(200) NULL,
        [Intent] nvarchar(100) NULL,
        [RequiresReason] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowActionDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowRoleDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowRoleDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [RoleKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowRoleDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowTaskDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowTaskDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [TaskKey] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [OwnerRoleKey] nvarchar(100) NOT NULL,
        [SlaPolicyKey] nvarchar(100) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowTaskDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowTransitionDefinition]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowTransitionDefinition] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [FromStateKey] nvarchar(100) NOT NULL,
        [ActionKey] nvarchar(100) NOT NULL,
        [AllowedRoleKey] nvarchar(100) NOT NULL,
        [ToStateKey] nvarchar(100) NOT NULL,
        [TaskToCloseKey] nvarchar(100) NULL,
        [TaskToCreateKey] nvarchar(100) NULL,
        [NotificationRouteKey] nvarchar(100) NULL,
        [AuditLabel] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkflowTransitionDefinition] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[SlaPolicy]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SlaPolicy] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [SlaPolicyKey] nvarchar(100) NOT NULL,
        [DurationMinutes] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_SlaPolicy] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[NotificationRoute]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NotificationRoute] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [RouteKey] nvarchar(100) NOT NULL,
        [NotificationType] nvarchar(100) NOT NULL,
        [RecipientRoleKey] nvarchar(100) NULL,
        [RecipientUserId] nvarchar(200) NULL,
        [SubjectTemplate] nvarchar(300) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_NotificationRoute] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowInstance]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowInstance] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowInstanceId] nvarchar(150) NOT NULL,
        [WorkflowKey] nvarchar(100) NOT NULL,
        [WorkflowDefinitionVersionId] bigint NOT NULL,
        [BusinessObjectType] nvarchar(100) NOT NULL,
        [BusinessObjectId] nvarchar(150) NOT NULL,
        [CurrentStateKey] nvarchar(100) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [StartedAtUtc] datetimeoffset NOT NULL,
        [StartedByUserId] nvarchar(200) NOT NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_WorkflowInstance] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WorkflowInstance_Status] CHECK ([Status] IN ('Active', 'Completed'))
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowTask]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowTask] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowInstanceId] nvarchar(150) NOT NULL,
        [TaskKey] nvarchar(100) NOT NULL,
        [AssignedRoleKey] nvarchar(100) NOT NULL,
        [AssignedUserId] nvarchar(200) NULL,
        [Status] nvarchar(30) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [DueAtUtc] datetimeoffset NULL,
        [CompletedAtUtc] datetimeoffset NULL,
        [CompletedByUserId] nvarchar(200) NULL,
        [ClosedByActionKey] nvarchar(100) NULL,
        CONSTRAINT [PK_WorkflowTask] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_WorkflowTask_Status] CHECK ([Status] IN ('Pending', 'Completed'))
    );
END;
GO

IF OBJECT_ID(N'[dbo].[MockSectionData]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MockSectionData] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [BusinessObjectType] nvarchar(100) NOT NULL,
        [BusinessObjectId] nvarchar(150) NOT NULL,
        [SectionKey] nvarchar(100) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [SavedByUserId] nvarchar(200) NOT NULL,
        [SavedAtUtc] datetimeoffset NOT NULL,
        [CorrelationId] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_MockSectionData] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[WorkflowAudit]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkflowAudit] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowInstanceId] nvarchar(150) NOT NULL,
        [BusinessObjectType] nvarchar(100) NOT NULL,
        [BusinessObjectId] nvarchar(150) NOT NULL,
        [ActionKey] nvarchar(100) NOT NULL,
        [FromStateKey] nvarchar(100) NOT NULL,
        [ToStateKey] nvarchar(100) NOT NULL,
        [PerformedByUserId] nvarchar(200) NOT NULL,
        [PerformedByRoleKey] nvarchar(100) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [CorrelationId] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_WorkflowAudit] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[NotificationOutbox]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[NotificationOutbox] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [WorkflowInstanceId] nvarchar(150) NOT NULL,
        [WorkflowTaskId] bigint NULL,
        [BusinessObjectType] nvarchar(100) NOT NULL,
        [BusinessObjectId] nvarchar(150) NOT NULL,
        [NotificationType] nvarchar(100) NOT NULL,
        [RecipientRoleKey] nvarchar(100) NULL,
        [RecipientUserId] nvarchar(200) NULL,
        [Subject] nvarchar(300) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [CorrelationId] nvarchar(150) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        [ProcessingToken] nvarchar(100) NULL,
        [ProcessingStartedAtUtc] datetimeoffset NULL,
        [RetryCount] int NOT NULL CONSTRAINT [DF_NotificationOutbox_RetryCount] DEFAULT (0),
        [LastError] nvarchar(1000) NULL,
        [SentAtUtc] datetimeoffset NULL,
        CONSTRAINT [PK_NotificationOutbox] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_NotificationOutbox_Status] CHECK ([Status] IN ('Pending', 'Processing', 'Sent'))
    );
END;
GO

IF OBJECT_ID(N'[dbo].[IdempotencyRecord]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[IdempotencyRecord] (
        [Id] bigint IDENTITY(1,1) NOT NULL,
        [IdempotencyKey] nvarchar(200) NOT NULL,
        [WorkflowInstanceId] nvarchar(150) NOT NULL,
        [ActionKey] nvarchar(100) NOT NULL,
        [RequestHash] nvarchar(128) NOT NULL,
        [ResultHash] nvarchar(128) NOT NULL,
        [ResultJson] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_IdempotencyRecord] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowDefinition_WorkflowKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowDefinition_WorkflowKey] ON [dbo].[WorkflowDefinition] ([WorkflowKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber' AND object_id = OBJECT_ID(N'[dbo].[WorkflowDefinitionVersion]'))
    CREATE UNIQUE INDEX [IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber] ON [dbo].[WorkflowDefinitionVersion] ([WorkflowDefinitionId], [VersionNumber]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowStateDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey] ON [dbo].[WorkflowStateDefinition] ([WorkflowDefinitionVersionId], [StateKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowActionDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey] ON [dbo].[WorkflowActionDefinition] ([WorkflowDefinitionVersionId], [ActionKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowRoleDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey] ON [dbo].[WorkflowRoleDefinition] ([WorkflowDefinitionVersionId], [RoleKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowTaskDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey] ON [dbo].[WorkflowTaskDefinition] ([WorkflowDefinitionVersionId], [TaskKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey' AND object_id = OBJECT_ID(N'[dbo].[WorkflowTransitionDefinition]'))
    CREATE UNIQUE INDEX [IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey] ON [dbo].[WorkflowTransitionDefinition] ([WorkflowDefinitionVersionId], [FromStateKey], [ActionKey], [AllowedRoleKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey' AND object_id = OBJECT_ID(N'[dbo].[SlaPolicy]'))
    CREATE UNIQUE INDEX [IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey] ON [dbo].[SlaPolicy] ([WorkflowDefinitionVersionId], [SlaPolicyKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey' AND object_id = OBJECT_ID(N'[dbo].[NotificationRoute]'))
    CREATE UNIQUE INDEX [IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey] ON [dbo].[NotificationRoute] ([WorkflowDefinitionVersionId], [RouteKey]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowInstance_WorkflowInstanceId' AND object_id = OBJECT_ID(N'[dbo].[WorkflowInstance]'))
    CREATE UNIQUE INDEX [IX_WorkflowInstance_WorkflowInstanceId] ON [dbo].[WorkflowInstance] ([WorkflowInstanceId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId' AND object_id = OBJECT_ID(N'[dbo].[WorkflowInstance]'))
    CREATE UNIQUE INDEX [IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId] ON [dbo].[WorkflowInstance] ([WorkflowKey], [BusinessObjectType], [BusinessObjectId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowTask_WorkflowInstanceId' AND object_id = OBJECT_ID(N'[dbo].[WorkflowTask]'))
    CREATE UNIQUE INDEX [IX_WorkflowTask_WorkflowInstanceId] ON [dbo].[WorkflowTask] ([WorkflowInstanceId]) WHERE [Status] = 'Pending';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id' AND object_id = OBJECT_ID(N'[dbo].[MockSectionData]'))
    CREATE INDEX [IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id] ON [dbo].[MockSectionData] ([BusinessObjectType], [BusinessObjectId], [SectionKey], [SavedAtUtc], [Id]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_WorkflowAudit_WorkflowInstanceId' AND object_id = OBJECT_ID(N'[dbo].[WorkflowAudit]'))
    CREATE INDEX [IX_WorkflowAudit_WorkflowInstanceId] ON [dbo].[WorkflowAudit] ([WorkflowInstanceId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_NotificationOutbox_WorkflowTaskId_NotificationType' AND object_id = OBJECT_ID(N'[dbo].[NotificationOutbox]'))
    CREATE UNIQUE INDEX [IX_NotificationOutbox_WorkflowTaskId_NotificationType] ON [dbo].[NotificationOutbox] ([WorkflowTaskId], [NotificationType]) WHERE [NotificationType] = 'SlaTaskOverdue';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_IdempotencyRecord_IdempotencyKey' AND object_id = OBJECT_ID(N'[dbo].[IdempotencyRecord]'))
    CREATE UNIQUE INDEX [IX_IdempotencyRecord_IdempotencyKey] ON [dbo].[IdempotencyRecord] ([IdempotencyKey]);
GO

IF OBJECT_ID(N'[dbo].[FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorkflowTask] ADD CONSTRAINT [FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId]
        FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId]) ON DELETE CASCADE;
END;
GO

IF OBJECT_ID(N'[dbo].[FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorkflowAudit] ADD CONSTRAINT [FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId]
        FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId]) ON DELETE CASCADE;
END;
GO

IF OBJECT_ID(N'[dbo].[FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[NotificationOutbox] ADD CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId]
        FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId]) ON DELETE CASCADE;
END;
GO

IF OBJECT_ID(N'[dbo].[FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId]', N'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[IdempotencyRecord] ADD CONSTRAINT [FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId]
        FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId]) ON DELETE CASCADE;
END;
GO
