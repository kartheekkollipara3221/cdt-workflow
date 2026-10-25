-- CP Atlas Section Master: role scope check (spec 01, finding SM-08, section 11.3).
-- READ ONLY. This script changes no data.
--
-- Purpose: before the role lookup changes to "own app + parent app only",
-- list the users who would lose access to an app (parts 1-4).
-- Part 5: before ActorRoles changes to "User", list the users without the role
-- that the workflow engine needs.
--
-- Set @AppCode to the app to check (for example 'SQMSO', 'SQMSB', 'TTD', 'AIN').
-- The family of an app is the app itself and its parent (CP_Atlas_Application.ParentID).
SET NOCOUNT ON;

DECLARE @AppCode varchar(20) = 'SQMSO';
DECLARE @RequiredRole varchar(100) = 'MFO';  -- part 5: the role that the workflow engine needs (spec 5.10)

DECLARE @AppId int, @ParentId int;
SELECT @AppId = a.ID, @ParentId = a.ParentID
FROM dbo.CP_Atlas_Application AS a
WHERE a.Code = @AppCode AND ISNULL(a.IsDeleted, 0) = 0;

IF @AppId IS NULL
    THROW 54001, 'App code not found in dbo.CP_Atlas_Application.', 1;

-- 1. The family of the app.
SELECT a.ID, a.Code, a.Name, a.ParentID,
       CASE WHEN a.ID = @AppId THEN 'Self' ELSE 'Parent' END AS Relation
FROM dbo.CP_Atlas_Application AS a
WHERE a.ID IN (@AppId, @ParentId);

-- 2. All active roles of each user, marked in-family or out-of-family.
;WITH ActiveRoles AS (
    SELECT u.ID AS UserId, u.LanID, u.DisplayName,
           e.Name AS RoleName, app.Code AS RoleAppCode,
           CASE WHEN e.CP_Atlas_ApplicationID IN (@AppId, @ParentId) THEN 1 ELSE 0 END AS InFamily
    FROM dbo.CP_Atlas_User AS u
    INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    LEFT JOIN dbo.CP_Atlas_Application AS app ON app.ID = e.CP_Atlas_ApplicationID
    WHERE ISNULL(u.IsDeleted, 0) = 0
      AND ISNULL(ue.IsDeleted, 0) = 0
      AND ISNULL(e.IsDeleted, 0) = 0
)
-- 3. Users with NO in-family role. Today SQMSO can give these users access
--    through a role of the same name in another app. After the change they lose it.
SELECT r.LanID, r.DisplayName,
       STRING_AGG(r.RoleAppCode + ':' + r.RoleName, ', ') AS OutOfFamilyRoles
FROM ActiveRoles AS r
GROUP BY r.UserId, r.LanID, r.DisplayName
HAVING MAX(r.InFamily) = 0
ORDER BY r.DisplayName;

-- 4. Rows that the current code counts as roles, but that are deleted.
--    The current role queries do not filter IsDeleted (SM-08).
SELECT u.LanID, u.DisplayName, e.Name AS RoleName, app.Code AS RoleAppCode,
       u.IsDeleted AS UserDeleted, ue.IsDeleted AS UserEntitlementDeleted, e.IsDeleted AS EntitlementDeleted
FROM dbo.CP_Atlas_User AS u
INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
LEFT JOIN dbo.CP_Atlas_Application AS app ON app.ID = e.CP_Atlas_ApplicationID
WHERE e.CP_Atlas_ApplicationID IN (@AppId, @ParentId)
  AND (ISNULL(u.IsDeleted, 0) = 1 OR ISNULL(ue.IsDeleted, 0) = 1 OR ISNULL(e.IsDeleted, 0) = 1)
ORDER BY u.DisplayName;

-- 5. Users with an in-family role, but without @RequiredRole in the family.
--    After the change to ActorRoles = "User" (spec 5.10), the workflow engine
--    refuses actions that need @RequiredRole from these users.
;WITH FamilyRoles AS (
    SELECT u.ID AS UserId, u.LanID, u.DisplayName, e.Name AS RoleName
    FROM dbo.CP_Atlas_User AS u
    INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    WHERE e.CP_Atlas_ApplicationID IN (@AppId, @ParentId)
      AND ISNULL(u.IsDeleted, 0) = 0
      AND ISNULL(ue.IsDeleted, 0) = 0
      AND ISNULL(e.IsDeleted, 0) = 0
)
SELECT f.LanID, f.DisplayName, STRING_AGG(f.RoleName, ', ') AS FamilyRoles
FROM FamilyRoles AS f
GROUP BY f.UserId, f.LanID, f.DisplayName
HAVING SUM(CASE WHEN f.RoleName = @RequiredRole THEN 1 ELSE 0 END) = 0
ORDER BY f.DisplayName;
