-- CDT PortfolioTrade v2, step 1 of 2: create version 2 INACTIVE.
-- Requires 20261019_01_cdt_v2_schema_ddl.sql (or EF migration AddCdtV2SlaReminderControls).
-- Version 1 stays active and unchanged. New trades keep using version 1.
-- Unchanged parts (states, roles, actions, participants, tasks, assignment roles,
-- transitions, route templates, TradingDeskRework recipients) are copied from the
-- version 1 rows in THIS database. The new routes, recipient rules and SLA policies
-- follow PortfolioTradeSeedDataV2. Addresses are placeholders (@example.test):
-- replace them with 20261019_03 before you run the verify and activate scripts.
-- Stops without changes if version 2 already exists. One transaction.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.SlaPolicy', N'DueReminderLocalTime') IS NULL
        OR COL_LENGTH(N'dbo.WorkflowTaskSlaSchedule', N'DueRecipientsJson') IS NULL
        THROW 52101, 'Run 20261019_01_cdt_v2_schema_ddl.sql first.', 1;

    DECLARE @DefinitionId bigint = (SELECT Id FROM dbo.WorkflowDefinition WHERE WorkflowKey = N'PortfolioTrade');
    IF @DefinitionId IS NULL
        THROW 52102, 'Workflow definition PortfolioTrade was not found.', 1;

    DECLARE @V1 bigint = (SELECT Id FROM dbo.WorkflowDefinitionVersion
        WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 1);
    IF @V1 IS NULL
        THROW 52103, 'PortfolioTrade version 1 was not found.', 1;

    IF EXISTS (SELECT 1 FROM dbo.WorkflowDefinitionVersion
        WHERE WorkflowDefinitionId = @DefinitionId AND VersionNumber = 2)
        THROW 52104, 'PortfolioTrade version 2 already exists. No changes were made.', 1;

    INSERT dbo.WorkflowDefinitionVersion (WorkflowDefinitionId, VersionNumber, IsActive, CreatedAtUtc)
    VALUES (@DefinitionId, 2, 0, SYSDATETIMEOFFSET());
    DECLARE @V2 bigint = SCOPE_IDENTITY();

    -- Unchanged definitions: copied from version 1.
    INSERT dbo.WorkflowStateDefinition (WorkflowDefinitionVersionId, StateKey, DisplayName, IsTerminal, IsActive)
    SELECT @V2, StateKey, DisplayName, IsTerminal, IsActive FROM dbo.WorkflowStateDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowParticipantDefinition (WorkflowDefinitionVersionId, ParticipantKey, Cardinality, CaptureStage, IsRequired, IsImmutable, IsActive)
    SELECT @V2, ParticipantKey, Cardinality, CaptureStage, IsRequired, IsImmutable, IsActive FROM dbo.WorkflowParticipantDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowRoleDefinition (WorkflowDefinitionVersionId, RoleKey, DisplayName, IsActive)
    SELECT @V2, RoleKey, DisplayName, IsActive FROM dbo.WorkflowRoleDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowActionDefinition (WorkflowDefinitionVersionId, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder, IsActive)
    SELECT @V2, ActionKey, DisplayName, SectionKey, ButtonKey, ButtonLabel, Intent, RequiresReason, SortOrder, IsActive FROM dbo.WorkflowActionDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowTaskDefinition (WorkflowDefinitionVersionId, TaskKey, DisplayName, OwnerRoleKey, SlaPolicyKey, IsActive)
    SELECT @V2, TaskKey, DisplayName, OwnerRoleKey, SlaPolicyKey, IsActive FROM dbo.WorkflowTaskDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowTaskAssignmentRoleDefinition (WorkflowDefinitionVersionId, TaskKey, RoleKey, IsActive)
    SELECT @V2, TaskKey, RoleKey, IsActive FROM dbo.WorkflowTaskAssignmentRoleDefinition WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.WorkflowTransitionDefinition (WorkflowDefinitionVersionId, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey, TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive)
    SELECT @V2, FromStateKey, ActionKey, AllowedRoleKey, ToStateKey, TaskToCloseKey, TaskToCreateKey, NotificationRouteKey, AuditLabel, IsActive FROM dbo.WorkflowTransitionDefinition WHERE WorkflowDefinitionVersionId = @V1;

    -- Routes: the version 1 routes, plus three new SLA reminder routes.
    INSERT dbo.NotificationRoute (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT @V2, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V1;

    INSERT dbo.NotificationRoute (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT @V2, N'RouteTradingDeskDueToday', NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, N'Due Today: ', 1
    FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V1 AND RouteKey = N'RouteSlaTaskDueToday';
    INSERT dbo.NotificationRoute (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT @V2, N'RouteTradingDeskOverdue', NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, N'Overdue: ', 1
    FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V1 AND RouteKey = N'RouteSlaTaskOverdue';
    INSERT dbo.NotificationRoute (WorkflowDefinitionVersionId, RouteKey, NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, OverdueSubjectPrefix, IsActive)
    SELECT @V2, N'RouteBdrmConfirmationOverdue', NotificationType, SubjectTemplate, BodyTemplate, HtmlBodyTemplate, N'Overdue: ', 1
    FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V1 AND RouteKey = N'RouteSlaTaskOverdue';

    IF (SELECT COUNT(*) FROM dbo.NotificationRoute WHERE WorkflowDefinitionVersionId = @V2
        AND RouteKey IN (N'RouteTradingDeskDueToday', N'RouteTradingDeskOverdue', N'RouteBdrmConfirmationOverdue')) <> 3
        THROW 52105, 'Version 1 must contain RouteSlaTaskDueToday and RouteSlaTaskOverdue.', 1;

    -- SLA policies (version 2 values). TradingDeskReworkSla is copied from version 1.
    -- Dynamic SQL: the new columns exist only after the DDL script.
    EXEC sys.sp_executesql N'
        INSERT dbo.SlaPolicy (WorkflowDefinitionVersionId, SlaPolicyKey, CalculationMode, DurationMinutes, SendDueDateReminder,
            ReminderLocalTime, TimeZoneId, OverdueStartOffsetDays, RecurrenceIntervalMinutes, RecurrenceIntervalDays,
            DueNotificationRouteKey, OverdueNotificationRouteKey, DueReminderLocalTime, SkipMissedDueReminder,
            OverdueStartsNextLocalDay, IsActive)
        VALUES
            (@V2, N''CheckerReviewSla'', N''DurationFromTaskCreation'', 60, 0, NULL, NULL, 0, 60, NULL,
                NULL, N''RouteSlaTaskOverdue'', NULL, 0, 0, 1),
            (@V2, N''TradingDeskReviewSla'', N''ApplicationTargetDate'', NULL, 1, ''08:00'', N''America/New_York'', 1, NULL, 1,
                N''RouteTradingDeskDueToday'', N''RouteTradingDeskOverdue'', ''14:00'', 1, 0, 1),
            (@V2, N''BdrmClientServiceReviewSla'', N''DurationFromTaskCreation'', 1440, 0, ''09:00'', N''America/New_York'', 0, NULL, 1,
                NULL, N''RouteBdrmConfirmationOverdue'', NULL, 0, 1, 1);
        INSERT dbo.SlaPolicy (WorkflowDefinitionVersionId, SlaPolicyKey, CalculationMode, DurationMinutes, SendDueDateReminder,
            ReminderLocalTime, TimeZoneId, OverdueStartOffsetDays, RecurrenceIntervalMinutes, RecurrenceIntervalDays,
            DueNotificationRouteKey, OverdueNotificationRouteKey, DueReminderLocalTime, SkipMissedDueReminder,
            OverdueStartsNextLocalDay, IsActive)
        SELECT @V2, SlaPolicyKey, CalculationMode, DurationMinutes, SendDueDateReminder,
            ReminderLocalTime, TimeZoneId, OverdueStartOffsetDays, RecurrenceIntervalMinutes, RecurrenceIntervalDays,
            DueNotificationRouteKey, OverdueNotificationRouteKey, DueReminderLocalTime, SkipMissedDueReminder,
            OverdueStartsNextLocalDay, IsActive
        FROM dbo.SlaPolicy WHERE WorkflowDefinitionVersionId = @V1 AND SlaPolicyKey = N''TradingDeskReworkSla'';',
        N'@V1 bigint, @V2 bigint', @V1 = @V1, @V2 = @V2;

    -- Recipient rules. SortOrder blocks: 1 originalMaker, 10-49 ICG RAs, 50-89 Risk Team,
    -- 100-199 Taxable, 200-299 Tax Exempt, 300-399 Equity, 1000 selectedCcUsers.
    DECLARE @Rules TABLE (
        RouteKey nvarchar(100) NOT NULL, RecipientKind nvarchar(10) NOT NULL, SourceType nvarchar(50) NOT NULL,
        SourceKey nvarchar(100) NULL, StaticAddress nvarchar(320) NULL, SortOrder int NOT NULL,
        IsRequired bit NOT NULL, AssignedRoleKey nvarchar(100) NULL);
    DECLARE @CheckerGroups TABLE (StaticAddress nvarchar(320) NOT NULL, SortOrder int NOT NULL);
    INSERT @CheckerGroups VALUES
        (N'icg-ra-1@example.test', 10), (N'icg-ra-2@example.test', 11),
        (N'risk-team-1@example.test', 50), (N'risk-team-2@example.test', 51);
    DECLARE @DeskUsers TABLE (StaticAddress nvarchar(320) NOT NULL, SortOrder int NOT NULL, AssignedRoleKey nvarchar(100) NOT NULL);
    INSERT @DeskUsers VALUES
        (N'taxable-trader-1@example.test', 100, N'TradingDeskTaxable'),
        (N'tax-exempt-trader-1@example.test', 200, N'TradingDeskTaxExempt'),
        (N'equity-trader-1@example.test', 300, N'TradingDeskEquity');

    INSERT @Rules SELECT r.RouteKey, N'To', N'StaticAddress', NULL, g.StaticAddress, g.SortOrder, 1, NULL
    FROM (VALUES (N'RouteCheckerAssignedOnSubmit'), (N'RouteCheckerAssignedOnResubmit'), (N'RouteTradingDeskAssigned')) AS r (RouteKey)
    CROSS JOIN @CheckerGroups AS g;
    INSERT @Rules SELECT r.RouteKey, N'To', N'StaticAddress', NULL, g.StaticAddress, g.SortOrder, 1, NULL
    FROM (VALUES (N'RouteBdrmAssigned'), (N'RouteWorkflowCompleted')) AS r (RouteKey)
    CROSS JOIN @CheckerGroups AS g WHERE g.SortOrder BETWEEN 50 AND 89;
    INSERT @Rules SELECT r.RouteKey, N'To', N'StaticAddress', NULL, d.StaticAddress, d.SortOrder, 1, d.AssignedRoleKey
    FROM (VALUES (N'RouteTradingDeskAssigned'), (N'RouteTradingDeskDueToday'), (N'RouteTradingDeskOverdue')) AS r (RouteKey)
    CROSS JOIN @DeskUsers AS d;
    INSERT @Rules SELECT r.RouteKey, N'To', N'WorkflowParticipant', N'originalMaker', NULL, 1, 1, NULL
    FROM (VALUES (N'RouteReturnedToMakerFromChecker'), (N'RouteReturnedToMakerFromTradingDesk'), (N'RouteBdrmAssigned'),
        (N'RouteBdrmConfirmationOverdue'), (N'RouteWorkflowCompleted')) AS r (RouteKey);
    INSERT @Rules SELECT r.RouteKey, N'Cc', N'RequestInput', N'selectedCcUsers', NULL, 1000, 0, NULL
    FROM (VALUES (N'RouteCheckerAssignedOnSubmit'), (N'RouteCheckerAssignedOnResubmit'), (N'RouteTradingDeskAssigned'),
        (N'RouteBdrmAssigned')) AS r (RouteKey);

    INSERT dbo.NotificationRecipientRule (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, SortOrder, IsRequired, AssignedRoleKey, IsActive)
    SELECT route.Id, nr.RecipientKind, nr.SourceType, nr.SourceKey, nr.StaticAddress, nr.SortOrder, nr.IsRequired, nr.AssignedRoleKey, 1
    FROM @Rules AS nr
    JOIN dbo.NotificationRoute AS route ON route.WorkflowDefinitionVersionId = @V2 AND route.RouteKey = nr.RouteKey;
    IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @Rules)
        THROW 52106, 'A version 2 recipient rule references a missing route.', 1;

    -- TradingDeskRework is not used by CDT; it keeps the active version 1 recipients.
    INSERT dbo.NotificationRecipientRule (NotificationRouteId, RecipientKind, SourceType, SourceKey, StaticAddress, SortOrder, IsRequired, AssignedRoleKey, IsActive)
    SELECT route2.Id, rule1.RecipientKind, rule1.SourceType, rule1.SourceKey, rule1.StaticAddress, rule1.SortOrder, rule1.IsRequired, rule1.AssignedRoleKey, 1
    FROM dbo.NotificationRecipientRule AS rule1
    JOIN dbo.NotificationRoute AS route1 ON route1.Id = rule1.NotificationRouteId
        AND route1.WorkflowDefinitionVersionId = @V1 AND route1.RouteKey = N'RouteTradingDeskReworkAssigned'
    JOIN dbo.NotificationRoute AS route2 ON route2.WorkflowDefinitionVersionId = @V2 AND route2.RouteKey = route1.RouteKey
    WHERE rule1.IsActive = 1;

    COMMIT TRANSACTION;
    PRINT CONCAT(N'PortfolioTrade version 2 created inactive. WorkflowDefinitionVersionId = ', @V2, N'.');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
