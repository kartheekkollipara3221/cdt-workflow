namespace WorkflowEngine.Infrastructure.Persistence.Seed;

// PortfolioTrade version 2: CDT notification routes and SLA timing (2026-10-18).
// Keys, states, actions, tasks and the transition matrix are the same as version 1.
// Static addresses are placeholders; the target replaces them with the membership
// template before activation. SortOrder blocks identify the recipient group:
// 1 originalMaker, 10-49 ICG RAs, 50-89 Risk Team, 100-199 Taxable,
// 200-299 Tax Exempt, 300-399 Equity, 1000 selectedCcUsers.
internal static class PortfolioTradeSeedDataV2
{
    public const int VersionNumber = 2;

    public const string TradingDeskDueTodayRouteKey = "RouteTradingDeskDueToday";
    public const string TradingDeskOverdueRouteKey = "RouteTradingDeskOverdue";
    public const string BdrmConfirmationOverdueRouteKey = "RouteBdrmConfirmationOverdue";

    public static readonly SlaPolicySeed[] SlaPolicies =
    [
        new("CheckerReviewSla", "DurationFromTaskCreation", 60, false, null, null, 0, 60, null, null, "RouteSlaTaskOverdue"),
        new("TradingDeskReviewSla", "ApplicationTargetDate", null, true, new TimeOnly(8, 0), "America/New_York", 1, null, 1,
            TradingDeskDueTodayRouteKey, TradingDeskOverdueRouteKey,
            DueReminderLocalTime: new TimeOnly(14, 0), SkipMissedDueReminder: true),
        PortfolioTradeSeedData.SlaPolicies.Single(row => row.Key == "TradingDeskReworkSla"),
        // DurationMinutes stays positive so an older engine accepts the policy.
        new("BdrmClientServiceReviewSla", "DurationFromTaskCreation", 1440, false, new TimeOnly(9, 0), "America/New_York", 0, null, 1,
            null, BdrmConfirmationOverdueRouteKey, OverdueStartsNextLocalDay: true)
    ];

    public static readonly NotificationRouteSeed[] NotificationRoutes =
    [
        .. PortfolioTradeSeedData.NotificationRoutes,
        PortfolioTradeSeedData.NotificationRoutes.Single(row => row.Key == "RouteSlaTaskDueToday")
            with { Key = TradingDeskDueTodayRouteKey, OverdueSubjectPrefix = "Due Today: " },
        PortfolioTradeSeedData.NotificationRoutes.Single(row => row.Key == "RouteSlaTaskOverdue")
            with { Key = TradingDeskOverdueRouteKey, OverdueSubjectPrefix = "Overdue: " },
        PortfolioTradeSeedData.NotificationRoutes.Single(row => row.Key == "RouteSlaTaskOverdue")
            with { Key = BdrmConfirmationOverdueRouteKey, OverdueSubjectPrefix = "Overdue: " }
    ];

    private static NotificationRecipientRuleSeed[] CheckerGroups(string routeKey) =>
    [
        new(routeKey, "To", "StaticAddress", null, "icg-ra-1@example.test", 10, true),
        new(routeKey, "To", "StaticAddress", null, "icg-ra-2@example.test", 11, true),
        new(routeKey, "To", "StaticAddress", null, "risk-team-1@example.test", 50, true),
        new(routeKey, "To", "StaticAddress", null, "risk-team-2@example.test", 51, true)
    ];

    private static NotificationRecipientRuleSeed[] RiskTeam(string routeKey) =>
    [
        new(routeKey, "To", "StaticAddress", null, "risk-team-1@example.test", 50, true),
        new(routeKey, "To", "StaticAddress", null, "risk-team-2@example.test", 51, true)
    ];

    private static NotificationRecipientRuleSeed[] DeskUsers(string routeKey) =>
    [
        new(routeKey, "To", "StaticAddress", null, "taxable-trader-1@example.test", 100, true, "TradingDeskTaxable"),
        new(routeKey, "To", "StaticAddress", null, "tax-exempt-trader-1@example.test", 200, true, "TradingDeskTaxExempt"),
        new(routeKey, "To", "StaticAddress", null, "equity-trader-1@example.test", 300, true, "TradingDeskEquity")
    ];

    private static NotificationRecipientRuleSeed OriginalMaker(string routeKey) =>
        new(routeKey, "To", "WorkflowParticipant", "originalMaker", null, 1, true);

    private static NotificationRecipientRuleSeed SelectedCcUsers(string routeKey) =>
        new(routeKey, "Cc", "RequestInput", "selectedCcUsers", null, 1000, false);

    public static readonly NotificationRecipientRuleSeed[] NotificationRecipientRules =
    [
        .. CheckerGroups("RouteCheckerAssignedOnSubmit"),
        SelectedCcUsers("RouteCheckerAssignedOnSubmit"),
        .. CheckerGroups("RouteCheckerAssignedOnResubmit"),
        SelectedCcUsers("RouteCheckerAssignedOnResubmit"),
        OriginalMaker("RouteReturnedToMakerFromChecker"),
        OriginalMaker("RouteReturnedToMakerFromTradingDesk"),
        .. CheckerGroups("RouteTradingDeskAssigned"),
        .. DeskUsers("RouteTradingDeskAssigned"),
        SelectedCcUsers("RouteTradingDeskAssigned"),
        .. DeskUsers(TradingDeskDueTodayRouteKey),
        .. DeskUsers(TradingDeskOverdueRouteKey),
        OriginalMaker("RouteBdrmAssigned"),
        .. RiskTeam("RouteBdrmAssigned"),
        SelectedCcUsers("RouteBdrmAssigned"),
        OriginalMaker(BdrmConfirmationOverdueRouteKey),
        OriginalMaker("RouteWorkflowCompleted"),
        .. RiskTeam("RouteWorkflowCompleted"),
        // TradingDeskRework is not used by CDT; it keeps the version 1 recipients.
        .. PortfolioTradeSeedData.NotificationRecipientRules.Where(row => row.RouteKey == "RouteTradingDeskReworkAssigned")
    ];

    // Declared last: static fields initialize in textual order.
    public static readonly PortfolioTradeSeedDefinition Definition = new(
        VersionNumber,
        PortfolioTradeSeedData.ParticipantDefinitions,
        PortfolioTradeSeedData.States,
        PortfolioTradeSeedData.Roles,
        PortfolioTradeSeedData.Actions,
        SlaPolicies,
        PortfolioTradeSeedData.Tasks,
        NotificationRoutes,
        NotificationRecipientRules,
        PortfolioTradeSeedData.TaskAssignmentRoles,
        PortfolioTradeSeedData.Transitions);
}
