-- CDT PortfolioTrade v2: read-only verification before activation.
-- Shows the versions, the version 2 recipient rules by group block and the SLA policies,
-- then the release checks. Ends with an error if any check fails. Changes nothing.
SET NOCOUNT ON;

SELECT v.VersionNumber, v.IsActive, v.CreatedAtUtc
FROM dbo.WorkflowDefinitionVersion AS v
JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId
WHERE d.WorkflowKey = N'PortfolioTrade'
ORDER BY v.VersionNumber;

SELECT nr.RouteKey, r.SortOrder,
    CASE WHEN r.SortOrder = 1 THEN N'originalMaker' WHEN r.SortOrder BETWEEN 10 AND 49 THEN N'ICG_RA'
         WHEN r.SortOrder BETWEEN 50 AND 89 THEN N'RISK' WHEN r.SortOrder BETWEEN 100 AND 199 THEN N'DESK_TAXABLE'
         WHEN r.SortOrder BETWEEN 200 AND 299 THEN N'DESK_TAX_EXEMPT' WHEN r.SortOrder BETWEEN 300 AND 399 THEN N'DESK_EQUITY'
         WHEN r.SortOrder = 1000 THEN N'selectedCcUsers' ELSE N'other' END AS RecipientGroup,
    r.RecipientKind, r.SourceType, COALESCE(r.StaticAddress, r.SourceKey) AS Recipient, r.AssignedRoleKey, r.IsActive
FROM dbo.NotificationRecipientRule AS r
JOIN dbo.NotificationRoute AS nr ON nr.Id = r.NotificationRouteId
JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = nr.WorkflowDefinitionVersionId AND v.VersionNumber = 2
JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId AND d.WorkflowKey = N'PortfolioTrade'
ORDER BY nr.RouteKey, r.SortOrder;

EXEC sys.sp_executesql N'
SELECT p.SlaPolicyKey, p.CalculationMode, p.DurationMinutes, p.SendDueDateReminder, p.ReminderLocalTime,
    p.DueReminderLocalTime, p.SkipMissedDueReminder, p.OverdueStartsNextLocalDay, p.TimeZoneId,
    p.OverdueStartOffsetDays, p.RecurrenceIntervalMinutes, p.RecurrenceIntervalDays,
    p.DueNotificationRouteKey, p.OverdueNotificationRouteKey
FROM dbo.SlaPolicy AS p
JOIN dbo.WorkflowDefinitionVersion AS v ON v.Id = p.WorkflowDefinitionVersionId AND v.VersionNumber = 2
JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId AND d.WorkflowKey = N''PortfolioTrade''
ORDER BY p.SlaPolicyKey;';

BEGIN
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

    SELECT CheckName, CASE WHEN Passed = 1 THEN N'PASS' ELSE N'FAIL' END AS Result, Detail FROM @Checks;
    IF EXISTS (SELECT 1 FROM @Checks WHERE Passed = 0)
        THROW 52401, 'One or more CDT v2 release checks failed. Do not activate version 2.', 1;
    PRINT N'All CDT v2 release checks passed.';
END;
