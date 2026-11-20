-- CP Atlas, release P2, script 120: the audit table CFP.AuditLog for the platform app CFPINFLOW.
-- The same shape as TTDV2.AuditLog (ReferenceID, ReferenceType, CreatedBy, CreatedByLanId, CreatedDate, LogMessage).
-- Dates are UTC. CreatedBy holds the display name, CreatedByLanId the LAN ID.
-- DDL only. Additive. Safe to run again: the table and the index are made only when they are not there.
-- Run in the CP Atlas database (cp_sharepoint).
--
-- Rollback: drop the table (no other table uses it). The History window of CFPINFLOW then has no entries.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'CFP') IS NULL
        THROW 55120, 'The schema CFP does not exist. Make the target table CFP.Inflow first, then run again.', 1;

    IF OBJECT_ID(N'CFP.AuditLog', N'U') IS NULL
        EXEC(N'CREATE TABLE [CFP].[AuditLog] (
            [ID]             int IDENTITY(1,1) NOT NULL,
            [ReferenceID]    int            NULL,
            [ReferenceType]  varchar(50)    NULL,
            [CreatedBy]      nvarchar(50)   NULL,
            [CreatedByLanId] nvarchar(50)   NULL,
            [CreatedDate]    datetime       NULL,
            [LogMessage]     nvarchar(max)  NULL,
            CONSTRAINT [PK_CFP_AuditLog] PRIMARY KEY CLUSTERED ([ID] ASC)
        );');

    -- A table with this name and no ReferenceID column is not the platform table. Stop and let the DBA decide.
    IF COL_LENGTH(N'CFP.AuditLog', N'ReferenceID') IS NULL OR COL_LENGTH(N'CFP.AuditLog', N'ReferenceType') IS NULL
        THROW 55121, 'CFP.AuditLog exists but has no ReferenceID or no ReferenceType column. Check the table, then run again.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'CFP.AuditLog') AND name = N'IX_AuditLog_ReferenceID')
        EXEC(N'CREATE INDEX [IX_AuditLog_ReferenceID] ON [CFP].[AuditLog] ([ReferenceID], [ReferenceType]);');

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'120-cfp-audit-log.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'120-cfp-audit-log.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the columns of the audit table.
SELECT c.name AS ColumnName, TYPE_NAME(c.system_type_id) AS TypeName FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'CFP.AuditLog') ORDER BY c.column_id;
GO
