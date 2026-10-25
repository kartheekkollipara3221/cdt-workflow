-- CP Atlas: grant or revoke one app role for one user (spec 06, section 4.4, question 6).
-- DML template. Changes rows in dbo.CP_Atlas_User and dbo.CP_Atlas_UserEntitlement only.
-- Safe to run again: a grant that exists or a revoke that is done changes nothing.
-- Run in the CP Atlas database (cp_sharepoint). In QC and production the DBA runs it.
--
-- Revoke is a soft delete (IsDeleted = 1). The script never deletes a row.
-- A later admin screen must use the same logic (find by LAN ID, app code and role name; soft delete).
--
-- Rollback: run the script again with the opposite @Action.
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ===== Edit these values =====
DECLARE @Action      varchar(10)   = 'Grant';          -- 'Grant' or 'Revoke'
DECLARE @LanId       nvarchar(50)  = N'DOMAIN\lanid';  -- the user's LAN ID
DECLARE @AppCode     varchar(20)   = 'TTD';            -- CP_Atlas_Application.Code
DECLARE @RoleName    varchar(100)  = 'Prepare';        -- CP_Atlas_Entitlement.Name in that app
DECLARE @DisplayName nvarchar(1000) = NULL;            -- only for a new user (Grant)
DECLARE @Email       nvarchar(500)  = NULL;            -- only for a new user (Grant)
DECLARE @RunBy       nvarchar(50)  = N'DOMAIN\dba';    -- the person who runs the script
-- =============================

DECLARE @Now datetime = GETUTCDATE();
DECLARE @EntitlementId int, @UserId int, @UserEntitlementId int;

IF @Action NOT IN ('Grant', 'Revoke')
    THROW 50001, '@Action must be Grant or Revoke.', 1;

SELECT @EntitlementId = e.ID
FROM dbo.CP_Atlas_Entitlement AS e
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
WHERE a.Code = @AppCode
  AND e.Name = @RoleName
  AND ISNULL(a.IsDeleted, 0) = 0
  AND ISNULL(e.IsDeleted, 0) = 0;

IF @EntitlementId IS NULL
    THROW 50002, 'No active role with this name in this app.', 1;

BEGIN TRANSACTION;

SELECT @UserId = u.ID
FROM dbo.CP_Atlas_User AS u WITH (UPDLOCK, HOLDLOCK)
WHERE u.LanID = @LanId AND ISNULL(u.IsDeleted, 0) = 0;

IF @Action = 'Grant'
BEGIN
    IF @UserId IS NULL
    BEGIN
        INSERT INTO dbo.CP_Atlas_User (LanID, DisplayName, Email, CreatedBy, CreatedDate, IsDeleted)
        VALUES (@LanId, @DisplayName, @Email, @RunBy, @Now, 0);
        SET @UserId = SCOPE_IDENTITY();
    END;

    -- Use the newest row for this user and role (active or soft-deleted).
    SELECT TOP (1) @UserEntitlementId = ue.ID
    FROM dbo.CP_Atlas_UserEntitlement AS ue WITH (UPDLOCK, HOLDLOCK)
    WHERE ue.CP_Atlas_UserID = @UserId AND ue.CP_Atlas_EntitlementID = @EntitlementId
    ORDER BY CASE WHEN ISNULL(ue.IsDeleted, 0) = 0 THEN 0 ELSE 1 END, ue.ID DESC;

    IF @UserEntitlementId IS NULL
        INSERT INTO dbo.CP_Atlas_UserEntitlement (CP_Atlas_UserID, CP_Atlas_EntitlementID, CreatedBy, CreatedDate, IsDeleted)
        VALUES (@UserId, @EntitlementId, @RunBy, @Now, 0);
    ELSE
        UPDATE dbo.CP_Atlas_UserEntitlement
        SET IsDeleted = 0, ModifiedBy = @RunBy, ModifiedDate = @Now
        WHERE ID = @UserEntitlementId AND ISNULL(IsDeleted, 0) = 1;
END
ELSE
BEGIN
    IF @UserId IS NOT NULL
        UPDATE dbo.CP_Atlas_UserEntitlement
        SET IsDeleted = 1, ModifiedBy = @RunBy, ModifiedDate = @Now
        WHERE CP_Atlas_UserID = @UserId
          AND CP_Atlas_EntitlementID = @EntitlementId
          AND ISNULL(IsDeleted, 0) = 0;
END;

COMMIT TRANSACTION;
GO

-- Check: the active roles of the user after the change.
DECLARE @CheckLanId nvarchar(50) = N'DOMAIN\lanid';
SELECT ApplicationCode, RoleName, IsAdmin, IsWrite, IsRead
FROM dbo.vw_CP_Atlas_ActiveUserRole
WHERE LanId = @CheckLanId
ORDER BY ApplicationCode, RoleName;
GO
