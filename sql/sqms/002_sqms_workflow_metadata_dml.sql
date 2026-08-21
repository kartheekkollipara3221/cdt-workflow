SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @OpenToJson nvarchar(max) = N'$(SqmsOpenToRecipientsJson)';
DECLARE @OpenCcJson nvarchar(max) = N'$(SqmsOpenCcRecipientsJson)';
DECLARE @SellerApprovalCcJson nvarchar(max) = N'$(SqmsSellerApprovalCcRecipientsJson)';
DECLARE @BidDispositionCcJson nvarchar(max) = N'$(SqmsBidDispositionCcRecipientsJson)';

IF ISJSON(@OpenToJson) <> 1 OR LEFT(LTRIM(@OpenToJson), 1) <> N'['
    THROW 51101, 'SqmsOpenToRecipientsJson must be a JSON array.', 1;
IF ISJSON(@OpenCcJson) <> 1 OR LEFT(LTRIM(@OpenCcJson), 1) <> N'['
    THROW 51102, 'SqmsOpenCcRecipientsJson must be a JSON array.', 1;
IF ISJSON(@SellerApprovalCcJson) <> 1 OR LEFT(LTRIM(@SellerApprovalCcJson), 1) <> N'['
    THROW 51103, 'SqmsSellerApprovalCcRecipientsJson must be a JSON array.', 1;
IF ISJSON(@BidDispositionCcJson) <> 1 OR LEFT(LTRIM(@BidDispositionCcJson), 1) <> N'['
    THROW 51104, 'SqmsBidDispositionCcRecipientsJson must be a JSON array.', 1;
IF NOT EXISTS (SELECT 1 FROM OPENJSON(@OpenToJson) WHERE [type] = 1)
    THROW 51105, 'SqmsOpenToRecipientsJson must contain at least one string value.', 1;
IF EXISTS (SELECT 1 FROM OPENJSON(@OpenToJson) WHERE [type] <> 1)
    OR EXISTS (SELECT 1 FROM OPENJSON(@OpenCcJson) WHERE [type] <> 1)
    OR EXISTS (SELECT 1 FROM OPENJSON(@SellerApprovalCcJson) WHERE [type] <> 1)
    OR EXISTS (SELECT 1 FROM OPENJSON(@BidDispositionCcJson) WHERE [type] <> 1)
    THROW 51106, 'SQMS recipient JSON arrays may contain string values only.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Workflows TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        BusinessObjectType nvarchar(100) NOT NULL
    );
    INSERT @Workflows VALUES
        (N'SqmsOpportunity', N'SQMS Opportunity', N'SqmsOpportunity'),
        (N'SqmsBid', N'SQMS Bid', N'SqmsBid');

    UPDATE definition
    SET DisplayName = expected.DisplayName,
        BusinessObjectType = expected.BusinessObjectType,
        IsActive = 1
    FROM [dbo].[WorkflowDefinition] AS definition
    INNER JOIN @Workflows AS expected ON expected.WorkflowKey = definition.WorkflowKey;

    INSERT [dbo].[WorkflowDefinition] (WorkflowKey, DisplayName, BusinessObjectType, IsActive)
    SELECT expected.WorkflowKey, expected.DisplayName, expected.BusinessObjectType, 1
    FROM @Workflows AS expected
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowDefinition] AS definition
        WHERE definition.WorkflowKey = expected.WorkflowKey
    );

    UPDATE version
    SET IsActive = CASE WHEN version.VersionNumber = 1 THEN 1 ELSE 0 END
    FROM [dbo].[WorkflowDefinitionVersion] AS version
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS expected ON expected.WorkflowKey = definition.WorkflowKey;

    INSERT [dbo].[WorkflowDefinitionVersion]
        (WorkflowDefinitionId, VersionNumber, IsActive, CreatedAtUtc)
    SELECT definition.Id, 1, 1, CONVERT(datetimeoffset, '2026-08-20T00:00:00+00:00')
    FROM [dbo].[WorkflowDefinition] AS definition
    INNER JOIN @Workflows AS expected ON expected.WorkflowKey = definition.WorkflowKey
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowDefinitionVersion] AS version
        WHERE version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    );

    DECLARE @States TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL,
        StateKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        IsTerminal bit NOT NULL,
        PRIMARY KEY (WorkflowKey, StateKey)
    );
    INSERT @States VALUES
        (N'SqmsOpportunity', N'Draft', N'Draft', 0),
        (N'SqmsOpportunity', N'SUBMITTED', N'Submitted', 0),
        (N'SqmsOpportunity', N'PENDING', N'Pending', 0),
        (N'SqmsOpportunity', N'OPEN TO BIDDING', N'Open to Bidding', 0),
        (N'SqmsOpportunity', N'CLOSED TO BIDDING', N'Closed to Bidding', 0),
        (N'SqmsOpportunity', N'PENDING SELLER APPROVAL', N'Pending Seller Approval', 0),
        (N'SqmsOpportunity', N'AWAITING ALLOCATION', N'Awaiting Allocation', 0),
        (N'SqmsOpportunity', N'PARTIALLY ALLOCATED', N'Partially Allocated', 0),
        (N'SqmsOpportunity', N'NOT FULFILLED', N'Not Fulfilled', 1),
        (N'SqmsOpportunity', N'COMPLETED', N'Completed', 1),
        (N'SqmsBid', N'Draft', N'Draft', 0),
        (N'SqmsBid', N'SUBMITTED', N'Submitted', 0),
        (N'SqmsBid', N'NOT ALLOCATED', N'Not Allocated', 1),
        (N'SqmsBid', N'IPG REVIEW', N'IPG Review', 0),
        (N'SqmsBid', N'UNDER REVIEW BY SELLER', N'Under Review by Seller', 0),
        (N'SqmsBid', N'ACCEPTED', N'Accepted', 1),
        (N'SqmsBid', N'PARTIALLY ACCEPTED', N'Partially Accepted', 1),
        (N'SqmsBid', N'NOT ACCEPTED', N'Not Accepted', 1);

    UPDATE stateDefinition
    SET DisplayName = expected.DisplayName, IsTerminal = expected.IsTerminal, IsActive = 1
    FROM [dbo].[WorkflowStateDefinition] AS stateDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = stateDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @States AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.StateKey = stateDefinition.StateKey
    WHERE version.VersionNumber = 1;

    INSERT [dbo].[WorkflowStateDefinition]
        (WorkflowDefinitionVersionId, StateKey, DisplayName, IsTerminal, IsActive)
    SELECT version.Id, expected.StateKey, expected.DisplayName, expected.IsTerminal, 1
    FROM @States AS expected
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowStateDefinition] AS existing
        WHERE existing.WorkflowDefinitionVersionId = version.Id AND existing.StateKey = expected.StateKey
    );

    UPDATE stateDefinition SET IsActive = 0
    FROM [dbo].[WorkflowStateDefinition] AS stateDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = stateDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM @States AS expected
          WHERE expected.WorkflowKey = definition.WorkflowKey AND expected.StateKey = stateDefinition.StateKey
      );

    DECLARE @Actions TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL,
        ActionKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        SortOrder int NOT NULL,
        PRIMARY KEY (WorkflowKey, ActionKey)
    );
    INSERT @Actions VALUES
        (N'SqmsOpportunity', N'SubmitOpportunity', N'Submit Opportunity', 10),
        (N'SqmsOpportunity', N'ReturnOpportunityToDraft', N'Return Opportunity to Draft', 20),
        (N'SqmsOpportunity', N'MarkOpportunityPending', N'Mark Opportunity Pending', 30),
        (N'SqmsOpportunity', N'OpenOpportunityForBidding', N'Open Opportunity for Bidding', 40),
        (N'SqmsOpportunity', N'CloseOpportunityBidding', N'Close Opportunity Bidding', 50),
        (N'SqmsOpportunity', N'RequestSellerApproval', N'Request Seller Approval', 60),
        (N'SqmsOpportunity', N'MarkOpportunityAwaitingAllocation', N'Mark Opportunity Awaiting Allocation', 70),
        (N'SqmsOpportunity', N'MarkOpportunityPartiallyAllocated', N'Mark Opportunity Partially Allocated', 80),
        (N'SqmsOpportunity', N'MarkOpportunityNotFulfilled', N'Mark Opportunity Not Fulfilled', 90),
        (N'SqmsOpportunity', N'CompleteOpportunity', N'Complete Opportunity', 100),
        (N'SqmsBid', N'SubmitBid', N'Submit Bid', 10),
        (N'SqmsBid', N'ReturnBidToDraft', N'Return Bid to Draft', 20),
        (N'SqmsBid', N'MarkBidNotAllocated', N'Mark Bid Not Allocated', 30),
        (N'SqmsBid', N'MoveBidToIpgReview', N'Move Bid to IPG Review', 40),
        (N'SqmsBid', N'SendBidToSellerReview', N'Send Bid to Seller Review', 50),
        (N'SqmsBid', N'AcceptBid', N'Accept Bid', 60),
        (N'SqmsBid', N'PartiallyAcceptBid', N'Partially Accept Bid', 70),
        (N'SqmsBid', N'RejectBid', N'Reject Bid', 80);

    UPDATE actionDefinition
    SET DisplayName = expected.DisplayName,
        SectionKey = CASE expected.WorkflowKey WHEN N'SqmsOpportunity' THEN N'Opportunity' ELSE N'Bid' END,
        ButtonKey = expected.ActionKey,
        ButtonLabel = expected.DisplayName,
        Intent = CASE
            WHEN expected.ActionKey LIKE N'Submit%' THEN N'Submit'
            WHEN expected.ActionKey LIKE N'Return%' THEN N'Return'
            WHEN expected.ActionKey LIKE N'%Complete%' THEN N'Complete'
            ELSE N'Approve'
        END,
        RequiresReason = 0,
        SortOrder = expected.SortOrder,
        IsActive = 1
    FROM [dbo].[WorkflowActionDefinition] AS actionDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = actionDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Actions AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.ActionKey = actionDefinition.ActionKey
    WHERE version.VersionNumber = 1;

    INSERT [dbo].[WorkflowActionDefinition]
        (WorkflowDefinitionVersionId, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder, IsActive)
    SELECT version.Id, expected.ActionKey, expected.DisplayName,
        CASE expected.WorkflowKey WHEN N'SqmsOpportunity' THEN N'Opportunity' ELSE N'Bid' END,
        expected.ActionKey, expected.DisplayName,
        CASE
            WHEN expected.ActionKey LIKE N'Submit%' THEN N'Submit'
            WHEN expected.ActionKey LIKE N'Return%' THEN N'Return'
            WHEN expected.ActionKey LIKE N'%Complete%' THEN N'Complete'
            ELSE N'Approve'
        END,
        0, expected.SortOrder, 1
    FROM @Actions AS expected
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowActionDefinition] AS existing
        WHERE existing.WorkflowDefinitionVersionId = version.Id AND existing.ActionKey = expected.ActionKey
    );

    UPDATE actionDefinition SET IsActive = 0
    FROM [dbo].[WorkflowActionDefinition] AS actionDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = actionDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM @Actions AS expected
          WHERE expected.WorkflowKey = definition.WorkflowKey AND expected.ActionKey = actionDefinition.ActionKey
      );

    DECLARE @Roles TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL,
        RoleKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        PRIMARY KEY (WorkflowKey, RoleKey)
    );
    INSERT @Roles VALUES
        (N'SqmsOpportunity', N'MFO', N'MFO'),
        (N'SqmsOpportunity', N'IPG', N'IPG'),
        (N'SqmsOpportunity', N'System', N'System'),
        (N'SqmsBid', N'MFO', N'MFO'),
        (N'SqmsBid', N'IPG', N'IPG'),
        (N'SqmsBid', N'System', N'System'),
        (N'SqmsBid', N'Seller', N'Seller');

    UPDATE roleDefinition
    SET DisplayName = expected.DisplayName, IsActive = 1
    FROM [dbo].[WorkflowRoleDefinition] AS roleDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = roleDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Roles AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.RoleKey = roleDefinition.RoleKey
    WHERE version.VersionNumber = 1;

    INSERT [dbo].[WorkflowRoleDefinition]
        (WorkflowDefinitionVersionId, RoleKey, DisplayName, IsActive)
    SELECT version.Id, expected.RoleKey, expected.DisplayName, 1
    FROM @Roles AS expected
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowRoleDefinition] AS existing
        WHERE existing.WorkflowDefinitionVersionId = version.Id AND existing.RoleKey = expected.RoleKey
    );

    UPDATE roleDefinition SET IsActive = 0
    FROM [dbo].[WorkflowRoleDefinition] AS roleDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = roleDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM @Roles AS expected
          WHERE expected.WorkflowKey = definition.WorkflowKey AND expected.RoleKey = roleDefinition.RoleKey
      );

    DECLARE @CrLf nchar(2) = NCHAR(13) + NCHAR(10);
    DECLARE @Routes TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL,
        RouteKey nvarchar(100) NOT NULL,
        SubjectTemplate nvarchar(300) NOT NULL,
        BodyTemplate nvarchar(max) NOT NULL,
        HtmlBodyTemplate nvarchar(max) NOT NULL,
        PRIMARY KEY (WorkflowKey, RouteKey)
    );
    INSERT @Routes VALUES
        (N'SqmsOpportunity', N'SqmsOpportunityOpen',
         N'{{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}',
         CONCAT(
             N'Please click the following link(s) to review secondary sales opportunities for this quarter.', @CrLf, @CrLf,
             N'View all Opportunities open for Bidding: {{dashboardUrl}}'),
         N'<p>Please click the following link(s) to review secondary sales opportunities for this quarter.</p><p><a href="{{dashboardUrl}}">View all Opportunities open for Bidding</a></p>'),
        (N'SqmsOpportunity', N'SqmsOpportunityReminder',
         N'REMINDER {{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDate}}',
         CONCAT(
             N'Please click the following link(s) to review secondary sales opportunities for this quarter.', @CrLf, @CrLf,
             N'View all Opportunities open for Bidding: {{dashboardUrl}}'),
         N'<p>Please click the following link(s) to review secondary sales opportunities for this quarter.</p><p><a href="{{dashboardUrl}}">View all Opportunities open for Bidding</a></p>'),
        (N'SqmsOpportunity', N'SqmsOpportunityPendingSellerApproval',
         N'{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{accountNumber}} - {{accountName}}: Response Required by {{sellerResponseDate}}',
         CONCAT(
             N'Please click the following link to review secondary sale bids for the following opportunity.', @CrLf, @CrLf,
             N'Account Number: {{accountNumber}}', @CrLf,
             N'Account Name: {{accountName}}', @CrLf,
             N'Name of Fund: {{fund}}', @CrLf,
             N'Name of Vehicle: {{vehicle}}', @CrLf,
             N'Total Amount Requested to Be Sold: {{saleAmount}}', @CrLf, @CrLf,
             N'Link to approve or reject bids received: {{opportunityUrl}}'),
         N'<p>Please click the following link to review secondary sale bids for the following opportunity.</p><p>Account Number: {{accountNumber}}<br>Account Name: {{accountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Sold: {{saleAmount}}</p><p>Link to approve or reject bids received: <a href="{{opportunityUrl}}">Click Here</a></p>'),
        (N'SqmsBid', N'SqmsBidAccepted',
         N'{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{buyerAccountNumber}} - {{buyerAccountName}}',
         CONCAT(
             N'The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
             N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', @CrLf, @CrLf,
             N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.', @CrLf, @CrLf,
             N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
             N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
             N'Name of Fund: {{fund}}', @CrLf,
             N'Name of Vehicle: {{vehicle}}', @CrLf,
             N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
             N'Link to View: {{bidUrl}}'),
         N'<p>The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.</p><p>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>'),
        (N'SqmsBid', N'SqmsBidPartiallyAccepted',
         N'{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{buyerAccountNumber}} - {{buyerAccountName}}',
         CONCAT(
             N'The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
             N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', @CrLf, @CrLf,
             N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.', @CrLf, @CrLf,
             N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
             N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
             N'Name of Fund: {{fund}}', @CrLf,
             N'Name of Vehicle: {{vehicle}}', @CrLf,
             N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
             N'Link to View: {{bidUrl}}'),
         N'<p>The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.</p><p>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to effect this sale.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>'),
        (N'SqmsBid', N'SqmsBidNotAccepted',
         N'{{quarter}} {{year}} Secondary Sale Bids - {{vehicle}}/{{buyerAccountNumber}} - {{buyerAccountName}}',
         CONCAT(
             N'The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDate}}.', @CrLf, @CrLf,
             N'Buyer Account Number: {{buyerAccountNumber}}', @CrLf,
             N'Buyer Account Name: {{buyerAccountName}}', @CrLf,
             N'Name of Fund: {{fund}}', @CrLf,
             N'Name of Vehicle: {{vehicle}}', @CrLf,
             N'Total Amount Requested to Be Purchased: {{requestedAmount}}', @CrLf, @CrLf,
             N'Link to View: {{bidUrl}}'),
         N'<p>The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDate}}.</p><p>Buyer Account Number: {{buyerAccountNumber}}<br>Buyer Account Name: {{buyerAccountName}}<br>Name of Fund: {{fund}}<br>Name of Vehicle: {{vehicle}}<br>Total Amount Requested to Be Purchased: {{requestedAmount}}</p><p>Link to View: <a href="{{bidUrl}}">Click Here</a></p>');

    UPDATE route
    SET NotificationType = expected.RouteKey,
        SubjectTemplate = expected.SubjectTemplate,
        BodyTemplate = expected.BodyTemplate,
        HtmlBodyTemplate = expected.HtmlBodyTemplate,
        OverdueSubjectPrefix = N'',
        IsActive = 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Routes AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.RouteKey = route.RouteKey
    WHERE version.VersionNumber = 1;

    INSERT [dbo].[NotificationRoute]
        (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT version.Id, expected.RouteKey, expected.RouteKey, expected.SubjectTemplate,
        expected.BodyTemplate, expected.HtmlBodyTemplate, N'', 1
    FROM @Routes AS expected
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[NotificationRoute] AS existing
        WHERE existing.WorkflowDefinitionVersionId = version.Id AND existing.RouteKey = expected.RouteKey
    );

    UPDATE route SET IsActive = 0
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM @Routes AS expected
          WHERE expected.WorkflowKey = definition.WorkflowKey AND expected.RouteKey = route.RouteKey
      );

    DECLARE @Transitions TABLE
    (
        WorkflowKey nvarchar(100) NOT NULL,
        FromStateKey nvarchar(100) NOT NULL,
        ActionKey nvarchar(100) NOT NULL,
        AllowedRoleKey nvarchar(100) NOT NULL,
        ToStateKey nvarchar(100) NOT NULL,
        NotificationRouteKey nvarchar(100) NULL,
        PRIMARY KEY (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey)
    );
    INSERT @Transitions VALUES
        (N'SqmsOpportunity', N'Draft', N'SubmitOpportunity', N'MFO', N'SUBMITTED', NULL),
        (N'SqmsOpportunity', N'SUBMITTED', N'ReturnOpportunityToDraft', N'IPG', N'Draft', NULL),
        (N'SqmsOpportunity', N'SUBMITTED', N'MarkOpportunityPending', N'IPG', N'PENDING', NULL),
        (N'SqmsOpportunity', N'PENDING', N'ReturnOpportunityToDraft', N'IPG', N'Draft', NULL),
        (N'SqmsOpportunity', N'PENDING', N'OpenOpportunityForBidding', N'IPG', N'OPEN TO BIDDING', NULL),
        (N'SqmsOpportunity', N'OPEN TO BIDDING', N'CloseOpportunityBidding', N'System', N'CLOSED TO BIDDING', NULL),
        (N'SqmsOpportunity', N'CLOSED TO BIDDING', N'RequestSellerApproval', N'IPG', N'PENDING SELLER APPROVAL', N'SqmsOpportunityPendingSellerApproval'),
        (N'SqmsOpportunity', N'PENDING SELLER APPROVAL', N'MarkOpportunityAwaitingAllocation', N'IPG', N'AWAITING ALLOCATION', NULL),
        (N'SqmsOpportunity', N'PENDING SELLER APPROVAL', N'MarkOpportunityPartiallyAllocated', N'IPG', N'PARTIALLY ALLOCATED', NULL),
        (N'SqmsOpportunity', N'PENDING SELLER APPROVAL', N'MarkOpportunityNotFulfilled', N'IPG', N'NOT FULFILLED', NULL),
        (N'SqmsOpportunity', N'AWAITING ALLOCATION', N'CompleteOpportunity', N'IPG', N'COMPLETED', NULL),
        (N'SqmsOpportunity', N'PARTIALLY ALLOCATED', N'CompleteOpportunity', N'IPG', N'COMPLETED', NULL),
        (N'SqmsBid', N'Draft', N'SubmitBid', N'MFO', N'SUBMITTED', NULL),
        (N'SqmsBid', N'SUBMITTED', N'ReturnBidToDraft', N'IPG', N'Draft', NULL),
        (N'SqmsBid', N'SUBMITTED', N'MarkBidNotAllocated', N'System', N'NOT ALLOCATED', NULL),
        (N'SqmsBid', N'SUBMITTED', N'MoveBidToIpgReview', N'System', N'IPG REVIEW', NULL),
        (N'SqmsBid', N'IPG REVIEW', N'SendBidToSellerReview', N'IPG', N'UNDER REVIEW BY SELLER', NULL),
        (N'SqmsBid', N'UNDER REVIEW BY SELLER', N'AcceptBid', N'Seller', N'ACCEPTED', N'SqmsBidAccepted'),
        (N'SqmsBid', N'UNDER REVIEW BY SELLER', N'PartiallyAcceptBid', N'Seller', N'PARTIALLY ACCEPTED', N'SqmsBidPartiallyAccepted'),
        (N'SqmsBid', N'UNDER REVIEW BY SELLER', N'RejectBid', N'Seller', N'NOT ACCEPTED', N'SqmsBidNotAccepted');

    UPDATE transitionDefinition
    SET ToStateKey = expected.ToStateKey,
        TaskToCloseKey = NULL,
        TaskToCreateKey = NULL,
        NotificationRouteKey = expected.NotificationRouteKey,
        AuditLabel = expected.ActionKey,
        IsActive = 1
    FROM [dbo].[WorkflowTransitionDefinition] AS transitionDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Transitions AS expected
        ON expected.WorkflowKey = definition.WorkflowKey
       AND expected.FromStateKey = transitionDefinition.FromStateKey
       AND expected.ActionKey = transitionDefinition.ActionKey
       AND expected.AllowedRoleKey = transitionDefinition.AllowedRoleKey
    WHERE version.VersionNumber = 1;

    INSERT [dbo].[WorkflowTransitionDefinition]
        (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey,
         TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive)
    SELECT version.Id, expected.FromStateKey, expected.ActionKey, expected.AllowedRoleKey,
        expected.ToStateKey, NULL, NULL, expected.NotificationRouteKey, expected.ActionKey, 1
    FROM @Transitions AS expected
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM [dbo].[WorkflowTransitionDefinition] AS existing
        WHERE existing.WorkflowDefinitionVersionId = version.Id
          AND existing.FromStateKey = expected.FromStateKey
          AND existing.ActionKey = expected.ActionKey
          AND existing.AllowedRoleKey = expected.AllowedRoleKey
    );

    UPDATE transitionDefinition SET IsActive = 0
    FROM [dbo].[WorkflowTransitionDefinition] AS transitionDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM @Transitions AS expected
          WHERE expected.WorkflowKey = definition.WorkflowKey
            AND expected.FromStateKey = transitionDefinition.FromStateKey
            AND expected.ActionKey = transitionDefinition.ActionKey
            AND expected.AllowedRoleKey = transitionDefinition.AllowedRoleKey
      );

    UPDATE taskDefinition SET IsActive = 0
    FROM [dbo].[WorkflowTaskDefinition] AS taskDefinition
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = taskDefinition.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey;

    UPDATE policy SET IsActive = 0
    FROM [dbo].[SlaPolicy] AS policy
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = policy.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey;

    DELETE recipientRule
    FROM [dbo].[NotificationRecipientRule] AS recipientRule
    INNER JOIN [dbo].[NotificationRoute] AS route ON route.Id = recipientRule.NotificationRouteId
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN @Workflows AS workflow ON workflow.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1;

    DECLARE @OpenToCount int = (SELECT COUNT(*) FROM OPENJSON(@OpenToJson));
    DECLARE @OpenCcCount int = (SELECT COUNT(*) FROM OPENJSON(@OpenCcJson));

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'To', N'StaticAddress', NULL, jsonAddress.[value], NULL,
        CONVERT(int, jsonAddress.[key]) + 1, 1, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS APPLY OPENJSON(@OpenToJson) AS jsonAddress
    WHERE definition.WorkflowKey = N'SqmsOpportunity'
      AND version.VersionNumber = 1
      AND route.RouteKey IN (N'SqmsOpportunityOpen', N'SqmsOpportunityReminder');

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'Cc', N'StaticAddress', NULL, jsonAddress.[value], NULL,
        @OpenToCount + CONVERT(int, jsonAddress.[key]) + 1, 0, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS APPLY OPENJSON(@OpenCcJson) AS jsonAddress
    WHERE definition.WorkflowKey = N'SqmsOpportunity'
      AND version.VersionNumber = 1
      AND route.RouteKey IN (N'SqmsOpportunityOpen', N'SqmsOpportunityReminder');

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'To', N'RequestInput', source.SourceKey, NULL, NULL, source.SortOrder, 1, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS JOIN (VALUES (N'relationshipManager', 1), (N'sellerRmTeam', 2)) AS source(SourceKey, SortOrder)
    WHERE definition.WorkflowKey = N'SqmsOpportunity'
      AND version.VersionNumber = 1
      AND route.RouteKey = N'SqmsOpportunityPendingSellerApproval';

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'Cc', N'StaticAddress', NULL, jsonAddress.[value], NULL,
        CONVERT(int, jsonAddress.[key]) + 3, 0, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS APPLY OPENJSON(@SellerApprovalCcJson) AS jsonAddress
    WHERE definition.WorkflowKey = N'SqmsOpportunity'
      AND version.VersionNumber = 1
      AND route.RouteKey = N'SqmsOpportunityPendingSellerApproval';

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'To', N'RequestInput', source.SourceKey, NULL, NULL, source.SortOrder, 1, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS JOIN (VALUES (N'relationshipManager', 1), (N'buyerRmTeam', 2)) AS source(SourceKey, SortOrder)
    WHERE definition.WorkflowKey = N'SqmsBid'
      AND version.VersionNumber = 1
      AND route.RouteKey IN (N'SqmsBidAccepted', N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted');

    INSERT [dbo].[NotificationRecipientRule]
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, N'Cc', N'StaticAddress', NULL, jsonAddress.[value], NULL,
        CONVERT(int, jsonAddress.[key]) + 3, 0, 1
    FROM [dbo].[NotificationRoute] AS route
    INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN [dbo].[WorkflowDefinition] AS definition ON definition.Id = version.WorkflowDefinitionId
    CROSS APPLY OPENJSON(@BidDispositionCcJson) AS jsonAddress
    WHERE definition.WorkflowKey = N'SqmsBid'
      AND version.VersionNumber = 1
      AND route.RouteKey IN (N'SqmsBidAccepted', N'SqmsBidPartiallyAccepted', N'SqmsBidNotAccepted');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
