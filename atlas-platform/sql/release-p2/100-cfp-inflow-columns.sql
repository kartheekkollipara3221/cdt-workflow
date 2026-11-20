-- CP Atlas, release P2, script 100: the columns that the platform app CFPINFLOW needs in CFP.Inflow.
-- The target team made CFP.Inflow. This script changes it in place. It removes and renames no column.
-- DDL only. Additive. Safe to run again: each change runs only when the column is not there or has the old type.
-- Run in the CP Atlas database (cp_sharepoint).
--
-- Changes:
--   Description, Comment   varchar(max) to nvarchar(max), only when the type is varchar. The data stays.
--   RowVersion             rowversion NOT NULL. The platform version check finds it by its type.
--   WorkflowState          varchar(100) NULL. The state key of the workflow engine.
--   Status                 varchar(100) NULL. The state label for the dashboard.
--   FundsReceived          bit NULL. The check box "Funds have been received".
--   CashSubmittedDate      datetime NULL, UTC.
--   RMCC                   varchar(1000) NULL. Display names, separated by ';'.
--   DestructionDate        computed: DATEADD(year, 7, CreatedDate). Read only.
--
-- Rollback: drop the added columns (no other table uses them); ALTER COLUMN Description and Comment back to
-- varchar(max) (loses the characters that varchar cannot store).
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'CFP.Inflow', N'U') IS NULL
        THROW 55100, 'CFP.Inflow does not exist. The target team makes this table. Make it, then run again.', 1;

    -- A column with the right name and a different type is not safe to change here. Stop and let the DBA decide.
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'CFP.Inflow') AND name = N'RowVersion'
               AND system_type_id <> TYPE_ID(N'timestamp'))
        THROW 55101, 'CFP.Inflow has a RowVersion column that is not rowversion. Rename or remove it, then run again.', 1;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'CFP.Inflow') AND name = N'Description'
               AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [CFP].[Inflow] ALTER COLUMN [Description] nvarchar(max) NULL;');
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'CFP.Inflow') AND name = N'Comment'
               AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [CFP].[Inflow] ALTER COLUMN [Comment] nvarchar(max) NULL;');

    IF COL_LENGTH(N'CFP.Inflow', N'RowVersion') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [RowVersion] rowversion NOT NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'WorkflowState') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [WorkflowState] varchar(100) NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'Status') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [Status] varchar(100) NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'FundsReceived') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [FundsReceived] bit NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'CashSubmittedDate') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [CashSubmittedDate] datetime NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'RMCC') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [RMCC] varchar(1000) NULL;');
    IF COL_LENGTH(N'CFP.Inflow', N'DestructionDate') IS NULL
        EXEC(N'ALTER TABLE [CFP].[Inflow] ADD [DestructionDate] AS (DATEADD(year, 7, [CreatedDate]));');

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'100-cfp-inflow-columns.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'100-cfp-inflow-columns.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the columns of CFP.Inflow that this script changes or adds.
SELECT c.name AS ColumnName, TYPE_NAME(c.system_type_id) AS TypeName, c.max_length, c.is_nullable, c.is_computed
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'CFP.Inflow')
  AND c.name IN (N'Description', N'Comment', N'RowVersion', N'WorkflowState', N'Status', N'FundsReceived',
                 N'CashSubmittedDate', N'RMCC', N'DestructionDate');
GO
