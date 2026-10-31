-- CP Atlas: active user roles view (spec 06, section 4.4, finding SE-03).
-- Additive DDL. Makes one view. Changes no data. Safe to run again (CREATE OR ALTER).
-- Run in the CP Atlas database (cp_sharepoint). In QC and production the DBA runs it.
--
-- All role queries (v1/authorize, dbo.Get_Application, the module services) use this view,
-- so that a deleted user, user entitlement, entitlement or application gives no role.
--
-- Before use: run part 4 of sql/01-section-master-role-scope-check.sql. It lists the users
-- who have roles today only through deleted rows. These users lose the roles.
--
-- Rollback: DROP VIEW dbo.vw_CP_Atlas_ActiveUserRole;
SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW dbo.vw_CP_Atlas_ActiveUserRole
AS
SELECT
    u.ID                       AS UserId,
    u.LanID                    AS LanId,
    u.DisplayName              AS DisplayName,
    u.Email                    AS Email,
    a.ID                       AS ApplicationId,
    a.Code                     AS ApplicationCode,
    a.ParentID                 AS ParentApplicationId,
    e.ID                       AS EntitlementId,
    e.Name                     AS RoleName,
    ISNULL(e.IsAdmin, 0)       AS IsAdmin,
    ISNULL(e.IsWrite, 0)       AS IsWrite,
    ISNULL(e.IsRead, 0)        AS IsRead
FROM dbo.CP_Atlas_User AS u
INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
WHERE ISNULL(u.IsDeleted, 0) = 0
  AND ISNULL(ue.IsDeleted, 0) = 0
  AND ISNULL(e.IsDeleted, 0) = 0
  AND ISNULL(a.IsDeleted, 0) = 0
  -- Review 3 RV-08: a row with no LAN ID, app code or role name gives no role (the target owns this data).
  AND u.LanID IS NOT NULL
  AND a.Code IS NOT NULL
  AND e.Name IS NOT NULL;
GO

-- Check: the view exists and returns rows.
SELECT COUNT(*) AS ActiveRoleRows FROM dbo.vw_CP_Atlas_ActiveUserRole;
GO
