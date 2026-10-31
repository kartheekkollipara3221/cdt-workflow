-- CP Atlas: roll back the TTDV2 app rows (release P1, script 090; review 3 RV-10).
-- Run only to undo 12-ttdv2-app.sql (040) and, if it ran, 12-ttdv2-copy-from-ttd.sql (050).
-- DML. Development: the target team runs it. QC and production: the DBA runs it.
--
-- What it does, in one transaction:
--   1. Gives back the TTD roles that 050 revoked: rows of TTD roles with IsDeleted = 1 and ModifiedBy = @CopyRunBy,
--      changed in the hour before the 050 log row. Skipped when 050 did not run (no log row).
--   2. Soft-deletes the TTDV2 user roles, the TTDV2 roles and the TTDV2 application row (IsDeleted = 1).
--      dbo.Get_Application and the platform then show no TTDV2 menu row. TTD works as before.
--   3. Writes its log row.
-- What it does not do:
--   * It does not drop the TTDV2 schema or its tables. The copied records stay for analysis. To drop them, see the end.
--   * It does not change TTD.General or TTD.AuditLog (050 did not change them).
-- Also set AppSettings:Modules:TTDV2:Enabled = false (or deploy the code without the platform).
-- To install TTDV2 again: set IsDeleted = 0 on the TTDV2 application row and its 3 roles, then run 040 (and 050).
-- Safe to run again: a second run changes nothing.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @RunBy nvarchar(50) = N'<RUN_BY>';             -- LAN ID of the person who runs this script.
DECLARE @CopyRunBy nvarchar(50) = N'<COPY_RUN_BY>';    -- The @RunBy value that was used for 050. Any text if 050 did not run.
IF @RunBy LIKE N'%<%' OR @CopyRunBy LIKE N'%<%'
    THROW 55020, 'Set @RunBy and @CopyRunBy before you run the script.', 1;

DECLARE @CopyAt datetime2(3) =
    (SELECT MAX(AppliedAtUtc) FROM dbo.CP_Atlas_ScriptLog WHERE Release = N'P1' AND ScriptName = N'12-ttdv2-copy-from-ttd.sql');

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. TTD roles back (only the rows that 050 revoked).
    IF @CopyAt IS NOT NULL
        UPDATE ue SET ue.IsDeleted = 0, ue.ModifiedBy = @RunBy, ue.ModifiedDate = GETUTCDATE()
        FROM dbo.CP_Atlas_UserEntitlement AS ue
        INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID AND ISNULL(e.IsDeleted, 0) = 0
        INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = 'TTD'
        WHERE ue.IsDeleted = 1
          AND ue.ModifiedBy = @CopyRunBy
          AND ue.ModifiedDate BETWEEN DATEADD(HOUR, -1, @CopyAt) AND @CopyAt;

    -- 2. TTDV2 user roles, roles and application row off.
    UPDATE ue SET ue.IsDeleted = 1, ue.ModifiedBy = @RunBy, ue.ModifiedDate = GETUTCDATE()
    FROM dbo.CP_Atlas_UserEntitlement AS ue
    INNER JOIN dbo.CP_Atlas_Entitlement AS e ON e.ID = ue.CP_Atlas_EntitlementID
    INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = 'TTDV2'
    WHERE ISNULL(ue.IsDeleted, 0) = 0;

    UPDATE e SET e.IsDeleted = 1, e.ModifiedBy = @RunBy, e.ModifiedDate = GETUTCDATE()
    FROM dbo.CP_Atlas_Entitlement AS e
    INNER JOIN dbo.CP_Atlas_Application AS a ON a.ID = e.CP_Atlas_ApplicationID AND a.Code = 'TTDV2'
    WHERE ISNULL(e.IsDeleted, 0) = 0;

    UPDATE dbo.CP_Atlas_Application SET IsDeleted = 1, ModifiedBy = @RunBy, ModifiedDate = GETUTCDATE()
    WHERE Code = 'TTDV2' AND ISNULL(IsDeleted, 0) = 0;

    -- 3. Log this script in the same transaction (review DA-07).
    IF OBJECT_ID(N'dbo.CP_Atlas_ScriptLog', N'U') IS NOT NULL
        INSERT INTO dbo.CP_Atlas_ScriptLog (ScriptName, Release) VALUES (N'12-ttdv2-rollback.sql', N'P1');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- Check: no active TTDV2 row, and the TTD roles that are active now.
SELECT COUNT(*) AS ActiveTtdv2Rows FROM dbo.vw_CP_Atlas_ActiveUserRole WHERE ApplicationCode = 'TTDV2';
SELECT COUNT(*) AS ActiveTtdRoles FROM dbo.vw_CP_Atlas_ActiveUserRole WHERE ApplicationCode = 'TTD';

-- Optional, only when the copied records are not needed (this loses the TTDV2 data):
-- DROP TABLE TTDV2.AuditLog; DROP TABLE TTDV2.General; DROP SCHEMA TTDV2;
