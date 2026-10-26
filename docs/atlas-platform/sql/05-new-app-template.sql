-- CP Atlas: new Section Master app template (spec 05, section 6.9).
-- TEMPLATE. Do not run as it is. Replace each <PLACEHOLDER> first.
--
--   <APP_SCHEMA>      Schema of the app, for example PBSP.
--   <APP_CODE>        App code, for example PBSP (max 20 characters). Must agree with the JSON "Code".
--   <APP_NAME>        Display name, for example 'Private Banking Service Plan'.
--   <PARENT_CODE>     Code of the parent app for a child app, or NULL for a single app.
--   <RUN_BY>          Name of the person who runs the script (for CreatedBy).
--
-- Who runs it: the target team in development; the DBAs in QC and production.
-- Use typed columns for the business data (int, decimal, date) (spec 05, question 3).
--
-- Additive. Safe to run again: each object is made only if it does not exist.
-- Run in the CP Atlas database (cp_sharepoint).
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- filtered indexes need it (sqlcmd default is OFF; finding B12)
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Schema sysname = N'<APP_SCHEMA>';
    DECLARE @AppCode varchar(20) = '<APP_CODE>';
    DECLARE @AppName varchar(500) = '<APP_NAME>';
    DECLARE @ParentCode varchar(20) = NULL;   -- '<PARENT_CODE>' for a child app
    DECLARE @RunBy nvarchar(50) = N'<RUN_BY>';

    IF @Schema LIKE N'%<%' OR @AppCode LIKE '%<%' OR @AppName LIKE '%<%' OR @RunBy LIKE N'%<%'
        THROW 55001, 'Replace all placeholders before you run the template.', 1;
    IF @Schema LIKE N'%[^A-Za-z0-9_]%' OR @AppCode LIKE '%[^A-Za-z0-9_]%'
        THROW 55003, 'Schema and app code can contain only letters, digits and underscore.', 1;

    -- 1. Schema.
    IF SCHEMA_ID(@Schema) IS NULL
        EXEC(N'CREATE SCHEMA ' + N'[' + @Schema + N']');

    -- 2. Main table. Add the business columns where marked.
    --    The control "Field" values in the JSON must be column names of this table.
    IF OBJECT_ID(QUOTENAME(@Schema) + N'.[General]', N'U') IS NULL
        EXEC(N'CREATE TABLE ' + N'[' + @Schema + N'].[General] (
            [ID]            int IDENTITY(10000,1) NOT NULL,
            -- Business columns. Prefer typed columns (spec 05, open question 3). Examples:
            -- [AccountNumber] varchar(100)   NULL,
            -- [AccountName]   varchar(1000)  NULL,
            -- [Amount]        decimal(19,2)  NULL,
            -- [EffectiveDate] date           NULL,
            -- Child app only: link to the parent record.
            -- [ParentId]      int            NULL,
            [Status]        varchar(100)   NULL,
            [WorkflowState] varchar(100)   NULL,
            [CreatedBy]     nvarchar(50)   NULL,
            [CreatedDate]   datetime       NULL,
            [ModifiedBy]    nvarchar(50)   NULL,
            [ModifiedDate]  datetime       NULL,
            -- NOT NULL (review DA-08): the dashboard filter is then [IsDeleted] = 0, which can use the filtered indexes.
            [IsDeleted]     bit            NOT NULL CONSTRAINT [DF_' + @Schema + N'_General_IsDeleted] DEFAULT (0),
            [RowVersion]    rowversion     NOT NULL,
            CONSTRAINT [PK_' + @Schema + N'_General] PRIMARY KEY CLUSTERED ([ID] ASC)
        );');

    -- 3. Audit table (standard shape, spec 05, section 6.6).
    IF OBJECT_ID(QUOTENAME(@Schema) + N'.[AuditLog]', N'U') IS NULL
        EXEC(N'CREATE TABLE ' + N'[' + @Schema + N'].[AuditLog] (
            [ID]          int IDENTITY(1,1) NOT NULL,
            [RefID]       int            NULL,
            [RefType]     varchar(50)    NULL,
            [CreatedBy]   nvarchar(50)   NULL,
            [CreatedDate] datetime       NULL,
            [LogMessage]  nvarchar(max)  NULL,
            CONSTRAINT [PK_' + @Schema + N'_AuditLog] PRIMARY KEY CLUSTERED ([ID] ASC)
        );');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(QUOTENAME(@Schema) + N'.[AuditLog]') AND name = N'IX_AuditLog_RefID')
        EXEC(N'CREATE INDEX [IX_AuditLog_RefID] ON [' + @Schema + N'].[AuditLog] ([RefID], [RefType]);');

    -- 3a. Attachment table (spec 08, section 4.2, question 1: one table for each app).
    --     Skip it if the app has no filepicker control. FileContent is GZip, as in dbo.CDT_Attachment.
    IF OBJECT_ID(QUOTENAME(@Schema) + N'.[Attachment]', N'U') IS NULL
        EXEC(N'CREATE TABLE ' + N'[' + @Schema + N'].[Attachment] (
            [ID]              int IDENTITY(10000,1) NOT NULL,
            [RecordID]        int             NOT NULL,
            [ControlName]     varchar(200)    NOT NULL,
            [FileName]        nvarchar(255)   NOT NULL,
            [ContentType]     varchar(200)    NULL,
            [SizeBytes]       bigint          NULL,
            [FileContent]     varbinary(max)  NULL,
            [CreatedBy]       nvarchar(50)    NULL,
            [CreatedByLanId]  nvarchar(50)    NULL,
            [CreatedDate]     datetime2(3)    NULL,
            [ModifiedBy]      nvarchar(50)    NULL,
            [ModifiedByLanId] nvarchar(50)    NULL,
            [ModifiedDate]    datetime2(3)    NULL,
            [IsDeleted]       bit             NOT NULL CONSTRAINT [DF_' + @Schema + N'_Attachment_IsDeleted] DEFAULT (0),
            CONSTRAINT [PK_' + @Schema + N'_Attachment] PRIMARY KEY CLUSTERED ([ID] ASC),
            CONSTRAINT [FK_' + @Schema + N'_Attachment_General] FOREIGN KEY ([RecordID]) REFERENCES [' + @Schema + N'].[General] ([ID])
        );');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id = OBJECT_ID(QUOTENAME(@Schema) + N'.[Attachment]') AND name = N'IX_Attachment_RecordID')
        EXEC(N'CREATE INDEX [IX_Attachment_RecordID] ON [' + @Schema + N'].[Attachment] ([RecordID]) WHERE [IsDeleted] = 0;');

    -- 4. Application row.
    DECLARE @ParentId int = NULL;
    IF @ParentCode IS NOT NULL
    BEGIN
        SELECT @ParentId = ID FROM dbo.CP_Atlas_Application
        WHERE Code = @ParentCode AND ISNULL(IsDeleted, 0) = 0;
        IF @ParentId IS NULL
            THROW 55002, 'The parent app code was not found.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = @AppCode)
        INSERT INTO dbo.CP_Atlas_Application (Name, Code, CreatedBy, CreatedDate, IsDeleted, ParentID)
        VALUES (@AppName, @AppCode, @RunBy, GETUTCDATE(), 0, @ParentId);

    DECLARE @AppId int = (SELECT ID FROM dbo.CP_Atlas_Application WHERE Code = @AppCode);

    -- 5. Roles. Change the list to the roles of the app (spec 01, section 10.5).
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

    -- 6. Permissions for the API login (QC and production; spec 05, question 4).
    --    The API login has no DDL rights. It needs data and execute rights on the new schema.
    --    Skip this step if the login gets these rights from a database role (for example
    --    db_datareader + db_datawriter + a database-level EXECUTE grant).
    --    Replace <API_PRINCIPAL> with the database user or role of the API login.
    -- EXEC(N'GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::[' + @Schema + N'] TO [<API_PRINCIPAL>];');

    COMMIT TRANSACTION;

    -- 7. Result.
    SELECT a.ID, a.Code, a.Name, a.ParentID FROM dbo.CP_Atlas_Application AS a WHERE a.ID = @AppId;
    SELECT e.ID, e.Name, e.IsAdmin, e.IsWrite, e.IsRead FROM dbo.CP_Atlas_Entitlement AS e
    WHERE e.CP_Atlas_ApplicationID = @AppId AND ISNULL(e.IsDeleted, 0) = 0;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
