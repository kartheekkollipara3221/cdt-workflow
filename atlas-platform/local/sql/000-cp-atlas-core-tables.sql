-- LOCAL ONLY: do not run at the target. The target database already has these tables.
-- Makes the dbo.CP_Atlas_* tables in the local database, in the target shape (CP_Sharepoint.txt, 2026-10-06).
-- Used by the local database setup (C6) and by the SQL Server tests.
-- Safe to run again.
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.CP_Atlas_Application', N'U') IS NULL
CREATE TABLE [dbo].[CP_Atlas_Application](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [Name] [varchar](500) NULL,
    [Code] [varchar](20) NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    [ParentID] [int] NULL,
    CONSTRAINT [PK_CP_Atlas_Application] PRIMARY KEY CLUSTERED ([ID] ASC)
);

IF OBJECT_ID(N'dbo.CP_Atlas_Entitlement', N'U') IS NULL
CREATE TABLE [dbo].[CP_Atlas_Entitlement](
    [ID] [int] IDENTITY(10000,1) NOT NULL,
    [CP_Atlas_ApplicationID] [int] NULL,
    [Name] [varchar](100) NULL,
    [IsAdmin] [bit] NULL,
    [IsWrite] [bit] NULL,
    [IsRead] [bit] NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_CP_Atlas_Entitlement] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CP_Atlas_Application_Entitlement] FOREIGN KEY ([CP_Atlas_ApplicationID])
        REFERENCES [dbo].[CP_Atlas_Application] ([ID])
);

IF OBJECT_ID(N'dbo.CP_Atlas_User', N'U') IS NULL
CREATE TABLE [dbo].[CP_Atlas_User](
    [ID] [int] IDENTITY(1000,1) NOT NULL,
    [LanID] [nvarchar](50) NULL,
    [FirstName] [nvarchar](500) NULL,
    [LastName] [nvarchar](500) NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    [Email] [nvarchar](500) NULL,
    [DisplayName] [nvarchar](1000) NULL,
    CONSTRAINT [PK_CP_Atlas_User] PRIMARY KEY CLUSTERED ([ID] ASC)
);

IF OBJECT_ID(N'dbo.CP_Atlas_UserEntitlement', N'U') IS NULL
CREATE TABLE [dbo].[CP_Atlas_UserEntitlement](
    [ID] [int] IDENTITY(1000,1) NOT NULL,
    [CP_Atlas_UserID] [int] NULL,
    [CP_Atlas_EntitlementID] [int] NULL,
    [CreatedBy] [nvarchar](50) NULL,
    [CreatedDate] [datetime] NULL,
    [ModifiedBy] [nvarchar](50) NULL,
    [ModifiedDate] [datetime] NULL,
    [IsDeleted] [bit] NULL,
    CONSTRAINT [PK_CP_Atlas_UserEntitlement] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CP_Atlas_User_Entitlement] FOREIGN KEY ([CP_Atlas_UserID])
        REFERENCES [dbo].[CP_Atlas_User] ([ID])
);
GO
