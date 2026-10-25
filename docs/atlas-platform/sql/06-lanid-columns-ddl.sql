-- CP Atlas: LAN ID columns for the existing Section Master tables (spec 06, section 4.5, finding SE-04).
-- Additive DDL. Adds nullable columns. Changes no data. Safe to run again.
-- Run in the CP Atlas database (cp_sharepoint). In QC and production the DBA runs it.
-- Then run 06-lanid-backfill-dml.sql.
--
-- The display name columns stay. The application uses the LAN ID column when it has a value,
-- and the display name column when it does not.
--
-- Rollback (only before the application uses the columns):
--   ALTER TABLE <table> DROP COLUMN <column>;
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @cols TABLE (TableName sysname, ColumnName sysname, TypeSql nvarchar(50));
    INSERT INTO @cols VALUES
        (N'SQMS.Opportunity',     N'CreatedByLanId',     N'varchar(50) NULL'),
        (N'SQMS.Opportunity',     N'ModifiedByLanId',    N'varchar(50) NULL'),
        (N'SQMS.Opportunity',     N'SellerRMTeamLanIds', N'varchar(1000) NULL'),  -- '#$#' separated, as SellerRMteam
        (N'SQMS.Bid',             N'CreatedByLanId',     N'varchar(50) NULL'),
        (N'SQMS.Bid',             N'ModifiedByLanId',    N'varchar(50) NULL'),
        (N'SQMS.Bid',             N'BuyerRMTeamLanIds',  N'varchar(1000) NULL'),  -- '#$#' separated, as BuyerRMteam
        (N'TTD.General',          N'CreatedByLanId',     N'varchar(50) NULL'),
        (N'TTD.General',          N'ModifiedByLanId',    N'varchar(50) NULL'),
        (N'TTD.General',          N'TaxPreparerLanId',   N'varchar(50) NULL'),
        (N'AIN.AccountInventory', N'CreatedByLanId',     N'varchar(50) NULL'),
        (N'AIN.AccountInventory', N'ModifiedByLanId',    N'varchar(50) NULL');

    IF EXISTS (SELECT 1 FROM @cols WHERE OBJECT_ID(TableName, N'U') IS NULL)
        THROW 56001, 'An expected table was not found.', 1;

    DECLARE @t sysname, @c sysname, @type nvarchar(50), @sql nvarchar(max);
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, ColumnName, TypeSql FROM @cols;
    OPEN cur;
    FETCH NEXT FROM cur INTO @t, @c, @type;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF COL_LENGTH(@t, @c) IS NULL
        BEGIN
            SET @sql = N'ALTER TABLE ' + QUOTENAME(PARSENAME(@t, 2)) + N'.' + QUOTENAME(PARSENAME(@t, 1))
                     + N' ADD ' + QUOTENAME(@c) + N' ' + @type + N';';
            EXEC sys.sp_executesql @sql;
        END;
        FETCH NEXT FROM cur INTO @t, @c, @type;
    END;
    CLOSE cur;
    DEALLOCATE cur;

    -- Shape check: each column exists and is nullable varchar.
    IF EXISTS (
        SELECT 1 FROM @cols AS x
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.columns AS sc
            WHERE sc.object_id = OBJECT_ID(x.TableName) AND sc.name = x.ColumnName
              AND sc.system_type_id = TYPE_ID(N'varchar') AND sc.is_nullable = 1))
        THROW 56002, 'A LAN ID column does not match the required definition.', 1;

    COMMIT TRANSACTION;
    PRINT 'LAN ID columns are present.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
