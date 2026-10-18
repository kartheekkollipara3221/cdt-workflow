-- CDT PortfolioTrade v2, step 2 of 2: activate version 2 for NEW trades.
-- Runs the same checks as 20261019_04 first and stops without changes if any fails.
-- Then sets version 1 inactive and version 2 active in one transaction.
-- Running trades stay on their own version. Do not install an engine build older
-- than the CDT v2 release while version 2 trades are open.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @DefinitionId bigint = (SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'PortfolioTrade');
    DECLARE @V1 bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 1);
    DECLARE @V2 bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 2);
    DECLARE @Checks TABLE (CheckName nvarchar(200) NOT NULL, Passed bit NOT NULL, Detail nvarchar(max) NULL);

    INSERT @Checks SELECT N'PortfolioTrade versions 1 and 2 exist',
        CASE WHEN @V1 IS NOT NULL AND @V2 IS NOT NULL THEN 1 ELSE 0 END, NULL;

    IF @V2 IS NOT NULL
    BEGIN
        INSERT @Checks SELECT N'No placeholder address (@example.test) is active in version 2',
            CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END,
            STRING_AGG(CONCAT(nr.RouteKey, N'/', r.SortOrder, N' ', r.StaticAddress), N'; ')
        FROM dbo.NotificationRecipientRule AS r
        JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
        WHERE nr.WorkflowDefinitionVersionId = @V2 AND r.IsActive = 1 AND r.StaticAddress LIKE N'%@example.test'
          -- Copied unchanged from version 1 and not used by CDT.
          AND nr.RouteKey <> N'RouteTradingDeskReworkAssigned';

        INSERT @Checks SELECT N'Every version 2 transition route has an active To recipient',
            CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END, STRING_AGG(t.RouteKey, N'; ')
        FROM (SELECT DISTINCT NotificationRouteKey AS RouteKey FROM dbo.WorkflowTransitionDefinition
              WHERE WorkflowDefinitionVersionId = @V2 AND IsActive = 1 AND NotificationRouteKey IS NOT NULL) AS t
        WHERE NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule AS r
            JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
            WHERE nr.WorkflowDefinitionVersionId = @V2 AND nr.RouteKey = t.RouteKey AND r.IsActive = 1 AND r.RecipientKind = N'To');

        -- Every desk needs an active desk user (AssignedRoleKey = desk) on each desk route.
        INSERT @Checks SELECT N'Every desk has an active desk user on each desk route',
            CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END, STRING_AGG(CONCAT(x.RouteKey, N'/', x.RoleKey), N'; ')
        FROM (SELECT route.RouteKey, roles.RoleKey
              FROM (VALUES (N'RouteTradingDeskAssigned'), (N'RouteTradingDeskDueToday'), (N'RouteTradingDeskOverdue')) AS route (RouteKey)
              CROSS JOIN (SELECT RoleKey COLLATE DATABASE_DEFAULT AS RoleKey FROM dbo.WorkflowTaskAssignmentRoleDefinition
                          WHERE WorkflowDefinitionVersionId = @V2 AND TaskKey = N'TradingDeskReviewTask' AND IsActive = 1) AS roles) AS x
        WHERE NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule AS r
            JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
            WHERE nr.WorkflowDefinitionVersionId = @V2 AND nr.RouteKey = x.RouteKey
              AND r.IsActive = 1 AND r.RecipientKind = N'To' AND r.AssignedRoleKey = x.RoleKey);

        INSERT @Checks SELECT N'BDRM confirmation overdue route has an active To recipient',
            CASE WHEN EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule AS r
                JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
                WHERE nr.WorkflowDefinitionVersionId = @V2 AND nr.RouteKey = N'RouteBdrmConfirmationOverdue'
                  AND r.IsActive = 1 AND r.RecipientKind = N'To') THEN 1 ELSE 0 END, NULL;

        INSERT @Checks SELECT N'Own-rules SLA reminder routes have no active RequestInput rule',
            CASE WHEN COUNT(*) = 0 THEN 1 ELSE 0 END, STRING_AGG(nr.RouteKey, N'; ')
        FROM dbo.NotificationRecipientRule AS r
        JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
        WHERE nr.WorkflowDefinitionVersionId = @V2 AND r.IsActive = 1 AND r.SourceType = N'RequestInput'
          AND nr.RouteKey IN (N'RouteTradingDeskDueToday', N'RouteTradingDeskOverdue', N'RouteBdrmConfirmationOverdue');

        INSERT @Checks SELECT N'Version 2 has the same states, roles, actions, tasks and transitions as version 1',
            CASE WHEN (SELECT COUNT(*) FROM dbo.WorkflowStateDefinition WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.WorkflowStateDefinition WHERE WorkflowDefinitionVersionId = @V1)
                  AND (SELECT COUNT(*) FROM dbo.WorkflowRoleDefinition WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.WorkflowRoleDefinition WHERE WorkflowDefinitionVersionId = @V1)
                  AND (SELECT COUNT(*) FROM dbo.WorkflowActionDefinition WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.WorkflowActionDefinition WHERE WorkflowDefinitionVersionId = @V1)
                  AND (SELECT COUNT(*) FROM dbo.WorkflowTaskDefinition WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.WorkflowTaskDefinition WHERE WorkflowDefinitionVersionId = @V1)
                  AND (SELECT COUNT(*) FROM dbo.WorkflowTransitionDefinition WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.WorkflowTransitionDefinition WHERE WorkflowDefinitionVersionId = @V1)
                  AND (SELECT COUNT(*) FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V2)
                    = (SELECT COUNT(*) FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V1) + 3
                  AND (SELECT COUNT(*) FROM dbo.SlaPolicy WHERE WorkflowDefinitionVersionId = @V2) = 4
             THEN 1 ELSE 0 END, NULL;
    END

    IF EXISTS (SELECT 1 FROM @Checks WHERE Passed = 0)
    BEGIN
        SELECT CheckName, CASE WHEN Passed = 1 THEN N'PASS' ELSE N'FAIL' END AS Result, Detail FROM @Checks;
        THROW 52501, 'CDT v2 release checks failed. Version 2 was not activated. No changes were made.', 1;
    END

    UPDATE dbo.WorkflowDefinitionVersion
    SET IsActive = CASE WHEN Id = @V2 THEN 1 ELSE 0 END
    WHERE WorkflowDefinitionId = @DefinitionId;

    IF (SELECT COUNT(*) FROM dbo.WorkflowDefinitionVersion WHERE WorkflowDefinitionId = @DefinitionId AND IsActive = 1) <> 1
        THROW 52502, 'Exactly one PortfolioTrade version must be active.', 1;

    COMMIT TRANSACTION;
    PRINT N'PortfolioTrade version 2 is active for new trades.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
