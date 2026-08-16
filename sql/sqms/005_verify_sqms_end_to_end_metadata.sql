SET NOCOUNT ON;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.WorkflowDefinition AS definition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id
    WHERE definition.WorkflowKey = N'SqmsSaleCycle'
      AND definition.BusinessObjectType = N'SqmsSaleCycle'
      AND definition.IsActive = 1
      AND version.VersionNumber = 1
      AND version.IsActive = 1
)
    THROW 51200, 'Missing active SqmsSaleCycle workflow definition version 1.', 1;

DECLARE @SaleCycleVersionId bigint =
(
    SELECT version.Id
    FROM dbo.WorkflowDefinition AS definition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id
    WHERE definition.WorkflowKey = N'SqmsSaleCycle' AND version.VersionNumber = 1
);

CREATE TABLE #ExpectedState
(
    StateKey nvarchar(100) NOT NULL PRIMARY KEY,
    DisplayName nvarchar(200) NOT NULL,
    IsTerminal bit NOT NULL
);
INSERT #ExpectedState (StateKey, DisplayName, IsTerminal)
VALUES
    (N'Draft', N'Draft', 0),
    (N'InitialNoticeSent', N'Initial Notice Sent', 0),
    (N'ReminderSent', N'Reminder Sent', 1);

IF EXISTS
(
    SELECT StateKey, DisplayName, IsTerminal FROM #ExpectedState
    EXCEPT
    SELECT StateKey, DisplayName, IsTerminal
    FROM dbo.WorkflowStateDefinition
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND IsActive = 1
)
    OR EXISTS
(
    SELECT StateKey, DisplayName, IsTerminal
    FROM dbo.WorkflowStateDefinition
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND IsActive = 1
    EXCEPT
    SELECT StateKey, DisplayName, IsTerminal FROM #ExpectedState
)
    THROW 51201, 'SqmsSaleCycle active states do not match the expected set.', 1;

CREATE TABLE #ExpectedAction
(
    ActionKey nvarchar(100) NOT NULL PRIMARY KEY,
    DisplayName nvarchar(200) NOT NULL,
    SectionKey nvarchar(100) NOT NULL,
    ButtonKey nvarchar(100) NOT NULL,
    ButtonLabel nvarchar(200) NOT NULL,
    Intent nvarchar(100) NOT NULL,
    SortOrder int NOT NULL
);
INSERT #ExpectedAction
    (ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, SortOrder)
VALUES
    (N'SendOpenBiddingNotice', N'Send Open Bidding Notice', N'SaleCycle', N'SendOpenBiddingNotice', N'Send notice', N'Notify', 10),
    (N'SendOpenBiddingReminder', N'Send Open Bidding Reminder', N'SaleCycle', N'SendOpenBiddingReminder', N'Send reminder', N'Notify', 20);

IF EXISTS
(
    SELECT ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, SortOrder FROM #ExpectedAction
    EXCEPT
    SELECT ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, SortOrder
    FROM dbo.WorkflowActionDefinition
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId
      AND IsActive = 1 AND RequiresReason = 0
)
    OR EXISTS
(
    SELECT ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, SortOrder
    FROM dbo.WorkflowActionDefinition
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND IsActive = 1
    EXCEPT
    SELECT ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, SortOrder FROM #ExpectedAction
)
    THROW 51202, 'SqmsSaleCycle active actions do not match the expected set.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.WorkflowRoleDefinition
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId
      AND RoleKey = N'System' AND DisplayName = N'System' AND IsActive = 1
)
    OR (SELECT COUNT(*) FROM dbo.WorkflowRoleDefinition
        WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND IsActive = 1) <> 1
    THROW 51203, 'SqmsSaleCycle must contain only the active System role.', 1;

CREATE TABLE #ExpectedRoute
(
    WorkflowKey nvarchar(100) NOT NULL,
    RouteKey nvarchar(100) NOT NULL,
    NotificationType nvarchar(100) NOT NULL,
    SubjectHash varchar(64) NOT NULL,
    BodyHash varchar(64) NOT NULL,
    HtmlBodyHash varchar(64) NOT NULL,
    PRIMARY KEY (WorkflowKey, RouteKey)
);
INSERT #ExpectedRoute
    (WorkflowKey, RouteKey, NotificationType, SubjectHash, BodyHash, HtmlBodyHash)
VALUES
    (N'SqmsSaleCycle', N'RouteSqmsOpenBiddingNotice', N'SqmsOpenBiddingNotice',
     'EC8D680F8E3CF5FB44C5F1EB7DCB599209313100091B17CEA2C38EF237BA1D27',
     'A4E47050CCF6FA57F8BFD864435CFDBC1956A9C4CAB377451557EBABD23FC1F3',
     'C3423F14AB83B28554C9B3E9B0063F631D9E0B1A8F09E5117CEFB260C67AE2D3'),
    (N'SqmsSaleCycle', N'RouteSqmsOpenBiddingReminder', N'SqmsOpenBiddingReminder',
     'E4CEB544394FB8F756244D79E7098A5C551B4BB4F00102789ABFDFC3DC934D0B',
     'A4E47050CCF6FA57F8BFD864435CFDBC1956A9C4CAB377451557EBABD23FC1F3',
     'C3423F14AB83B28554C9B3E9B0063F631D9E0B1A8F09E5117CEFB260C67AE2D3'),
    (N'SqmsOpportunity', N'RouteSqmsSellerApprovalRequest', N'SqmsSellerApprovalRequested',
     'A0226538D2772E042344516B99CE2D63BC490A4394D9CE8BA4BE4C1A3A32381A',
     '6C9B177C7B7286EC9BCF914E68F5D3CDE57E99B5D374BA932AF352DAFEE7D402',
     'D7D72CBDB3A679ACCCE104C26250EBD2A890E87CF44600AA7B15B22F3697EB02'),
    (N'SqmsBid', N'RouteSqmsBidAccepted', N'SqmsBidAccepted',
     'E3B91C48B89C2821881DEB42ECC6579B77F416A10072D14DEAADEF0275ACC735',
     '8DD4E1926C46AEF350D50863D8EBA4F9C1E159F2C8CCC48577CE2713411A95D2',
     'B6CF7E55971DC672CD2A5381D09AAC48ABFF8A901C4055CAA2A30664348C6219'),
    (N'SqmsBid', N'RouteSqmsBidPartiallyAccepted', N'SqmsBidPartiallyAccepted',
     'E3B91C48B89C2821881DEB42ECC6579B77F416A10072D14DEAADEF0275ACC735',
     '408D8024481242CCA0796DABA6949821856C2D0365F0A4725D15BE96D0DAA069',
     '375C18FACA762F8AB82FEE34A9C23616BEE4F51FC8765072A69C89F09714E0B9'),
    (N'SqmsBid', N'RouteSqmsBidNotAccepted', N'SqmsBidNotAccepted',
     'E3B91C48B89C2821881DEB42ECC6579B77F416A10072D14DEAADEF0275ACC735',
     '392BA7EAACC811CA80F9CB60A20AA8284B09C3BD8D289D396C1A24A337157BAD',
     '2ABDF039AD4FD290C7E3E364A757F7DD01EB5B179A5A163F16FCA39A971B7479');

CREATE TABLE #ActualRoute
(
    WorkflowKey nvarchar(100) NOT NULL,
    RouteKey nvarchar(100) NOT NULL,
    NotificationType nvarchar(100) NOT NULL,
    SubjectHash varchar(64) NOT NULL,
    BodyHash varchar(64) NOT NULL,
    HtmlBodyHash varchar(64) NOT NULL,
    PRIMARY KEY (WorkflowKey, RouteKey)
);
INSERT #ActualRoute
    (WorkflowKey, RouteKey, NotificationType, SubjectHash, BodyHash, HtmlBodyHash)
SELECT definition.WorkflowKey, route.RouteKey, route.NotificationType,
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), route.SubjectTemplate)), 2),
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), route.BodyTemplate)), 2),
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), route.HtmlBodyTemplate)), 2)
FROM dbo.NotificationRoute AS route
INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = route.WorkflowDefinitionVersionId
INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
WHERE version.VersionNumber = 1 AND route.IsActive = 1
  AND route.RouteKey LIKE N'RouteSqms%'
  AND route.OverdueSubjectPrefix = N'Overdue: ';

IF EXISTS (SELECT * FROM #ExpectedRoute EXCEPT SELECT * FROM #ActualRoute)
    OR EXISTS (SELECT * FROM #ActualRoute EXCEPT SELECT * FROM #ExpectedRoute)
    THROW 51204, 'SQMS route types or exact template hashes do not match.', 1;

CREATE TABLE #ExpectedRecipient
(
    RouteKey nvarchar(100) NOT NULL,
    SortOrder int NOT NULL,
    RecipientKind nvarchar(10) NOT NULL,
    SourceType nvarchar(40) NOT NULL,
    SourceValue nvarchar(max) NOT NULL,
    IsRequired bit NOT NULL,
    PRIMARY KEY (RouteKey, SortOrder)
);
INSERT #ExpectedRecipient
    (RouteKey, SortOrder, RecipientKind, SourceType, SourceValue, IsRequired)
VALUES
    (N'RouteSqmsOpenBiddingNotice', 1, N'To', N'StaticAddress', N'sqms-open-bidding-to1@example.test', 1),
    (N'RouteSqmsOpenBiddingNotice', 2, N'To', N'StaticAddress', N'sqms-open-bidding-to2@example.test', 1),
    (N'RouteSqmsOpenBiddingNotice', 3, N'To', N'StaticAddress', N'sqms-open-bidding-to3@example.test', 1),
    (N'RouteSqmsOpenBiddingNotice', 4, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsOpenBiddingNotice', 5, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1),
    (N'RouteSqmsOpenBiddingReminder', 1, N'To', N'StaticAddress', N'sqms-open-bidding-to1@example.test', 1),
    (N'RouteSqmsOpenBiddingReminder', 2, N'To', N'StaticAddress', N'sqms-open-bidding-to2@example.test', 1),
    (N'RouteSqmsOpenBiddingReminder', 3, N'To', N'StaticAddress', N'sqms-open-bidding-to3@example.test', 1),
    (N'RouteSqmsOpenBiddingReminder', 4, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsOpenBiddingReminder', 5, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1),
    (N'RouteSqmsSellerApprovalRequest', 1, N'To', N'RequestInput', N'opportunityRelationshipManager', 1),
    (N'RouteSqmsSellerApprovalRequest', 2, N'To', N'RequestInput', N'sellerRmTeam', 1),
    (N'RouteSqmsSellerApprovalRequest', 3, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsSellerApprovalRequest', 4, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1),
    (N'RouteSqmsBidAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', 1),
    (N'RouteSqmsBidAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', 1),
    (N'RouteSqmsBidAccepted', 3, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsBidAccepted', 4, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1),
    (N'RouteSqmsBidPartiallyAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', 1),
    (N'RouteSqmsBidPartiallyAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', 1),
    (N'RouteSqmsBidPartiallyAccepted', 3, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsBidPartiallyAccepted', 4, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1),
    (N'RouteSqmsBidNotAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', 1),
    (N'RouteSqmsBidNotAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', 1),
    (N'RouteSqmsBidNotAccepted', 3, N'Cc', N'StaticAddress', N'sqms-static-cc1@example.test', 1),
    (N'RouteSqmsBidNotAccepted', 4, N'Cc', N'StaticAddress', N'sqms-static-cc2@example.test', 1);

CREATE TABLE #ActualRecipient
(
    RouteKey nvarchar(100) NOT NULL,
    SortOrder int NOT NULL,
    RecipientKind nvarchar(10) NOT NULL,
    SourceType nvarchar(40) NOT NULL,
    SourceValue nvarchar(max) NOT NULL,
    IsRequired bit NOT NULL,
    PRIMARY KEY (RouteKey, SortOrder)
);
INSERT #ActualRecipient
    (RouteKey, SortOrder, RecipientKind, SourceType, SourceValue, IsRequired)
SELECT route.RouteKey, recipientRule.SortOrder, recipientRule.RecipientKind, recipientRule.SourceType,
    COALESCE(recipientRule.SourceKey, recipientRule.StaticAddress), recipientRule.IsRequired
FROM dbo.NotificationRecipientRule AS recipientRule
INNER JOIN dbo.NotificationRoute AS route ON route.Id = recipientRule.NotificationRouteId
WHERE route.RouteKey IN (SELECT RouteKey FROM #ExpectedRoute)
  AND route.IsActive = 1 AND recipientRule.IsActive = 1
  AND recipientRule.AssignedRoleKey IS NULL;

IF EXISTS (SELECT * FROM #ExpectedRecipient EXCEPT SELECT * FROM #ActualRecipient)
    OR EXISTS (SELECT * FROM #ActualRecipient EXCEPT SELECT * FROM #ExpectedRecipient)
    THROW 51205, 'SQMS active recipient rules do not match the expected ordered snapshots.', 1;

CREATE TABLE #ExpectedTransitionRoute
(
    WorkflowKey nvarchar(100) NOT NULL,
    FromStateKey nvarchar(100) NOT NULL,
    ActionKey nvarchar(100) NOT NULL,
    AllowedRoleKey nvarchar(100) NOT NULL,
    ToStateKey nvarchar(100) NOT NULL,
    RouteKey nvarchar(100) NOT NULL,
    PRIMARY KEY (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey)
);
INSERT #ExpectedTransitionRoute
VALUES
    (N'SqmsSaleCycle', N'Draft', N'SendOpenBiddingNotice', N'System', N'InitialNoticeSent', N'RouteSqmsOpenBiddingNotice'),
    (N'SqmsSaleCycle', N'InitialNoticeSent', N'SendOpenBiddingReminder', N'System', N'ReminderSent', N'RouteSqmsOpenBiddingReminder'),
    (N'SqmsOpportunity', N'ClosedToBidding', N'RequestSellerApproval', N'IPG', N'PendingSellerApproval', N'RouteSqmsSellerApprovalRequest'),
    (N'SqmsBid', N'UnderReviewBySeller', N'AcceptBid', N'MFO', N'Accepted', N'RouteSqmsBidAccepted'),
    (N'SqmsBid', N'UnderReviewBySeller', N'PartiallyAcceptBid', N'MFO', N'PartiallyAccepted', N'RouteSqmsBidPartiallyAccepted'),
    (N'SqmsBid', N'UnderReviewBySeller', N'RejectBid', N'MFO', N'NotAccepted', N'RouteSqmsBidNotAccepted');

IF EXISTS
(
    SELECT * FROM #ExpectedTransitionRoute
    EXCEPT
    SELECT definition.WorkflowKey, transitionDefinition.FromStateKey,
        transitionDefinition.ActionKey, transitionDefinition.AllowedRoleKey,
        transitionDefinition.ToStateKey, transitionDefinition.NotificationRouteKey
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE version.VersionNumber = 1 AND transitionDefinition.IsActive = 1
      AND transitionDefinition.NotificationRouteKey LIKE N'RouteSqms%'
)
    THROW 51206, 'Missing or incorrect SQMS notification transition binding.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid', N'SqmsSaleCycle')
      AND version.VersionNumber = 1 AND transitionDefinition.IsActive = 1
      AND (transitionDefinition.TaskToCloseKey IS NOT NULL OR transitionDefinition.TaskToCreateKey IS NOT NULL)
)
    THROW 51207, 'SQMS end-to-end transitions must remain taskless.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.WorkflowTaskDefinition AS taskDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = taskDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid', N'SqmsSaleCycle')
      AND version.VersionNumber = 1 AND taskDefinition.IsActive = 1
)
    OR EXISTS
(
    SELECT 1
    FROM dbo.SlaPolicy AS policy
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = policy.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid', N'SqmsSaleCycle')
      AND version.VersionNumber = 1 AND policy.IsActive = 1
)
    THROW 51208, 'SQMS workflows must not contain active tasks or SLA policies.', 1;

SELECT WorkflowKey, RouteKey, NotificationType
FROM #ExpectedRoute
ORDER BY WorkflowKey, RouteKey;

DROP TABLE #ExpectedTransitionRoute;
DROP TABLE #ActualRecipient;
DROP TABLE #ExpectedRecipient;
DROP TABLE #ActualRoute;
DROP TABLE #ExpectedRoute;
DROP TABLE #ExpectedAction;
DROP TABLE #ExpectedState;

PRINT 'SQMS end-to-end metadata verification passed.';
