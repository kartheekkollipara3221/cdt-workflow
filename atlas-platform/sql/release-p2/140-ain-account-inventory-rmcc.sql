-- CP Atlas, release P2, script 140: the column RMCC in AIN.AccountInventory.
-- The table belongs to the AIN app. The AIN team runs this script or copies the change into its own scripts.
-- RMCC holds the display names of the RM copy list, separated by ';'. The platform reads it and never writes it.
-- DDL only. Additive. Safe to run again: the column is added only when it is not there.
-- Run in the CP Atlas database (cp_sharepoint).
--
-- Rollback: drop the column RMCC (the AIN app does not use it).
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'AIN.AccountInventory', N'U') IS NULL
        THROW 55140, 'AIN.AccountInventory does not exist. Check the database, then run again.', 1;

    IF COL_LENGTH(N'AIN.AccountInventory', N'RMCC') IS NULL
        EXEC(N'ALTER TABLE [AIN].[AccountInventory] ADD [RMCC] varchar(1000) NULL;');

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'140-ain-account-inventory-rmcc.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'140-ain-account-inventory-rmcc.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the column.
SELECT c.name AS ColumnName, TYPE_NAME(c.system_type_id) AS TypeName, c.max_length, c.is_nullable
FROM sys.columns AS c WHERE c.object_id = OBJECT_ID(N'AIN.AccountInventory') AND c.name = N'RMCC';
GO
