SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'[dbo].[NotificationOutbox]', N'U') IS NULL
    OR NOT EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[NotificationOutbox]')
          AND name = N'WorkflowInstanceId' AND is_nullable = 1
          AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 300
    )
    OR NOT EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[NotificationOutbox]')
          AND name = N'NotificationOccurrenceKey' AND is_nullable = 1
          AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 600
    )
    THROW 51201, 'NotificationOutbox ownership schema is missing or incorrect.', 1;

IF NOT EXISTS
    (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[NotificationOutbox]') AND name = N'UX_NotificationOutbox_NotificationOccurrenceKey' AND is_unique = 1)
    OR NOT EXISTS
    (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'[dbo].[NotificationOutbox]') AND name = N'CK_NotificationOutbox_Owner' AND is_disabled = 0 AND is_not_trusted = 0)
    THROW 51202, 'NotificationOutbox occurrence ownership constraints are missing.', 1;

IF OBJECT_ID(N'[SSQMS].[Opportunity]', N'U') IS NULL OR OBJECT_ID(N'[SSQMS].[Bid]', N'U') IS NULL
    THROW 51203, 'Expected SSQMS Opportunity and Bid tables were not found.', 1;

DECLARE @ExpectedColumns TABLE
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    TypeName sysname NOT NULL,
    MaxLength smallint NOT NULL,
    IsNullable bit NOT NULL,
    IsIdentity bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);
INSERT @ExpectedColumns VALUES
    (N'Opportunity',N'ID',N'int',4,0,1),(N'Opportunity',N'SellerRMTeam',N'varchar',-1,0,0),
    (N'Opportunity',N'Account',N'varchar',-1,0,0),(N'Opportunity',N'AccountName',N'varchar',1000,0,0),
    (N'Opportunity',N'AccountNumber',N'varchar',1000,0,0),(N'Opportunity',N'RelationshipManager',N'varchar',1000,1,0),
    (N'Opportunity',N'FundConsideredSecondarySale',N'varchar',1000,1,0),(N'Opportunity',N'Vehicle',N'varchar',1000,1,0),
    (N'Opportunity',N'ClientCommitmentToVehicle',N'varchar',100,1,0),(N'Opportunity',N'CommitmentAmountSale',N'varchar',100,1,0),
    (N'Opportunity',N'ClientRelationshipLeavingBBH',N'varchar',100,1,0),(N'Opportunity',N'RationaleForSecondarySale',N'varchar',-1,1,0),
    (N'Opportunity',N'EffectiveDateForSale',N'datetime',8,1,0),(N'Opportunity',N'Status',N'varchar',200,1,0),
    (N'Opportunity',N'FundManagementFee',N'varchar',100,1,0),(N'Opportunity',N'MinimumBid',N'varchar',100,1,0),
    (N'Opportunity',N'RecentDateOfValuation',N'datetime',8,1,0),(N'Opportunity',N'ValuationAsRecentDate',N'varchar',100,1,0),
    (N'Opportunity',N'ContributionSinceValuation',N'varchar',1000,1,0),(N'Opportunity',N'DistributionSinceValuation',N'varchar',1000,1,0),
    (N'Opportunity',N'EstimatedFairValue',N'varchar',1000,1,0),(N'Opportunity',N'RemainingCapitalCommitment',N'varchar',1000,1,0),
    (N'Opportunity',N'IsValuationForPrivateFundVehicle',N'bit',1,1,0),(N'Opportunity',N'TotalBid',N'int',4,1,0),
    (N'Opportunity',N'LargestBid',N'varchar',100,1,0),(N'Opportunity',N'LowestBid',N'varchar',100,1,0),
    (N'Opportunity',N'WorkflowState',N'nvarchar',200,1,0),(N'Opportunity',N'CreatedBy',N'nvarchar',100,1,0),
    (N'Opportunity',N'CreatedDate',N'datetime',8,1,0),(N'Opportunity',N'ModifiedBy',N'nvarchar',100,1,0),
    (N'Opportunity',N'ModifiedDate',N'datetime',8,1,0),(N'Opportunity',N'IsDeleted',N'bit',1,1,0),
    (N'Bid',N'ID',N'int',4,0,1),(N'Bid',N'OpportunityId',N'int',4,0,0),
    (N'Bid',N'BuyerRmteam',N'varchar',-1,0,0),(N'Bid',N'BidForFullCommitment',N'varchar',10,0,0),
    (N'Bid',N'CommitmentAmountRequested',N'varchar',100,1,0),(N'Bid',N'AccountName',N'varchar',1000,0,0),
    (N'Bid',N'AccountNumber',N'varchar',1000,0,0),(N'Bid',N'RelationshipManager',N'varchar',1000,1,0),
    (N'Bid',N'BBHAffiliateAccount',N'varchar',1000,1,0),(N'Bid',N'BidFairValue',N'varchar',1000,1,0),
    (N'Bid',N'BidExpressedAsFairValue',N'varchar',100,1,0),(N'Bid',N'Status',N'varchar',200,1,0),
    (N'Bid',N'WorkflowState',N'nvarchar',200,1,0),(N'Bid',N'CreatedBy',N'nvarchar',100,1,0),
    (N'Bid',N'CreatedDate',N'datetime',8,1,0),(N'Bid',N'ModifiedBy',N'nvarchar',100,1,0),
    (N'Bid',N'ModifiedDate',N'datetime',8,1,0),(N'Bid',N'IsDeleted',N'bit',1,1,0);

IF EXISTS
(
    SELECT TableName, ColumnName, TypeName, MaxLength, IsNullable, IsIdentity FROM @ExpectedColumns
    EXCEPT
    SELECT tableDefinition.name, columnDefinition.name, typeDefinition.name,
           columnDefinition.max_length, columnDefinition.is_nullable, columnDefinition.is_identity
    FROM sys.columns AS columnDefinition
    INNER JOIN sys.tables AS tableDefinition ON tableDefinition.object_id = columnDefinition.object_id
    INNER JOIN sys.schemas AS schemaDefinition ON schemaDefinition.schema_id = tableDefinition.schema_id
    INNER JOIN sys.types AS typeDefinition ON typeDefinition.user_type_id = columnDefinition.user_type_id
    WHERE schemaDefinition.name = N'SSQMS' AND tableDefinition.name IN (N'Opportunity', N'Bid')
)
OR EXISTS
(
    SELECT tableDefinition.name, columnDefinition.name, typeDefinition.name,
           columnDefinition.max_length, columnDefinition.is_nullable, columnDefinition.is_identity
    FROM sys.columns AS columnDefinition
    INNER JOIN sys.tables AS tableDefinition ON tableDefinition.object_id = columnDefinition.object_id
    INNER JOIN sys.schemas AS schemaDefinition ON schemaDefinition.schema_id = tableDefinition.schema_id
    INNER JOIN sys.types AS typeDefinition ON typeDefinition.user_type_id = columnDefinition.user_type_id
    WHERE schemaDefinition.name = N'SSQMS' AND tableDefinition.name IN (N'Opportunity', N'Bid')
    EXCEPT
    SELECT TableName, ColumnName, TypeName, MaxLength, IsNullable, IsIdentity FROM @ExpectedColumns
)
    THROW 51204, 'Unexpected SSQMS Opportunity or Bid column shape.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'[SSQMS].[Bid]')
      AND name = N'FK_SSQMS_Opportunity_Bid' AND is_disabled = 0 AND is_not_trusted = 0
)
    THROW 51205, 'Expected trusted SSQMS Bid foreign key was not found.', 1;

IF (SELECT COUNT(*) FROM [dbo].[WorkflowDefinition] WHERE WorkflowKey IN (N'SqmsOpportunity',N'SqmsBid') AND IsActive = 1) <> 2
    THROW 51206, 'SQMS workflow definitions are missing or inactive.', 1;
IF (SELECT COUNT(*) FROM [dbo].[WorkflowDefinitionVersion] AS version INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id=version.WorkflowDefinitionId WHERE definition.WorkflowKey IN (N'SqmsOpportunity',N'SqmsBid') AND version.VersionNumber=1 AND version.IsActive=1) <> 2
    THROW 51207, 'SQMS workflow version 1 rows are missing or inactive.', 1;

DECLARE @ExpectedStates TABLE (WorkflowKey nvarchar(100), StateKey nvarchar(100), IsTerminal bit, PRIMARY KEY(WorkflowKey,StateKey));
INSERT @ExpectedStates VALUES
 (N'SqmsOpportunity',N'Draft',0),(N'SqmsOpportunity',N'SUBMITTED',0),(N'SqmsOpportunity',N'PENDING',0),
 (N'SqmsOpportunity',N'OPEN TO BIDDING',0),(N'SqmsOpportunity',N'CLOSED TO BIDDING',0),
 (N'SqmsOpportunity',N'PENDING SELLER APPROVAL',0),(N'SqmsOpportunity',N'AWAITING ALLOCATION',0),
 (N'SqmsOpportunity',N'PARTIALLY ALLOCATED',0),(N'SqmsOpportunity',N'NOT FULFILLED',1),(N'SqmsOpportunity',N'COMPLETED',1),
 (N'SqmsBid',N'Draft',0),(N'SqmsBid',N'SUBMITTED',0),(N'SqmsBid',N'NOT ALLOCATED',1),(N'SqmsBid',N'IPG REVIEW',0),
 (N'SqmsBid',N'UNDER REVIEW BY SELLER',0),(N'SqmsBid',N'ACCEPTED',1),(N'SqmsBid',N'PARTIALLY ACCEPTED',1),(N'SqmsBid',N'NOT ACCEPTED',1);
IF EXISTS
(
 SELECT WorkflowKey,StateKey,IsTerminal FROM @ExpectedStates
 EXCEPT
 SELECT definition.WorkflowKey,stateDefinition.StateKey,stateDefinition.IsTerminal
 FROM [dbo].[WorkflowStateDefinition] stateDefinition
 JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=stateDefinition.WorkflowDefinitionVersionId
 JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE version.VersionNumber=1 AND stateDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')
)
OR (SELECT COUNT(*) FROM [dbo].[WorkflowStateDefinition] stateDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=stateDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId WHERE version.VersionNumber=1 AND stateDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')) <> 18
    THROW 51208, 'SQMS state keys or terminal flags are incorrect.', 1;

DECLARE @ExpectedActions TABLE (WorkflowKey nvarchar(100), ActionKey nvarchar(100), PRIMARY KEY(WorkflowKey,ActionKey));
INSERT @ExpectedActions VALUES
 (N'SqmsOpportunity',N'SubmitOpportunity'),(N'SqmsOpportunity',N'ReturnOpportunityToDraft'),(N'SqmsOpportunity',N'MarkOpportunityPending'),
 (N'SqmsOpportunity',N'OpenOpportunityForBidding'),(N'SqmsOpportunity',N'CloseOpportunityBidding'),(N'SqmsOpportunity',N'RequestSellerApproval'),
 (N'SqmsOpportunity',N'MarkOpportunityAwaitingAllocation'),(N'SqmsOpportunity',N'MarkOpportunityPartiallyAllocated'),
 (N'SqmsOpportunity',N'MarkOpportunityNotFulfilled'),(N'SqmsOpportunity',N'CompleteOpportunity'),
 (N'SqmsBid',N'SubmitBid'),(N'SqmsBid',N'ReturnBidToDraft'),(N'SqmsBid',N'MarkBidNotAllocated'),(N'SqmsBid',N'MoveBidToIpgReview'),
 (N'SqmsBid',N'SendBidToSellerReview'),(N'SqmsBid',N'AcceptBid'),(N'SqmsBid',N'PartiallyAcceptBid'),(N'SqmsBid',N'RejectBid');
IF EXISTS
(
 SELECT WorkflowKey,ActionKey FROM @ExpectedActions
 EXCEPT
 SELECT definition.WorkflowKey,actionDefinition.ActionKey
 FROM [dbo].[WorkflowActionDefinition] actionDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=actionDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE version.VersionNumber=1 AND actionDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')
)
OR (SELECT COUNT(*) FROM [dbo].[WorkflowActionDefinition] actionDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=actionDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId WHERE version.VersionNumber=1 AND actionDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')) <> 18
    THROW 51209, 'SQMS action definitions are incorrect.', 1;

DECLARE @ExpectedRoles TABLE (WorkflowKey nvarchar(100), RoleKey nvarchar(100), PRIMARY KEY(WorkflowKey,RoleKey));
INSERT @ExpectedRoles VALUES (N'SqmsOpportunity',N'MFO'),(N'SqmsOpportunity',N'IPG'),(N'SqmsOpportunity',N'System'),(N'SqmsBid',N'MFO'),(N'SqmsBid',N'IPG'),(N'SqmsBid',N'System'),(N'SqmsBid',N'Seller');
IF EXISTS
(
 SELECT WorkflowKey,RoleKey FROM @ExpectedRoles
 EXCEPT
 SELECT definition.WorkflowKey,roleDefinition.RoleKey FROM [dbo].[WorkflowRoleDefinition] roleDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=roleDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE version.VersionNumber=1 AND roleDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')
)
OR (SELECT COUNT(*) FROM [dbo].[WorkflowRoleDefinition] roleDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=roleDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId WHERE version.VersionNumber=1 AND roleDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')) <> 7
    THROW 51210, 'SQMS role definitions are incorrect.', 1;

DECLARE @ExpectedTransitions TABLE (WorkflowKey nvarchar(100),FromStateKey nvarchar(100),ActionKey nvarchar(100),AllowedRoleKey nvarchar(100),ToStateKey nvarchar(100),NotificationRouteKey nvarchar(100) NULL,PRIMARY KEY(WorkflowKey,FromStateKey,ActionKey,AllowedRoleKey));
INSERT @ExpectedTransitions VALUES
 (N'SqmsOpportunity',N'Draft',N'SubmitOpportunity',N'MFO',N'SUBMITTED',NULL),(N'SqmsOpportunity',N'SUBMITTED',N'ReturnOpportunityToDraft',N'IPG',N'Draft',NULL),
 (N'SqmsOpportunity',N'SUBMITTED',N'MarkOpportunityPending',N'IPG',N'PENDING',NULL),(N'SqmsOpportunity',N'PENDING',N'ReturnOpportunityToDraft',N'IPG',N'Draft',NULL),
 (N'SqmsOpportunity',N'PENDING',N'OpenOpportunityForBidding',N'IPG',N'OPEN TO BIDDING',NULL),(N'SqmsOpportunity',N'OPEN TO BIDDING',N'CloseOpportunityBidding',N'System',N'CLOSED TO BIDDING',NULL),
 (N'SqmsOpportunity',N'CLOSED TO BIDDING',N'RequestSellerApproval',N'IPG',N'PENDING SELLER APPROVAL',N'SqmsOpportunityPendingSellerApproval'),
 (N'SqmsOpportunity',N'PENDING SELLER APPROVAL',N'MarkOpportunityAwaitingAllocation',N'IPG',N'AWAITING ALLOCATION',NULL),
 (N'SqmsOpportunity',N'PENDING SELLER APPROVAL',N'MarkOpportunityPartiallyAllocated',N'IPG',N'PARTIALLY ALLOCATED',NULL),
 (N'SqmsOpportunity',N'PENDING SELLER APPROVAL',N'MarkOpportunityNotFulfilled',N'IPG',N'NOT FULFILLED',NULL),
 (N'SqmsOpportunity',N'AWAITING ALLOCATION',N'CompleteOpportunity',N'IPG',N'COMPLETED',NULL),(N'SqmsOpportunity',N'PARTIALLY ALLOCATED',N'CompleteOpportunity',N'IPG',N'COMPLETED',NULL),
 (N'SqmsBid',N'Draft',N'SubmitBid',N'MFO',N'SUBMITTED',NULL),(N'SqmsBid',N'SUBMITTED',N'ReturnBidToDraft',N'IPG',N'Draft',NULL),
 (N'SqmsBid',N'SUBMITTED',N'MarkBidNotAllocated',N'System',N'NOT ALLOCATED',NULL),(N'SqmsBid',N'SUBMITTED',N'MoveBidToIpgReview',N'System',N'IPG REVIEW',NULL),
 (N'SqmsBid',N'IPG REVIEW',N'SendBidToSellerReview',N'IPG',N'UNDER REVIEW BY SELLER',NULL),(N'SqmsBid',N'UNDER REVIEW BY SELLER',N'AcceptBid',N'Seller',N'ACCEPTED',N'SqmsBidAccepted'),
 (N'SqmsBid',N'UNDER REVIEW BY SELLER',N'PartiallyAcceptBid',N'Seller',N'PARTIALLY ACCEPTED',N'SqmsBidPartiallyAccepted'),(N'SqmsBid',N'UNDER REVIEW BY SELLER',N'RejectBid',N'Seller',N'NOT ACCEPTED',N'SqmsBidNotAccepted');
IF EXISTS
(
 SELECT WorkflowKey,FromStateKey,ActionKey,AllowedRoleKey,ToStateKey,ISNULL(NotificationRouteKey,N'') FROM @ExpectedTransitions
 EXCEPT
 SELECT definition.WorkflowKey,transitionDefinition.FromStateKey,transitionDefinition.ActionKey,transitionDefinition.AllowedRoleKey,transitionDefinition.ToStateKey,ISNULL(transitionDefinition.NotificationRouteKey,N'')
 FROM [dbo].[WorkflowTransitionDefinition] transitionDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=transitionDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE version.VersionNumber=1 AND transitionDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid') AND transitionDefinition.TaskToCloseKey IS NULL AND transitionDefinition.TaskToCreateKey IS NULL
)
OR (SELECT COUNT(*) FROM [dbo].[WorkflowTransitionDefinition] transitionDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=transitionDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId WHERE version.VersionNumber=1 AND transitionDefinition.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')) <> 20
    THROW 51211, 'SQMS transition matrix is incorrect.', 1;

IF (SELECT COUNT(*) FROM [dbo].[NotificationRoute] route JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=route.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId WHERE version.VersionNumber=1 AND route.IsActive=1 AND definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid')) <> 6
    THROW 51212, 'SQMS notification routes are missing or incorrect.', 1;

IF EXISTS
(
 SELECT 1 FROM [dbo].[NotificationRoute] route JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=route.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid') AND version.VersionNumber=1 AND route.IsActive=1
 AND (route.SubjectTemplate NOT LIKE N'%{{%}}%' OR route.BodyTemplate NOT LIKE N'%{{%}}%' OR route.HtmlBodyTemplate NOT LIKE N'%<a href="{{%}}">%')
)
    THROW 51213, 'SQMS notification route templates or URL tokens are incorrect.', 1;

IF NOT EXISTS
(
 SELECT 1 FROM [dbo].[NotificationRecipientRule] recipientRule JOIN [dbo].[NotificationRoute] route ON route.Id=recipientRule.NotificationRouteId
 WHERE route.RouteKey=N'SqmsOpportunityOpen' AND recipientRule.RecipientKind=N'To' AND recipientRule.SourceType=N'StaticAddress' AND recipientRule.IsActive=1
)
OR EXISTS
(
 SELECT route.RouteKey FROM [dbo].[NotificationRoute] route
 WHERE route.RouteKey IN(N'SqmsOpportunityPendingSellerApproval',N'SqmsBidAccepted',N'SqmsBidPartiallyAccepted',N'SqmsBidNotAccepted')
 EXCEPT
 SELECT route.RouteKey FROM [dbo].[NotificationRecipientRule] recipientRule JOIN [dbo].[NotificationRoute] route ON route.Id=recipientRule.NotificationRouteId
 WHERE recipientRule.RecipientKind=N'To' AND recipientRule.SourceType=N'RequestInput' AND recipientRule.IsRequired=1 AND recipientRule.IsActive=1
 GROUP BY route.RouteKey HAVING COUNT(*)=2
)
    THROW 51214, 'SQMS notification recipient rules are missing or incorrect.', 1;

IF EXISTS
(
 SELECT 1 FROM [dbo].[WorkflowTaskDefinition] taskDefinition JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=taskDefinition.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid') AND taskDefinition.IsActive=1
)
OR EXISTS
(
 SELECT 1 FROM [dbo].[SlaPolicy] policy JOIN [dbo].[WorkflowDefinitionVersion] version ON version.Id=policy.WorkflowDefinitionVersionId JOIN [dbo].[WorkflowDefinition] definition ON definition.Id=version.WorkflowDefinitionId
 WHERE definition.WorkflowKey IN(N'SqmsOpportunity',N'SqmsBid') AND policy.IsActive=1
)
    THROW 51215, 'SQMS workflows must not define active tasks or SLA policies.', 1;

PRINT 'SQMS deployment verification passed.';
