-- LOCAL ONLY: do not run at the target. The target database already has the TTD schema.
-- Makes TTD.General and TTD.AuditLog in the target shape (CP_Sharepoint.txt, 2026-10-06), the TTD app row and roles,
-- and sample rows in the TTD text format (display names, local dates). Used by the copy-script test (spec 12, 11.2 C10).
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF SCHEMA_ID(N'TTD') IS NULL EXEC(N'CREATE SCHEMA [TTD]');

IF OBJECT_ID(N'TTD.AuditLog', N'U') IS NULL
CREATE TABLE [TTD].[AuditLog](
    [ID] [int] IDENTITY(1,1) NOT NULL,
    [RefID] [int] NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [LogMessage] [nvarchar](max) NULL,
    CONSTRAINT [PK_TTD_AuditLog] PRIMARY KEY CLUSTERED ([ID] ASC));

IF OBJECT_ID(N'TTD.General', N'U') IS NULL
CREATE TABLE [TTD].[General](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [Account] [varchar](1000) NULL,
    [AccountNumber] [varchar](100) NULL,
    [AccountName] [varchar](1000) NULL,
    [TaxYear] [varchar](200) NULL,
    [Category] [varchar](200) NULL,
    [WorkDescription] [varchar](max) NULL,
    [WeekEnding] [varchar](100) NULL,
    [MondayMinute] [varchar](100) NULL,
    [TuesdayMinute] [varchar](100) NULL,
    [WednesdayMinute] [varchar](100) NULL,
    [ThursdayMinute] [varchar](100) NULL,
    [FridayMinute] [varchar](100) NULL,
    [SaturdayMinute] [varchar](100) NULL,
    [SundayMinute] [varchar](100) NULL,
    [WeeklyTotal] [varchar](100) NULL,
    [TaxPreparerName] [varchar](1000) NULL,
    [CreatedBy] [varchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [varchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_TTD_General] PRIMARY KEY CLUSTERED ([ID] ASC));
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Application WHERE Code = 'TTD')
    INSERT INTO dbo.CP_Atlas_Application (Name, Code, IsDeleted) VALUES ('Tax Time Database', 'TTD', 0);
DECLARE @App int = (SELECT ID FROM dbo.CP_Atlas_Application WHERE Code = 'TTD');
INSERT INTO dbo.CP_Atlas_Entitlement (CP_Atlas_ApplicationID, Name, IsAdmin, IsWrite, IsRead, IsDeleted)
SELECT @App, r.Name, 0, 0, 0, 0 FROM (VALUES ('Read'), ('Prepare'), ('Owner')) AS r(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.CP_Atlas_Entitlement WHERE CP_Atlas_ApplicationID = @App AND Name = r.Name);

IF NOT EXISTS (SELECT 1 FROM TTD.General)
BEGIN
    SET IDENTITY_INSERT TTD.General ON;
    INSERT INTO TTD.General (ID, Account, AccountNumber, AccountName, TaxYear, Category, WorkDescription, WeekEnding,
        MondayMinute, TuesdayMinute, WednesdayMinute, ThursdayMinute, FridayMinute, SaturdayMinute, SundayMinute, WeeklyTotal,
        TaxPreparerName, CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, IsDeleted)
    VALUES
    (10001, '100100 - Smith Family Trust', '100100', 'Smith Family Trust', '2025', 'TAX_N', 'Form 1041', '2025-03-02',
        '120', '90', '', '0', ' 30 ', NULL, NULL, '240', 'Pat Preparer', 'Pat Preparer', '2025-03-03 09:15:00', NULL, NULL, 0),
    (10002, '100200 - Brown Foundation', '100200', 'Brown Foundation', '2026', 'STMT_N', NULL, '2026-01-04',
        '60', '60', '60', '60', '60', '0', '0', '999', 'Olive Owner', 'Olive Owner', '2026-01-05 18:30:00', 'Pat Preparer', '2026-01-06 08:00:00', 0),
    (10003, '100300 - Green Holdings LLC', '100300', 'Green Holdings LLC', '2024', 'OTHER', 'Deleted sheet', '2024-12-29',
        '15', NULL, NULL, NULL, NULL, NULL, NULL, '15', 'Pat Preparer', 'system', '2024-12-30 12:00:00', NULL, NULL, 1);
    SET IDENTITY_INSERT TTD.General OFF;

    INSERT INTO TTD.AuditLog (RefID, CreatedBy, CreatedDate, LogMessage) VALUES
    (10001, 'Pat Preparer', '2025-03-03 09:15:00', N'[{"Name":"Category","Value":"TAX_N"}]'),
    (10002, 'Olive Owner', '2026-01-05 18:30:00', N'[{"Name":"Category","Value":"STMT_N"}]'),
    (10002, 'Pat Preparer', '2026-01-06 08:00:00', N'[{"Name":"WeeklyTotal","Value":"999"}]');
END
GO
