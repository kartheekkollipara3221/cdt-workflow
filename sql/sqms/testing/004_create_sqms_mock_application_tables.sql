SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF SCHEMA_ID(N'SSQMS') IS NULL
        EXEC(N'CREATE SCHEMA SSQMS AUTHORIZATION dbo;');

    IF OBJECT_ID(N'SSQMS.Opportunity', N'U') IS NULL
    BEGIN
        CREATE TABLE [SSQMS].[Opportunity]
        (
            [ID] [int] IDENTITY(10000,1) NOT NULL,
            [SellerRMTeam] [varchar](max) NOT NULL,
            [Account] [varchar](max) NOT NULL,
            [AccountName] [varchar](1000) NOT NULL,
            [AccountNumber] [varchar](1000) NOT NULL,
            [RelationshipManager] [varchar](1000) NULL,
            [FundConsideredSecondarySale] [varchar](1000) NULL,
            [Vehicle] [varchar](1000) NULL,
            [ClientCommitmentToVehicle] [varchar](100) NULL,
            [CommitmentAmountSale] [varchar](100) NULL,
            [ClientRelationshipLeavingBBH] [varchar](100) NULL,
            [RationaleForSecondarySale] [varchar](max) NULL,
            [EffectiveDateForSale] [datetime] NULL,
            [Status] [varchar](200) NULL,
            [FundManagementFee] [varchar](100) NULL,
            [MinimumBid] [varchar](100) NULL,
            [RecentDateOfValuation] [datetime] NULL,
            [ValuationAsRecentDate] [varchar](100) NULL,
            [ContributionSinceValuation] [varchar](1000) NULL,
            [DistributionSinceValuation] [varchar](1000) NULL,
            [EstimatedFairValue] [varchar](1000) NULL,
            [RemainingCapitalCommitment] [varchar](1000) NULL,
            [IsValuationForPrivateFundVehicle] [bit] NULL,
            [TotalBid] [int] NULL,
            [LargestBid] [varchar](100) NULL,
            [LowestBid] [varchar](100) NULL,
            [WorkflowState] [nvarchar](100) NULL,
            [CreatedBy] [nvarchar](50) NULL,
            [CreatedDate] [datetime] NULL,
            [ModifiedBy] [nvarchar](50) NULL,
            [ModifiedDate] [datetime] NULL,
            [IsDeleted] [bit] NULL,
            [OpenBiddingNoticeAt] [datetimeoffset](0) NULL,
            [OpenBiddingReminderAt] [datetimeoffset](0) NULL,
            [BidResponseDueDate] [date] NULL,
            [BiddingCloseAt] [datetimeoffset](0) NULL,
            [SellerResponseDueDate] [date] NULL,
            [OpportunityRelationshipManagerEmail] [varchar](320) NULL,
            [SellerRmTeamEmailsJson] [nvarchar](max) NULL,
            CONSTRAINT [PK_SSQMS_Opportunity] PRIMARY KEY CLUSTERED ([ID] ASC)
                WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF,
                      IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON,
                      ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 100,
                      OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
    END;

    IF OBJECT_ID(N'SSQMS.Bid', N'U') IS NULL
    BEGIN
        CREATE TABLE [SSQMS].[Bid]
        (
            [ID] [int] IDENTITY(10000,1) NOT NULL,
            [OpportunityId] [int] NOT NULL,
            [BuyerRmteam] [varchar](max) NOT NULL,
            [BidForFullCommitment] [varchar](10) NOT NULL,
            [CommitmentAmountRequested] [varchar](100) NULL,
            [AccountName] [varchar](1000) NOT NULL,
            [AccountNumber] [varchar](1000) NOT NULL,
            [RelationshipManager] [varchar](1000) NULL,
            [BBHAffiliateAccount] [varchar](1000) NULL,
            [BidFairValue] [varchar](1000) NULL,
            [BidExpressedAsFairValue] [varchar](100) NULL,
            [Status] [varchar](200) NULL,
            [WorkflowState] [nvarchar](100) NULL,
            [CreatedBy] [nvarchar](50) NULL,
            [CreatedDate] [datetime] NULL,
            [ModifiedBy] [nvarchar](50) NULL,
            [ModifiedDate] [datetime] NULL,
            [IsDeleted] [bit] NULL,
            [BidPricingCategory] [varchar](20) NULL,
            [BidRelationshipManagerEmail] [varchar](320) NULL,
            [BuyerRmTeamEmailsJson] [nvarchar](max) NULL,
            CONSTRAINT [PK_SSQMS_Bid] PRIMARY KEY CLUSTERED ([ID] ASC)
                WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF,
                      IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON,
                      ALLOW_PAGE_LOCKS = ON, FILLFACTOR = 100,
                      OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF)
        ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
    END;

    IF OBJECT_ID(N'SSQMS.Opportunity', N'U') IS NULL
       OR OBJECT_ID(N'SSQMS.Bid', N'U') IS NULL
        THROW 51200, 'SQMS mock tables could not be created.', 1;

    IF COL_LENGTH(N'SSQMS.Opportunity', N'OpenBiddingNoticeAt') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [OpenBiddingNoticeAt] [datetimeoffset](0) NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'OpenBiddingReminderAt') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [OpenBiddingReminderAt] [datetimeoffset](0) NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'BidResponseDueDate') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [BidResponseDueDate] [date] NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'BiddingCloseAt') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [BiddingCloseAt] [datetimeoffset](0) NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'SellerResponseDueDate') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [SellerResponseDueDate] [date] NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'OpportunityRelationshipManagerEmail') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [OpportunityRelationshipManagerEmail] [varchar](320) NULL;
    IF COL_LENGTH(N'SSQMS.Opportunity', N'SellerRmTeamEmailsJson') IS NULL
        ALTER TABLE [SSQMS].[Opportunity] ADD [SellerRmTeamEmailsJson] [nvarchar](max) NULL;

    IF COL_LENGTH(N'SSQMS.Bid', N'BidPricingCategory') IS NULL
        ALTER TABLE [SSQMS].[Bid] ADD [BidPricingCategory] [varchar](20) NULL;
    IF COL_LENGTH(N'SSQMS.Bid', N'BidRelationshipManagerEmail') IS NULL
        ALTER TABLE [SSQMS].[Bid] ADD [BidRelationshipManagerEmail] [varchar](320) NULL;
    IF COL_LENGTH(N'SSQMS.Bid', N'BuyerRmTeamEmailsJson') IS NULL
        ALTER TABLE [SSQMS].[Bid] ADD [BuyerRmTeamEmailsJson] [nvarchar](max) NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'SSQMS.Bid')
          AND name = N'FK_SSQMS_Opportunity_Bid'
    )
    BEGIN
        ALTER TABLE [SSQMS].[Bid] WITH CHECK
            ADD CONSTRAINT [FK_SSQMS_Opportunity_Bid]
            FOREIGN KEY ([OpportunityId]) REFERENCES [SSQMS].[Opportunity] ([ID]);
        ALTER TABLE [SSQMS].[Bid]
            CHECK CONSTRAINT [FK_SSQMS_Opportunity_Bid];
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'SSQMS.Opportunity')
          AND name = N'CK_SSQMS_Opportunity_SellerRmTeamEmailsJson'
    )
        ALTER TABLE [SSQMS].[Opportunity] WITH CHECK
            ADD CONSTRAINT [CK_SSQMS_Opportunity_SellerRmTeamEmailsJson]
            CHECK ([SellerRmTeamEmailsJson] IS NULL OR ISJSON([SellerRmTeamEmailsJson]) = 1);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'SSQMS.Bid')
          AND name = N'CK_SSQMS_Bid_BidPricingCategory'
    )
        ALTER TABLE [SSQMS].[Bid] WITH CHECK
            ADD CONSTRAINT [CK_SSQMS_Bid_BidPricingCategory]
            CHECK ([BidPricingCategory] IS NULL OR [BidPricingCategory] IN ('FairValue', 'Premium', 'Discount'));

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'SSQMS.Bid')
          AND name = N'CK_SSQMS_Bid_BuyerRmTeamEmailsJson'
    )
        ALTER TABLE [SSQMS].[Bid] WITH CHECK
            ADD CONSTRAINT [CK_SSQMS_Bid_BuyerRmTeamEmailsJson]
            CHECK ([BuyerRmTeamEmailsJson] IS NULL OR ISJSON([BuyerRmTeamEmailsJson]) = 1);

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'SSQMS.Opportunity')
          AND name = N'IX_SSQMS_Opportunity_OpenBiddingNoticeDue'
    )
        CREATE INDEX [IX_SSQMS_Opportunity_OpenBiddingNoticeDue]
            ON [SSQMS].[Opportunity] ([OpenBiddingNoticeAt], [ID])
            INCLUDE ([EffectiveDateForSale], [BidResponseDueDate], [OpenBiddingReminderAt])
            WHERE [IsDeleted] = 0 AND [WorkflowState] = N'OpenToBidding';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'SSQMS.Opportunity')
          AND name = N'IX_SSQMS_Opportunity_OpenBiddingReminderDue'
    )
        CREATE INDEX [IX_SSQMS_Opportunity_OpenBiddingReminderDue]
            ON [SSQMS].[Opportunity] ([OpenBiddingReminderAt], [ID])
            INCLUDE ([EffectiveDateForSale], [BidResponseDueDate], [OpenBiddingNoticeAt])
            WHERE [IsDeleted] = 0 AND [WorkflowState] = N'OpenToBidding';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'SSQMS.Opportunity')
          AND name = N'IX_SSQMS_Opportunity_BiddingCloseDue'
    )
        CREATE INDEX [IX_SSQMS_Opportunity_BiddingCloseDue]
            ON [SSQMS].[Opportunity] ([BiddingCloseAt], [ID])
            INCLUDE ([CommitmentAmountSale])
            WHERE [IsDeleted] = 0 AND [WorkflowState] = N'OpenToBidding';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'SSQMS.Bid')
          AND name = N'IX_SSQMS_Bid_OpportunityWorkflowState'
    )
        CREATE INDEX [IX_SSQMS_Bid_OpportunityWorkflowState]
            ON [SSQMS].[Bid] ([OpportunityId], [WorkflowState], [IsDeleted])
            INCLUDE ([CommitmentAmountRequested], [BidPricingCategory]);

    IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Opportunity] WHERE [ID] = 10000)
    BEGIN
        SET IDENTITY_INSERT [SSQMS].[Opportunity] ON;
        INSERT [SSQMS].[Opportunity]
        (
            [ID], [SellerRMTeam], [Account], [AccountName], [AccountNumber],
            [RelationshipManager], [FundConsideredSecondarySale], [Vehicle],
            [ClientCommitmentToVehicle], [CommitmentAmountSale],
            [ClientRelationshipLeavingBBH], [RationaleForSecondarySale],
            [EffectiveDateForSale], [Status], [FundManagementFee], [MinimumBid],
            [RecentDateOfValuation], [ValuationAsRecentDate],
            [ContributionSinceValuation], [DistributionSinceValuation],
            [EstimatedFairValue], [RemainingCapitalCommitment],
            [IsValuationForPrivateFundVehicle], [TotalBid], [LargestBid], [LowestBid],
            [WorkflowState], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted]
        )
        VALUES
        (
            10000, 'seller-rm-team@example.test', 'SQMS-CONSOLE-ACCOUNT',
            'SQMS Console Mock Opportunity', 'SQMS-OPP-10000', 'SQMS Relationship Manager',
            'Yes', 'Private Fund Vehicle', '1000000', '500000', 'No',
            'Deterministic SQMS console integration fixture.', '2030-01-15T00:00:00',
            'DRAFT', '1.00', '450000', '2030-01-01T00:00:00', 'Yes',
            '10000', '5000', '600000', '400000', 1, 1, '475000', '450000',
            N'Draft', N'sqms.mfo', '2030-01-15T14:00:00', NULL, NULL, 0
        );
        SET IDENTITY_INSERT [SSQMS].[Opportunity] OFF;
    END;

    IF NOT EXISTS (SELECT 1 FROM [SSQMS].[Bid] WHERE [ID] = 10000)
    BEGIN
        SET IDENTITY_INSERT [SSQMS].[Bid] ON;
        INSERT [SSQMS].[Bid]
        (
            [ID], [OpportunityId], [BuyerRmteam], [BidForFullCommitment],
            [CommitmentAmountRequested], [AccountName], [AccountNumber],
            [RelationshipManager], [BBHAffiliateAccount], [BidFairValue],
            [BidExpressedAsFairValue], [Status], [WorkflowState], [CreatedBy],
            [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted]
        )
        VALUES
        (
            10000, 10000, 'buyer-rm-team@example.test', 'Yes', '500000',
            'SQMS Console Mock Bid', 'SQMS-BID-10000', 'SQMS Buyer Relationship Manager',
            'No', '475000', '95%', 'DRAFT', N'Draft', N'sqms.mfo',
            '2030-01-15T14:05:00', NULL, NULL, 0
        );
        SET IDENTITY_INSERT [SSQMS].[Bid] OFF;
    END;

    UPDATE [SSQMS].[Opportunity]
    SET [OpenBiddingNoticeAt] = COALESCE([OpenBiddingNoticeAt], '2030-01-01T08:00:00-05:00'),
        [OpenBiddingReminderAt] = COALESCE([OpenBiddingReminderAt], '2030-01-14T08:00:00-05:00'),
        [BidResponseDueDate] = COALESCE([BidResponseDueDate], '2030-01-15'),
        [BiddingCloseAt] = COALESCE([BiddingCloseAt], '2030-01-16T00:00:00-05:00'),
        [SellerResponseDueDate] = COALESCE([SellerResponseDueDate], '2030-01-24'),
        [OpportunityRelationshipManagerEmail] = COALESCE(
            [OpportunityRelationshipManagerEmail], 'seller.rm@example.test'),
        [SellerRmTeamEmailsJson] = COALESCE(
            [SellerRmTeamEmailsJson], N'["seller.team1@example.test","seller.team2@example.test"]')
    WHERE [ID] = 10000;

    UPDATE [SSQMS].[Bid]
    SET [BidPricingCategory] = COALESCE([BidPricingCategory], 'FairValue'),
        [BidRelationshipManagerEmail] = COALESCE(
            [BidRelationshipManagerEmail], 'buyer.rm@example.test'),
        [BuyerRmTeamEmailsJson] = COALESCE(
            [BuyerRmTeamEmailsJson], N'["buyer.team1@example.test","buyer.team2@example.test"]')
    WHERE [ID] = 10000;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Opportunity' AS EntityName, [ID], [Status], [WorkflowState]
FROM [SSQMS].[Opportunity]
WHERE [ID] = 10000;

SELECT N'Bid' AS EntityName, [ID], [OpportunityId], [Status], [WorkflowState]
FROM [SSQMS].[Bid]
WHERE [ID] = 10000;

PRINT 'SQMS mock application table setup passed.';
