namespace WorkflowEngine.Infrastructure.Persistence.Seed;

internal static class PortfolioTradeSeedData
{
    public const string WorkflowKey = "PortfolioTrade";
    public const int VersionNumber = 1;

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
        new("CheckerReviewSla", 2),
        new("TradingDeskReviewSla", 2),
        new("TradingDeskReworkSla", 2),
        new("BdrmClientServiceReviewSla", 2)
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

    private const string ReturnedToMakerBody = """
        Reference {{referenceNumber}} was returned from {{returnedByDisplayName}} to {{recipientDisplayName}}.
        This is for account {{accountShortName}}, {{accountDisplayName}} originally submitted on {{submittedDate}}
        The account and trade information can be accessed by clicking on the following SharePoint link=> {{tradeLink}}
        """;

    private const string TradingDeskAssignedBody = """
        This is to advise that {{submitterDisplayName}} has submitted a request for the {{deskName}} desk for security type {{securityType}} to execute a trade for account {{accountShortName}}, {{accountDisplayName}}.
        The trade detail for account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string BdrmAssignedBody = """
        This is to confirm that the trade request for account {{accountShortName}}, {{accountDisplayName}} has been completed by trader {{traderDisplayName}} on the {{deskName}} desk for security type {{securityType}}.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string WorkflowCompletedBody = """
        This is to confirm that {{confirmerDisplayName}} has verified that the trade was executed and settled in account {{accountShortName}}, {{accountDisplayName}}. The trade request on Sharepoint is now closed.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string TradingDeskReworkAssignedBody = """
        This is to advise that the trade request for account {{accountShortName}}, {{accountDisplayName}} was returned to the {{deskName}} desk for rework.
        The trade detail and account information can be accessed by clicking on the following Sharepoint link => {{tradeLink}}
        """;

    private const string SlaTaskOverdueBody = """
        Workflow instance {{workflowInstanceId}} has an overdue task {{taskKey}} assigned to {{assignedRoleKey}}.
        Task due date: {{dueAtUtc}}
        Overdue at: {{overdueAtUtc}}
        """;

    public static readonly NotificationRouteSeed[] NotificationRoutes =
    [
        new("RouteCheckerAssignedOnSubmit", "WorkflowTaskAssigned", "Checker", "Directed Trade Pending Checker Approval", CheckerAssignedBody, "Overdue: "),
        new("RouteCheckerAssignedOnResubmit", "WorkflowTaskAssigned", "Checker", "Directed Trade Pending Checker Approval", CheckerAssignedBody, "Overdue: "),
        new("RouteReturnedToMakerFromChecker", "WorkflowReturnedToMaker", "Maker", "Updates Required to Directed Trade Request", ReturnedToMakerBody, "Overdue: "),
        new("RouteTradingDeskAssigned", "WorkflowTaskAssigned", null, "Directed Trade Request Pending Execution", TradingDeskAssignedBody, "Overdue: "),
        new("RouteReturnedToMakerFromTradingDesk", "WorkflowReturnedToMaker", "Maker", "Updates Required to Directed Trade Request", ReturnedToMakerBody, "Overdue: "),
        new("RouteBdrmAssigned", "WorkflowTaskAssigned", "BDRMClientService", "Trade Execution Pending Confirmation", BdrmAssignedBody, "Overdue: "),
        new("RouteWorkflowCompleted", "WorkflowCompleted", "Maker", "Confirmation that Trade settled in account", WorkflowCompletedBody, "Overdue: "),
        new("RouteTradingDeskReworkAssigned", "WorkflowTaskAssigned", "TradingDesk", "Directed Trade Request Pending Rework", TradingDeskReworkAssignedBody, "Overdue: "),
        new("RouteSlaTaskOverdue", "SlaTaskOverdue", null, "Workflow task overdue", SlaTaskOverdueBody, "Overdue: ")
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

internal sealed record SlaPolicySeed(string Key, int DurationMinutes);

internal sealed record TaskSeed(string Key, string DisplayName, string OwnerRoleKey, string? SlaPolicyKey);

internal sealed record TaskAssignmentRoleSeed(string TaskKey, string RoleKey);

internal sealed record NotificationRouteSeed(
    string Key,
    string NotificationType,
    string? RecipientRoleKey,
    string SubjectTemplate,
    string BodyTemplate,
    string OverdueSubjectPrefix);

internal sealed record TransitionSeed(
    string FromStateKey,
    string ActionKey,
    string AllowedRoleKey,
    string ToStateKey,
    string? TaskToCloseKey,
    string? TaskToCreateKey,
    string? NotificationRouteKey,
    string AuditLabel);
