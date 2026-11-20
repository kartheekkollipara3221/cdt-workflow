-- CP Atlas, release P2, script 130: the app row and the roles of the platform app CFPINFLOW.
-- App "Client Flows Portal - Inflow", code CFPINFLOW, no parent.
-- Roles: Read (IsRead) and Write (IsWrite, IsRead). No admin role.
-- DML only. Additive. Safe to run again: each row is added only when it is not there.
-- Run in the CP Atlas database (cp_sharepoint). Grant the roles to the users with the grant template
-- (docs/atlas-platform/sql/06-grant-revoke-role-template.sql), not in this script.
--
-- Rollback: set AppSettings:Modules:CFPINFLOW:Enabled = false and restart. To remove the menu row, set IsDeleted = 1
-- on the app row and its roles.
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @RunBy nvarchar(50) = N'<RUN_BY>';   -- LAN ID of the person who runs the script.
IF @RunBy LIKE N'%<%'
    THROW 55130, 'Set @RunBy before you run the script.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- A deleted CFPINFLOW row or a second CFPINFLOW row stops the script. The roles must not go to a deleted row.
    IF EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'CFPINFLOW' AND ISNULL(IsDeleted, 0) = 1)
        THROW 55131, 'dbo.CP_Atlas_Application has a deleted CFPINFLOW row. Restore it (IsDeleted = 0) or remove it, then run again.', 1;
    IF (SELECT COUNT(*) FROM dbo.CP_Atlas_Application WHERE Code = 'CFPINFLOW') > 1
        THROW 55132, 'dbo.CP_Atlas_Application has more than one CFPINFLOW row. Keep one, then run again.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'CFPINFLOW')
        INSERT INTO dbo.CP_Atlas_Application (Name, Code, CreatedBy, CreatedDate, IsDeleted, ParentID)
        VALUES ('Client Flows Portal - Inflow', 'CFPINFLOW', @RunBy, GETUTCDATE(), 0, NULL);

    DECLARE @AppId int = (SELECT ID FROM dbo.CP_Atlas_Application WHERE Code = 'CFPINFLOW' AND ISNULL(IsDeleted, 0) = 0);

    DECLARE @Roles TABLE (Name varchar(100) NOT NULL, IsAdmin bit NOT NULL, IsWrite bit NOT NULL, IsRead bit NOT NULL);
    INSERT INTO @Roles (Name, IsAdmin, IsWrite, IsRead) VALUES
        ('Read',  0, 0, 1),
        ('Write', 0, 1, 1);

    INSERT INTO dbo.CP_Atlas_Entitlement (CP_Atlas_ApplicationID, Name, IsAdmin, IsWrite, IsRead, CreatedBy, CreatedDate, IsDeleted)
    SELECT @AppId, r.Name, r.IsAdmin, r.IsWrite, r.IsRead, @RunBy, GETUTCDATE(), 0
    FROM @Roles AS r
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Entitlement AS e
                      WHERE e.CP_Atlas_ApplicationID = @AppId AND e.Name = r.Name);

    -- Log this script in the same transaction: the log says "applied" only when the changes are committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P2' AND ScriptName = N'130-cfpinflow-app.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'130-cfpinflow-app.sql', N'P2');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

-- Result: the app row and its roles.
SELECT a.ID, a.Code, a.Name, a.ParentID FROM dbo.CP_Atlas_Application AS a WHERE a.Code = 'CFPINFLOW';
SELECT e.ID, e.Name, e.IsAdmin, e.IsWrite, e.IsRead
FROM dbo.CP_Atlas_Entitlement AS e
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
WHERE a.Code = 'CFPINFLOW' AND ISNULL(e.IsDeleted, 0) = 0;
GO
