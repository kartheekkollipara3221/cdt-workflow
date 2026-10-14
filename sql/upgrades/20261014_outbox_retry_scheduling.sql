-- Additive manual upgrade for existing target databases. Run in the intended database.
-- Does not seed, reset runtime data or edit EF migration history.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.NotificationOutbox', N'U') IS NULL
        THROW 51001, 'Expected dbo.NotificationOutbox table was not found.', 1;

    IF COL_LENGTH(N'dbo.NotificationOutbox', N'NextAttemptAtUtc') IS NULL
        ALTER TABLE dbo.NotificationOutbox ADD NextAttemptAtUtc datetimeoffset NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.NotificationOutbox') AND name = N'NextAttemptAtUtc'
          AND system_type_id = TYPE_ID(N'datetimeoffset') AND scale = 7 AND is_nullable = 1)
        THROW 51002, 'NextAttemptAtUtc must be nullable datetimeoffset(7).', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.NotificationOutbox') AND name = N'IX_NotificationOutbox_RetryEligibility')
        EXEC(N'CREATE INDEX IX_NotificationOutbox_RetryEligibility
            ON dbo.NotificationOutbox (Status, NextAttemptAtUtc, CreatedAtUtc, Id);');

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes AS i
        WHERE i.object_id = OBJECT_ID(N'dbo.NotificationOutbox')
          AND i.name = N'IX_NotificationOutbox_RetryEligibility'
          AND i.is_unique = 0 AND i.has_filter = 0 AND i.is_disabled = 0 AND i.type = 2
          AND 4 = (SELECT COUNT(*) FROM sys.index_columns AS ic
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0)
          AND 4 = (SELECT COUNT(*) FROM sys.index_columns AS ic
              INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
              WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_descending_key = 0
                AND ((ic.key_ordinal = 1 AND c.name = N'Status')
                  OR (ic.key_ordinal = 2 AND c.name = N'NextAttemptAtUtc')
                  OR (ic.key_ordinal = 3 AND c.name = N'CreatedAtUtc')
                  OR (ic.key_ordinal = 4 AND c.name = N'Id'))))
        THROW 51003, 'Existing retry eligibility index does not match the required definition.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
