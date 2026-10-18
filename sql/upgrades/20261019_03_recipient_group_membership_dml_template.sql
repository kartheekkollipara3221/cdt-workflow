-- CDT PortfolioTrade: add or deactivate ONE person in ONE recipient group.
-- Edit the four parameters, then run. One transaction. Repeating a run changes nothing.
-- The change applies to every route of the group in the chosen version.
-- Pending tasks keep their saved recipients; the change applies to tasks created later.
-- Rows are never deleted. To replace a person, ADD the new person first, then DEACTIVATE.
--
-- Groups, SortOrder blocks and routes:
--   ICG_RA          10-49    RouteCheckerAssignedOnSubmit, RouteCheckerAssignedOnResubmit, RouteTradingDeskAssigned
--   RISK            50-89    the three routes above + RouteBdrmAssigned, RouteWorkflowCompleted
--   DESK_TAXABLE    100-199  RouteTradingDeskAssigned, RouteTradingDeskDueToday, RouteTradingDeskOverdue (TradingDeskTaxable)
--   DESK_TAX_EXEMPT 200-299  same three routes (TradingDeskTaxExempt)
--   DESK_EQUITY     300-399  same three routes (TradingDeskEquity)
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @VersionNumber int = 2;                               -- PortfolioTrade version to change
DECLARE @Group nvarchar(30) = N'ICG_RA';                      -- see the list above
DECLARE @Address nvarchar(320) = N'first.last@yourbank.com';  -- one address, no delimiters
DECLARE @Action nvarchar(10) = N'ADD';                        -- ADD or DEACTIVATE

BEGIN TRY
    BEGIN TRANSACTION;

    IF @Action NOT IN (N'ADD', N'DEACTIVATE')
        THROW 52301, '@Action must be ADD or DEACTIVATE.', 1;
    IF @Address IS NULL OR LTRIM(RTRIM(@Address)) = N'' OR @Address LIKE N'%[;,]%'
        THROW 52302, '@Address must be one non-empty address without ; or , delimiters.', 1;

    DECLARE @BlockStart int, @BlockEnd int, @AssignedRoleKey nvarchar(100);
    SELECT @BlockStart = BlockStart, @BlockEnd = BlockEnd, @AssignedRoleKey = AssignedRoleKey
    FROM (VALUES
        (N'ICG_RA', 10, 49, CAST(NULL AS nvarchar(100))),
        (N'RISK', 50, 89, NULL),
        (N'DESK_TAXABLE', 100, 199, N'TradingDeskTaxable'),
        (N'DESK_TAX_EXEMPT', 200, 299, N'TradingDeskTaxExempt'),
        (N'DESK_EQUITY', 300, 399, N'TradingDeskEquity')
    ) AS g (GroupKey, BlockStart, BlockEnd, AssignedRoleKey)
    WHERE g.GroupKey = @Group;
    IF @BlockStart IS NULL
        THROW 52303, '@Group is not a known recipient group.', 1;

    DECLARE @Routes TABLE (RouteKey nvarchar(100) NOT NULL PRIMARY KEY);
    INSERT @Routes (RouteKey)
    SELECT RouteKey FROM (VALUES
        (N'ICG_RA', N'RouteCheckerAssignedOnSubmit'), (N'ICG_RA', N'RouteCheckerAssignedOnResubmit'), (N'ICG_RA', N'RouteTradingDeskAssigned'),
        (N'RISK', N'RouteCheckerAssignedOnSubmit'), (N'RISK', N'RouteCheckerAssignedOnResubmit'), (N'RISK', N'RouteTradingDeskAssigned'),
        (N'RISK', N'RouteBdrmAssigned'), (N'RISK', N'RouteWorkflowCompleted'),
        (N'DESK_TAXABLE', N'RouteTradingDeskAssigned'), (N'DESK_TAXABLE', N'RouteTradingDeskDueToday'), (N'DESK_TAXABLE', N'RouteTradingDeskOverdue'),
        (N'DESK_TAX_EXEMPT', N'RouteTradingDeskAssigned'), (N'DESK_TAX_EXEMPT', N'RouteTradingDeskDueToday'), (N'DESK_TAX_EXEMPT', N'RouteTradingDeskOverdue'),
        (N'DESK_EQUITY', N'RouteTradingDeskAssigned'), (N'DESK_EQUITY', N'RouteTradingDeskDueToday'), (N'DESK_EQUITY', N'RouteTradingDeskOverdue')
    ) AS m (GroupKey, RouteKey)
    WHERE m.GroupKey = @Group;

    DECLARE @VersionId bigint = (
        SELECT v.Id FROM dbo.WorkflowDefinitionVersion AS v
        JOIN dbo.WorkflowDefinition AS d ON d.Id = v.WorkflowDefinitionId
        WHERE d.WorkflowKey = N'PortfolioTrade' AND v.VersionNumber = @VersionNumber);
    IF @VersionId IS NULL
        THROW 52304, 'The chosen PortfolioTrade version was not found.', 1;

    DECLARE @RouteIds TABLE (RouteKey nvarchar(100) NOT NULL, RouteId bigint NOT NULL);
    INSERT @RouteIds SELECT r.RouteKey, nr.Id
    FROM @Routes AS r
    JOIN dbo.NotificationRoute AS nr ON nr.WorkflowDefinitionVersionId = @VersionId AND nr.RouteKey = r.RouteKey;
    IF (SELECT COUNT(*) FROM @RouteIds) <> (SELECT COUNT(*) FROM @Routes)
        THROW 52305, 'A route of this group is missing in the chosen version.', 1;

    DECLARE @Report TABLE (RouteKey nvarchar(100), SortOrder int, Result nvarchar(40));
    DECLARE @RouteKey nvarchar(100), @RouteId bigint, @SortOrder int;
    DECLARE route_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT RouteKey, RouteId FROM @RouteIds ORDER BY RouteKey;
    OPEN route_cursor;
    FETCH NEXT FROM route_cursor INTO @RouteKey, @RouteId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @Action = N'ADD'
        BEGIN
            IF EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule WITH (UPDLOCK, HOLDLOCK)
                WHERE NotificationRouteId = @RouteId AND SortOrder BETWEEN @BlockStart AND @BlockEnd
                  AND SourceType = N'StaticAddress' AND StaticAddress = @Address AND IsActive = 1)
                INSERT @Report VALUES (@RouteKey, NULL, N'already active');
            ELSE
            BEGIN
                SET @SortOrder = (SELECT MIN(SortOrder) FROM dbo.NotificationRecipientRule
                    WHERE NotificationRouteId = @RouteId AND SortOrder BETWEEN @BlockStart AND @BlockEnd
                      AND SourceType = N'StaticAddress' AND StaticAddress = @Address AND IsActive = 0);
                IF @SortOrder IS NOT NULL
                BEGIN
                    UPDATE dbo.NotificationRecipientRule
                    SET IsActive = 1, RecipientKind = N'To', IsRequired = 1, AssignedRoleKey = @AssignedRoleKey
                    WHERE NotificationRouteId = @RouteId AND SortOrder = @SortOrder;
                    INSERT @Report VALUES (@RouteKey, @SortOrder, N'reactivated');
                END
                ELSE
                BEGIN
                    -- Inactive rows keep their SortOrder, so the next free value skips them.
                    SET @SortOrder = (
                        SELECT MIN(n.SortOrder)
                        FROM (SELECT TOP (@BlockEnd - @BlockStart + 1)
                                  @BlockStart - 1 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS SortOrder
                              FROM sys.all_objects) AS n
                        WHERE NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule AS used
                            WHERE used.NotificationRouteId = @RouteId AND used.SortOrder = n.SortOrder));
                    IF @SortOrder IS NULL
                        THROW 52306, 'The SortOrder block of this group is full on a route. No changes were made.', 1;
                    INSERT dbo.NotificationRecipientRule
                        (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, SortOrder, IsRequired, AssignedRoleKey, IsActive)
                    VALUES (@RouteId, N'To', N'StaticAddress', NULL, @Address, @SortOrder, 1, @AssignedRoleKey, 1);
                    INSERT @Report VALUES (@RouteKey, @SortOrder, N'added');
                END
            END
        END
        ELSE
        BEGIN
            UPDATE dbo.NotificationRecipientRule SET IsActive = 0
            OUTPUT @RouteKey, inserted.SortOrder, N'deactivated' INTO @Report
            WHERE NotificationRouteId = @RouteId AND SortOrder BETWEEN @BlockStart AND @BlockEnd
              AND SourceType = N'StaticAddress' AND StaticAddress = @Address AND IsActive = 1;
            IF @@ROWCOUNT = 0
                INSERT @Report VALUES (@RouteKey, NULL, N'not active; no change');

            -- Never leave a desk without an active desk user on a desk route, or a
            -- group route without any active To recipient.
            IF NOT EXISTS (SELECT 1 FROM dbo.NotificationRecipientRule
                WHERE NotificationRouteId = @RouteId AND IsActive = 1 AND RecipientKind = N'To'
                  AND (@AssignedRoleKey IS NULL OR AssignedRoleKey = @AssignedRoleKey))
                THROW 52307, 'This would leave a route or desk without an active To recipient. Add the replacement first. No changes were made.', 1;
        END

        FETCH NEXT FROM route_cursor INTO @RouteKey, @RouteId;
    END
    CLOSE route_cursor;
    DEALLOCATE route_cursor;

    COMMIT TRANSACTION;
    SELECT @Group AS GroupKey, @Address AS Address, RouteKey, SortOrder, Result FROM @Report ORDER BY RouteKey;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
