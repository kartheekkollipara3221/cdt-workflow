SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.WorkflowDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowDefinitionVersion', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowStateDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowActionDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowRoleDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowTransitionDefinition', N'U') IS NULL
BEGIN
    THROW 51000, 'Apply the workflow-engine EF migrations before running the SQMS seed.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE #DefinitionSeed
    (
        WorkflowKey nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        BusinessObjectType nvarchar(100) NOT NULL
    );

    INSERT #DefinitionSeed (WorkflowKey, DisplayName, BusinessObjectType)
    VALUES
        (N'SqmsOpportunity', N'SQMS Opportunity', N'SqmsOpportunity'),
        (N'SqmsBid', N'SQMS Bid', N'SqmsBid');

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowDefinition AS actual
        INNER JOIN #DefinitionSeed AS expected ON expected.WorkflowKey = actual.WorkflowKey
        WHERE actual.BusinessObjectType <> expected.BusinessObjectType
    )
    BEGIN
        THROW 51001, 'An SQMS workflow key already exists with a different BusinessObjectType.', 1;
    END;

    INSERT dbo.WorkflowDefinition (WorkflowKey, DisplayName, BusinessObjectType, IsActive)
    SELECT expected.WorkflowKey, expected.DisplayName, expected.BusinessObjectType, CAST(1 AS bit)
    FROM #DefinitionSeed AS expected
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowDefinition AS actual
        WHERE actual.WorkflowKey = expected.WorkflowKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        IsActive = 1
    FROM dbo.WorkflowDefinition AS actual
    INNER JOIN #DefinitionSeed AS expected ON expected.WorkflowKey = actual.WorkflowKey;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowDefinitionVersion AS version
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #DefinitionSeed AS expected ON expected.WorkflowKey = definition.WorkflowKey
        WHERE version.IsActive = 1
          AND version.VersionNumber <> 1
    )
    BEGIN
        THROW 51002, 'An SQMS workflow already has an active version other than version 1.', 1;
    END;

    INSERT dbo.WorkflowDefinitionVersion
        (WorkflowDefinitionId, VersionNumber, IsActive, CreatedAtUtc)
    SELECT definition.Id, 1, CAST(1 AS bit), SYSDATETIMEOFFSET()
    FROM dbo.WorkflowDefinition AS definition
    INNER JOIN #DefinitionSeed AS expected ON expected.WorkflowKey = definition.WorkflowKey
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowDefinitionVersion AS version
        WHERE version.WorkflowDefinitionId = definition.Id
          AND version.VersionNumber = 1
    );

    UPDATE version
    SET IsActive = 1
    FROM dbo.WorkflowDefinitionVersion AS version
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #DefinitionSeed AS expected ON expected.WorkflowKey = definition.WorkflowKey
    WHERE version.VersionNumber = 1;

    CREATE TABLE #StateSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        StateKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        IsTerminal bit NOT NULL,
        PRIMARY KEY (WorkflowKey, StateKey)
    );

    INSERT #StateSeed (WorkflowKey, StateKey, DisplayName, IsTerminal)
    VALUES
        (N'SqmsOpportunity', N'Draft', N'Draft', 0),
        (N'SqmsOpportunity', N'Submitted', N'Submitted', 0),
        (N'SqmsOpportunity', N'Pending', N'Pending', 0),
        (N'SqmsOpportunity', N'OpenToBidding', N'Open to Bidding', 0),
        (N'SqmsOpportunity', N'ClosedToBidding', N'Closed to Bidding', 0),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'Pending Seller Approval', 0),
        (N'SqmsOpportunity', N'AwaitingAllocation', N'Awaiting Allocation', 0),
        (N'SqmsOpportunity', N'PartiallyAllocated', N'Partially Allocated', 0),
        (N'SqmsOpportunity', N'NotFulfilled', N'Not Fulfilled', 1),
        (N'SqmsOpportunity', N'Completed', N'Completed', 1),
        (N'SqmsOpportunity', N'Cancelled', N'Cancelled', 1),
        (N'SqmsBid', N'Draft', N'Draft', 0),
        (N'SqmsBid', N'Submitted', N'Submitted', 0),
        (N'SqmsBid', N'NotAllocated', N'Not Allocated', 1),
        (N'SqmsBid', N'IpgReview', N'IPG Review', 0),
        (N'SqmsBid', N'UnderReviewBySeller', N'Under Review by Seller', 0),
        (N'SqmsBid', N'Accepted', N'Accepted', 1),
        (N'SqmsBid', N'PartiallyAccepted', N'Partially Accepted', 1),
        (N'SqmsBid', N'NotAccepted', N'Not Accepted', 1);

    CREATE TABLE #RoleSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        RoleKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        PRIMARY KEY (WorkflowKey, RoleKey)
    );

    INSERT #RoleSeed (WorkflowKey, RoleKey, DisplayName)
    VALUES
        (N'SqmsOpportunity', N'MFO', N'MFO'),
        (N'SqmsOpportunity', N'IPG', N'IPG'),
        (N'SqmsOpportunity', N'System', N'System'),
        (N'SqmsBid', N'MFO', N'MFO'),
        (N'SqmsBid', N'IPG', N'IPG'),
        (N'SqmsBid', N'System', N'System');

    CREATE TABLE #ActionSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        ActionKey nvarchar(100) NOT NULL,
        DisplayName nvarchar(200) NOT NULL,
        SectionKey nvarchar(100) NOT NULL,
        ButtonKey nvarchar(100) NOT NULL,
        ButtonLabel nvarchar(200) NOT NULL,
        Intent nvarchar(100) NOT NULL,
        RequiresReason bit NOT NULL,
        SortOrder int NOT NULL,
        PRIMARY KEY (WorkflowKey, ActionKey)
    );

    INSERT #ActionSeed
        (WorkflowKey, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder)
    VALUES
        (N'SqmsOpportunity', N'SubmitOpportunity', N'Submit Opportunity', N'Opportunity', N'OpportunitySubmit', N'Submit', N'Submit', 0, 10),
        (N'SqmsOpportunity', N'ReturnOpportunityToDraft', N'Return Opportunity to Draft', N'Opportunity', N'OpportunityReturnDraft', N'Return to Draft', N'Return', 0, 20),
        (N'SqmsOpportunity', N'MarkOpportunityPending', N'Mark Opportunity Pending', N'Opportunity', N'OpportunityPending', N'Mark Pending', N'Advance', 0, 30),
        (N'SqmsOpportunity', N'OpenOpportunityForBidding', N'Open Opportunity for Bidding', N'Opportunity', N'OpportunityOpenBidding', N'Open to Bidding', N'Advance', 0, 40),
        (N'SqmsOpportunity', N'CloseOpportunityBidding', N'Close Opportunity Bidding', N'Opportunity', N'OpportunityCloseBidding', N'Close Bidding', N'Advance', 0, 50),
        (N'SqmsOpportunity', N'RequestSellerApproval', N'Request Seller Approval', N'Opportunity', N'OpportunitySellerApproval', N'Request Seller Approval', N'Advance', 0, 60),
        (N'SqmsOpportunity', N'MarkOpportunityAwaitingAllocation', N'Mark Opportunity Awaiting Allocation', N'Opportunity', N'OpportunityAwaitingAllocation', N'Awaiting Allocation', N'Advance', 0, 70),
        (N'SqmsOpportunity', N'MarkOpportunityPartiallyAllocated', N'Mark Opportunity Partially Allocated', N'Opportunity', N'OpportunityPartialAllocation', N'Partially Allocated', N'Advance', 0, 80),
        (N'SqmsOpportunity', N'MarkOpportunityNotFulfilled', N'Mark Opportunity Not Fulfilled', N'Opportunity', N'OpportunityNotFulfilled', N'Not Fulfilled', N'Complete', 0, 90),
        (N'SqmsOpportunity', N'CompleteOpportunity', N'Complete Opportunity', N'Opportunity', N'OpportunityComplete', N'Complete', N'Complete', 0, 100),
        (N'SqmsOpportunity', N'CancelOpportunity', N'Cancel Opportunity', N'Opportunity', N'OpportunityCancel', N'Cancel', N'Complete', 0, 110),
        (N'SqmsBid', N'SubmitBid', N'Submit Bid', N'Bid', N'BidSubmit', N'Submit', N'Submit', 0, 10),
        (N'SqmsBid', N'MarkBidNotAllocated', N'Mark Bid Not Allocated', N'Bid', N'BidNotAllocated', N'Not Allocated', N'Complete', 0, 20),
        (N'SqmsBid', N'MoveBidToIpgReview', N'Move Bid to IPG Review', N'Bid', N'BidIpgReview', N'IPG Review', N'Advance', 0, 30),
        (N'SqmsBid', N'SendBidToSellerReview', N'Send Bid to Seller Review', N'Bid', N'BidSellerReview', N'Seller Review', N'Advance', 0, 40),
        (N'SqmsBid', N'AcceptBid', N'Accept Bid', N'Bid', N'BidAccept', N'Accept Full Bid', N'Complete', 0, 50),
        (N'SqmsBid', N'PartiallyAcceptBid', N'Partially Accept Bid', N'Bid', N'BidPartialAccept', N'Accept Partial Bid', N'Complete', 0, 60),
        (N'SqmsBid', N'RejectBid', N'Do Not Accept Bid', N'Bid', N'BidReject', N'Do Not Accept Bid', N'Complete', 0, 70);

    INSERT dbo.WorkflowStateDefinition
        (WorkflowDefinitionVersionId, StateKey, DisplayName, IsTerminal, IsActive)
    SELECT version.Id, expected.StateKey, expected.DisplayName, expected.IsTerminal, CAST(1 AS bit)
    FROM #StateSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowStateDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.StateKey = expected.StateKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        IsTerminal = expected.IsTerminal,
        IsActive = 1
    FROM dbo.WorkflowStateDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #StateSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.StateKey = actual.StateKey
    WHERE version.VersionNumber = 1;

    INSERT dbo.WorkflowRoleDefinition
        (WorkflowDefinitionVersionId, RoleKey, DisplayName, IsActive)
    SELECT version.Id, expected.RoleKey, expected.DisplayName, CAST(1 AS bit)
    FROM #RoleSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowRoleDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.RoleKey = expected.RoleKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        IsActive = 1
    FROM dbo.WorkflowRoleDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #RoleSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.RoleKey = actual.RoleKey
    WHERE version.VersionNumber = 1;

    INSERT dbo.WorkflowActionDefinition
        (WorkflowDefinitionVersionId, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder, IsActive)
    SELECT version.Id, expected.ActionKey, expected.DisplayName, expected.SectionKey,
        expected.ButtonKey, expected.ButtonLabel, expected.Intent, expected.RequiresReason,
        expected.SortOrder, CAST(1 AS bit)
    FROM #ActionSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowActionDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.ActionKey = expected.ActionKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        SectionKey = expected.SectionKey,
        ButtonKey = expected.ButtonKey,
        ButtonLabel = expected.ButtonLabel,
        Intent = expected.Intent,
        RequiresReason = expected.RequiresReason,
        SortOrder = expected.SortOrder,
        IsActive = 1
    FROM dbo.WorkflowActionDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #ActionSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.ActionKey = actual.ActionKey
    WHERE version.VersionNumber = 1;

    IF EXISTS
    (
        SELECT definition.WorkflowKey, actual.StateKey
        FROM dbo.WorkflowStateDefinition AS actual
        INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #DefinitionSeed AS scoped ON scoped.WorkflowKey = definition.WorkflowKey
        WHERE version.VersionNumber = 1 AND actual.IsActive = 1
        EXCEPT
        SELECT WorkflowKey, StateKey FROM #StateSeed
    )
        THROW 51003, 'An SQMS workflow contains an unexpected active state.', 1;

    IF EXISTS
    (
        SELECT definition.WorkflowKey, actual.RoleKey
        FROM dbo.WorkflowRoleDefinition AS actual
        INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #DefinitionSeed AS scoped ON scoped.WorkflowKey = definition.WorkflowKey
        WHERE version.VersionNumber = 1 AND actual.IsActive = 1
        EXCEPT
        SELECT WorkflowKey, RoleKey FROM #RoleSeed
    )
        THROW 51004, 'An SQMS workflow contains an unexpected active role.', 1;

    IF EXISTS
    (
        SELECT definition.WorkflowKey, actual.ActionKey
        FROM dbo.WorkflowActionDefinition AS actual
        INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #DefinitionSeed AS scoped ON scoped.WorkflowKey = definition.WorkflowKey
        WHERE version.VersionNumber = 1 AND actual.IsActive = 1
        EXCEPT
        SELECT WorkflowKey, ActionKey FROM #ActionSeed
    )
        THROW 51005, 'An SQMS workflow contains an unexpected active action.', 1;

    CREATE TABLE #TransitionSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        FromStateKey nvarchar(100) NOT NULL,
        ActionKey nvarchar(100) NOT NULL,
        AllowedRoleKey nvarchar(100) NOT NULL,
        ToStateKey nvarchar(100) NOT NULL,
        AuditLabel nvarchar(200) NOT NULL,
        PRIMARY KEY (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey)
    );

    INSERT #TransitionSeed
        (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey, AuditLabel)
    VALUES
        (N'SqmsOpportunity', N'Draft', N'SubmitOpportunity', N'MFO', N'Submitted', N'MFO submitted opportunity'),
        (N'SqmsOpportunity', N'Submitted', N'ReturnOpportunityToDraft', N'IPG', N'Draft', N'IPG returned opportunity to draft'),
        (N'SqmsOpportunity', N'Submitted', N'MarkOpportunityPending', N'IPG', N'Pending', N'IPG marked opportunity pending'),
        (N'SqmsOpportunity', N'Pending', N'ReturnOpportunityToDraft', N'IPG', N'Draft', N'IPG returned pending opportunity to draft'),
        (N'SqmsOpportunity', N'Pending', N'OpenOpportunityForBidding', N'IPG', N'OpenToBidding', N'IPG opened opportunity for bidding'),
        (N'SqmsOpportunity', N'OpenToBidding', N'CloseOpportunityBidding', N'System', N'ClosedToBidding', N'System closed opportunity bidding'),
        (N'SqmsOpportunity', N'ClosedToBidding', N'RequestSellerApproval', N'IPG', N'PendingSellerApproval', N'IPG requested seller approval'),
        (N'SqmsOpportunity', N'ClosedToBidding', N'MarkOpportunityNotFulfilled', N'System', N'NotFulfilled', N'System marked opportunity not fulfilled'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityAwaitingAllocation', N'IPG', N'AwaitingAllocation', N'IPG marked opportunity awaiting allocation'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityAwaitingAllocation', N'System', N'AwaitingAllocation', N'System marked opportunity awaiting allocation'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityPartiallyAllocated', N'IPG', N'PartiallyAllocated', N'IPG marked opportunity partially allocated'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityPartiallyAllocated', N'System', N'PartiallyAllocated', N'System marked opportunity partially allocated'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityNotFulfilled', N'System', N'NotFulfilled', N'System marked opportunity not fulfilled'),
        (N'SqmsOpportunity', N'AwaitingAllocation', N'CompleteOpportunity', N'IPG', N'Completed', N'IPG completed opportunity'),
        (N'SqmsOpportunity', N'PartiallyAllocated', N'CompleteOpportunity', N'IPG', N'Completed', N'IPG completed partially allocated opportunity'),
        (N'SqmsOpportunity', N'Draft', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled draft opportunity'),
        (N'SqmsOpportunity', N'Draft', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled draft opportunity'),
        (N'SqmsOpportunity', N'Submitted', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled submitted opportunity'),
        (N'SqmsOpportunity', N'Submitted', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled submitted opportunity'),
        (N'SqmsOpportunity', N'Pending', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled pending opportunity'),
        (N'SqmsOpportunity', N'Pending', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled pending opportunity'),
        (N'SqmsOpportunity', N'OpenToBidding', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled open opportunity'),
        (N'SqmsOpportunity', N'OpenToBidding', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled open opportunity'),
        (N'SqmsOpportunity', N'ClosedToBidding', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled closed opportunity'),
        (N'SqmsOpportunity', N'ClosedToBidding', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled closed opportunity'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled opportunity pending seller approval'),
        (N'SqmsOpportunity', N'PendingSellerApproval', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled opportunity pending seller approval'),
        (N'SqmsOpportunity', N'AwaitingAllocation', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled opportunity awaiting allocation'),
        (N'SqmsOpportunity', N'AwaitingAllocation', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled opportunity awaiting allocation'),
        (N'SqmsOpportunity', N'PartiallyAllocated', N'CancelOpportunity', N'IPG', N'Cancelled', N'IPG cancelled partially allocated opportunity'),
        (N'SqmsOpportunity', N'PartiallyAllocated', N'CancelOpportunity', N'System', N'Cancelled', N'System cancelled partially allocated opportunity'),
        (N'SqmsBid', N'Draft', N'SubmitBid', N'MFO', N'Submitted', N'MFO submitted bid'),
        (N'SqmsBid', N'Submitted', N'MarkBidNotAllocated', N'IPG', N'NotAllocated', N'IPG marked bid not allocated'),
        (N'SqmsBid', N'Submitted', N'MarkBidNotAllocated', N'System', N'NotAllocated', N'System marked bid not allocated'),
        (N'SqmsBid', N'Submitted', N'MoveBidToIpgReview', N'IPG', N'IpgReview', N'IPG moved bid to review'),
        (N'SqmsBid', N'Submitted', N'MoveBidToIpgReview', N'System', N'IpgReview', N'System moved bid to IPG review'),
        (N'SqmsBid', N'IpgReview', N'SendBidToSellerReview', N'IPG', N'UnderReviewBySeller', N'IPG sent bid to seller review'),
        (N'SqmsBid', N'UnderReviewBySeller', N'AcceptBid', N'MFO', N'Accepted', N'MFO accepted full bid'),
        (N'SqmsBid', N'UnderReviewBySeller', N'PartiallyAcceptBid', N'MFO', N'PartiallyAccepted', N'MFO partially accepted bid'),
        (N'SqmsBid', N'UnderReviewBySeller', N'RejectBid', N'MFO', N'NotAccepted', N'MFO did not accept bid');

    UPDATE actual
    SET IsActive = 0
    FROM dbo.WorkflowTransitionDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey = N'SqmsOpportunity'
      AND version.VersionNumber = 1
      AND actual.ActionKey = N'CancelOpportunity'
      AND actual.AllowedRoleKey = N'MFO'
      AND actual.ToStateKey = N'Cancelled'
      AND actual.FromStateKey IN
          (N'Draft', N'Submitted', N'Pending', N'OpenToBidding', N'ClosedToBidding',
           N'PendingSellerApproval', N'AwaitingAllocation', N'PartiallyAllocated');

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowTransitionDefinition AS actual
        INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #TransitionSeed AS expected
            ON expected.WorkflowKey = definition.WorkflowKey
            AND expected.FromStateKey = actual.FromStateKey
            AND expected.ActionKey = actual.ActionKey
            AND expected.AllowedRoleKey = actual.AllowedRoleKey
        WHERE version.VersionNumber = 1
          AND actual.ToStateKey <> expected.ToStateKey
    )
        THROW 51006, 'An existing SQMS transition has a conflicting destination state.', 1;

    INSERT dbo.WorkflowTransitionDefinition
        (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey,
         TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive)
    SELECT version.Id, expected.FromStateKey, expected.ActionKey, expected.AllowedRoleKey,
        expected.ToStateKey, NULL, NULL, NULL, expected.AuditLabel, CAST(1 AS bit)
    FROM #TransitionSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowTransitionDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.FromStateKey = expected.FromStateKey
          AND actual.ActionKey = expected.ActionKey
          AND actual.AllowedRoleKey = expected.AllowedRoleKey
    );

    UPDATE actual
    SET AuditLabel = expected.AuditLabel,
        IsActive = 1
    FROM dbo.WorkflowTransitionDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #TransitionSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey
        AND expected.FromStateKey = actual.FromStateKey
        AND expected.ActionKey = actual.ActionKey
        AND expected.AllowedRoleKey = actual.AllowedRoleKey
    WHERE version.VersionNumber = 1;

    IF EXISTS
    (
        SELECT definition.WorkflowKey, actual.FromStateKey, actual.ActionKey,
            actual.AllowedRoleKey, actual.ToStateKey
        FROM dbo.WorkflowTransitionDefinition AS actual
        INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
        INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
        INNER JOIN #DefinitionSeed AS scoped ON scoped.WorkflowKey = definition.WorkflowKey
        WHERE version.VersionNumber = 1 AND actual.IsActive = 1
        EXCEPT
        SELECT WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey
        FROM #TransitionSeed
    )
        THROW 51007, 'An SQMS workflow contains an unexpected active transition.', 1;

    COMMIT TRANSACTION;

    SELECT definition.WorkflowKey,
        COUNT(DISTINCT state.Id) AS ActiveStateCount,
        COUNT(DISTINCT action.Id) AS ActiveActionCount,
        COUNT(DISTINCT role.Id) AS ActiveRoleCount,
        COUNT(DISTINCT transitionDefinition.Id) AS ActiveTransitionCount
    FROM dbo.WorkflowDefinition AS definition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    LEFT JOIN dbo.WorkflowStateDefinition AS state
        ON state.WorkflowDefinitionVersionId = version.Id AND state.IsActive = 1
    LEFT JOIN dbo.WorkflowActionDefinition AS action
        ON action.WorkflowDefinitionVersionId = version.Id AND action.IsActive = 1
    LEFT JOIN dbo.WorkflowRoleDefinition AS role
        ON role.WorkflowDefinitionVersionId = version.Id AND role.IsActive = 1
    LEFT JOIN dbo.WorkflowTransitionDefinition AS transitionDefinition
        ON transitionDefinition.WorkflowDefinitionVersionId = version.Id AND transitionDefinition.IsActive = 1
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
    GROUP BY definition.WorkflowKey
    ORDER BY definition.WorkflowKey;

    DROP TABLE #TransitionSeed;
    DROP TABLE #ActionSeed;
    DROP TABLE #RoleSeed;
    DROP TABLE #StateSeed;
    DROP TABLE #DefinitionSeed;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
