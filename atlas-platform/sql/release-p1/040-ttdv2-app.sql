-- CP Atlas: TTDV2 app (release P1, script 040). Makes the schema, tables, app row and roles of TTDV2.
-- Made from the new-app template (sql/05-new-app-template.sql) for TTDV2.
-- DDL + DML. Development: the target team runs it. QC and production: the DBA runs it.
-- Additive. Safe to run again: each object is made only if it does not exist.
-- It does not change the TTD schema or any existing table. Exception: if an earlier run made
-- TTDV2.General.IsDeleted bit NULL, step 2a makes it bit NOT NULL. If an earlier run made the free-text
-- columns varchar, step 2b makes them nvarchar. A rerun adds the index IX_General_ModifiedBy (no data change).
-- Run in the CP Atlas database (cp_sharepoint).
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- filtered indexes need it (sqlcmd default is OFF)
SET XACT_ABORT ON;

DECLARE @RunBy nvarchar(50) = N'<RUN_BY>';   -- LAN ID of the person who runs the script.
IF @RunBy LIKE N'%<%'
    THROW 55001, 'Set @RunBy before you run the script.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Schema.
    IF SCHEMA_ID(N'TTDV2') IS NULL
        EXEC(N'CREATE SCHEMA [TTDV2]');

    -- 2. Main table. Each field has a typed column. Each person column holds a LAN ID.
    --    System dates are UTC.
    IF OBJECT_ID(N'TTDV2.General', N'U') IS NULL
        EXEC(N'CREATE TABLE [TTDV2].[General] (
            [ID]               int IDENTITY(10000,1) NOT NULL,
            [Account]          nvarchar(1000)  NULL,
            [AccountNumber]    varchar(100)   NULL,
            [AccountName]      nvarchar(1000)  NULL,
            [TaxYear]          smallint       NULL,
            [Category]         varchar(200)   NULL,
            [WorkDescription]  nvarchar(max)   NULL,
            [WeekEnding]       date           NULL,
            [MondayMinute]     int            NULL,
            [TuesdayMinute]    int            NULL,
            [WednesdayMinute]  int            NULL,
            [ThursdayMinute]   int            NULL,
            [FridayMinute]     int            NULL,
            [SaturdayMinute]   int            NULL,
            [SundayMinute]     int            NULL,
            [WeeklyTotal]      int            NULL,
            [TaxPreparerLanId] nvarchar(50)   NULL,
            [CreatedBy]        nvarchar(50)   NULL,
            [CreatedDate]      datetime       NULL,
            [ModifiedBy]       nvarchar(50)   NULL,
            [ModifiedDate]     datetime       NULL,
            [IsDeleted]        bit            NOT NULL CONSTRAINT [DF_TTDV2_General_IsDeleted] DEFAULT (0),
            [RowVersion]       rowversion     NOT NULL,
            CONSTRAINT [PK_TTDV2_General] PRIMARY KEY CLUSTERED ([ID] ASC),
            CONSTRAINT [CK_TTDV2_General_Minutes] CHECK (
                    ([MondayMinute]    IS NULL OR [MondayMinute]    BETWEEN 0 AND 1440)
                AND ([TuesdayMinute]   IS NULL OR [TuesdayMinute]   BETWEEN 0 AND 1440)
                AND ([WednesdayMinute] IS NULL OR [WednesdayMinute] BETWEEN 0 AND 1440)
                AND ([ThursdayMinute]  IS NULL OR [ThursdayMinute]  BETWEEN 0 AND 1440)
                AND ([FridayMinute]    IS NULL OR [FridayMinute]    BETWEEN 0 AND 1440)
                AND ([SaturdayMinute]  IS NULL OR [SaturdayMinute]  BETWEEN 0 AND 1440)
                AND ([SundayMinute]    IS NULL OR [SundayMinute]    BETWEEN 0 AND 1440))
        );');

    -- 2a. IsDeleted is bit NOT NULL, so the dashboard filter [IsDeleted] = 0 can seek the filtered indexes.
    --     If an earlier run made it bit NULL: stop when a row has NULL (no data is changed).
    --     Else drop the filtered indexes (they block ALTER COLUMN), alter the column and make the indexes
    --     again below. No data loss. Rollback: ALTER COLUMN [IsDeleted] bit NULL (drop and make
    --     the indexes again in the same way).
    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IsDeleted' AND is_nullable = 1)
    BEGIN
        IF EXISTS (SELECT 1 FROM [TTDV2].[General] WHERE [IsDeleted] IS NULL)
            THROW 55008, 'TTDV2.General has rows with IsDeleted NULL. Check them (NULL means not deleted), set them to 0, then run the script again.', 1;
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_CreatedBy')
            EXEC(N'DROP INDEX [IX_General_CreatedBy] ON [TTDV2].[General];');
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_TaxPreparerLanId')
            EXEC(N'DROP INDEX [IX_General_TaxPreparerLanId] ON [TTDV2].[General];');
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_ModifiedBy')
            EXEC(N'DROP INDEX [IX_General_ModifiedBy] ON [TTDV2].[General];');
        EXEC(N'ALTER TABLE [TTDV2].[General] ALTER COLUMN [IsDeleted] bit NOT NULL;');
    END

    -- 2b. Free text is nvarchar, so pasted Unicode text is not stored as '?'.
    --     If an earlier run made these columns varchar, alter them to nvarchar of the same length.
    --     No data loss (each varchar value fits in nvarchar). Rollback: ALTER COLUMN back to varchar (loses the
    --     characters that varchar cannot store).
    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'Account' AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [TTDV2].[General] ALTER COLUMN [Account] nvarchar(1000) NULL;');
    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'AccountName' AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [TTDV2].[General] ALTER COLUMN [AccountName] nvarchar(1000) NULL;');
    IF EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'WorkDescription' AND system_type_id = TYPE_ID(N'varchar'))
        EXEC(N'ALTER TABLE [TTDV2].[General] ALTER COLUMN [WorkDescription] nvarchar(max) NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_CreatedBy')
        EXEC(N'CREATE INDEX [IX_General_CreatedBy] ON [TTDV2].[General] ([CreatedBy]) WHERE [IsDeleted] = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_TaxPreparerLanId')
        EXEC(N'CREATE INDEX [IX_General_TaxPreparerLanId] ON [TTDV2].[General] ([TaxPreparerLanId]) WHERE [IsDeleted] = 0;');
    -- The "Current Tax Timesheet" tab filters CreatedBy OR TaxPreparerLanId OR ModifiedBy.
    -- An OR can use an index union only when each column has an index. Without this one, the tab scans the table.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(N'TTDV2.General') AND name = N'IX_General_ModifiedBy')
        EXEC(N'CREATE INDEX [IX_General_ModifiedBy] ON [TTDV2].[General] ([ModifiedBy]) WHERE [IsDeleted] = 0;');

    -- 3. Audit table in the target shape (ReferenceID, ReferenceType), plus CreatedByLanId. Dates are UTC.
    IF OBJECT_ID(N'TTDV2.AuditLog', N'U') IS NULL
        EXEC(N'CREATE TABLE [TTDV2].[AuditLog] (
            [ID]             int IDENTITY(1,1) NOT NULL,
            [ReferenceID]    int            NULL,
            [ReferenceType]  varchar(50)    NULL,
            [CreatedBy]      nvarchar(50)   NULL,
            [CreatedByLanId] nvarchar(50)   NULL,
            [CreatedDate]    datetime       NULL,
            [LogMessage]     nvarchar(max)  NULL,
            CONSTRAINT [PK_TTDV2_AuditLog] PRIMARY KEY CLUSTERED ([ID] ASC)
        );');

    -- A table from an older 040 has RefID and RefType. 045 renames these columns and the index.
    IF COL_LENGTH(N'TTDV2.AuditLog', N'ReferenceID') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'TTDV2.AuditLog') AND name = N'IX_AuditLog_ReferenceID')
        EXEC(N'CREATE INDEX [IX_AuditLog_ReferenceID] ON [TTDV2].[AuditLog] ([ReferenceID], [ReferenceType]);');

    -- No attachment table: release P1 has no attachments.

    -- 4. Application row. Single app (no parent).
    --    A deleted TTDV2 row or a second TTDV2 row stops the script. The roles must not go to a deleted row.
    IF EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'TTDV2' AND ISNULL(IsDeleted, 0) = 1)
        THROW 55010, 'dbo.CP_Atlas_Application has a deleted TTDV2 row. Restore it (IsDeleted = 0) or remove it, then run again.', 1;
    IF (SELECT COUNT(*) FROM dbo.CP_Atlas_Application WHERE Code = 'TTDV2') > 1
        THROW 55011, 'dbo.CP_Atlas_Application has more than one TTDV2 row. Keep one, then run again.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'TTDV2')
        INSERT INTO dbo.CP_Atlas_Application (Name, Code, CreatedBy, CreatedDate, IsDeleted, ParentID)
        VALUES ('Tax Time Database', 'TTDV2', @RunBy, GETUTCDATE(), 0, NULL);

    DECLARE @AppId int = (SELECT ID FROM dbo.CP_Atlas_Application WHERE Code = 'TTDV2' AND ISNULL(IsDeleted, 0) = 0);

    -- 5. Roles with app-level flags.
    DECLARE @Roles TABLE (Name varchar(100) NOT NULL, IsAdmin bit NOT NULL, IsWrite bit NOT NULL, IsRead bit NOT NULL);
    INSERT INTO @Roles (Name, IsAdmin, IsWrite, IsRead) VALUES
        ('Read',    0, 0, 1),
        ('Prepare', 0, 1, 1),
        ('Owner',   1, 1, 1);

    INSERT INTO dbo.CP_Atlas_Entitlement (CP_Atlas_ApplicationID, Name, IsAdmin, IsWrite, IsRead, CreatedBy, CreatedDate, IsDeleted)
    SELECT @AppId, r.Name, r.IsAdmin, r.IsWrite, r.IsRead, @RunBy, GETUTCDATE(), 0
    FROM @Roles AS r
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Entitlement AS e
                      WHERE e.CP_Atlas_ApplicationID = @AppId AND e.Name = r.Name);

    -- 6. Permissions for the API login (QC and production).
    --    Skip if the login gets the rights from a database role.
    -- GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[TTDV2] TO [<API_PRINCIPAL>];

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'12-ttdv2-app.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'12-ttdv2-app.sql', N'P1');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- 7. Result.
SELECT a.ID, a.Code, a.Name, a.ParentID FROM dbo.CP_Atlas_Application AS a WHERE a.Code = 'TTDV2';
SELECT e.ID, e.Name, e.IsAdmin, e.IsWrite, e.IsRead
FROM dbo.CP_Atlas_Entitlement AS e
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
WHERE a.Code = 'TTDV2' AND ISNULL(e.IsDeleted, 0) = 0;
GO
