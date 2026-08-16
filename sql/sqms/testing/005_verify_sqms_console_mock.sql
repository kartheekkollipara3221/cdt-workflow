SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'SSQMS.Opportunity', N'U') IS NULL
    THROW 51210, 'Missing SSQMS.Opportunity mock table.', 1;

IF OBJECT_ID(N'SSQMS.Bid', N'U') IS NULL
    THROW 51211, 'Missing SSQMS.Bid mock table.', 1;

CREATE TABLE #ExpectedColumn
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    DataType sysname NOT NULL,
    CharacterMaximumLength int NULL,
    IsNullable varchar(3) NOT NULL,
    IsIdentity bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);

INSERT #ExpectedColumn
    (TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity)
VALUES
    (N'Opportunity', N'ID', N'int', NULL, 'NO', 1),
    (N'Opportunity', N'SellerRMTeam', N'varchar', -1, 'NO', 0),
    (N'Opportunity', N'Account', N'varchar', -1, 'NO', 0),
    (N'Opportunity', N'AccountName', N'varchar', 1000, 'NO', 0),
    (N'Opportunity', N'AccountNumber', N'varchar', 1000, 'NO', 0),
    (N'Opportunity', N'RelationshipManager', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'FundConsideredSecondarySale', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'Vehicle', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'ClientCommitmentToVehicle', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'CommitmentAmountSale', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'ClientRelationshipLeavingBBH', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'RationaleForSecondarySale', N'varchar', -1, 'YES', 0),
    (N'Opportunity', N'EffectiveDateForSale', N'datetime', NULL, 'YES', 0),
    (N'Opportunity', N'Status', N'varchar', 200, 'YES', 0),
    (N'Opportunity', N'FundManagementFee', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'MinimumBid', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'RecentDateOfValuation', N'datetime', NULL, 'YES', 0),
    (N'Opportunity', N'ValuationAsRecentDate', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'ContributionSinceValuation', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'DistributionSinceValuation', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'EstimatedFairValue', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'RemainingCapitalCommitment', N'varchar', 1000, 'YES', 0),
    (N'Opportunity', N'IsValuationForPrivateFundVehicle', N'bit', NULL, 'YES', 0),
    (N'Opportunity', N'TotalBid', N'int', NULL, 'YES', 0),
    (N'Opportunity', N'LargestBid', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'LowestBid', N'varchar', 100, 'YES', 0),
    (N'Opportunity', N'WorkflowState', N'nvarchar', 100, 'YES', 0),
    (N'Opportunity', N'CreatedBy', N'nvarchar', 50, 'YES', 0),
    (N'Opportunity', N'CreatedDate', N'datetime', NULL, 'YES', 0),
    (N'Opportunity', N'ModifiedBy', N'nvarchar', 50, 'YES', 0),
    (N'Opportunity', N'ModifiedDate', N'datetime', NULL, 'YES', 0),
    (N'Opportunity', N'IsDeleted', N'bit', NULL, 'YES', 0),
    (N'Bid', N'ID', N'int', NULL, 'NO', 1),
    (N'Bid', N'OpportunityId', N'int', NULL, 'NO', 0),
    (N'Bid', N'BuyerRmteam', N'varchar', -1, 'NO', 0),
    (N'Bid', N'BidForFullCommitment', N'varchar', 10, 'NO', 0),
    (N'Bid', N'CommitmentAmountRequested', N'varchar', 100, 'YES', 0),
    (N'Bid', N'AccountName', N'varchar', 1000, 'NO', 0),
    (N'Bid', N'AccountNumber', N'varchar', 1000, 'NO', 0),
    (N'Bid', N'RelationshipManager', N'varchar', 1000, 'YES', 0),
    (N'Bid', N'BBHAffiliateAccount', N'varchar', 1000, 'YES', 0),
    (N'Bid', N'BidFairValue', N'varchar', 1000, 'YES', 0),
    (N'Bid', N'BidExpressedAsFairValue', N'varchar', 100, 'YES', 0),
    (N'Bid', N'Status', N'varchar', 200, 'YES', 0),
    (N'Bid', N'WorkflowState', N'nvarchar', 100, 'YES', 0),
    (N'Bid', N'CreatedBy', N'nvarchar', 50, 'YES', 0),
    (N'Bid', N'CreatedDate', N'datetime', NULL, 'YES', 0),
    (N'Bid', N'ModifiedBy', N'nvarchar', 50, 'YES', 0),
    (N'Bid', N'ModifiedDate', N'datetime', NULL, 'YES', 0),
    (N'Bid', N'IsDeleted', N'bit', NULL, 'YES', 0);

CREATE TABLE #ActualColumn
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    DataType sysname NOT NULL,
    CharacterMaximumLength int NULL,
    IsNullable varchar(3) NOT NULL,
    IsIdentity bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);

INSERT #ActualColumn
    (TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity)
SELECT information.TABLE_NAME,
       information.COLUMN_NAME,
       information.DATA_TYPE,
       information.CHARACTER_MAXIMUM_LENGTH,
       information.IS_NULLABLE,
       columnMetadata.is_identity
FROM INFORMATION_SCHEMA.COLUMNS AS information
INNER JOIN sys.schemas AS schemaMetadata
    ON schemaMetadata.name = information.TABLE_SCHEMA
INNER JOIN sys.tables AS tableMetadata
    ON tableMetadata.schema_id = schemaMetadata.schema_id
   AND tableMetadata.name = information.TABLE_NAME
INNER JOIN sys.columns AS columnMetadata
    ON columnMetadata.object_id = tableMetadata.object_id
   AND columnMetadata.name = information.COLUMN_NAME
WHERE information.TABLE_SCHEMA = N'SSQMS'
  AND information.TABLE_NAME IN (N'Opportunity', N'Bid');

IF EXISTS
(
    SELECT TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity
    FROM #ExpectedColumn
    EXCEPT
    SELECT TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity
    FROM #ActualColumn
)
    THROW 51212, 'Missing or incorrect SQMS mock column.', 1;

IF EXISTS
(
    SELECT TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity
    FROM #ActualColumn
    EXCEPT
    SELECT TableName, ColumnName, DataType, CharacterMaximumLength, IsNullable, IsIdentity
    FROM #ExpectedColumn
)
    THROW 51213, 'Unexpected SQMS mock column.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints AS keyConstraint
    INNER JOIN sys.index_columns AS indexColumn
        ON indexColumn.object_id = keyConstraint.parent_object_id
       AND indexColumn.index_id = keyConstraint.unique_index_id
    INNER JOIN sys.columns AS columnMetadata
        ON columnMetadata.object_id = indexColumn.object_id
       AND columnMetadata.column_id = indexColumn.column_id
    WHERE keyConstraint.name = N'PK_SSQMS_Opportunity'
      AND keyConstraint.parent_object_id = OBJECT_ID(N'SSQMS.Opportunity')
      AND columnMetadata.name = N'ID'
)
    THROW 51214, 'Missing or incorrect Opportunity primary key.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints AS keyConstraint
    INNER JOIN sys.index_columns AS indexColumn
        ON indexColumn.object_id = keyConstraint.parent_object_id
       AND indexColumn.index_id = keyConstraint.unique_index_id
    INNER JOIN sys.columns AS columnMetadata
        ON columnMetadata.object_id = indexColumn.object_id
       AND columnMetadata.column_id = indexColumn.column_id
    WHERE keyConstraint.name = N'PK_SSQMS_Bid'
      AND keyConstraint.parent_object_id = OBJECT_ID(N'SSQMS.Bid')
      AND columnMetadata.name = N'ID'
)
    THROW 51215, 'Missing or incorrect Bid primary key.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SSQMS_Opportunity_Bid'
      AND parent_object_id = OBJECT_ID(N'SSQMS.Bid')
      AND referenced_object_id = OBJECT_ID(N'SSQMS.Opportunity')
      AND is_disabled = 0
      AND is_not_trusted = 0
)
    THROW 51216, 'Missing, disabled, or untrusted Opportunity-to-Bid foreign key.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM [SSQMS].[Opportunity]
    WHERE [ID] = 10000
      AND [Status] = 'DRAFT'
      AND [WorkflowState] = N'Draft'
      AND [CreatedBy] = N'sqms.mfo'
      AND [ModifiedBy] IS NULL
      AND [ModifiedDate] IS NULL
      AND [IsDeleted] = 0
)
    THROW 51217, 'Opportunity fixture is missing or was modified.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM [SSQMS].[Bid]
    WHERE [ID] = 10000
      AND [OpportunityId] = 10000
      AND [Status] = 'DRAFT'
      AND [WorkflowState] = N'Draft'
      AND [CreatedBy] = N'sqms.mfo'
      AND [ModifiedBy] IS NULL
      AND [ModifiedDate] IS NULL
      AND [IsDeleted] = 0
)
    THROW 51218, 'Bid fixture is missing, unrelated, or was modified.', 1;

IF EXISTS
(
    SELECT expected.WorkflowInstanceId, expected.CurrentStateKey, expected.Version
    FROM
    (
        VALUES
            (N'WF-SQMS-OPP-10000', N'Submitted', 2),
            (N'WF-SQMS-BID-10000', N'Submitted', 2)
    ) AS expected(WorkflowInstanceId, CurrentStateKey, Version)
    EXCEPT
    SELECT instance.WorkflowInstanceId, instance.CurrentStateKey, instance.Version
    FROM dbo.WorkflowInstance AS instance
    WHERE instance.WorkflowInstanceId IN (N'WF-SQMS-OPP-10000', N'WF-SQMS-BID-10000')
)
    THROW 51219, 'SQMS mock workflow instances are missing or not Submitted version 2.', 1;

IF (SELECT COUNT(*) FROM dbo.WorkflowAudit
    WHERE WorkflowInstanceId = N'WF-SQMS-OPP-10000'
      AND ActionKey = N'SubmitOpportunity') <> 1
    THROW 51220, 'Opportunity submit audit count is incorrect.', 1;

IF (SELECT COUNT(*) FROM dbo.WorkflowAudit
    WHERE WorkflowInstanceId = N'WF-SQMS-BID-10000'
      AND ActionKey = N'SubmitBid') <> 1
    THROW 51221, 'Bid submit audit count is incorrect.', 1;

IF EXISTS
(
    SELECT 1 FROM dbo.WorkflowTask
    WHERE WorkflowInstanceId IN (N'WF-SQMS-OPP-10000', N'WF-SQMS-BID-10000')
)
    THROW 51222, 'SQMS transition-only mock must not create tasks.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.WorkflowTaskSlaSchedule AS schedule
    INNER JOIN dbo.WorkflowTask AS task ON task.Id = schedule.WorkflowTaskId
    WHERE task.WorkflowInstanceId IN (N'WF-SQMS-OPP-10000', N'WF-SQMS-BID-10000')
)
    THROW 51223, 'SQMS transition-only mock must not create SLA schedules.', 1;

IF EXISTS
(
    SELECT 1 FROM dbo.NotificationOutbox
    WHERE WorkflowInstanceId IN (N'WF-SQMS-OPP-10000', N'WF-SQMS-BID-10000')
)
    THROW 51224, 'SQMS transition-only mock must not create notification rows.', 1;

SELECT N'Opportunity' AS EntityName, [ID], [Status], [WorkflowState]
FROM [SSQMS].[Opportunity]
WHERE [ID] = 10000;

SELECT N'Bid' AS EntityName, [ID], [OpportunityId], [Status], [WorkflowState]
FROM [SSQMS].[Bid]
WHERE [ID] = 10000;

SELECT [WorkflowInstanceId], [CurrentStateKey], [Status], [Version]
FROM dbo.WorkflowInstance
WHERE [WorkflowInstanceId] IN (N'WF-SQMS-OPP-10000', N'WF-SQMS-BID-10000')
ORDER BY [WorkflowInstanceId];

PRINT 'SQMS console mock verification passed.';
