-- CP Atlas, release P1: TTDV2.AuditLog column names as in the target audit tables.
-- Renames RefID to ReferenceID, RefType to ReferenceType and IX_AuditLog_RefID to IX_AuditLog_ReferenceID.
-- Changes no data. Safe to run again: each rename runs only when the old name is there and the new name is not.
-- A new database from the current 040 has the new names already. Then this script changes nothing.
--
-- Order: stop the app pool, run this script, copy the new platform files, start the app pool.
-- The old platform code cannot read the new names, and the new code cannot read the old names.
--
-- Rollback: run the three sp_rename calls in the other direction, then copy the old platform files.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'TTDV2.AuditLog', N'U') IS NULL
        THROW 55020, 'TTDV2.AuditLog does not exist. Run 040 first.', 1;

    IF COL_LENGTH(N'TTDV2.AuditLog', N'RefID') IS NOT NULL AND COL_LENGTH(N'TTDV2.AuditLog', N'ReferenceID') IS NULL
        EXEC sp_rename N'TTDV2.AuditLog.RefID', N'ReferenceID', N'COLUMN';

    IF COL_LENGTH(N'TTDV2.AuditLog', N'RefType') IS NOT NULL AND COL_LENGTH(N'TTDV2.AuditLog', N'ReferenceType') IS NULL
        EXEC sp_rename N'TTDV2.AuditLog.RefType', N'ReferenceType', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name = N'IX_AuditLog_RefID')
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name = N'IX_AuditLog_ReferenceID')
        EXEC sp_rename N'TTDV2.AuditLog.IX_AuditLog_RefID', N'IX_AuditLog_ReferenceID', N'INDEX';

    -- A table with both names (a manual change) is not safe to change here. Stop and let the DBA decide.
    IF COL_LENGTH(N'TTDV2.AuditLog', N'RefID') IS NOT NULL OR COL_LENGTH(N'TTDV2.AuditLog', N'RefType') IS NOT NULL
        THROW 55021, 'TTDV2.AuditLog has RefID or RefType next to ReferenceID or ReferenceType. Merge the columns by hand, then run again.', 1;
    IF COL_LENGTH(N'TTDV2.AuditLog', N'ReferenceID') IS NULL OR COL_LENGTH(N'TTDV2.AuditLog', N'ReferenceType') IS NULL
        THROW 55022, 'TTDV2.AuditLog has no ReferenceID or no ReferenceType column after the rename.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name = N'IX_AuditLog_ReferenceID')
        EXEC(N'CREATE INDEX [IX_AuditLog_ReferenceID] ON [TTDV2].[AuditLog] ([ReferenceID], [ReferenceType]);');

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'045-ttdv2-audit-reference-columns.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'045-ttdv2-audit-reference-columns.sql', N'P1');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the audit columns and the index.
SELECT c.name AS ColumnName FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'TTDV2.AuditLog') AND c.name IN (N'RefID', N'RefType', N'ReferenceID', N'ReferenceType');
SELECT i.name AS IndexName FROM sys.indexes AS i WHERE i.object_id = OBJECT_ID(N'TTDV2.AuditLog') AND i.name LIKE N'IX_AuditLog_%';
GO
