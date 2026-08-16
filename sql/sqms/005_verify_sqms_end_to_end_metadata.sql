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
    (N'NoticeQueued', N'Notice Queued', 0),
    (N'ReminderQueued', N'Reminder Queued', 0),
    (N'Completed', N'Completed', 1);

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
    (N'SendOpenBiddingReminder', N'Send Open Bidding Reminder', N'SaleCycle', N'SendOpenBiddingReminder', N'Send reminder', N'Notify', 20),
    (N'CompleteSaleCycle', N'Complete Sale Cycle', N'SaleCycle', N'CompleteSaleCycle', N'Complete cycle', N'Notify', 30);

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
     'E6E264972DA0C42332C5E40A338707876CB4BBE1E279277B070F030679C5EDAE',
     'D803AE1B3B38D2E072C1690BAA118E2F0960018EC1D654DA62186E725B5AA1AF',
     '1F036931C926EADCEB393C6405455BB99476E0BE65E14DF1D9299764DDF821DC'),
    (N'SqmsSaleCycle', N'RouteSqmsOpenBiddingReminder', N'SqmsOpenBiddingReminder',
     '0B063D405D8CBBBFEDF4EA7300CF74E466AC47DED2DBBDAA47BF1DE5EC2BD112',
     'D803AE1B3B38D2E072C1690BAA118E2F0960018EC1D654DA62186E725B5AA1AF',
     '1F036931C926EADCEB393C6405455BB99476E0BE65E14DF1D9299764DDF821DC'),
    (N'SqmsOpportunity', N'RouteSqmsSellerApprovalRequest', N'SqmsSellerApprovalRequested',
     '617BF3609E8689469FABC229CFF4156FA427F5A987CC1A5CD8E6781E61CD5BFC',
     'AB7265278304060EE3915200A4E11850BFEF3BC1011877B0EA5E4A8E3BA205D5',
     'AB3751CDEDEC42E008A52FE8892B14C9EA51BBC14FBB6BC2E24EC8C34468C516'),
    (N'SqmsBid', N'RouteSqmsBidAccepted', N'SqmsBidAccepted',
     'AB71468CCB086ADCB0FAB8E4983A0D841D0BAF162F8FB403B265ABEDD9ADD4CC',
     'D7318BE55B09A9C56FA05BE46C13E9214A95916F6CDBD46AC59999E086A95DCA',
     '07A5E22CC98DB2DCEEBAC62ED45C766898511851B7F28CA45C849CE099689DA2'),
    (N'SqmsBid', N'RouteSqmsBidPartiallyAccepted', N'SqmsBidPartiallyAccepted',
     'AB71468CCB086ADCB0FAB8E4983A0D841D0BAF162F8FB403B265ABEDD9ADD4CC',
     'D9E639D0F3C527DE8A600F5A1B77AE32F0F9330F02C5617651A652980BDB95F4',
     'B78BE83BFBF869C05974C5E3B8CB3B44D4683DC0F1D809EE73C34F8A558CD4DC'),
    (N'SqmsBid', N'RouteSqmsBidNotAccepted', N'SqmsBidNotAccepted',
     'AB71468CCB086ADCB0FAB8E4983A0D841D0BAF162F8FB403B265ABEDD9ADD4CC',
     '6824532429370C876685DE7864691563DEB62E68877561A0176CCED4DC780540',
     '3918571D23528CF79306975133D03E7C76102FE199D5CFBDD1EAABB8077490D6');

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
    (N'SqmsSaleCycle', N'Draft', N'SendOpenBiddingNotice', N'System', N'NoticeQueued', N'RouteSqmsOpenBiddingNotice'),
    (N'SqmsSaleCycle', N'NoticeQueued', N'SendOpenBiddingReminder', N'System', N'ReminderQueued', N'RouteSqmsOpenBiddingReminder'),
    (N'SqmsSaleCycle', N'ReminderQueued', N'CompleteSaleCycle', N'System', N'Completed', N''),
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
        transitionDefinition.ToStateKey, COALESCE(transitionDefinition.NotificationRouteKey, N'')
    FROM dbo.WorkflowTransitionDefinition AS transitionDefinition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = transitionDefinition.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE version.VersionNumber = 1 AND transitionDefinition.IsActive = 1
      AND (definition.WorkflowKey = N'SqmsSaleCycle'
           OR transitionDefinition.NotificationRouteKey LIKE N'RouteSqms%')
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
