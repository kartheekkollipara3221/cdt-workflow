-- ============================================================
-- WorkflowEngine Database-First Schema
-- Idempotent: safe to run on an existing database.
-- Incorporates all migrations:
--   20260507145558_InitialWorkflowSchema
--   20260510120000_HardenRuntimeConstraints
--   20260512100000_WorkflowInstanceAlternateKey
-- ============================================================

-- ------------------------------------------------------------
-- Tables (dependency order)
-- ------------------------------------------------------------

IF OBJECT_ID(N'WorkflowDefinition', N'U') IS NULL
CREATE TABLE [WorkflowDefinition] (
    [Id]          bigint        IDENTITY(1,1) NOT NULL,
    [WorkflowKey] nvarchar(100) NOT NULL,
    [DisplayName] nvarchar(200) NOT NULL,
    [IsActive]    bit           NOT NULL,
    CONSTRAINT [PK_WorkflowDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowInstance', N'U') IS NULL
CREATE TABLE [WorkflowInstance] (
    [Id]                         bigint           IDENTITY(1,1) NOT NULL,
    [WorkflowInstanceId]         nvarchar(150)    NOT NULL,
    [WorkflowKey]                nvarchar(100)    NOT NULL,
    [WorkflowDefinitionVersionId] bigint          NOT NULL,
    [BusinessObjectType]         nvarchar(100)    NOT NULL,
    [BusinessObjectId]           nvarchar(150)    NOT NULL,
    [CurrentStateKey]            nvarchar(100)    NOT NULL,
    [Status]                     nvarchar(30)     NOT NULL,
    [StartedAtUtc]               datetimeoffset   NOT NULL,
    [StartedByUserId]            nvarchar(200)    NOT NULL,
    [CompletedAtUtc]             datetimeoffset   NULL,
    [Version]                    int              NOT NULL,
    CONSTRAINT [PK_WorkflowInstance] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowTask', N'U') IS NULL
CREATE TABLE [WorkflowTask] (
    [Id]                  bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowInstanceId]  nvarchar(150)   NOT NULL,
    [TaskKey]             nvarchar(100)   NOT NULL,
    [AssignedRoleKey]     nvarchar(100)   NOT NULL,
    [AssignedUserId]      nvarchar(200)   NULL,
    [Status]              nvarchar(30)    NOT NULL,
    [CreatedAtUtc]        datetimeoffset  NOT NULL,
    [DueAtUtc]            datetimeoffset  NULL,
    [CompletedAtUtc]      datetimeoffset  NULL,
    [CompletedByUserId]   nvarchar(200)   NULL,
    [ClosedByActionKey]   nvarchar(100)   NULL,
    CONSTRAINT [PK_WorkflowTask] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowAudit', N'U') IS NULL
CREATE TABLE [WorkflowAudit] (
    [Id]                  bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowInstanceId]  nvarchar(150)   NOT NULL,
    [BusinessObjectType]  nvarchar(100)   NOT NULL,
    [BusinessObjectId]    nvarchar(150)   NOT NULL,
    [ActionKey]           nvarchar(100)   NOT NULL,
    [FromStateKey]        nvarchar(100)   NOT NULL,
    [ToStateKey]          nvarchar(100)   NOT NULL,
    [PerformedByUserId]   nvarchar(200)   NOT NULL,
    [PerformedByRoleKey]  nvarchar(100)   NOT NULL,
    [Reason]              nvarchar(1000)  NULL,
    [CorrelationId]       nvarchar(150)   NOT NULL,
    [CreatedAtUtc]        datetimeoffset  NOT NULL,
    CONSTRAINT [PK_WorkflowAudit] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'NotificationOutbox', N'U') IS NULL
CREATE TABLE [NotificationOutbox] (
    [Id]                       bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowInstanceId]       nvarchar(150)   NOT NULL,
    [WorkflowTaskId]           bigint          NULL,
    [BusinessObjectType]       nvarchar(100)   NOT NULL,
    [BusinessObjectId]         nvarchar(150)   NOT NULL,
    [NotificationType]         nvarchar(100)   NOT NULL,
    [RecipientRoleKey]         nvarchar(100)   NULL,
    [RecipientUserId]          nvarchar(200)   NULL,
    [Subject]                  nvarchar(300)   NOT NULL,
    [PayloadJson]              nvarchar(max)   NOT NULL,
    [Status]                   nvarchar(30)    NOT NULL,
    [CorrelationId]            nvarchar(150)   NOT NULL,
    [CreatedAtUtc]             datetimeoffset  NOT NULL,
    [SentAtUtc]                datetimeoffset  NULL,
    [ProcessingToken]          nvarchar(100)   NULL,
    [ProcessingStartedAtUtc]   datetimeoffset  NULL,
    [RetryCount]               int             NOT NULL DEFAULT 0,
    [LastError]                nvarchar(1000)  NULL,
    CONSTRAINT [PK_NotificationOutbox] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'IdempotencyRecord', N'U') IS NULL
CREATE TABLE [IdempotencyRecord] (
    [Id]                 bigint          IDENTITY(1,1) NOT NULL,
    [IdempotencyKey]     nvarchar(200)   NOT NULL,
    [WorkflowInstanceId] nvarchar(150)   NOT NULL,
    [ActionKey]          nvarchar(100)   NOT NULL,
    [RequestHash]        nvarchar(128)   NOT NULL,
    [ResultHash]         nvarchar(128)   NOT NULL,
    [ResultJson]         nvarchar(max)   NOT NULL,
    [CreatedAtUtc]       datetimeoffset  NOT NULL,
    CONSTRAINT [PK_IdempotencyRecord] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'MockSectionData', N'U') IS NULL
CREATE TABLE [MockSectionData] (
    [Id]                 bigint          IDENTITY(1,1) NOT NULL,
    [BusinessObjectType] nvarchar(100)   NOT NULL,
    [BusinessObjectId]   nvarchar(150)   NOT NULL,
    [SectionKey]         nvarchar(100)   NOT NULL,
    [PayloadJson]        nvarchar(max)   NOT NULL,
    [SavedByUserId]      nvarchar(200)   NOT NULL,
    [SavedAtUtc]         datetimeoffset  NOT NULL,
    [CorrelationId]      nvarchar(150)   NOT NULL,
    CONSTRAINT [PK_MockSectionData] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowDefinitionVersion', N'U') IS NULL
CREATE TABLE [WorkflowDefinitionVersion] (
    [Id]                   bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionId] bigint          NOT NULL,
    [VersionNumber]        int             NOT NULL,
    [IsActive]             bit             NOT NULL,
    [CreatedAtUtc]         datetimeoffset  NOT NULL,
    CONSTRAINT [PK_WorkflowDefinitionVersion] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowStateDefinition', N'U') IS NULL
CREATE TABLE [WorkflowStateDefinition] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [StateKey]                   nvarchar(100)   NOT NULL,
    [DisplayName]                nvarchar(200)   NOT NULL,
    [IsTerminal]                 bit             NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_WorkflowStateDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowActionDefinition', N'U') IS NULL
CREATE TABLE [WorkflowActionDefinition] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [ActionKey]                  nvarchar(100)   NOT NULL,
    [DisplayName]                nvarchar(200)   NOT NULL,
    [SectionKey]                 nvarchar(100)   NULL,
    [ButtonKey]                  nvarchar(100)   NULL,
    [ButtonLabel]                nvarchar(200)   NULL,
    [Intent]                     nvarchar(100)   NULL,
    [RequiresReason]             bit             NOT NULL,
    [SortOrder]                  int             NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_WorkflowActionDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowRoleDefinition', N'U') IS NULL
CREATE TABLE [WorkflowRoleDefinition] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [RoleKey]                    nvarchar(100)   NOT NULL,
    [DisplayName]                nvarchar(200)   NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_WorkflowRoleDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowTaskDefinition', N'U') IS NULL
CREATE TABLE [WorkflowTaskDefinition] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [TaskKey]                    nvarchar(100)   NOT NULL,
    [DisplayName]                nvarchar(200)   NOT NULL,
    [OwnerRoleKey]               nvarchar(100)   NOT NULL,
    [SlaPolicyKey]               nvarchar(100)   NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_WorkflowTaskDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'WorkflowTransitionDefinition', N'U') IS NULL
CREATE TABLE [WorkflowTransitionDefinition] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [FromStateKey]               nvarchar(100)   NOT NULL,
    [ActionKey]                  nvarchar(100)   NOT NULL,
    [AllowedRoleKey]             nvarchar(100)   NOT NULL,
    [ToStateKey]                 nvarchar(100)   NOT NULL,
    [TaskToCloseKey]             nvarchar(100)   NULL,
    [TaskToCreateKey]            nvarchar(100)   NULL,
    [NotificationRouteKey]       nvarchar(100)   NULL,
    [AuditLabel]                 nvarchar(200)   NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_WorkflowTransitionDefinition] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'SlaPolicy', N'U') IS NULL
CREATE TABLE [SlaPolicy] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [SlaPolicyKey]               nvarchar(100)   NOT NULL,
    [DurationMinutes]            int             NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_SlaPolicy] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'NotificationRoute', N'U') IS NULL
CREATE TABLE [NotificationRoute] (
    [Id]                         bigint          IDENTITY(1,1) NOT NULL,
    [WorkflowDefinitionVersionId] bigint         NOT NULL,
    [RouteKey]                   nvarchar(100)   NOT NULL,
    [NotificationType]           nvarchar(100)   NOT NULL,
    [RecipientRoleKey]           nvarchar(100)   NULL,
    [RecipientUserId]            nvarchar(200)   NULL,
    [SubjectTemplate]            nvarchar(300)   NOT NULL,
    [IsActive]                   bit             NOT NULL,
    CONSTRAINT [PK_NotificationRoute] PRIMARY KEY ([Id])
);

-- ------------------------------------------------------------
-- Unique constraint (alternate key) — sys.key_constraints
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE name = N'AK_WorkflowInstance_WorkflowInstanceId'
      AND parent_object_id = OBJECT_ID(N'WorkflowInstance')
)
ALTER TABLE [WorkflowInstance]
    ADD CONSTRAINT [AK_WorkflowInstance_WorkflowInstanceId]
    UNIQUE ([WorkflowInstanceId]);

-- ------------------------------------------------------------
-- Unique indexes — sys.indexes
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowDefinition_WorkflowKey'
      AND object_id = OBJECT_ID(N'WorkflowDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowDefinition_WorkflowKey]
    ON [WorkflowDefinition] ([WorkflowKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber'
      AND object_id = OBJECT_ID(N'WorkflowDefinitionVersion')
)
CREATE UNIQUE INDEX [IX_WorkflowDefinitionVersion_WorkflowDefinitionId_VersionNumber]
    ON [WorkflowDefinitionVersion] ([WorkflowDefinitionId], [VersionNumber]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId'
      AND object_id = OBJECT_ID(N'WorkflowInstance')
)
CREATE UNIQUE INDEX [IX_WorkflowInstance_WorkflowKey_BusinessObjectType_BusinessObjectId]
    ON [WorkflowInstance] ([WorkflowKey], [BusinessObjectType], [BusinessObjectId]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey'
      AND object_id = OBJECT_ID(N'WorkflowStateDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowStateDefinition_WorkflowDefinitionVersionId_StateKey]
    ON [WorkflowStateDefinition] ([WorkflowDefinitionVersionId], [StateKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey'
      AND object_id = OBJECT_ID(N'WorkflowActionDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowActionDefinition_WorkflowDefinitionVersionId_ActionKey]
    ON [WorkflowActionDefinition] ([WorkflowDefinitionVersionId], [ActionKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey'
      AND object_id = OBJECT_ID(N'WorkflowRoleDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowRoleDefinition_WorkflowDefinitionVersionId_RoleKey]
    ON [WorkflowRoleDefinition] ([WorkflowDefinitionVersionId], [RoleKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey'
      AND object_id = OBJECT_ID(N'WorkflowTaskDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowTaskDefinition_WorkflowDefinitionVersionId_TaskKey]
    ON [WorkflowTaskDefinition] ([WorkflowDefinitionVersionId], [TaskKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey'
      AND object_id = OBJECT_ID(N'WorkflowTransitionDefinition')
)
CREATE UNIQUE INDEX [IX_WorkflowTransitionDefinition_WorkflowDefinitionVersionId_FromStateKey_ActionKey_AllowedRoleKey]
    ON [WorkflowTransitionDefinition] ([WorkflowDefinitionVersionId], [FromStateKey], [ActionKey], [AllowedRoleKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey'
      AND object_id = OBJECT_ID(N'SlaPolicy')
)
CREATE UNIQUE INDEX [IX_SlaPolicy_WorkflowDefinitionVersionId_SlaPolicyKey]
    ON [SlaPolicy] ([WorkflowDefinitionVersionId], [SlaPolicyKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey'
      AND object_id = OBJECT_ID(N'NotificationRoute')
)
CREATE UNIQUE INDEX [IX_NotificationRoute_WorkflowDefinitionVersionId_RouteKey]
    ON [NotificationRoute] ([WorkflowDefinitionVersionId], [RouteKey]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowTask_WorkflowInstanceId'
      AND object_id = OBJECT_ID(N'WorkflowTask')
)
CREATE UNIQUE INDEX [IX_WorkflowTask_WorkflowInstanceId]
    ON [WorkflowTask] ([WorkflowInstanceId])
    WHERE [Status] = 'Pending';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_NotificationOutbox_WorkflowTaskId_NotificationType'
      AND object_id = OBJECT_ID(N'NotificationOutbox')
)
CREATE UNIQUE INDEX [IX_NotificationOutbox_WorkflowTaskId_NotificationType]
    ON [NotificationOutbox] ([WorkflowTaskId], [NotificationType])
    WHERE [NotificationType] = 'SlaTaskOverdue';

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_IdempotencyRecord_IdempotencyKey'
      AND object_id = OBJECT_ID(N'IdempotencyRecord')
)
CREATE UNIQUE INDEX [IX_IdempotencyRecord_IdempotencyKey]
    ON [IdempotencyRecord] ([IdempotencyKey]);

-- ------------------------------------------------------------
-- Non-unique indexes
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_WorkflowAudit_WorkflowInstanceId'
      AND object_id = OBJECT_ID(N'WorkflowAudit')
)
CREATE INDEX [IX_WorkflowAudit_WorkflowInstanceId]
    ON [WorkflowAudit] ([WorkflowInstanceId]);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id'
      AND object_id = OBJECT_ID(N'MockSectionData')
)
CREATE INDEX [IX_MockSectionData_BusinessObjectType_BusinessObjectId_SectionKey_SavedAtUtc_Id]
    ON [MockSectionData] ([BusinessObjectType], [BusinessObjectId], [SectionKey], [SavedAtUtc], [Id]);

-- ------------------------------------------------------------
-- Check constraints — sys.check_constraints
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_WorkflowInstance_Status'
      AND parent_object_id = OBJECT_ID(N'WorkflowInstance')
)
ALTER TABLE [WorkflowInstance]
    ADD CONSTRAINT [CK_WorkflowInstance_Status]
    CHECK ([Status] IN ('Active', 'Completed'));

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_WorkflowTask_Status'
      AND parent_object_id = OBJECT_ID(N'WorkflowTask')
)
ALTER TABLE [WorkflowTask]
    ADD CONSTRAINT [CK_WorkflowTask_Status]
    CHECK ([Status] IN ('Pending', 'Completed'));

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = N'CK_NotificationOutbox_Status'
      AND parent_object_id = OBJECT_ID(N'NotificationOutbox')
)
ALTER TABLE [NotificationOutbox]
    ADD CONSTRAINT [CK_NotificationOutbox_Status]
    CHECK ([Status] IN ('Pending', 'Processing', 'Sent'));

-- ------------------------------------------------------------
-- Foreign keys — sys.foreign_keys
-- ------------------------------------------------------------

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_WorkflowDefinitionVersion_WorkflowDefinition_WorkflowDefinitionId'
      AND parent_object_id = OBJECT_ID(N'WorkflowDefinitionVersion')
)
ALTER TABLE [WorkflowDefinitionVersion]
    ADD CONSTRAINT [FK_WorkflowDefinitionVersion_WorkflowDefinition_WorkflowDefinitionId]
    FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinition] ([Id])
    ON DELETE NO ACTION;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId'
      AND parent_object_id = OBJECT_ID(N'WorkflowTask')
)
ALTER TABLE [WorkflowTask]
    ADD CONSTRAINT [FK_WorkflowTask_WorkflowInstance_WorkflowInstanceId]
    FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstance] ([WorkflowInstanceId])
    ON DELETE CASCADE;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId'
      AND parent_object_id = OBJECT_ID(N'WorkflowAudit')
)
ALTER TABLE [WorkflowAudit]
    ADD CONSTRAINT [FK_WorkflowAudit_WorkflowInstance_WorkflowInstanceId]
    FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstance] ([WorkflowInstanceId])
    ON DELETE CASCADE;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId'
      AND parent_object_id = OBJECT_ID(N'NotificationOutbox')
)
ALTER TABLE [NotificationOutbox]
    ADD CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId]
    FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstance] ([WorkflowInstanceId])
    ON DELETE CASCADE;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId'
      AND parent_object_id = OBJECT_ID(N'IdempotencyRecord')
)
ALTER TABLE [IdempotencyRecord]
    ADD CONSTRAINT [FK_IdempotencyRecord_WorkflowInstance_WorkflowInstanceId]
    FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstance] ([WorkflowInstanceId])
    ON DELETE CASCADE;
