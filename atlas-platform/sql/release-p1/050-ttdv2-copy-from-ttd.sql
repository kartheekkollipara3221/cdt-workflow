-- CP Atlas: copy TTD data to TTDV2 (spec 12, section 4; release P1, script 050).
-- Run at the change-over only (spec 12, section 10, question 4). Run after 12-ttdv2-app.sql.
-- DML. Development: the target team runs it. QC and production: the DBA runs it.
--
-- Rules:
--   * TTD data tables are read only. The script does not change TTD.General or TTD.AuditLog.
--   * Part 2d revokes all TTD roles (TTD stops at once, spec 12, question 4).
--   * Record IDs are kept (IDENTITY_INSERT), so links and audit ReferenceIDs stay the same.
--   * Text values are converted to the typed columns. Minutes: whole numbers 0-1440, empty = NULL.
--     WeekEnding: 'yyyy-MM-dd'. TaxYear: a whole number.
--   * People: a display name gives a LAN ID only if exactly one active dbo.CP_Atlas_User row has it.
--     'system' stays 'system'.
--   * WeeklyTotal is calculated again from the seven day columns (spec 01, 10.2, Computed).
--   * Local dates are converted to UTC with @ServerTimeZone (spec 09, 4.5).
--   * Part 1 is read only. If it finds a value that it cannot convert, part 2 stops.
--     Correct the source data or CP_Atlas_User, then run again.
--   * Safe to run again: it copies only rows and audit entries that are not in TTDV2 yet.
--   * Part 2 revokes the TTD roles first in its transaction, then checks that TTD did not change after the
--     snapshot of part 1 (a user saved during the run). If it changed, nothing is copied: run the script again.
--   * The script log row is written in the copy transaction, so it is there only when the copy is committed.
--
-- Needs SQL Server 2016 or later (AT TIME ZONE).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ServerTimeZone sysname = N'<SERVER_TIME_ZONE>';   -- For example N'Eastern Standard Time' (sys.time_zone_info).
DECLARE @RunBy nvarchar(50) = N'<RUN_BY>';                   -- LAN ID of the person who runs the script.
IF @ServerTimeZone LIKE N'%<%' OR @RunBy LIKE N'%<%'
    THROW 55001, 'Set @ServerTimeZone and @RunBy before you run the script.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.time_zone_info WHERE name = @ServerTimeZone)
    THROW 55004, 'The time zone name is not in sys.time_zone_info.', 1;
IF OBJECT_ID(N'TTDV2.General', N'U') IS NULL
    THROW 55005, 'Run 12-ttdv2-app.sql first.', 1;

-- Name map: unique active display names only.
IF OBJECT_ID('tempdb..#NameMap') IS NOT NULL DROP TABLE #NameMap;
SELECT LTRIM(RTRIM(u.DisplayName)) AS DisplayName, MIN(u.LanID) AS LanId
INTO #NameMap
FROM dbo.CP_Atlas_User AS u
WHERE ISNULL(u.IsDeleted, 0) = 0 AND u.DisplayName IS NOT NULL AND u.LanID IS NOT NULL
GROUP BY LTRIM(RTRIM(u.DisplayName))
HAVING COUNT(DISTINCT u.LanID) = 1;
IF NOT EXISTS (SELECT 1 FROM #NameMap WHERE DisplayName = 'system')
    INSERT INTO #NameMap (DisplayName, LanId) VALUES ('system', 'system');

-- Snapshot marks, taken before the converted rows (a change between the two gives a false stop, never a lost row).
DECLARE @SrcRows int, @SrcModified datetime2, @SrcCreated datetime2;
SELECT @SrcRows = COUNT(*), @SrcModified = MAX(CAST(ModifiedDate AS datetime2)), @SrcCreated = MAX(CAST(CreatedDate AS datetime2))
FROM TTD.General;

-- Converted rows.
IF OBJECT_ID('tempdb..#Src') IS NOT NULL DROP TABLE #Src;
SELECT
    t.ID, t.Account, t.AccountNumber, t.AccountName,
    t.TaxYear AS TaxYearText,   TRY_CONVERT(smallint, NULLIF(LTRIM(RTRIM(t.TaxYear)), '')) AS TaxYear,
    t.Category, t.WorkDescription,
    t.WeekEnding AS WeekEndingText, TRY_CONVERT(date, NULLIF(LTRIM(RTRIM(t.WeekEnding)), ''), 23) AS WeekEnding,
    t.MondayMinute    AS MonText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.MondayMinute)), ''))    AS MondayMinute,
    t.TuesdayMinute   AS TueText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.TuesdayMinute)), ''))   AS TuesdayMinute,
    t.WednesdayMinute AS WedText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.WednesdayMinute)), '')) AS WednesdayMinute,
    t.ThursdayMinute  AS ThuText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.ThursdayMinute)), ''))  AS ThursdayMinute,
    t.FridayMinute    AS FriText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.FridayMinute)), ''))    AS FridayMinute,
    t.SaturdayMinute  AS SatText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.SaturdayMinute)), ''))  AS SaturdayMinute,
    t.SundayMinute    AS SunText, TRY_CONVERT(int, NULLIF(LTRIM(RTRIM(t.SundayMinute)), ''))    AS SundayMinute,
    t.WeeklyTotal AS WeeklyTotalText,
    t.TaxPreparerName, mp.LanId AS TaxPreparerLanId,
    t.CreatedBy  AS CreatedByName,  mc.LanId AS CreatedBy,
    t.ModifiedBy AS ModifiedByName, mm.LanId AS ModifiedBy,
    -- Local time to UTC: a datetime2 value AT TIME ZONE gets the offset of that zone, then converts to UTC.
    CAST((CAST(t.CreatedDate  AS datetime2) AT TIME ZONE @ServerTimeZone) AT TIME ZONE 'UTC' AS datetime) AS CreatedDate,
    CAST((CAST(t.ModifiedDate AS datetime2) AT TIME ZONE @ServerTimeZone) AT TIME ZONE 'UTC' AS datetime) AS ModifiedDate,
    t.IsDeleted
INTO #Src
FROM TTD.General AS t
LEFT JOIN #NameMap AS mp ON mp.DisplayName = LTRIM(RTRIM(t.TaxPreparerName))
LEFT JOIN #NameMap AS mc ON mc.DisplayName = LTRIM(RTRIM(t.CreatedBy))
LEFT JOIN #NameMap AS mm ON mm.DisplayName = LTRIM(RTRIM(t.ModifiedBy));

-- Part 1 (read only): values that cannot be converted.
IF OBJECT_ID('tempdb..#Problem') IS NOT NULL DROP TABLE #Problem;
CREATE TABLE #Problem (ID int NOT NULL, ColumnName sysname NOT NULL, Value nvarchar(1000) NULL, Problem nvarchar(200) NOT NULL);

INSERT INTO #Problem (ID, ColumnName, Value, Problem)
SELECT ID, v.ColumnName, v.Value, N'Not a whole number from 0 to 1440'
FROM #Src
CROSS APPLY (VALUES
    (N'MondayMinute', MonText, MondayMinute), (N'TuesdayMinute', TueText, TuesdayMinute),
    (N'WednesdayMinute', WedText, WednesdayMinute), (N'ThursdayMinute', ThuText, ThursdayMinute),
    (N'FridayMinute', FriText, FridayMinute), (N'SaturdayMinute', SatText, SaturdayMinute),
    (N'SundayMinute', SunText, SundayMinute)) AS v(ColumnName, Value, Converted)
WHERE NULLIF(LTRIM(RTRIM(v.Value)), '') IS NOT NULL AND (v.Converted IS NULL OR v.Converted NOT BETWEEN 0 AND 1440);

INSERT INTO #Problem (ID, ColumnName, Value, Problem)
SELECT ID, N'TaxYear', TaxYearText, N'Not a whole number'
FROM #Src WHERE NULLIF(LTRIM(RTRIM(TaxYearText)), '') IS NOT NULL AND TaxYear IS NULL;

INSERT INTO #Problem (ID, ColumnName, Value, Problem)
SELECT ID, N'WeekEnding', WeekEndingText, N'Not a date in the format yyyy-MM-dd'
FROM #Src WHERE NULLIF(LTRIM(RTRIM(WeekEndingText)), '') IS NOT NULL AND WeekEnding IS NULL;

INSERT INTO #Problem (ID, ColumnName, Value, Problem)
SELECT ID, v.ColumnName, v.Value, N'Display name not found or not unique in CP_Atlas_User'
FROM #Src
CROSS APPLY (VALUES
    (N'TaxPreparerName', TaxPreparerName, TaxPreparerLanId),
    (N'CreatedBy', CreatedByName, CreatedBy),
    (N'ModifiedBy', ModifiedByName, ModifiedBy)) AS v(ColumnName, Value, LanId)
WHERE NULLIF(LTRIM(RTRIM(v.Value)), '') IS NOT NULL AND v.LanId IS NULL;

-- Review 3 RV-09: part 2 revokes all TTD roles and gives the TTDV2 role with the same name.
-- A TTD role that a user has and that has no TTDV2 role of the same name would leave the user with no role.
INSERT INTO #Problem (ID, ColumnName, Value, Problem)
SELECT MIN(ue.CP_Atlas_UserID), N'Role', e1.Name, N'TTD role has no TTDV2 role with the same name (add it in 12-ttdv2-app.sql or move the users first)'
FROM dbo.CP_Atlas_UserEntitlement AS ue
INNER JOIN dbo.CP_Atlas_Entitlement AS e1 ON e1.ID = ue.CP_Atlas_EntitlementID AND ISNULL(e1.IsDeleted, 0) = 0
INNER JOIN dbo.CP_Atlas_Application AS a1 ON a1.ID = e1.CP_Atlas_ApplicationID AND a1.Code = 'TTD'
INNER JOIN dbo.CP_Atlas_User AS u ON u.ID = ue.CP_Atlas_UserID AND ISNULL(u.IsDeleted, 0) = 0
WHERE ISNULL(ue.IsDeleted, 0) = 0
  AND NOT EXISTS (SELECT 1
                  FROM dbo.CP_Atlas_Entitlement AS e2
                  INNER JOIN dbo.CP_Atlas_Application AS a2 ON a2.ID = e2.CP_Atlas_ApplicationID AND a2.Code = 'TTDV2'
                                                           AND ISNULL(a2.IsDeleted, 0) = 0
                  WHERE e2.Name = e1.Name AND ISNULL(e2.IsDeleted, 0) = 0)
GROUP BY e1.Name;

SELECT p.ColumnName, p.Problem, p.Value, COUNT(*) AS Records, MIN(p.ID) AS FirstID
FROM #Problem AS p
GROUP BY p.ColumnName, p.Problem, p.Value
ORDER BY p.ColumnName, Records DESC;

-- Information only: records where the saved WeeklyTotal is not the sum of the days.
SELECT ID, WeeklyTotalText AS OldWeeklyTotal,
       ISNULL(MondayMinute,0) + ISNULL(TuesdayMinute,0) + ISNULL(WednesdayMinute,0) + ISNULL(ThursdayMinute,0)
     + ISNULL(FridayMinute,0) + ISNULL(SaturdayMinute,0) + ISNULL(SundayMinute,0) AS NewWeeklyTotal
FROM #Src
WHERE ISNULL(TRY_CONVERT(int, WeeklyTotalText), -1) <>
      ISNULL(MondayMinute,0) + ISNULL(TuesdayMinute,0) + ISNULL(WednesdayMinute,0) + ISNULL(ThursdayMinute,0)
    + ISNULL(FridayMinute,0) + ISNULL(SaturdayMinute,0) + ISNULL(SundayMinute,0);

IF EXISTS (SELECT 1 FROM #Problem)
    THROW 55006, 'Some TTD values or roles cannot be converted. See the first result set. Nothing was copied and no role changed.', 1;

-- Part 2: copy.
BEGIN TRY
    BEGIN TRANSACTION;

    -- 2a. Roles: each active TTD role of a user gives the TTDV2 role with the same name.
    INSERT INTO dbo.CP_Atlas_UserEntitlement (CP_Atlas_UserID, CP_Atlas_EntitlementID, CreatedBy, CreatedDate, IsDeleted)
    SELECT DISTINCT ue.CP_Atlas_UserID, e2.ID, @RunBy, GETUTCDATE(), 0
    FROM dbo.CP_Atlas_UserEntitlement AS ue
    INNER JOIN dbo.CP_Atlas_Entitlement AS e1 ON e1.ID = ue.CP_Atlas_EntitlementID AND ISNULL(e1.IsDeleted, 0) = 0
    INNER JOIN dbo.CP_Atlas_Application AS a1 ON a1.ID = e1.CP_Atlas_ApplicationID AND a1.Code = 'TTD'
    INNER JOIN dbo.CP_Atlas_Application AS a2 ON a2.Code = 'TTDV2' AND ISNULL(a2.IsDeleted, 0) = 0
    INNER JOIN dbo.CP_Atlas_Entitlement AS e2 ON e2.CP_Atlas_ApplicationID = a2.ID AND e2.Name = e1.Name AND ISNULL(e2.IsDeleted, 0) = 0
    INNER JOIN dbo.CP_Atlas_User AS u ON u.ID = ue.CP_Atlas_UserID AND ISNULL(u.IsDeleted, 0) = 0
    WHERE ISNULL(ue.IsDeleted, 0) = 0
      AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_UserEntitlement AS x
                      WHERE x.CP_Atlas_UserID = ue.CP_Atlas_UserID AND x.CP_Atlas_EntitlementID = e2.ID
                        AND ISNULL(x.IsDeleted, 0) = 0);

    -- 2b. TTD stops at once (spec 12, question 4): revoke all active TTD roles (soft delete).
    --     Rollback: set IsDeleted = 0 on the rows with ModifiedBy = @RunBy and this ModifiedDate.
    UPDATE ue SET ue.IsDeleted = 1, ue.ModifiedBy = @RunBy, ue.ModifiedDate = GETUTCDATE()
    FROM dbo.CP_Atlas_UserEntitlement AS ue
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = 'TTD'
    WHERE ISNULL(ue.IsDeleted, 0) = 0;

    -- 2c. TTD must not have changed after the snapshot (review DA-07). The locks keep it so until the commit.
    IF EXISTS (SELECT 1
               FROM (SELECT COUNT(*) AS Rows, MAX(CAST(ModifiedDate AS datetime2)) AS Modified, MAX(CAST(CreatedDate AS datetime2)) AS Created
                     FROM TTD.General WITH (UPDLOCK, HOLDLOCK)) AS now
               WHERE now.Rows <> @SrcRows
                  OR ISNULL(now.Modified, '19000101') <> ISNULL(@SrcModified, '19000101')
                  OR ISNULL(now.Created, '19000101') <> ISNULL(@SrcCreated, '19000101'))
        THROW 55007, 'TTD data changed during the run (a user saved). Nothing was copied and no role changed. Run the script again.', 1;

    -- 2d. Records (IDs kept).
    SET IDENTITY_INSERT TTDV2.General ON;
    INSERT INTO TTDV2.General (
        ID, Account, AccountNumber, AccountName, TaxYear, Category, WorkDescription, WeekEnding,
        MondayMinute, TuesdayMinute, WednesdayMinute, ThursdayMinute, FridayMinute, SaturdayMinute, SundayMinute,
        WeeklyTotal, TaxPreparerLanId, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted)
    SELECT
        s.ID, s.Account, s.AccountNumber, s.AccountName, s.TaxYear, s.Category, s.WorkDescription, s.WeekEnding,
        s.MondayMinute, s.TuesdayMinute, s.WednesdayMinute, s.ThursdayMinute, s.FridayMinute, s.SaturdayMinute, s.SundayMinute,
        ISNULL(s.MondayMinute,0) + ISNULL(s.TuesdayMinute,0) + ISNULL(s.WednesdayMinute,0) + ISNULL(s.ThursdayMinute,0)
      + ISNULL(s.FridayMinute,0) + ISNULL(s.SaturdayMinute,0) + ISNULL(s.SundayMinute,0),
        s.TaxPreparerLanId, s.CreatedBy, s.CreatedDate, s.ModifiedBy, s.ModifiedDate, ISNULL(s.IsDeleted, 0)
    FROM #Src AS s
    WHERE NOT EXISTS (SELECT 1 FROM TTDV2.General AS g WHERE g.ID = s.ID);
    SET IDENTITY_INSERT TTDV2.General OFF;

    -- 2e. Audit entries. ReferenceType = 'TTD' marks a copied entry. The old LogMessage format
    --     (Name/Value) is kept; the History window reads it (spec 09, 4.2).
    --     TTD.AuditLog and TTDV2.AuditLog name the record column ReferenceID (the target renamed RefID, 2026-11).
    INSERT INTO TTDV2.AuditLog (ReferenceID, ReferenceType, CreatedBy, CreatedByLanId, CreatedDate, LogMessage)
    SELECT a.ReferenceID, 'TTD', a.CreatedBy, m.LanId,
           CAST((CAST(a.CreatedDate AS datetime2) AT TIME ZONE @ServerTimeZone) AT TIME ZONE 'UTC' AS datetime),
           a.LogMessage
    FROM TTD.AuditLog AS a
    LEFT JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(a.CreatedBy))
    WHERE a.ReferenceID IN (SELECT ID FROM TTDV2.General)
      AND NOT EXISTS (SELECT 1 FROM TTDV2.AuditLog AS x WHERE x.ReferenceID = a.ReferenceID AND x.ReferenceType = 'TTD');

    -- Log this script in the same transaction: the log says "applied" only when the copy is committed.
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'12-ttdv2-copy-from-ttd.sql')
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'12-ttdv2-copy-from-ttd.sql', N'P1');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Part 3 (read only): result.
SELECT 'TTD.General' AS TableName, COUNT(*) AS Rows FROM TTD.General
UNION ALL SELECT 'TTDV2.General', COUNT(*) FROM TTDV2.General
UNION ALL SELECT 'TTD.AuditLog', COUNT(*) FROM TTD.AuditLog
UNION ALL SELECT 'TTDV2.AuditLog (copied)', COUNT(*) FROM TTDV2.AuditLog WHERE ReferenceType = 'TTD'
UNION ALL SELECT 'TTD active user roles', COUNT(*) FROM dbo.CP_Atlas_UserEntitlement AS ue
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = 'TTD'
    WHERE ISNULL(ue.IsDeleted, 0) = 0;
GO
