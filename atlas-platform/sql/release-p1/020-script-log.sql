-- CP Atlas: script log table (spec 11, section 4.4, question 3, finding CD-04).
-- Additive DDL. Makes one table. Changes no existing data. Safe to run again.
-- Run in the CP Atlas database (cp_sharepoint). In QC and production the DBA runs it.
-- Run it once, as the first platform script (after the engine migration script) of release P1.
--
-- Each later platform script ends with the "log this script" block at the bottom of this file.
-- The API login needs SELECT on the table (start-up check, module 7 4.8); it does not write to it.
--
-- Rollback: DROP TABLE dbo.CP_Atlas_ScriptLog;  (no other object depends on it)
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CP_Atlas_ScriptLog
    (
        ID           int IDENTITY(1,1) NOT NULL,
        ScriptName   nvarchar(200)     NOT NULL,
        Release      nvarchar(50)      NOT NULL,
        AppliedAtUtc datetime2(3)      NOT NULL CONSTRAINT DF_CP_Atlas_ScriptLog_AppliedAtUtc DEFAULT (SYSUTCDATETIME()),
        AppliedBy    nvarchar(128)     NOT NULL CONSTRAINT DF_CP_Atlas_ScriptLog_AppliedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_CP_Atlas_ScriptLog PRIMARY KEY CLUSTERED (ID ASC)
    );

    CREATE INDEX IX_CP_Atlas_ScriptLog_Release_ScriptName ON dbo.CP_Atlas_ScriptLog (Release, ScriptName);
END;
GO

-- Optional (DBA): give the API login read access.
-- GRANT SELECT ON dbo.CP_Atlas_ScriptLog TO [<API_PRINCIPAL>];
GO

-- Log this script.
IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'11-script-log.sql')
    INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'11-script-log.sql', N'P1');
GO

-- ===== Block to copy to the end of each platform script =====
-- IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
--    AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'<RELEASE>' AND ScriptName = N'<SCRIPT_FILE_NAME>')
--     INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'<SCRIPT_FILE_NAME>', N'<RELEASE>');
-- GO
