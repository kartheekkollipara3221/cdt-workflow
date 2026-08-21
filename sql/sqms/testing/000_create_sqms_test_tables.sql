SET NOCOUNT ON;
SET XACT_ABORT ON;

IF SCHEMA_ID(N'SSQMS') IS NULL
    EXEC(N'CREATE SCHEMA [SSQMS]');

IF OBJECT_ID(N'[SSQMS].[Opportunity]', N'U') IS NULL
BEGIN
    CREATE TABLE [SSQMS].[Opportunity]
    (
        [ID] int IDENTITY(10000,1) NOT NULL,
        [SellerRMTeam] varchar(max) NOT NULL,
        [Account] varchar(max) NOT NULL,
        [AccountName] varchar(1000) NOT NULL,
        [AccountNumber] varchar(1000) NOT NULL,
        [RelationshipManager] varchar(1000) NULL,
        [FundConsideredSecondarySale] varchar(1000) NULL,
        [Vehicle] varchar(1000) NULL,
        [ClientCommitmentToVehicle] varchar(100) NULL,
        [CommitmentAmountSale] varchar(100) NULL,
        [ClientRelationshipLeavingBBH] varchar(100) NULL,
        [RationaleForSecondarySale] varchar(max) NULL,
        [EffectiveDateForSale] datetime NULL,
        [Status] varchar(200) NULL,
        [FundManagementFee] varchar(100) NULL,
        [MinimumBid] varchar(100) NULL,
        [RecentDateOfValuation] datetime NULL,
        [ValuationAsRecentDate] varchar(100) NULL,
        [ContributionSinceValuation] varchar(1000) NULL,
        [DistributionSinceValuation] varchar(1000) NULL,
        [EstimatedFairValue] varchar(1000) NULL,
        [RemainingCapitalCommitment] varchar(1000) NULL,
        [IsValuationForPrivateFundVehicle] bit NULL,
        [TotalBid] int NULL,
        [LargestBid] varchar(100) NULL,
        [LowestBid] varchar(100) NULL,
        [WorkflowState] nvarchar(100) NULL,
        [CreatedBy] nvarchar(50) NULL,
        [CreatedDate] datetime NULL,
        [ModifiedBy] nvarchar(50) NULL,
        [ModifiedDate] datetime NULL,
        [IsDeleted] bit NULL,
        CONSTRAINT [PK_SSQMS_Opportunity] PRIMARY KEY CLUSTERED ([ID] ASC)
    );
END;

IF OBJECT_ID(N'[SSQMS].[Bid]', N'U') IS NULL
BEGIN
    CREATE TABLE [SSQMS].[Bid]
    (
        [ID] int IDENTITY(10000,1) NOT NULL,
        [OpportunityId] int NOT NULL,
        [BuyerRmteam] varchar(max) NOT NULL,
        [BidForFullCommitment] varchar(10) NOT NULL,
        [CommitmentAmountRequested] varchar(100) NULL,
        [AccountName] varchar(1000) NOT NULL,
        [AccountNumber] varchar(1000) NOT NULL,
        [RelationshipManager] varchar(1000) NULL,
        [BBHAffiliateAccount] varchar(1000) NULL,
        [BidFairValue] varchar(1000) NULL,
        [BidExpressedAsFairValue] varchar(100) NULL,
        [Status] varchar(200) NULL,
        [WorkflowState] nvarchar(100) NULL,
        [CreatedBy] nvarchar(50) NULL,
        [CreatedDate] datetime NULL,
        [ModifiedBy] nvarchar(50) NULL,
        [ModifiedDate] datetime NULL,
        [IsDeleted] bit NULL,
        CONSTRAINT [PK_SSQMS_Bid] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [FK_SSQMS_Opportunity_Bid] FOREIGN KEY ([OpportunityId])
            REFERENCES [SSQMS].[Opportunity] ([ID])
    );
END;

ALTER TABLE [SSQMS].[Bid] CHECK CONSTRAINT [FK_SSQMS_Opportunity_Bid];
