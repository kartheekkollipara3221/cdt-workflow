-- CP Atlas: record visibility rule for one app (spec 06, section 4.6, finding SE-07).
-- TEMPLATE. Do not run as it is. Replace each <PLACEHOLDER> and write the rule first.
--
--   <APP_SCHEMA>   Schema of the app, for example SQMS.
--   <APP_TABLE>    Main table of the app, for example Bid.
--
-- One rule, two users of it:
--   1. <APP_SCHEMA>.fn_VisibleRecords(@LanID) returns the IDs that the user can see.
--   2. <APP_SCHEMA>.Can_Access_Record uses the function for one record. The platform calls it
--      in each access check (view, edit, delete, execute, history, download).
--   3. The dashboard procedures of the app JOIN the same function, so the list and the record
--      check always agree.
--
-- The roles (Read / Prepare / Owner ...) are checked by the platform BEFORE this rule.
-- This rule adds the user-data condition only (department, branch, relationship manager ...).
--
-- The app JSON names the procedure:  "Visibility": { "Procedure": "<APP_SCHEMA>.Can_Access_Record" }
-- Additive. CREATE OR ALTER: safe to run again. In QC and production the DBA runs it.
-- The API login needs EXECUTE on the procedure and SELECT on the function.
SET NOCOUNT ON;
GO

CREATE OR ALTER FUNCTION <APP_SCHEMA>.fn_VisibleRecords (@LanID varchar(50))
RETURNS TABLE
AS
RETURN
(
    -- Write the rule here. Example: the user sees the records of the branches of the user.
    -- The example uses illustrative names; change them to the real tables and columns.
    SELECT r.ID
    FROM <APP_SCHEMA>.<APP_TABLE> AS r
    WHERE ISNULL(r.IsDeleted, 0) = 0
      AND EXISTS (
          SELECT 1
          FROM dbo.CP_Atlas_User AS u
          WHERE u.LanID = @LanID
            AND ISNULL(u.IsDeleted, 0) = 0
            -- AND r.BranchNumber IN (SELECT b.BranchNumber FROM dbo.UserBranch AS b WHERE b.LanID = @LanID)
      )
);
GO

CREATE OR ALTER PROCEDURE <APP_SCHEMA>.Can_Access_Record
    @LanID     varchar(50),
    @ID        int,
    @Operation varchar(30),          -- View, Edit, Delete, Execute:<action>, History, Download
    @Allowed   bit OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- A new record (ID = 0) has no row yet. The roles decide (Permissions.Create).
    IF @ID = 0
    BEGIN
        SET @Allowed = 1;
        RETURN;
    END;

    SET @Allowed = CASE WHEN EXISTS (SELECT 1 FROM <APP_SCHEMA>.fn_VisibleRecords(@LanID) AS v WHERE v.ID = @ID)
                        THEN 1 ELSE 0 END;

    -- Optional: a stricter rule for some operations. Example: only view for other branches.
    -- IF @Allowed = 1 AND @Operation <> 'View' AND <condition> SET @Allowed = 0;
END;
GO

-- Dashboard procedure pattern (for each list of the app):
--   SELECT ... FROM <APP_SCHEMA>.<APP_TABLE> AS r
--   INNER JOIN <APP_SCHEMA>.fn_VisibleRecords(@LanID) AS v ON v.ID = r.ID
--   WHERE ...;
