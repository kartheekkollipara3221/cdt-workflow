-- CP Atlas: users with more than one active role in one app (spec 04, question 1, finding RE-02).
-- Read-only. Changes no data. Safe to run at any time.
-- Run in the CP Atlas database (cp_sharepoint).
--
-- With RuleMode FirstMatch (today), these users get the first matching rule in the JSON,
-- not the most permissive result. Use the output to decide if the existing apps need MostPermissive.
SET NOCOUNT ON;

-- Part 1: number of multi-role users for each app.
SELECT
    a.Code                         AS ApplicationCode,
    COUNT(*)                       AS MultiRoleUsers
FROM (
    SELECT u.ID AS UserId, e.CP_Atlas_ApplicationID AS ApplicationId
    FROM dbo.CP_Atlas_User AS u
    INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    WHERE ISNULL(u.IsDeleted, 0) = 0
      AND ISNULL(ue.IsDeleted, 0) = 0
      AND ISNULL(e.IsDeleted, 0) = 0
    GROUP BY u.ID, e.CP_Atlas_ApplicationID
    HAVING COUNT(DISTINCT e.ID) > 1
) AS m
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = m.ApplicationId
WHERE ISNULL(a.IsDeleted, 0) = 0
GROUP BY a.Code
ORDER BY a.Code;

-- Part 2: the users and their roles in each app (one row for each user and app).
SELECT
    a.Code                         AS ApplicationCode,
    u.LanID                        AS LanId,
    u.DisplayName                  AS DisplayName,
    STRING_AGG(e.Name, ', ') WITHIN GROUP (ORDER BY e.Name) AS Roles
FROM dbo.CP_Atlas_User AS u
INNER JOIN dbo.CP_Atlas_UserEntitlement AS ue ON ue.CP_Atlas_UserID = u.ID
INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID
WHERE ISNULL(u.IsDeleted, 0) = 0
  AND ISNULL(ue.IsDeleted, 0) = 0
  AND ISNULL(e.IsDeleted, 0) = 0
  AND ISNULL(a.IsDeleted, 0) = 0
GROUP BY a.Code, u.ID, u.LanID, u.DisplayName
HAVING COUNT(DISTINCT e.ID) > 1
ORDER BY a.Code, u.DisplayName;
