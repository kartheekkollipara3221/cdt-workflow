SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.WorkflowDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowDefinitionVersion', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowStateDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowActionDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowRoleDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.WorkflowTransitionDefinition', N'U') IS NULL
    OR OBJECT_ID(N'dbo.NotificationRoute', N'U') IS NULL
    OR OBJECT_ID(N'dbo.NotificationRecipientRule', N'U') IS NULL
    THROW 51220, 'Apply workflow-engine migrations before running the SQMS end-to-end seed.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.WorkflowDefinition AS definition
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.WorkflowDefinitionId = definition.Id
    WHERE definition.WorkflowKey IN (N'SqmsOpportunity', N'SqmsBid')
      AND version.VersionNumber = 1
)
    THROW 51221, 'Run 001_seed_sqms_transition_metadata.sql before the SQMS end-to-end seed.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.WorkflowDefinition
        WHERE WorkflowKey = N'SqmsSaleCycle'
          AND BusinessObjectType <> N'SqmsSaleCycle'
    )
        THROW 51222, 'SqmsSaleCycle exists with a conflicting BusinessObjectType.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'SqmsSaleCycle')
    BEGIN
        INSERT dbo.WorkflowDefinition (WorkflowKey, DisplayName, BusinessObjectType, IsActive)
        VALUES (N'SqmsSaleCycle', N'SQMS Sale Cycle', N'SqmsSaleCycle', 1);
    END;

    UPDATE dbo.WorkflowDefinition
    SET DisplayName = N'SQMS Sale Cycle', IsActive = 1
    WHERE WorkflowKey = N'SqmsSaleCycle';

    DECLARE @SaleCycleDefinitionId bigint =
    (
        SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'SqmsSaleCycle'
    );

    IF EXISTS
    (
        SELECT 1 FROM dbo.WorkflowDefinitionVersion
        WHERE WorkflowDefinitionId = @SaleCycleDefinitionId
          AND IsActive = 1
          AND VersionNumber <> 1
    )
        THROW 51223, 'SqmsSaleCycle has an active definition version other than version 1.', 1;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowDefinitionVersion
        WHERE WorkflowDefinitionId = @SaleCycleDefinitionId AND VersionNumber = 1
    )
    BEGIN
        INSERT dbo.WorkflowDefinitionVersion
            (WorkflowDefinitionId, VersionNumber, IsActive, CreatedAtUtc)
        VALUES (@SaleCycleDefinitionId, 1, 1, SYSDATETIMEOFFSET());
    END;

    UPDATE dbo.WorkflowDefinitionVersion
    SET IsActive = 1
    WHERE WorkflowDefinitionId = @SaleCycleDefinitionId AND VersionNumber = 1;

    DECLARE @SaleCycleVersionId bigint =
    (
        SELECT Id FROM dbo.WorkflowDefinitionVersion
        WHERE WorkflowDefinitionId = @SaleCycleDefinitionId AND VersionNumber = 1
    );

    CREATE TABLE #StateSeed
    (
        StateKey nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        IsTerminal bit NOT NULL
    );
    INSERT #StateSeed (StateKey, DisplayName, IsTerminal)
    VALUES
        (N'Draft', N'Draft', 0),
        (N'NoticeQueued', N'Notice Queued', 0),
        (N'ReminderQueued', N'Reminder Queued', 0),
        (N'Completed', N'Completed', 1);

    INSERT dbo.WorkflowStateDefinition
        (WorkflowDefinitionVersionId, StateKey, DisplayName, IsTerminal, IsActive)
    SELECT @SaleCycleVersionId, expected.StateKey, expected.DisplayName, expected.IsTerminal, 1
    FROM #StateSeed AS expected
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowStateDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = @SaleCycleVersionId
          AND actual.StateKey = expected.StateKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        IsTerminal = expected.IsTerminal,
        IsActive = 1
    FROM dbo.WorkflowStateDefinition AS actual
    INNER JOIN #StateSeed AS expected ON expected.StateKey = actual.StateKey
    WHERE actual.WorkflowDefinitionVersionId = @SaleCycleVersionId;

    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowRoleDefinition
        WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND RoleKey = N'System'
    )
    BEGIN
        INSERT dbo.WorkflowRoleDefinition
            (WorkflowDefinitionVersionId, RoleKey, DisplayName, IsActive)
        VALUES (@SaleCycleVersionId, N'System', N'System', 1);
    END;

    UPDATE dbo.WorkflowRoleDefinition
    SET DisplayName = N'System', IsActive = 1
    WHERE WorkflowDefinitionVersionId = @SaleCycleVersionId AND RoleKey = N'System';

    CREATE TABLE #ActionSeed
    (
        ActionKey nvarchar(100) NOT NULL PRIMARY KEY,
        DisplayName nvarchar(200) NOT NULL,
        ButtonKey nvarchar(100) NOT NULL,
        ButtonLabel nvarchar(200) NOT NULL,
        SortOrder int NOT NULL
    );
    INSERT #ActionSeed (ActionKey, DisplayName, ButtonKey, ButtonLabel, SortOrder)
    VALUES
        (N'SendOpenBiddingNotice', N'Send Open Bidding Notice', N'SendOpenBiddingNotice', N'Send notice', 10),
        (N'SendOpenBiddingReminder', N'Send Open Bidding Reminder', N'SendOpenBiddingReminder', N'Send reminder', 20),
        (N'CompleteSaleCycle', N'Complete Sale Cycle', N'CompleteSaleCycle', N'Complete cycle', 30);

    INSERT dbo.WorkflowActionDefinition
        (WorkflowDefinitionVersionId, ActionKey, DisplayName, SectionKey, ButtonKey,
         ButtonLabel, Intent, RequiresReason, SortOrder, IsActive)
    SELECT @SaleCycleVersionId, expected.ActionKey, expected.DisplayName, N'SaleCycle',
        expected.ButtonKey, expected.ButtonLabel, N'Notify', 0, expected.SortOrder, 1
    FROM #ActionSeed AS expected
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowActionDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = @SaleCycleVersionId
          AND actual.ActionKey = expected.ActionKey
    );

    UPDATE actual
    SET DisplayName = expected.DisplayName,
        SectionKey = N'SaleCycle',
        ButtonKey = expected.ButtonKey,
        ButtonLabel = expected.ButtonLabel,
        Intent = N'Notify',
        RequiresReason = 0,
        SortOrder = expected.SortOrder,
        IsActive = 1
    FROM dbo.WorkflowActionDefinition AS actual
    INNER JOIN #ActionSeed AS expected ON expected.ActionKey = actual.ActionKey
    WHERE actual.WorkflowDefinitionVersionId = @SaleCycleVersionId;

    CREATE TABLE #RouteSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        RouteKey nvarchar(100) NOT NULL,
        NotificationType nvarchar(100) NOT NULL,
        SubjectTemplate nvarchar(300) NOT NULL,
        BodyTemplate nvarchar(max) NOT NULL,
        HtmlBodyTemplate nvarchar(max) NOT NULL,
        PRIMARY KEY (WorkflowKey, RouteKey)
    );

    INSERT #RouteSeed
        (WorkflowKey, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate)
    VALUES
    (
        N'SqmsSaleCycle', N'RouteSqmsOpenBiddingNotice', N'SqmsOpenBiddingNotice',
        N'Q{{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDueDate}}',
        CONCAT(
            N'Please click the following link(s) to review secondary sales opportunities for this quarter.',
            NCHAR(10), NCHAR(10),
            N'View all Opportunities open for Bidding: {{viewUrl}}'),
        N'<p>Please click the following link(s) to review secondary sales opportunities for this quarter.</p><p><a href="{{viewUrl}}">View all Opportunities open for Bidding</a></p>'
    ),
    (
        N'SqmsSaleCycle', N'RouteSqmsOpenBiddingReminder', N'SqmsOpenBiddingReminder',
        N'REMINDER Q{{quarter}} {{year}} Secondary Sale Opportunities - Open to Bidding: Response Required by {{responseDueDate}}',
        CONCAT(
            N'Please click the following link(s) to review secondary sales opportunities for this quarter.',
            NCHAR(10), NCHAR(10),
            N'View all Opportunities open for Bidding: {{viewUrl}}'),
        N'<p>Please click the following link(s) to review secondary sales opportunities for this quarter.</p><p><a href="{{viewUrl}}">View all Opportunities open for Bidding</a></p>'
    ),
    (
        N'SqmsOpportunity', N'RouteSqmsSellerApprovalRequest', N'SqmsSellerApprovalRequested',
        N'Q{{quarter}} {{year}} Secondary Sale Bids- "{{vehicle}}"/"{{accountNumber}}" - "{{accountName}}" : Response required by {{responseDueDate}}',
        CONCAT(
            N'Please click the following link to review secondary sales bids for the following opportunity', NCHAR(10),
            N'Account Number: {{accountNumber}}', NCHAR(10),
            N'Account Name: {{accountName}}', NCHAR(10),
            N'Name of Fund: {{fundName}}', NCHAR(10),
            N'Name of Vehicle: {{vehicle}}', NCHAR(10),
            N'Total amount requested to be sold: {{saleAmount}}', NCHAR(10),
            N'Link to approve or reject bids received: {{viewUrl}}'),
        N'<p>Please click the following link to review secondary sales bids for the following opportunity.</p><p>Account Number: <strong>{{accountNumber}}</strong><br>Account Name: <strong>{{accountName}}</strong><br>Name of Fund: <strong>{{fundName}}</strong><br>Name of Vehicle: <strong>{{vehicle}}</strong><br>Total amount requested to be sold: <strong>{{saleAmount}}</strong></p><p><a href="{{viewUrl}}">Click here to approve or reject bids received</a></p>'
    ),
    (
        N'SqmsBid', N'RouteSqmsBidAccepted', N'SqmsBidAccepted',
        N'Q{{quarter}} {{year}} Secondary Sale Bids - "{{vehicle}}"/"{{buyerAccountNumber}}" - "{{buyerAccountName}}"',
        CONCAT(
            N'The bid placed for the following opportunity has been accepted by the Seller for an effective date of {{effectiveDateForSale}}.', NCHAR(10),
            N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', NCHAR(10),
            N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to affect this sale.', NCHAR(10),
            N'Buyer Account Number: {{buyerAccountNumber}}', NCHAR(10),
            N'Buyer Account Name: {{buyerAccountName}}', NCHAR(10),
            N'Name of Fund: {{fundName}}', NCHAR(10),
            N'Name of Vehicle: {{vehicle}}', NCHAR(10),
            N'Total Amount Request to be Purchased: {{purchaseAmount}}', NCHAR(10),
            N'Link To View: {{viewUrl}}'),
        N'<p>The bid placed for the following opportunity has been accepted by the Seller for an effective date of <strong>{{effectiveDateForSale}}</strong>.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.<br>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to affect this sale.</p><p>Buyer Account Number: <strong>{{buyerAccountNumber}}</strong><br>Buyer Account Name: <strong>{{buyerAccountName}}</strong><br>Name of Fund: <strong>{{fundName}}</strong><br>Name of Vehicle: <strong>{{vehicle}}</strong><br>Total Amount Request to be Purchased: <strong>{{purchaseAmount}}</strong></p><p><a href="{{viewUrl}}">Click Here</a></p>'
    ),
    (
        N'SqmsBid', N'RouteSqmsBidPartiallyAccepted', N'SqmsBidPartiallyAccepted',
        N'Q{{quarter}} {{year}} Secondary Sale Bids - "{{vehicle}}"/"{{buyerAccountNumber}}" - "{{buyerAccountName}}"',
        CONCAT(
            N'The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of {{effectiveDateForSale}}.', NCHAR(10),
            N'Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.', NCHAR(10),
            N'Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to affect this sale.', NCHAR(10),
            N'Buyer Account Number: {{buyerAccountNumber}}', NCHAR(10),
            N'Buyer Account Name: {{buyerAccountName}}', NCHAR(10),
            N'Name of Fund: {{fundName}}', NCHAR(10),
            N'Name of Vehicle: {{vehicle}}', NCHAR(10),
            N'Total Amount Request to be Purchased: {{purchaseAmount}}', NCHAR(10),
            N'Link To View: {{viewUrl}}'),
        N'<p>The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of <strong>{{effectiveDateForSale}}</strong>.</p><p>Please confirm your buyer will proceed with the purchase of the stated private fund interest below at your earliest convenience.<br>Once confirmed, you will receive further communication shortly from the Private Funds team regarding the documentation required to affect this sale.</p><p>Buyer Account Number: <strong>{{buyerAccountNumber}}</strong><br>Buyer Account Name: <strong>{{buyerAccountName}}</strong><br>Name of Fund: <strong>{{fundName}}</strong><br>Name of Vehicle: <strong>{{vehicle}}</strong><br>Total Amount Request to be Purchased: <strong>{{purchaseAmount}}</strong></p><p><a href="{{viewUrl}}">Click Here</a></p>'
    ),
    (
        N'SqmsBid', N'RouteSqmsBidNotAccepted', N'SqmsBidNotAccepted',
        N'Q{{quarter}} {{year}} Secondary Sale Bids - "{{vehicle}}"/"{{buyerAccountNumber}}" - "{{buyerAccountName}}"',
        CONCAT(
            N'The bid placed for the following opportunity has not been accepted by the Seller for an effective date of {{effectiveDateForSale}}.', NCHAR(10),
            N'Buyer Account Number: {{buyerAccountNumber}}', NCHAR(10),
            N'Buyer Account Name: {{buyerAccountName}}', NCHAR(10),
            N'Name of Fund: {{fundName}}', NCHAR(10),
            N'Name of Vehicle: {{vehicle}}', NCHAR(10),
            N'Total Amount Request to be Purchased: {{purchaseAmount}}', NCHAR(10),
            N'Link To View: {{viewUrl}}'),
        N'<p>The bid placed for the following opportunity has not been accepted by the Seller for an effective date of <strong>{{effectiveDateForSale}}</strong>.</p><p>Buyer Account Number: <strong>{{buyerAccountNumber}}</strong><br>Buyer Account Name: <strong>{{buyerAccountName}}</strong><br>Name of Fund: <strong>{{fundName}}</strong><br>Name of Vehicle: <strong>{{vehicle}}</strong><br>Total Amount Request to be Purchased: <strong>{{purchaseAmount}}</strong></p><p><a href="{{viewUrl}}">Click Here</a></p>'
    );

    INSERT dbo.NotificationRoute
        (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate,
         BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT version.Id, expected.RouteKey, expected.NotificationType, expected.SubjectTemplate,
        expected.BodyTemplate, expected.HtmlBodyTemplate, N'Overdue: ', 1
    FROM #RouteSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.NotificationRoute AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.RouteKey = expected.RouteKey
    );

    UPDATE actual
    SET NotificationType = expected.NotificationType,
        SubjectTemplate = expected.SubjectTemplate,
        BodyTemplate = expected.BodyTemplate,
        HtmlBodyTemplate = expected.HtmlBodyTemplate,
        OverdueSubjectPrefix = N'Overdue: ',
        IsActive = 1
    FROM dbo.NotificationRoute AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #RouteSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey AND expected.RouteKey = actual.RouteKey
    WHERE version.VersionNumber = 1;

    CREATE TABLE #RecipientSeed
    (
        RouteKey nvarchar(100) NOT NULL,
        SortOrder int NOT NULL,
        RecipientKind nvarchar(10) NOT NULL,
        SourceType nvarchar(40) NOT NULL,
        SourceKey nvarchar(100) NULL,
        StaticAddress nvarchar(max) NULL,
        IsRequired bit NOT NULL,
        PRIMARY KEY (RouteKey, SortOrder)
    );

    INSERT #RecipientSeed
        (RouteKey, SortOrder, RecipientKind, SourceType, SourceKey, StaticAddress, IsRequired)
    VALUES
        (N'RouteSqmsOpenBiddingNotice', 1, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to1@example.test', 1),
        (N'RouteSqmsOpenBiddingNotice', 2, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to2@example.test', 1),
        (N'RouteSqmsOpenBiddingNotice', 3, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to3@example.test', 1),
        (N'RouteSqmsOpenBiddingNotice', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsOpenBiddingNotice', 5, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1),
        (N'RouteSqmsOpenBiddingReminder', 1, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to1@example.test', 1),
        (N'RouteSqmsOpenBiddingReminder', 2, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to2@example.test', 1),
        (N'RouteSqmsOpenBiddingReminder', 3, N'To', N'StaticAddress', NULL, N'sqms-open-bidding-to3@example.test', 1),
        (N'RouteSqmsOpenBiddingReminder', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsOpenBiddingReminder', 5, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1),
        (N'RouteSqmsSellerApprovalRequest', 1, N'To', N'RequestInput', N'opportunityRelationshipManager', NULL, 1),
        (N'RouteSqmsSellerApprovalRequest', 2, N'To', N'RequestInput', N'sellerRmTeam', NULL, 1),
        (N'RouteSqmsSellerApprovalRequest', 3, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsSellerApprovalRequest', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1),
        (N'RouteSqmsBidAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', NULL, 1),
        (N'RouteSqmsBidAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', NULL, 1),
        (N'RouteSqmsBidAccepted', 3, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsBidAccepted', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1),
        (N'RouteSqmsBidPartiallyAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', NULL, 1),
        (N'RouteSqmsBidPartiallyAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', NULL, 1),
        (N'RouteSqmsBidPartiallyAccepted', 3, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsBidPartiallyAccepted', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1),
        (N'RouteSqmsBidNotAccepted', 1, N'To', N'RequestInput', N'bidRelationshipManager', NULL, 1),
        (N'RouteSqmsBidNotAccepted', 2, N'To', N'RequestInput', N'buyerRmTeam', NULL, 1),
        (N'RouteSqmsBidNotAccepted', 3, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc1@example.test', 1),
        (N'RouteSqmsBidNotAccepted', 4, N'Cc', N'StaticAddress', NULL, N'sqms-static-cc2@example.test', 1);

    INSERT dbo.NotificationRecipientRule
        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress,
         AssignedRoleKey, SortOrder, IsRequired, IsActive)
    SELECT route.Id, expected.RecipientKind, expected.SourceType, expected.SourceKey,
        expected.StaticAddress, NULL, expected.SortOrder, expected.IsRequired, 1
    FROM #RecipientSeed AS expected
    INNER JOIN dbo.NotificationRoute AS route ON route.RouteKey = expected.RouteKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #RouteSeed AS routeScope
        ON routeScope.WorkflowKey = definition.WorkflowKey AND routeScope.RouteKey = route.RouteKey
    WHERE version.VersionNumber = 1
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.NotificationRecipientRule AS actual
          WHERE actual.NotificationRouteId = route.Id
            AND actual.SortOrder = expected.SortOrder
      );

    UPDATE actual
    SET RecipientKind = expected.RecipientKind,
        SourceType = expected.SourceType,
        SourceKey = expected.SourceKey,
        StaticAddress = expected.StaticAddress,
        AssignedRoleKey = NULL,
        IsRequired = expected.IsRequired,
        IsActive = 1
    FROM dbo.NotificationRecipientRule AS actual
    INNER JOIN dbo.NotificationRoute AS route ON route.Id = actual.NotificationRouteId
    INNER JOIN #RecipientSeed AS expected
        ON expected.RouteKey = route.RouteKey AND expected.SortOrder = actual.SortOrder;

    CREATE TABLE #TransitionRouteSeed
    (
        WorkflowKey nvarchar(100) NOT NULL,
        FromStateKey nvarchar(100) NOT NULL,
        ActionKey nvarchar(100) NOT NULL,
        AllowedRoleKey nvarchar(100) NOT NULL,
        ToStateKey nvarchar(100) NOT NULL,
        RouteKey nvarchar(100) NOT NULL,
        AuditLabel nvarchar(200) NOT NULL,
        PRIMARY KEY (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey)
    );
    INSERT #TransitionRouteSeed
        (WorkflowKey, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey, RouteKey, AuditLabel)
    VALUES
        (N'SqmsSaleCycle', N'Draft', N'SendOpenBiddingNotice', N'System', N'NoticeQueued', N'RouteSqmsOpenBiddingNotice', N'System sent open bidding notice'),
        (N'SqmsSaleCycle', N'NoticeQueued', N'SendOpenBiddingReminder', N'System', N'ReminderQueued', N'RouteSqmsOpenBiddingReminder', N'System sent open bidding reminder'),
        (N'SqmsSaleCycle', N'ReminderQueued', N'CompleteSaleCycle', N'System', N'Completed', N'', N'System completed sale cycle'),
        (N'SqmsOpportunity', N'ClosedToBidding', N'RequestSellerApproval', N'IPG', N'PendingSellerApproval', N'RouteSqmsSellerApprovalRequest', N'IPG requested seller approval'),
        (N'SqmsBid', N'UnderReviewBySeller', N'AcceptBid', N'MFO', N'Accepted', N'RouteSqmsBidAccepted', N'MFO accepted full bid'),
        (N'SqmsBid', N'UnderReviewBySeller', N'PartiallyAcceptBid', N'MFO', N'PartiallyAccepted', N'RouteSqmsBidPartiallyAccepted', N'MFO partially accepted bid'),
        (N'SqmsBid', N'UnderReviewBySeller', N'RejectBid', N'MFO', N'NotAccepted', N'RouteSqmsBidNotAccepted', N'MFO did not accept bid');

    INSERT dbo.WorkflowTransitionDefinition
        (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey,
         TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive)
    SELECT version.Id, expected.FromStateKey, expected.ActionKey, expected.AllowedRoleKey,
        expected.ToStateKey, NULL, NULL, NULLIF(expected.RouteKey, N''), expected.AuditLabel, 1
    FROM #TransitionRouteSeed AS expected
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.WorkflowKey = expected.WorkflowKey
    INNER JOIN dbo.WorkflowDefinitionVersion AS version
        ON version.WorkflowDefinitionId = definition.Id AND version.VersionNumber = 1
    WHERE NOT EXISTS
    (
        SELECT 1 FROM dbo.WorkflowTransitionDefinition AS actual
        WHERE actual.WorkflowDefinitionVersionId = version.Id
          AND actual.FromStateKey = expected.FromStateKey
          AND actual.ActionKey = expected.ActionKey
          AND actual.AllowedRoleKey = expected.AllowedRoleKey
    );

    UPDATE actual
    SET ToStateKey = expected.ToStateKey,
        TaskToCloseKey = NULL,
        TaskToCreateKey = NULL,
        NotificationRouteKey = NULLIF(expected.RouteKey, N''),
        AuditLabel = expected.AuditLabel,
        IsActive = 1
    FROM dbo.WorkflowTransitionDefinition AS actual
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = actual.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    INNER JOIN #TransitionRouteSeed AS expected
        ON expected.WorkflowKey = definition.WorkflowKey
        AND expected.FromStateKey = actual.FromStateKey
        AND expected.ActionKey = actual.ActionKey
        AND expected.AllowedRoleKey = actual.AllowedRoleKey
    WHERE version.VersionNumber = 1;

    COMMIT TRANSACTION;

    SELECT definition.WorkflowKey, route.RouteKey, route.NotificationType
    FROM dbo.NotificationRoute AS route
    INNER JOIN dbo.WorkflowDefinitionVersion AS version ON version.Id = route.WorkflowDefinitionVersionId
    INNER JOIN dbo.WorkflowDefinition AS definition ON definition.Id = version.WorkflowDefinitionId
    WHERE route.RouteKey LIKE N'RouteSqms%'
    ORDER BY definition.WorkflowKey, route.RouteKey;

    DROP TABLE #TransitionRouteSeed;
    DROP TABLE #RecipientSeed;
    DROP TABLE #RouteSeed;
    DROP TABLE #ActionSeed;
    DROP TABLE #StateSeed;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

PRINT 'SQMS end-to-end metadata seed passed.';
