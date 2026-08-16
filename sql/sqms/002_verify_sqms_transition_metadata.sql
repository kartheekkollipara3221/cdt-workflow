SET NOCOUNT ON;
SET XACT_ABORT ON;

CREATE TABLE #VerifyState
(
    WorkflowKey nvarchar(100) NOT NULL,
    StateKey nvarchar(100) NOT NULL,
    IsTerminal bit NOT NULL,
    PRIMARY KEY (WorkflowKey, StateKey)
);

INSERT #VerifyState (WorkflowKey, StateKey, IsTerminal)
VALUES
    (N'SqmsOpportunity', N'Draft', 0),
    (N'SqmsOpportunity', N'Submitted', 0),
    (N'SqmsOpportunity', N'Pending', 0),
    (N'SqmsOpportunity', N'OpenToBidding', 0),
    (N'SqmsOpportunity', N'ClosedToBidding', 0),
    (N'SqmsOpportunity', N'PendingSellerApproval', 0),
    (N'SqmsOpportunity', N'AwaitingAllocation', 0),
    (N'SqmsOpportunity', N'PartiallyAllocated', 0),
    (N'SqmsOpportunity', N'NotFulfilled', 1),
    (N'SqmsOpportunity', N'Completed', 1),
    (N'SqmsOpportunity', N'Cancelled', 1),
    (N'SqmsBid', N'Draft', 0),
    (N'SqmsBid', N'Submitted', 0),
    (N'SqmsBid', N'NotAllocated', 1),
    (N'SqmsBid', N'IpgReview', 0),
    (N'SqmsBid', N'UnderReviewBySeller', 0),
    (N'SqmsBid', N'Accepted', 1),
    (N'SqmsBid', N'PartiallyAccepted', 1),
    (N'SqmsBid', N'NotAccepted', 1);

CREATE TABLE #VerifyRole
(
    WorkflowKey nvarchar(100) NOT NULL,
    RoleKey nvarchar(100) NOT NULL,
    PRIMARY KEY (WorkflowKey, RoleKey)
);

INSERT #VerifyRole (WorkflowKey, RoleKey)
VALUES
    (N'SqmsOpportunity', N'MFO'),
    (N'SqmsOpportunity', N'IPG'),
    (N'SqmsOpportunity', N'System'),
    (N'SqmsBid', N'MFO'),
    (N'SqmsBid', N'IPG'),
    (N'SqmsBid', N'System');

CREATE TABLE #VerifyAction
(
    WorkflowKey nvarchar(100) NOT NULL,
    ActionKey nvarchar(100) NOT NULL,
    PRIMARY KEY (WorkflowKey, ActionKey)
);

INSERT #VerifyAction (WorkflowKey, ActionKey)
VALUES
    (N'SqmsOpportunity', N'SubmitOpportunity'),
    (N'SqmsOpportunity', N'ReturnOpportunityToDraft'),
    (N'SqmsOpportunity', N'MarkOpportunityPending'),
    (N'SqmsOpportunity', N'OpenOpportunityForBidding'),
    (N'SqmsOpportunity', N'CloseOpportunityBidding'),
    (N'SqmsOpportunity', N'RequestSellerApproval'),
    (N'SqmsOpportunity', N'MarkOpportunityAwaitingAllocation'),
    (N'SqmsOpportunity', N'MarkOpportunityPartiallyAllocated'),
    (N'SqmsOpportunity', N'MarkOpportunityNotFulfilled'),
    (N'SqmsOpportunity', N'CompleteOpportunity'),
    (N'SqmsOpportunity', N'CancelOpportunity'),
    (N'SqmsBid', N'SubmitBid'),
    (N'SqmsBid', N'MarkBidNotAllocated'),
    (N'SqmsBid', N'MoveBidToIpgReview'),
    (N'SqmsBid', N'SendBidToSellerReview'),
    (N'SqmsBid', N'AcceptBid'),
    (N'SqmsBid', N'PartiallyAcceptBid'),
    (N'SqmsBid', N'RejectBid');

CREATE TABLE #VerifyTransition
(
    WorkflowKey nvarchar(100) NOT NULL,
    FromStateKey nvarchar(100) NOT NULL,
    ActionKey nvarchar(100) NOT NULL,
    AllowedRoleKey nvarchar(100) NOT NULL,
    ToStateKey nvarchar(100) NOT NULL,
    PRIMARY KEY (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey)
);

INSERT #VerifyTransition (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey)
VALUES
    (N'SqmsOpportunity', N'Draft', N'SubmitOpportunity', N'MFO', N'Submitted'),
    (N'SqmsOpportunity', N'Submitted', N'ReturnOpportunityToDraft', N'IPG', N'Draft'),
    (N'SqmsOpportunity', N'Submitted', N'MarkOpportunityPending', N'IPG', N'Pending'),
    (N'SqmsOpportunity', N'Pending', N'ReturnOpportunityToDraft', N'IPG', N'Draft'),
    (N'SqmsOpportunity', N'Pending', N'OpenOpportunityForBidding', N'IPG', N'OpenToBidding'),
    (N'SqmsOpportunity', N'OpenToBidding', N'CloseOpportunityBidding', N'System', N'ClosedToBidding'),
    (N'SqmsOpportunity', N'ClosedToBidding', N'RequestSellerApproval', N'IPG', N'PendingSellerApproval'),
    (N'SqmsOpportunity', N'ClosedToBidding', N'MarkOpportunityNotFulfilled', N'System', N'NotFulfilled'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityAwaitingAllocation', N'IPG', N'AwaitingAllocation'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityAwaitingAllocation', N'System', N'AwaitingAllocation'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityPartiallyAllocated', N'IPG', N'PartiallyAllocated'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityPartiallyAllocated', N'System', N'PartiallyAllocated'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'MarkOpportunityNotFulfilled', N'System', N'NotFulfilled'),
    (N'SqmsOpportunity', N'AwaitingAllocation', N'CompleteOpportunity', N'IPG', N'Completed'),
    (N'SqmsOpportunity', N'PartiallyAllocated', N'CompleteOpportunity', N'IPG', N'Completed'),
    (N'SqmsOpportunity', N'Draft', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'Draft', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'Submitted', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'Submitted', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'Pending', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'Pending', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'OpenToBidding', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'OpenToBidding', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'ClosedToBidding', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'ClosedToBidding', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'PendingSellerApproval', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'AwaitingAllocation', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'AwaitingAllocation', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsOpportunity', N'PartiallyAllocated', N'CancelOpportunity', N'IPG', N'Cancelled'),
    (N'SqmsOpportunity', N'PartiallyAllocated', N'CancelOpportunity', N'System', N'Cancelled'),
    (N'SqmsBid', N'Draft', N'SubmitBid', N'MFO', N'Submitted'),
    (N'SqmsBid', N'Submitted', N'MarkBidNotAllocated', N'IPG', N'NotAllocated'),
    (N'SqmsBid', N'Submitted', N'MarkBidNotAllocated', N'System', N'NotAllocated'),
    (N'SqmsBid', N'Submitted', N'MoveBidToIpgReview', N'IPG', N'IpgReview'),
    (N'SqmsBid', N'Submitted', N'MoveBidToIpgReview', N'System', N'IpgReview'),
    (N'SqmsBid', N'IpgReview', N'SendBidToSellerReview', N'IPG', N'UnderReviewBySeller'),
    (N'SqmsBid', N'UnderReviewBySeller', N'AcceptBid', N'MFO', N'Accepted'),
    (N'SqmsBid', N'UnderReviewBySeller', N'PartiallyAcceptBid', N'MFO', N'PartiallyAccepted'),
    (N'SqmsBid', N'UnderReviewBySeller', N'RejectBid', N'MFO', N'NotAccepted');

IF EXISTS
(
    SELECT WorkflowKey FROM (VALUES (N'SqmsOpportunity'), (N'SqmsBid')) AS expected(WorkflowKey)
    EXCEPT
    SELECT WorkflowKey
    FROM dbo.WorkflowDefinition
    WHERE IsActive = 1
)
    THROW 51100, 'Missing active SQMS workflow definition.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey
    FROM dbo.WorkflowDefinition AS definition
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND definition.BusinessObjectType <> definition.WorkflowKey
)
    THROW 51101, 'SQMS BusinessObjectType does not match its workflow key.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey
    FROM dbo.WorkflowDefinition AS definition
    LEFT JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id
        AND version.VersionNumber = 1
        AND version.IsActive = 1
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.Id IS NULL
)
    THROW 51102, 'Missing active SQMS workflow version 1.', 1;

IF EXISTS
(
    SELECT WorkflowKey, StateKey, IsTerminal FROM #VerifyState
    EXCEPT
    SELECT definition.WorkflowKey, state.StateKey, state.IsTerminal
    FROM dbo.WorkflowStateDefinition AS state
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = state.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND state.IsActive = 1
)
    THROW 51103, 'Missing or incorrect active SQMS state.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey, state.StateKey, state.IsTerminal
    FROM dbo.WorkflowStateDefinition AS state
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = state.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND state.IsActive = 1
    EXCEPT
    SELECT WorkflowKey, StateKey, IsTerminal FROM #VerifyState
)
    THROW 51104, 'Unexpected active SQMS state.', 1;

IF EXISTS
(
    SELECT WorkflowKey, RoleKey FROM #VerifyRole
    EXCEPT
    SELECT definition.WorkflowKey, role.RoleKey
    FROM dbo.WorkflowRoleDefinition AS role
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = role.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND role.IsActive = 1
)
    THROW 51105, 'Missing active SQMS role.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey, role.RoleKey
    FROM dbo.WorkflowRoleDefinition AS role
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = role.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND role.IsActive = 1
    EXCEPT
    SELECT WorkflowKey, RoleKey FROM #VerifyRole
)
    THROW 51106, 'Unexpected active SQMS role.', 1;

IF EXISTS
(
    SELECT WorkflowKey, ActionKey FROM #VerifyAction
    EXCEPT
    SELECT definition.WorkflowKey, action.ActionKey
    FROM dbo.WorkflowActionDefinition AS action
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = action.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND action.IsActive = 1
)
    THROW 51107, 'Missing active SQMS action.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey, action.ActionKey
    FROM dbo.WorkflowActionDefinition AS action
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = action.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND action.IsActive = 1
    EXCEPT
    SELECT WorkflowKey, ActionKey FROM #VerifyAction
)
    THROW 51108, 'Unexpected active SQMS action.', 1;

IF EXISTS
(
    SELECT WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey FROM #VerifyTransition
    EXCEPT
    SELECT definition.WorkflowKey, transitionDefinition.FromStateKey,
        transitionDefinition.ActionKey, transitionDefinition.AllowedRoleKey,
        transitionDefinition.ToStateKey
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND transitionDefinition.IsActive = 1
)
    THROW 51109, 'Missing or incorrect active SQMS transition.', 1;

IF EXISTS
(
    SELECT definition.WorkflowKey, transitionDefinition.FromStateKey,
        transitionDefinition.ActionKey, transitionDefinition.AllowedRoleKey,
        transitionDefinition.ToStateKey
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1 AND transitionDefinition.IsActive = 1
    EXCEPT
    SELECT WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey FROM #VerifyTransition
)
    THROW 51110, 'Unexpected active SQMS transition.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1
      AND transitionDefinition.IsActive = 1
      AND (transitionDefinition.TaskToCloseKey IS NOT NULL
           OR transitionDefinition.TaskToCreateKey IS NOT NULL)
)
    THROW 51111, 'SQMS transitions must remain taskless and must not create SLA-backed tasks.', 1;

SELECT definition.WorkflowKey,
    (SELECT COUNT(*) FROM #VerifyState AS expected WHERE expected.WorkflowKey = definition.WorkflowKey) AS VerifiedStateCount,
    (SELECT COUNT(*) FROM #VerifyAction AS expected WHERE expected.WorkflowKey = definition.WorkflowKey) AS VerifiedActionCount,
    (SELECT COUNT(*) FROM #VerifyRole AS expected WHERE expected.WorkflowKey = definition.WorkflowKey) AS VerifiedRoleCount,
    (SELECT COUNT(*) FROM #VerifyTransition AS expected WHERE expected.WorkflowKey = definition.WorkflowKey) AS VerifiedTransitionCount
FROM dbo.WorkflowDefinition AS definition
WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
ORDER BY definition.WorkflowKey;

PRINT 'SQMS transition metadata verification passed.';
