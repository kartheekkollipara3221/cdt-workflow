-- CP Atlas, release P2, script 110: the columns that the platform attachments need in CFP.InflowAttachment.
-- The target team made CFP.InflowAttachment. This script changes it in place. It removes and renames no column.
-- DDL only. Additive. Safe to run again: each change runs only when the column or index is not there, or when the
-- column has the old type.
-- Run in the CP Atlas database (cp_sharepoint).
--
-- Changes:
--   ContentType                      varchar(200) NULL.
--   SizeBytes                        bigint NULL. The size of the file before compression.
--   CreatedByLanId, ModifiedByLanId  nvarchar(50) NULL. CreatedBy and ModifiedBy keep the display name.
--   FileName                         varchar(1000) to nvarchar(1000), only when the type is varchar. The data stays.
--   IX_InflowAttachment_InflowId     index on InflowId.
-- Type (varchar(500)) holds the control name of the file picker. FileContent holds the compressed file.
--
-- Rollback: drop the index and the added columns; ALTER COLUMN FileName back to varchar(1000) (loses the
-- characters that varchar cannot store).
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'CFP.InflowAttachment', N'U') IS NULL
        THROW 55110, 'CFP.InflowAttachment does not exist. The target team makes this table. Make it, then run again.', 1;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'CFP.InflowAttachment') AND name = N'FileName'
               AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [CFP].[InflowAttachment] ALTER COLUMN [FileName] nvarchar(1000) NULL;');

    IF COL_LENGTH(N'CFP.InflowAttachment', N'ContentType') IS NULL
        EXEC(N'ALTER TABLE [CFP].[InflowAttachment] ADD [ContentType] varchar(200) NULL;');
    IF COL_LENGTH(N'CFP.InflowAttachment', N'SizeBytes') IS NULL
        EXEC(N'ALTER TABLE [CFP].[InflowAttachment] ADD [SizeBytes] bigint NULL;');
    IF COL_LENGTH(N'CFP.InflowAttachment', N'CreatedByLanId') IS NULL
        EXEC(N'ALTER TABLE [CFP].[InflowAttachment] ADD [CreatedByLanId] nvarchar(50) NULL;');
    IF COL_LENGTH(N'CFP.InflowAttachment', N'ModifiedByLanId') IS NULL
        EXEC(N'ALTER TABLE [CFP].[InflowAttachment] ADD [ModifiedByLanId] nvarchar(50) NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'CFP.InflowAttachment') AND name = N'IX_InflowAttachment_InflowId')
        EXEC(N'CREATE INDEX [IX_InflowAttachment_InflowId] ON [CFP].[InflowAttachment] ([InflowId]);');

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'110-cfp-inflow-attachment-columns.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'110-cfp-inflow-attachment-columns.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the columns that this script changes or adds, and the index.
SELECT c.name AS ColumnName, TYPE_NAME(c.system_type_id) AS TypeName, c.max_length, c.is_nullable
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'CFP.InflowAttachment')
  AND c.name IN (N'FileName', N'ContentType', N'SizeBytes', N'CreatedByLanId', N'ModifiedByLanId');
SELECT i.name AS IndexName FROM sys.indexes AS i
WHERE i.object_id = OBJECT_ID(N'CFP.InflowAttachment') AND i.name = N'IX_InflowAttachment_InflowId';
GO
