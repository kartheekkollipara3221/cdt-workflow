-- LOCAL ONLY: do not run at the target.
-- Seed for the local review database (spec 12, 11.3): test users with TTDV2 roles, the current Windows login
-- as Owner, dbo.Get_Application (copy of the target procedure), and sample TTDV2 time sheets.
-- Placeholders $(CurrentLanId), $(CurrentDisplayName): setup-local-db.ps1 replaces them. Safe to run again.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Users TABLE (LanId nvarchar(50) NOT NULL, DisplayName nvarchar(1000) NOT NULL, RoleName varchar(100) NULL);
INSERT INTO @Users (LanId, DisplayName, RoleName) VALUES
    (N'$(CurrentLanId)', N'$(CurrentDisplayName)', 'Owner'),
    (N'ttd.read',  N'Riley Reader',   'Read'),
    (N'ttd.prep',  N'Pat Preparer',   'Prepare'),
    (N'ttd.prep2', N'Quinn Preparer', 'Prepare'),
    (N'ttd.owner', N'Olive Owner',    'Owner'),
    (N'ttd.none',  N'Nora Norole',    NULL);

INSERT INTO dbo.CP_Atlas_User (LanID, DisplayName, FirstName, LastName, Email, CreatedBy, CreatedDate, IsDeleted)
SELECT u.LanId, u.DisplayName, NULL, NULL, u.LanId + N'@example.local', N'local.setup', GETUTCDATE(), 0
FROM (SELECT DISTINCT LanId, DisplayName FROM @Users) AS u
WHERE NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_User AS x WHERE x.LanID = u.LanId);

INSERT INTO dbo.CP_Atlas_UserEntitlement (CP_Atlas_UserID, CP_Atlas_EntitlementID, CreatedBy, CreatedDate, IsDeleted)
SELECT cu.ID, e.ID, N'local.setup', GETUTCDATE(), 0
FROM @Users AS u
INNER JOIN dbo.CP_Atlas_User AS cu ON cu.LanID = u.LanId
INNER JOIN dbo.CP_Atlas_Application AS a ON a.Code = 'TTDV2'
INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.CP_Atlas_ApplicationID = a.ID AND e.Name = u.RoleName
WHERE u.RoleName IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_UserEntitlement AS x
                  WHERE x.CP_Atlas_UserID = cu.ID AND x.CP_Atlas_EntitlementID = e.ID);
GO

-- Copy of the target procedure (CP_Sharepoint.txt, 2026-10-06). Used by v1/Application.
CREATE OR ALTER PROCEDURE [dbo].[Get_Application]
    @LanID NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    WITH CTE AS (
    SELECT A.ID,A.Name,A.Code, A.ParentID, E.Name As 'Role'
    FROM [dbo].[CP_Atlas_User] U
    INNER JOIN [dbo].[CP_Atlas_UserEntitlement] UE
    ON U.ID = UE.CP_Atlas_UserID
    INNER JOIN [dbo].[CP_Atlas_Entitlement] E
    ON E.ID = UE.CP_Atlas_EntitlementID
    INNER JOIN [CP_Atlas_Application] A
    ON A.ID = E.CP_Atlas_ApplicationID
    WHERE U.LanID = @LanID)

    Select  D.ID, D.Name, D.Code, D.ParentID,
    (SELECT STRING_AGG(T.Role, ', ') WITHIN GROUP (ORDER BY T.Role ASC) FROM CTE T Where T.ID = D.ID) As 'Role'

    from CTE D
    Group By D.ID, D.Name, D.Code, D.ParentID
    ORDER BY ID, ParentID, Name
END
GO

-- Sample time sheets (only when the table is empty). Dates relative to today; system dates UTC.
IF NOT EXISTS (SELECT 1 FROM TTDV2.General)
BEGIN
    DECLARE @LastSunday date = DATEADD(day, -((DATEPART(weekday, CAST(GETDATE() AS date)) + @@DATEFIRST - 1) % 7), CAST(GETDATE() AS date));

    INSERT INTO TTDV2.General (Account, AccountNumber, AccountName, TaxYear, Category, WorkDescription, WeekEnding,
        MondayMinute, TuesdayMinute, WednesdayMinute, ThursdayMinute, FridayMinute, SaturdayMinute, SundayMinute, WeeklyTotal,
        TaxPreparerLanId, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted)
    VALUES
    ('100100 - Smith Family Trust', '100100', 'Smith Family Trust', YEAR(GETDATE()), 'TAX_N', 'Form 1041 preparation', @LastSunday,
        120, 90, 60, 0, 30, 0, 0, 300, N'ttd.prep', N'ttd.prep', DATEADD(day, -3, GETUTCDATE()), N'ttd.prep', DATEADD(day, -2, GETUTCDATE()), 0),
    ('100200 - Brown Foundation', '100200', 'Brown Foundation', YEAR(GETDATE()), 'STMT_N', 'Statement review', @LastSunday,
        45, 45, 45, 45, 45, 0, 0, 225, N'ttd.prep2', N'ttd.prep2', DATEADD(day, -4, GETUTCDATE()), N'ttd.prep2', DATEADD(day, -4, GETUTCDATE()), 0),
    ('100300 - Green Holdings LLC', '100300', 'Green Holdings LLC', YEAR(GETDATE()) - 1, 'OTHER', 'Amended return research', DATEADD(day, -7, @LastSunday),
        0, 200, 0, 200, 0, 60, 0, 460, N'ttd.owner', N'ttd.owner', DATEADD(day, -9, GETUTCDATE()), N'ttd.owner', DATEADD(day, -8, GETUTCDATE()), 0),
    ('100400 - White Estate', '100400', 'White Estate', YEAR(GETDATE()), 'TAX_N', 'Estate tax schedule', DATEADD(day, -7, @LastSunday),
        30, 30, 30, 30, 30, 30, 30, 210, N'$(CurrentLanId)', N'$(CurrentLanId)', DATEADD(day, -10, GETUTCDATE()), N'$(CurrentLanId)', DATEADD(day, -1, GETUTCDATE()), 0),
    ('100500 - Black River Partners', '100500', 'Black River Partners', YEAR(GETDATE()) - 2, 'STMT_N', 'Old record (week not in the list)', '2024-03-03',
        60, 0, 0, 0, 0, 0, 0, 60, N'ttd.prep', N'ttd.owner', DATEADD(day, -400, GETUTCDATE()), N'ttd.owner', DATEADD(day, -390, GETUTCDATE()), 0);

    INSERT INTO TTDV2.AuditLog (ReferenceID, ReferenceType, CreatedBy, CreatedByLanId, CreatedDate, LogMessage)
    SELECT g.ID, 'TTDV2', u.DisplayName, g.CreatedBy, g.CreatedDate, N'[{"Name":"_Event","Value":"Create"}]'
    FROM TTDV2.General AS g
    LEFT JOIN dbo.CP_Atlas_User AS u ON u.LanID = g.CreatedBy;
END
GO

SELECT u.LanID, u.DisplayName, STRING_AGG(r.RoleName, ', ') AS Roles
FROM dbo.CP_Atlas_User AS u
LEFT JOIN dbo.vw_CP_Atlas_ActiveUserRole AS r ON r.UserId = u.ID AND r.ApplicationCode = 'TTDV2'
GROUP BY u.LanID, u.DisplayName;
SELECT COUNT(*) AS TTDV2Rows FROM TTDV2.General;
GO
