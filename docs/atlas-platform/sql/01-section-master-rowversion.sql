-- CP Atlas Section Master: optimistic concurrency token (spec 01, section 5.13, finding SM-18).
-- Additive DDL. Adds one rowversion column to each Section Master table.
-- SQL Server fills the column for existing rows. No existing data changes.
-- Safe to run again. Run in the CP Atlas database (cp_sharepoint).
--
-- Before you run it:
--   1. Make sure that no procedure, view or job uses INSERT without a column list, or
--      SELECT * into a fixed shape, on these tables (spec open question 8).
--   2. A table can have only one rowversion column. The script stops if a table already
--      has a rowversion column with a different name.
--   3. On a large table, the ALTER writes each row. Run it in a maintenance window.
--
-- SCHEMA NAME: the live database uses [SQMS] (confirmed 2026-10-25, spec open question 9).
--
-- Known other writer: the workflow engine SQMS scheduled job updates Status, WorkflowState,
-- ModifiedBy and ModifiedDate with an explicit column list. The new column does not break it.
--
-- PRE-CHECK (read only, run first, outside this script): list the database modules that
-- use these tables. Review each one for INSERT without a column list or SELECT *.
--   SELECT OBJECT_SCHEMA_NAME(m.object_id) AS SchemaName, OBJECT_NAME(m.object_id) AS ObjectName,
--          o.type_desc
--   FROM sys.sql_modules AS m INNER JOIN sys.objects AS o ON o.object_id = m.object_id
--   WHERE m.definition LIKE N'%Opportunity%' OR m.definition LIKE N'%Bid%'
--      OR m.definition LIKE N'%TTD%General%' OR m.definition LIKE N'%AccountInventory%'
--   ORDER BY SchemaName, ObjectName;
--
-- Rollback (only if the application does not use the column yet):
--   ALTER TABLE <table> DROP COLUMN RowVersion;
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @tables TABLE (TableName sysname NOT NULL);
    INSERT INTO @tables (TableName) VALUES
        (N'SQMS.Opportunity'),
        (N'SQMS.Bid'),
        (N'TTD.General'),
        (N'AIN.AccountInventory');

    IF EXISTS (SELECT 1 FROM @tables WHERE OBJECT_ID(TableName, N'U') IS NULL)
        THROW 53001, 'An expected Section Master table was not found.', 1;

    IF EXISTS (
        SELECT 1
        FROM @tables AS t
        INNER JOIN sys.columns AS c ON c.object_id = OBJECT_ID(t.TableName)
        WHERE c.system_type_id = TYPE_ID(N'timestamp') AND c.name <> N'RowVersion')
        THROW 53002, 'A table already has a rowversion column with a different name. Review before you continue.', 1;

    IF COL_LENGTH(N'SQMS.Opportunity', N'RowVersion') IS NULL
        ALTER TABLE SQMS.Opportunity ADD RowVersion rowversion NOT NULL;
    IF COL_LENGTH(N'SQMS.Bid', N'RowVersion') IS NULL
        ALTER TABLE SQMS.Bid ADD RowVersion rowversion NOT NULL;
    IF COL_LENGTH(N'TTD.General', N'RowVersion') IS NULL
        ALTER TABLE TTD.General ADD RowVersion rowversion NOT NULL;
    IF COL_LENGTH(N'AIN.AccountInventory', N'RowVersion') IS NULL
        ALTER TABLE AIN.AccountInventory ADD RowVersion rowversion NOT NULL;

    -- Shape check: each table has RowVersion as a non-null rowversion column.
    IF EXISTS (
        SELECT 1 FROM @tables AS t
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(t.TableName)
              AND c.name = N'RowVersion'
              AND c.system_type_id = TYPE_ID(N'timestamp')
              AND c.is_nullable = 0))
        THROW 53003, 'RowVersion does not match the required definition.', 1;

    COMMIT TRANSACTION;
    PRINT 'Section Master RowVersion columns are present.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
