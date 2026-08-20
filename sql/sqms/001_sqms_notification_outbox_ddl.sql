SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[NotificationOutbox]', N'U') IS NULL
        THROW 51001, 'Expected table dbo.NotificationOutbox was not found.', 1;

    DECLARE @OutboxObjectId int = OBJECT_ID(N'[dbo].[NotificationOutbox]', N'U');
    DECLARE @WorkflowInstanceColumnId int;
    DECLARE @WorkflowInstanceNullable bit;

    SELECT
        @WorkflowInstanceColumnId = column_id,
        @WorkflowInstanceNullable = is_nullable
    FROM sys.columns
    WHERE object_id = @OutboxObjectId
      AND name = N'WorkflowInstanceId'
      AND system_type_id = TYPE_ID(N'nvarchar')
      AND max_length = 300;

    IF @WorkflowInstanceColumnId IS NULL
        THROW 51002, 'dbo.NotificationOutbox.WorkflowInstanceId must be nvarchar(150).', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys AS foreignKey
        INNER JOIN sys.foreign_key_columns AS foreignKeyColumn
            ON foreignKeyColumn.constraint_object_id = foreignKey.object_id
        WHERE foreignKey.parent_object_id = @OutboxObjectId
          AND foreignKey.name = N'FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId'
          AND foreignKeyColumn.parent_column_id = @WorkflowInstanceColumnId
          AND foreignKey.delete_referential_action = 1
          AND foreignKey.is_disabled = 0
          AND foreignKey.is_not_trusted = 0
    )
        THROW 51003, 'Expected trusted cascading NotificationOutbox workflow-instance foreign key was not found.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes AS indexDefinition
        WHERE indexDefinition.object_id = @OutboxObjectId
          AND indexDefinition.name = N'IX_NotificationOutbox_WorkflowInstanceId'
          AND indexDefinition.is_unique = 0
          AND indexDefinition.has_filter = 0
          AND 1 =
          (
              SELECT COUNT(*)
              FROM sys.index_columns AS indexColumn
              WHERE indexColumn.object_id = indexDefinition.object_id
                AND indexColumn.index_id = indexDefinition.index_id
                AND indexColumn.key_ordinal > 0
                AND indexColumn.column_id = @WorkflowInstanceColumnId
          )
          AND 1 =
          (
              SELECT COUNT(*)
              FROM sys.index_columns AS indexColumn
              WHERE indexColumn.object_id = indexDefinition.object_id
                AND indexColumn.index_id = indexDefinition.index_id
                AND indexColumn.key_ordinal > 0
          )
    )
        THROW 51004, 'Expected NotificationOutbox workflow-instance index was not found or is incompatible.', 1;

    IF @WorkflowInstanceNullable = 0
    BEGIN
        ALTER TABLE [dbo].[NotificationOutbox]
            DROP CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId];
        DROP INDEX [IX_NotificationOutbox_WorkflowInstanceId]
            ON [dbo].[NotificationOutbox];

        ALTER TABLE [dbo].[NotificationOutbox]
            ALTER COLUMN [WorkflowInstanceId] nvarchar(150) NULL;

        CREATE INDEX [IX_NotificationOutbox_WorkflowInstanceId]
            ON [dbo].[NotificationOutbox] ([WorkflowInstanceId]);
        ALTER TABLE [dbo].[NotificationOutbox] WITH CHECK
            ADD CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId]
            FOREIGN KEY ([WorkflowInstanceId])
            REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId])
            ON DELETE CASCADE;
        ALTER TABLE [dbo].[NotificationOutbox]
            CHECK CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId];
    END;

    IF COL_LENGTH(N'dbo.NotificationOutbox', N'NotificationOccurrenceKey') IS NULL
    BEGIN
        ALTER TABLE [dbo].[NotificationOutbox]
            ADD [NotificationOccurrenceKey] nvarchar(300) NULL;
    END
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @OutboxObjectId
          AND name = N'NotificationOccurrenceKey'
          AND system_type_id = TYPE_ID(N'nvarchar')
          AND max_length = 600
          AND is_nullable = 1
    )
        THROW 51005, 'dbo.NotificationOutbox.NotificationOccurrenceKey has an incompatible definition.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = @OutboxObjectId
          AND name = N'UX_NotificationOutbox_NotificationOccurrenceKey'
    )
    BEGIN
        CREATE UNIQUE INDEX [UX_NotificationOutbox_NotificationOccurrenceKey]
            ON [dbo].[NotificationOutbox] ([NotificationOccurrenceKey])
            WHERE NotificationOccurrenceKey IS NOT NULL;
    END
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes AS indexDefinition
        INNER JOIN sys.index_columns AS indexColumn
            ON indexColumn.object_id = indexDefinition.object_id
           AND indexColumn.index_id = indexDefinition.index_id
        INNER JOIN sys.columns AS indexedColumn
            ON indexedColumn.object_id = indexColumn.object_id
           AND indexedColumn.column_id = indexColumn.column_id
        WHERE indexDefinition.object_id = @OutboxObjectId
          AND indexDefinition.name = N'UX_NotificationOutbox_NotificationOccurrenceKey'
          AND indexDefinition.is_unique = 1
          AND indexDefinition.has_filter = 1
          AND indexDefinition.filter_definition = N'([NotificationOccurrenceKey] IS NOT NULL)'
          AND indexColumn.key_ordinal = 1
          AND indexedColumn.name = N'NotificationOccurrenceKey'
    )
        THROW 51006, 'UX_NotificationOutbox_NotificationOccurrenceKey is incompatible.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = @OutboxObjectId
          AND name = N'CK_NotificationOutbox_Owner'
    )
    BEGIN
        ALTER TABLE [dbo].[NotificationOutbox] WITH CHECK
            ADD CONSTRAINT [CK_NotificationOutbox_Owner]
            CHECK ([WorkflowInstanceId] IS NOT NULL OR [NotificationOccurrenceKey] IS NOT NULL);
        ALTER TABLE [dbo].[NotificationOutbox]
            CHECK CONSTRAINT [CK_NotificationOutbox_Owner];
    END
    ELSE IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = @OutboxObjectId
          AND name = N'CK_NotificationOutbox_Owner'
          AND is_disabled = 0
          AND is_not_trusted = 0
          AND definition = N'([WorkflowInstanceId] IS NOT NULL OR [NotificationOccurrenceKey] IS NOT NULL)'
    )
        THROW 51007, 'CK_NotificationOutbox_Owner is incompatible or untrusted.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
