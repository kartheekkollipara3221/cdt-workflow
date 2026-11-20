-- LOCAL ONLY: do not run at the target. The target database already has these tables.
-- Makes CFP.Inflow, CFP.InflowAttachment and AIN.AccountInventory in the exact target shape (target schema dump of
-- 2026-11-18), so the release P2 scripts are tested against the real start point. Also adds sample AIN accounts.
-- Safe to run again: each table is made and each sample row is added only when it is not there.
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF SCHEMA_ID(N'CFP') IS NULL EXEC(N'CREATE SCHEMA [CFP]');
IF SCHEMA_ID(N'AIN') IS NULL EXEC(N'CREATE SCHEMA [AIN]');
GO

IF OBJECT_ID(N'CFP.Inflow', N'U') IS NULL
CREATE TABLE [CFP].[Inflow](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [Account] [varchar](max) NULL,
    [AccountNumber] [varchar](100) NULL,
    [AccountName] [varchar](1000) NULL,
    [StarNumber] [varchar](100) NULL,
    [RelationshipManager] [varchar](1000) NULL,
    [Custodian] [varchar](1000) NULL,
    [InvestmentType] [varchar](500) NULL,
    [Amount] [decimal](19, 4) NULL,
    [InflowDate] [datetime] NULL,
    [Description] [varchar](max) NULL,
    [FlowSubmittedBy] [varchar](500) NULL,
    [FlowConfirm] [bit] NULL,
    [Comment] [varchar](max) NULL,
    [CashSubmittedBy] [varchar](500) NULL,
    [CashConfirm] [bit] NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_CFP_Inflow] PRIMARY KEY CLUSTERED ([ID] ASC));
GO

IF OBJECT_ID(N'CFP.InflowAttachment', N'U') IS NULL
CREATE TABLE [CFP].[InflowAttachment](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [InflowId] [int] NULL,
    [Type] [varchar](500) NULL,
    [FileName] [varchar](1000) NULL,
    [FileContent] [varbinary](max) NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_CFP_InflowAttachment] PRIMARY KEY CLUSTERED ([ID] ASC));
GO

IF OBJECT_ID(N'AIN.AccountInventory', N'U') IS NULL
CREATE TABLE [AIN].[AccountInventory](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [Account] [varchar](max) NOT NULL,
    [AccountName] [varchar](1000) NOT NULL,
    [AccountNumber] [varchar](1000) NOT NULL,
    [RelationshipManager] [varchar](1000) NULL,
    [StarAccountNumber] [varchar](100) NULL,
    [ClientFamilyCode] [varchar](100) NULL,
    [ClientFamilyDesc] [varchar](1000) NULL,
    [Custodian] [varchar](200) NULL,
    [CustodianAccountNumber] [varchar](100) NULL,
    [ReportingLead] [varchar](150) NULL,
    [CombinedAccountNumber] [varchar](100) NULL,
    [ERISAIndicator] [varchar](100) NULL,
    [InceptionDate] [varchar](100) NULL,
    [ClientLegalName] [varchar](250) NULL,
    [KYCID] [varchar](100) NULL,
    [FASCloseDate] [varchar](100) NULL,
    [MailingStatus] [varchar](100) NULL,
    [InflowOutflow] [varchar](100) NULL,
    [AccountType] [varchar](100) NULL,
    [PortfolioManager] [varchar](150) NULL,
    [PortfolioManager2] [varchar](150) NULL,
    [PortfolioManager3] [varchar](150) NULL,
    [Strategy] [varchar](200) NULL,
    [ConsultantsDriscretionaryInvMgr] [varchar](1000) NULL,
    [ClientTypeGroup] [varchar](150) NULL,
    [ClientFamilyAlias] [varchar](200) NULL,
    [AccountAlias] [varchar](150) NULL,
    [IsAccountActive] [bit] NULL,
    [IsAdminUser] [bit] NULL,
    [Mandate] [varchar](100) NULL,
    [SecurityName] [varchar](200) NULL,
    [Cusip] [varchar](100) NULL,
    [Ticker] [varchar](100) NULL,
    [CurrentRate] [varchar](100) NULL,
    [MarketDataLicense] [varchar](100) NULL,
    [MarketDataLicenseProvider] [varchar](200) NULL,
    [TaxRateValue] [varchar](100) NULL,
    [DiscretionaryOrNondiscretionary] [varchar](200) NULL,
    [AUM] [varchar](100) NULL,
    [CreatedBy] [varchar](100) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [varchar](100) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_AIN_AccountInventory] PRIMARY KEY CLUSTERED ([ID] ASC));
GO

-- Sample accounts (only when the table is empty). The RM names test the name rules of the recipients:
-- a name with one user, a name with no user, a name with two users, and a row with an empty account name.
-- Script 140 adds RMCC; 900-local-seed.sql fills it after that script.
IF NOT EXISTS (SELECT 1 FROM AIN.AccountInventory)
    INSERT INTO AIN.AccountInventory (Account, AccountName, AccountNumber, RelationshipManager, StarAccountNumber,
        Custodian, CreatedBy, CreatedDate, IsDeleted)
    VALUES
    ('700100 - Harbor Family Trust', 'Harbor Family Trust', '700100', 'Riley Manager', 'S-700100', 'Custodian One', 'local.setup', GETUTCDATE(), 0),
    ('700200 - Maple Foundation', 'Maple Foundation', '700200', 'Nobody Known', 'S-700200', 'Custodian Two', 'local.setup', GETUTCDATE(), 0),
    ('700300 - Cedar Holdings', 'Cedar Holdings', '700300', 'Sam Twin', 'S-700300', 'Custodian One', 'local.setup', GETUTCDATE(), 0),
    ('700400 - ', '', '700400', 'Riley Manager', 'S-700400', 'Custodian Three', 'local.setup', GETUTCDATE(), 0),
    ('700500 - Old Closed Account', 'Old Closed Account', '700500', 'Riley Manager', 'S-700500', 'Custodian One', 'local.setup', GETUTCDATE(), 1);
GO
