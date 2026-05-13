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

    public static readonly NotificationRouteSeed[] NotificationRoutes =
    [
        new("RouteCheckerAssignedOnSubmit", "WorkflowTaskAssigned", "Checker", "Portfolio Trade submitted for Checker review"),
        new("RouteCheckerAssignedOnResubmit", "WorkflowTaskAssigned", "Checker", "Portfolio Trade resubmitted for Checker review"),
        new("RouteReturnedToMakerFromChecker", "WorkflowReturnedToMaker", "Maker", "Portfolio Trade returned by Checker"),
        new("RouteTradingDeskAssigned", "WorkflowTaskAssigned", "TradingDesk", "Portfolio Trade assigned to Trading Desk"),
        new("RouteReturnedToMakerFromTradingDesk", "WorkflowReturnedToMaker", "Maker", "Portfolio Trade returned by Trading Desk"),
        new("RouteBdrmAssigned", "WorkflowTaskAssigned", "BDRMClientService", "Portfolio Trade assigned to BDRM Client Service"),
        new("RouteWorkflowCompleted", "WorkflowCompleted", "Maker", "Portfolio Trade workflow completed"),
        new("RouteTradingDeskReworkAssigned", "WorkflowTaskAssigned", "TradingDesk", "Portfolio Trade returned for Trading Desk rework"),
        new("RouteSlaTaskOverdue", "SlaTaskOverdue", null, "Portfolio Trade workflow task is overdue")
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

internal sealed record NotificationRouteSeed(string Key, string NotificationType, string? RecipientRoleKey, string SubjectTemplate);

internal sealed record TransitionSeed(
    string FromStateKey,
    string ActionKey,
    string AllowedRoleKey,
    string ToStateKey,
    string? TaskToCloseKey,
    string? TaskToCreateKey,
    string? NotificationRouteKey,
    string AuditLabel);

