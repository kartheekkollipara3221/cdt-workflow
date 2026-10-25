-- CP Atlas: fill the LAN ID columns from the display names (spec 06, section 4.5, finding SE-04).
-- DML. Run after 06-lanid-columns-ddl.sql. In QC and production the DBA runs it.
-- Safe to run again: it fills only empty LAN ID columns. It does not change the display name columns.
--
-- Rules:
--   * A display name gives a LAN ID only if exactly one active row in dbo.CP_Atlas_User has
--     that display name. Duplicate or unknown names stay NULL (the application then uses the name).
--   * 'system' (written by the workflow engine SQMS scheduled job) stays 'system'.
--   * A multi-person field ('#$#' separated) is filled only if every name in it resolves.
--     Otherwise it stays NULL.
--   * Parts 2 and 3 are read only. They show the result and the names that did not resolve.
--
-- Needs SQL Server 2017 or later (STRING_AGG, STRING_SPLIT).
SET NOCOUNT ON;
SET XACT_ABORT ON;

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

-- Part 1: fill.
BEGIN TRY
    BEGIN TRANSACTION;

    -- Single-person columns.
    UPDATE t SET t.CreatedByLanId = m.LanId
    FROM SQMS.Opportunity AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.CreatedBy))
    WHERE t.CreatedByLanId IS NULL;
    UPDATE t SET t.ModifiedByLanId = m.LanId
    FROM SQMS.Opportunity AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.ModifiedBy))
    WHERE t.ModifiedByLanId IS NULL;

    UPDATE t SET t.CreatedByLanId = m.LanId
    FROM SQMS.Bid AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.CreatedBy))
    WHERE t.CreatedByLanId IS NULL;
    UPDATE t SET t.ModifiedByLanId = m.LanId
    FROM SQMS.Bid AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.ModifiedBy))
    WHERE t.ModifiedByLanId IS NULL;

    UPDATE t SET t.CreatedByLanId = m.LanId
    FROM TTD.General AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.CreatedBy))
    WHERE t.CreatedByLanId IS NULL;
    UPDATE t SET t.ModifiedByLanId = m.LanId
    FROM TTD.General AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.ModifiedBy))
    WHERE t.ModifiedByLanId IS NULL;
    UPDATE t SET t.TaxPreparerLanId = m.LanId
    FROM TTD.General AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.TaxPreparerName))
    WHERE t.TaxPreparerLanId IS NULL;

    UPDATE t SET t.CreatedByLanId = m.LanId
    FROM AIN.AccountInventory AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.CreatedBy))
    WHERE t.CreatedByLanId IS NULL;
    UPDATE t SET t.ModifiedByLanId = m.LanId
    FROM AIN.AccountInventory AS t INNER JOIN #NameMap AS m ON m.DisplayName = LTRIM(RTRIM(t.ModifiedBy))
    WHERE t.ModifiedByLanId IS NULL;

    -- Multi-person columns ('#$#' separated). All names must resolve.
    ;WITH Parts AS (
        SELECT o.ID, LTRIM(RTRIM(p.value)) AS DisplayName
        FROM SQMS.Opportunity AS o
        CROSS APPLY STRING_SPLIT(REPLACE(o.SellerRMteam, '#$#', CHAR(30)), CHAR(30)) AS p
        WHERE o.SellerRMTeamLanIds IS NULL AND NULLIF(LTRIM(RTRIM(o.SellerRMteam)), '') IS NOT NULL
          AND LTRIM(RTRIM(p.value)) <> ''
    ), Resolved AS (
        SELECT p.ID, STRING_AGG(m.LanId, '#$#') AS LanIds, COUNT(*) AS PartCount, COUNT(m.LanId) AS FoundCount
        FROM Parts AS p LEFT JOIN #NameMap AS m ON m.DisplayName = p.DisplayName
        GROUP BY p.ID
    )
    UPDATE o SET o.SellerRMTeamLanIds = r.LanIds
    FROM SQMS.Opportunity AS o INNER JOIN Resolved AS r ON r.ID = o.ID
    WHERE r.PartCount = r.FoundCount;

    ;WITH Parts AS (
        SELECT b.ID, LTRIM(RTRIM(p.value)) AS DisplayName
        FROM SQMS.Bid AS b
        CROSS APPLY STRING_SPLIT(REPLACE(b.BuyerRMteam, '#$#', CHAR(30)), CHAR(30)) AS p
        WHERE b.BuyerRMTeamLanIds IS NULL AND NULLIF(LTRIM(RTRIM(b.BuyerRMteam)), '') IS NOT NULL
          AND LTRIM(RTRIM(p.value)) <> ''
    ), Resolved AS (
        SELECT p.ID, STRING_AGG(m.LanId, '#$#') AS LanIds, COUNT(*) AS PartCount, COUNT(m.LanId) AS FoundCount
        FROM Parts AS p LEFT JOIN #NameMap AS m ON m.DisplayName = p.DisplayName
        GROUP BY p.ID
    )
    UPDATE b SET b.BuyerRMTeamLanIds = r.LanIds
    FROM SQMS.Bid AS b INNER JOIN Resolved AS r ON r.ID = b.ID
    WHERE r.PartCount = r.FoundCount;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Part 2 (read only): result for each column.
SELECT 'SQMS.Opportunity' AS TableName, 'CreatedBy' AS ColumnName,
       COUNT(*) AS RowsWithName, COUNT(CreatedByLanId) AS RowsWithLanId
FROM SQMS.Opportunity WHERE NULLIF(CreatedBy, '') IS NOT NULL
UNION ALL SELECT 'SQMS.Opportunity', 'ModifiedBy', COUNT(*), COUNT(ModifiedByLanId) FROM SQMS.Opportunity WHERE NULLIF(ModifiedBy, '') IS NOT NULL
UNION ALL SELECT 'SQMS.Opportunity', 'SellerRMteam', COUNT(*), COUNT(SellerRMTeamLanIds) FROM SQMS.Opportunity WHERE NULLIF(SellerRMteam, '') IS NOT NULL
UNION ALL SELECT 'SQMS.Bid', 'CreatedBy', COUNT(*), COUNT(CreatedByLanId) FROM SQMS.Bid WHERE NULLIF(CreatedBy, '') IS NOT NULL
UNION ALL SELECT 'SQMS.Bid', 'ModifiedBy', COUNT(*), COUNT(ModifiedByLanId) FROM SQMS.Bid WHERE NULLIF(ModifiedBy, '') IS NOT NULL
UNION ALL SELECT 'SQMS.Bid', 'BuyerRMteam', COUNT(*), COUNT(BuyerRMTeamLanIds) FROM SQMS.Bid WHERE NULLIF(BuyerRMteam, '') IS NOT NULL
UNION ALL SELECT 'TTD.General', 'CreatedBy', COUNT(*), COUNT(CreatedByLanId) FROM TTD.General WHERE NULLIF(CreatedBy, '') IS NOT NULL
UNION ALL SELECT 'TTD.General', 'ModifiedBy', COUNT(*), COUNT(ModifiedByLanId) FROM TTD.General WHERE NULLIF(ModifiedBy, '') IS NOT NULL
UNION ALL SELECT 'TTD.General', 'TaxPreparerName', COUNT(*), COUNT(TaxPreparerLanId) FROM TTD.General WHERE NULLIF(TaxPreparerName, '') IS NOT NULL
UNION ALL SELECT 'AIN.AccountInventory', 'CreatedBy', COUNT(*), COUNT(CreatedByLanId) FROM AIN.AccountInventory WHERE NULLIF(CreatedBy, '') IS NOT NULL
UNION ALL SELECT 'AIN.AccountInventory', 'ModifiedBy', COUNT(*), COUNT(ModifiedByLanId) FROM AIN.AccountInventory WHERE NULLIF(ModifiedBy, '') IS NOT NULL;

-- Part 3 (read only): display names that do not resolve, and why.
;WITH Names AS (
    SELECT LTRIM(RTRIM(CreatedBy)) AS DisplayName FROM SQMS.Opportunity WHERE CreatedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(ModifiedBy)) FROM SQMS.Opportunity WHERE ModifiedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(CreatedBy)) FROM SQMS.Bid WHERE CreatedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(ModifiedBy)) FROM SQMS.Bid WHERE ModifiedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(CreatedBy)) FROM TTD.General WHERE CreatedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(ModifiedBy)) FROM TTD.General WHERE ModifiedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(TaxPreparerName)) FROM TTD.General WHERE TaxPreparerLanId IS NULL
    UNION SELECT LTRIM(RTRIM(CreatedBy)) FROM AIN.AccountInventory WHERE CreatedByLanId IS NULL
    UNION SELECT LTRIM(RTRIM(ModifiedBy)) FROM AIN.AccountInventory WHERE ModifiedByLanId IS NULL
)
SELECT n.DisplayName,
       CASE WHEN COUNT(DISTINCT u.LanID) > 1 THEN 'Duplicate display name'
            ELSE 'Not in CP_Atlas_User (or deleted)' END AS Reason,
       STRING_AGG(u.LanID, ', ') AS CandidateLanIds
FROM Names AS n
LEFT JOIN dbo.CP_Atlas_User AS u
       ON LTRIM(RTRIM(u.DisplayName)) = n.DisplayName AND ISNULL(u.IsDeleted, 0) = 0
WHERE NULLIF(n.DisplayName, '') IS NOT NULL
GROUP BY n.DisplayName
ORDER BY n.DisplayName;

DROP TABLE #NameMap;
