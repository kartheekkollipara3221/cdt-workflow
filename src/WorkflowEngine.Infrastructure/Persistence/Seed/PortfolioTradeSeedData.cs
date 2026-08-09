namespace WorkflowEngine.Infrastructure.Persistence.Seed;

internal static class PortfolioTradeSeedData
{
    public const string WorkflowKey = "PortfolioTrade";
    public const string BusinessObjectType = "PortfolioTrade";
    public const int VersionNumber = 1;

    public static readonly ParticipantDefinitionSeed[] ParticipantDefinitions =
    [
        new("originalMaker", "Single", "Start", true, true)
    ];

    public static readonly StateSeed[] States =
    [
        new("Draft", "Draft", false),
        new("ReturnedToMaker", "Returned to Maker", false),
        new("CheckerReview", "Checker Review", false),
        new("TradingDeskReview", "Trading Desk Review", false),
        new("TradingDeskRework", "Trading Desk Rework", false),
        new("BdrmClientServiceReview", "BDRM Client Service Review", false),
        new("Completed", "Completed", true)
    ];

    public static readonly RoleSeed[] Roles =
    [
        new("Maker", "Maker"),
        new("Checker", "Checker"),
        new("TradingDesk", "Trading Desk"),
        new("TradingDeskTaxable", "Taxable"),
        new("TradingDeskTaxExempt", "Tax Exempt"),
        new("TradingDeskEquity", "Equity"),
        new("BDRMClientService", "BDRM Client Service")
    ];

    public static readonly ActionSeed[] Actions =
    [
        new("SaveDraft", "Save Draft", "Maker", "MakerPrimary", "Save Draft", "SaveDraft", false, 10),
        new("SubmitForChecker", "Submit for Checker", "Maker", "MakerPrimary", "Submit", "Submit", false, 20),
        new("ResubmitForChecker", "Resubmit for Checker", "Maker", "MakerPrimary", "Resubmit", "Submit", false, 30),
        new("CheckerReturnToMaker", "Return to Maker", "Checker", "CheckerPrimary", "Return to Maker", "Return", true, 40),
        new("CheckerSendToTradingDesk", "Send to Trading Desk", "Checker", "CheckerPrimary", "Send to Trading Desk", "Approve", false, 50),
        new("TradingReturnToMaker", "Return to Maker", "TradingDesk", "TradingPrimary", "Return to Maker", "Return", true, 60),
        new("TradingTradeExecuted", "Trade Executed", "TradingDesk", "TradingPrimary", "Trade Executed", "Approve", false, 70),
        new("BdrmTradeFulfilled", "Trade Fulfilled", "BDRMClientService", "BdrmPrimary", "Fulfill", "Complete", false, 80),
        new("BdrmTradeNotExecutedProperly", "Trade Not Executed Properly", "BDRMClientService", "BdrmPrimary", "Return to Trading Desk", "Rework", true, 90)
    ];

    public static readonly SlaPolicySeed[] SlaPolicies =
    [
        new("CheckerReviewSla", "DurationFromTaskCreation", 2, false, null, null, 0, null, null, "RouteSlaTaskOverdue"),
        new("TradingDeskReviewSla", "ApplicationTargetDate", null, true, new TimeOnly(9, 0), "America/New_York", 1, 1, "RouteSlaTaskDueToday", "RouteSlaTaskOverdue"),
        new("TradingDeskReworkSla", "DurationFromTaskCreation", 2, false, null, null, 0, null, null, "RouteSlaTaskOverdue"),
        new("BdrmClientServiceReviewSla", "DurationFromTaskCreation", 2, false, new TimeOnly(9, 0), "America/New_York", 0, 1, null, "RouteSlaTaskOverdue")
    ];

    public static readonly TaskSeed[] Tasks =
    [
        new("CheckerReviewTask", "Checker Review Task", "Checker", "CheckerReviewSla"),
        new("TradingDeskReviewTask", "Trading Desk Review Task", "TradingDesk", "TradingDeskReviewSla"),
        new("TradingDeskReworkTask", "Trading Desk Rework Task", "TradingDesk", "TradingDeskReworkSla"),
        new("BdrmClientServiceReviewTask", "BDRM Client Service Review Task", "BDRMClientService", "BdrmClientServiceReviewSla"),
        new("MakerReworkTask", "Maker Rework Task", "Maker", null)
    ];

    private const string CheckerAssignedBody = """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following
        Sharepoint link = {{tradeLink}}
        """;

    private const string CheckerAssignedHtmlBody = """
        <p>This is to advise that <strong>{{submitterDisplayName}}</strong> has submitted a request for the <strong>{{deskName}}</strong> desk for security type <strong>{{securityType}}</strong> to execute a trade for account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong>.</p>
        <p>The trade detail for account information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string ReturnedToMakerBody = """
        Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
        This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
        The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
        """;

    private const string ReturnedToMakerHtmlBody = """
        <p>Reference <strong>{{referenceNumber}}</strong> was returned from <strong>{{returnedByDisplayName}}</strong> to <strong>{{recipientDisplayName}}</strong>.</p>
        <p>This is for account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong> originally submitted on <strong>{{submittedDate}}</strong>.</p>
        <p>The account and trade information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string TradingDeskAssignedBody = """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string TradingDeskAssignedHtmlBody = """
        <p>This is to advise that <strong>{{submitterDisplayName}}</strong> has submitted a request for the <strong>{{deskName}}</strong> desk for security type <strong>{{securityType}}</strong> to execute a trade for account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong>.</p>
        <p>The trade detail for account information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string BdrmAssignedBody = """
        This is to confirm that the trade request for account {{accountShortName}}, {{accountDisplayName}} has been completed by trader {{traderDisplayName}} on the {{deskName}} desk for security type {{securityType}}.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string BdrmAssignedHtmlBody = """
        <p>This is to confirm that the trade request for account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong> has been completed by trader <strong>{{traderDisplayName}}</strong> on the <strong>{{deskName}}</strong> desk for security type <strong>{{securityType}}</strong>.</p>
        <p>The trade detail and account information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string WorkflowCompletedBody = """
        This is to confirm that {{confirmerDisplayName}} has verified that the trade was executed and settled in account {{accountShortName}}, {{accountDisplayName}}. The trade request on Sharepoint is now closed.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string WorkflowCompletedHtmlBody = """
        <p>This is to confirm that <strong>{{confirmerDisplayName}}</strong> has verified that the trade was executed and settled in account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong>. The trade request on Sharepoint is now closed.</p>
        <p>The trade detail and account information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string TradingDeskReworkAssignedBody = """
        This is to advise that the trade request for account {{accountShortName}}, {{accountDisplayName}} was returned to the {{deskName}} desk for rework.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string TradingDeskReworkAssignedHtmlBody = """
        <p>This is to advise that the trade request for account <strong>{{accountShortName}}</strong>, <strong>{{accountDisplayName}}</strong> was returned to the <strong>{{deskName}}</strong> desk for rework.</p>
        <p>The trade detail and account information can be accessed here: <a href="{{tradeLink}}">Open trade</a></p>
        """;

    private const string SlaTaskOverdueBody = """
        Workflow instance {{workflowInstanceId}} has an overdue task {{taskKey}} assigned to {{assignedRoleKey}}.
        Task due date: {{dueDateDisplay}}
        Overdue date: {{overdueDateDisplay}}
        Request link: {{tradeLink}}
        """;

    private const string SlaTaskOverdueHtmlBody = """
        <p>Workflow instance <strong>{{workflowInstanceId}}</strong> has an overdue task <strong>{{taskKey}}</strong> assigned to <strong>{{assignedRoleKey}}</strong>.</p>
        <p>Task due date: <strong>{{dueDateDisplay}}</strong></p>
        <p>Overdue date: <strong>{{overdueDateDisplay}}</strong></p>
        <p><a href="{{tradeLink}}">Open request</a></p>
        """;

    private const string SlaTaskDueTodayBody = """
        Workflow instance {{workflowInstanceId}} has task {{taskKey}} assigned to {{assignedRoleKey}} due for completion today.
        Target date: {{targetDate}}
        SLA overdue date: {{dueAtUtc}}
        """;

    private const string SlaTaskDueTodayHtmlBody = """
        <p>Workflow instance <strong>{{workflowInstanceId}}</strong> has task <strong>{{taskKey}}</strong> assigned to <strong>{{assignedRoleKey}}</strong> due for completion today.</p>
        <p>Target date: <strong>{{targetDate}}</strong></p>
        <p>SLA overdue date: <strong>{{dueAtUtc}}</strong></p>
        """;

    public static readonly NotificationRouteSeed[] NotificationRoutes =
    [
        new("RouteCheckerAssignedOnSubmit", "WorkflowTaskAssigned", "Directed Trade Pending Checker Approval", CheckerAssignedBody, CheckerAssignedHtmlBody, "Overdue: "),
        new("RouteCheckerAssignedOnResubmit", "WorkflowTaskAssigned", "Directed Trade Pending Checker Approval", CheckerAssignedBody, CheckerAssignedHtmlBody, "Overdue: "),
        new("RouteReturnedToMakerFromChecker", "WorkflowReturnedToMaker", "Updates Required to Directed Trade Request", ReturnedToMakerBody, ReturnedToMakerHtmlBody, "Overdue: "),
        new("RouteTradingDeskAssigned", "WorkflowTaskAssigned", "Directed Trade Request Pending Execution", TradingDeskAssignedBody, TradingDeskAssignedHtmlBody, "Overdue: "),
        new("RouteReturnedToMakerFromTradingDesk", "WorkflowReturnedToMaker", "Updates Required to Directed Trade Request", ReturnedToMakerBody, ReturnedToMakerHtmlBody, "Overdue: "),
        new("RouteBdrmAssigned", "WorkflowTaskAssigned", "Trade Execution Pending Confirmation", BdrmAssignedBody, BdrmAssignedHtmlBody, "Overdue: "),
        new("RouteWorkflowCompleted", "WorkflowCompleted", "Confirmation that Trade settled in account", WorkflowCompletedBody, WorkflowCompletedHtmlBody, "Overdue: "),
        new("RouteTradingDeskReworkAssigned", "WorkflowTaskAssigned", "Directed Trade Request Pending Rework", TradingDeskReworkAssignedBody, TradingDeskReworkAssignedHtmlBody, "Overdue: "),
        new("RouteSlaTaskDueToday", "SlaTaskDueToday", "Workflow task due today", SlaTaskDueTodayBody, SlaTaskDueTodayHtmlBody, string.Empty),
        new("RouteSlaTaskOverdue", "SlaTaskOverdue", "Workflow task overdue", SlaTaskOverdueBody, SlaTaskOverdueHtmlBody, "Overdue: ")
    ];

    public static readonly NotificationRecipientRuleSeed[] NotificationRecipientRules =
    [
        new("RouteCheckerAssignedOnSubmit", "To", "StaticAddress", null, "checker@example.test", 1, true),
        new("RouteCheckerAssignedOnSubmit", "Cc", "RequestInput", "selectedCcUsers", null, 2, false),
        new("RouteCheckerAssignedOnResubmit", "To", "StaticAddress", null, "checker@example.test", 1, true),
        new("RouteReturnedToMakerFromChecker", "To", "WorkflowParticipant", "originalMaker", null, 1, true),
        new("RouteReturnedToMakerFromChecker", "Cc", "RequestInput", "selectedCcUsers", null, 2, false),
        new("RouteReturnedToMakerFromChecker", "Cc", "StaticAddress", null, "workflow-operations@example.test", 3, false),
        new("RouteTradingDeskAssigned", "To", "StaticAddress", null, "trading-desk@example.test", 1, true),
        new("RouteReturnedToMakerFromTradingDesk", "To", "WorkflowParticipant", "originalMaker", null, 1, true),
        new("RouteBdrmAssigned", "To", "StaticAddress", null, "bdrm-client-service@example.test", 1, true),
        new("RouteTradingDeskReworkAssigned", "To", "StaticAddress", null, "trading-desk@example.test", 1, true),
        new("RouteWorkflowCompleted", "To", "WorkflowParticipant", "originalMaker", null, 1, true)
    ];

    public static readonly TaskAssignmentRoleSeed[] TaskAssignmentRoles =
    [
        new("TradingDeskReviewTask", "TradingDeskTaxable"),
        new("TradingDeskReviewTask", "TradingDeskTaxExempt"),
        new("TradingDeskReviewTask", "TradingDeskEquity")
    ];

    public static readonly TransitionSeed[] Transitions =
    [
        new("Draft", "SaveDraft", "Maker", "Draft", null, null, null, "Maker saved draft"),
        new("Draft", "SubmitForChecker", "Maker", "CheckerReview", null, "CheckerReviewTask", "RouteCheckerAssignedOnSubmit", "Submitted for Checker review"),
        new("ReturnedToMaker", "SaveDraft", "Maker", "ReturnedToMaker", null, null, null, "Maker saved rework draft"),
        new("ReturnedToMaker", "ResubmitForChecker", "Maker", "CheckerReview", "MakerReworkTask", "CheckerReviewTask", "RouteCheckerAssignedOnResubmit", "Resubmitted for Checker review"),
        new("CheckerReview", "CheckerReturnToMaker", "Checker", "ReturnedToMaker", "CheckerReviewTask", "MakerReworkTask", "RouteReturnedToMakerFromChecker", "Checker returned to Maker"),
        new("CheckerReview", "CheckerSendToTradingDesk", "Checker", "TradingDeskReview", "CheckerReviewTask", "TradingDeskReviewTask", "RouteTradingDeskAssigned", "Checker sent to Trading Desk"),
        new("TradingDeskReview", "TradingReturnToMaker", "TradingDesk", "ReturnedToMaker", "TradingDeskReviewTask", "MakerReworkTask", "RouteReturnedToMakerFromTradingDesk", "Trading Desk returned to Maker"),
        new("TradingDeskReview", "TradingTradeExecuted", "TradingDesk", "BdrmClientServiceReview", "TradingDeskReviewTask", "BdrmClientServiceReviewTask", "RouteBdrmAssigned", "Trading Desk executed trade"),
        new("TradingDeskRework", "TradingTradeExecuted", "TradingDesk", "BdrmClientServiceReview", "TradingDeskReworkTask", "BdrmClientServiceReviewTask", "RouteBdrmAssigned", "Trading Desk executed rework trade"),
        new("BdrmClientServiceReview", "BdrmTradeFulfilled", "BDRMClientService", "Completed", "BdrmClientServiceReviewTask", null, "RouteWorkflowCompleted", "BDRM fulfilled trade"),
        new("BdrmClientServiceReview", "BdrmTradeNotExecutedProperly", "BDRMClientService", "TradingDeskRework", "BdrmClientServiceReviewTask", "TradingDeskReworkTask", "RouteTradingDeskReworkAssigned", "BDRM returned to Trading Desk")
    ];
}

internal sealed record StateSeed(string Key, string DisplayName, bool IsTerminal);

internal sealed record ParticipantDefinitionSeed(
    string ParticipantKey,
    string Cardinality,
    string CaptureStage,
    bool IsRequired,
    bool IsImmutable);

internal sealed record RoleSeed(string Key, string DisplayName);

internal sealed record ActionSeed(
    string Key,
    string DisplayName,
    string SectionKey,
    string ButtonKey,
    string ButtonLabel,
    string Intent,
    bool RequiresReason,
    int SortOrder);

internal sealed record SlaPolicySeed(
    string Key,
    string CalculationMode,
    int? DurationMinutes,
    bool SendDueDateReminder,
    TimeOnly? ReminderLocalTime,
    string? TimeZoneId,
    int OverdueStartOffsetDays,
    int? RecurrenceIntervalDays,
    string? DueNotificationRouteKey,
    string? OverdueNotificationRouteKey);

internal sealed record TaskSeed(string Key, string DisplayName, string OwnerRoleKey, string? SlaPolicyKey);

internal sealed record TaskAssignmentRoleSeed(string TaskKey, string RoleKey);

internal sealed record NotificationRouteSeed(
    string Key,
    string NotificationType,
    string SubjectTemplate,
    string BodyTemplate,
    string HtmlBodyTemplate,
    string OverdueSubjectPrefix);

internal sealed record NotificationRecipientRuleSeed(
    string RouteKey,
    string RecipientKind,
    string SourceType,
    string? SourceKey,
    string? StaticAddress,
    int SortOrder,
    bool IsRequired);

internal sealed record TransitionSeed(
    string FromStateKey,
    string ActionKey,
    string AllowedRoleKey,
    string ToStateKey,
    string? TaskToCloseKey,
    string? TaskToCreateKey,
    string? NotificationRouteKey,
    string AuditLabel);
