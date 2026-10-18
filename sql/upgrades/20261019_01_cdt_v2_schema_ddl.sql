-- CDT PortfolioTrade v2: additive SLA reminder controls (6 columns).
-- Matches EF migration AddCdtV2SlaReminderControls. Run in the intended database.
-- Use either this script or the EF migration for a database, not both.
-- Does not seed, reset runtime data or edit EF migration history. Safe to run again.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.SlaPolicy', N'U') IS NULL
        THROW 52001, 'Expected dbo.SlaPolicy table was not found.', 1;
    IF OBJECT_ID(N'dbo.WorkflowTaskSlaSchedule', N'U') IS NULL
        THROW 52002, 'Expected dbo.WorkflowTaskSlaSchedule table was not found.', 1;

    IF COL_LENGTH(N'dbo.SlaPolicy', N'DueReminderLocalTime') IS NULL
        ALTER TABLE dbo.SlaPolicy ADD DueReminderLocalTime time NULL;
    IF COL_LENGTH(N'dbo.SlaPolicy', N'SkipMissedDueReminder') IS NULL
        ALTER TABLE dbo.SlaPolicy ADD SkipMissedDueReminder bit NOT NULL
            CONSTRAINT DF_SlaPolicy_SkipMissedDueReminder DEFAULT (CONVERT(bit, 0));
    IF COL_LENGTH(N'dbo.SlaPolicy', N'OverdueStartsNextLocalDay') IS NULL
        ALTER TABLE dbo.SlaPolicy ADD OverdueStartsNextLocalDay bit NOT NULL
            CONSTRAINT DF_SlaPolicy_OverdueStartsNextLocalDay DEFAULT (CONVERT(bit, 0));
    IF COL_LENGTH(N'dbo.WorkflowTaskSlaSchedule', N'DueReminderLocalTime') IS NULL
        ALTER TABLE dbo.WorkflowTaskSlaSchedule ADD DueReminderLocalTime time NULL;
    IF COL_LENGTH(N'dbo.WorkflowTaskSlaSchedule', N'DueRecipientsJson') IS NULL
        ALTER TABLE dbo.WorkflowTaskSlaSchedule ADD DueRecipientsJson nvarchar(max) NULL;
    IF COL_LENGTH(N'dbo.WorkflowTaskSlaSchedule', N'OverdueRecipientsJson') IS NULL
        ALTER TABLE dbo.WorkflowTaskSlaSchedule ADD OverdueRecipientsJson nvarchar(max) NULL;

    -- Shape checks: type, nullability and (for nvarchar) max length.
    IF EXISTS (
        SELECT 1
        FROM (VALUES
            (N'dbo.SlaPolicy', N'DueReminderLocalTime', N'time', 1, NULL),
            (N'dbo.SlaPolicy', N'SkipMissedDueReminder', N'bit', 0, NULL),
            (N'dbo.SlaPolicy', N'OverdueStartsNextLocalDay', N'bit', 0, NULL),
            (N'dbo.WorkflowTaskSlaSchedule', N'DueReminderLocalTime', N'time', 1, NULL),
            (N'dbo.WorkflowTaskSlaSchedule', N'DueRecipientsJson', N'nvarchar', 1, -1),
            (N'dbo.WorkflowTaskSlaSchedule', N'OverdueRecipientsJson', N'nvarchar', 1, -1)
        ) AS expected (TableName, ColumnName, TypeName, IsNullable, MaxLength)
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(expected.TableName)
              AND c.name = expected.ColumnName
              AND c.system_type_id = TYPE_ID(expected.TypeName)
              AND c.is_nullable = expected.IsNullable
              AND (expected.MaxLength IS NULL OR c.max_length = expected.MaxLength)))
        THROW 52003, 'A CDT v2 SLA column does not match the required definition.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
