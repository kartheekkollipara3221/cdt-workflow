namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed record BidScenario(
    string Key,
    decimal RequestedAmount,
    string Category,
    string TerminalState,
    bool ReturnBeforeResubmit = false);

public sealed record OpportunityScenario(
    string Key,
    string AllocationPath,
    string TerminalState,
    int ExpectedFinalVersion,
    IReadOnlyList<BidScenario> Bids)
{
    public string Account => $"{AcceptanceConstants.RunMarker}-OPP-{Key}";
    public int TotalBidSentinel => 777;
    public string LargestBidSentinel => "L-SENTINEL";
    public string LowestBidSentinel => "S-SENTINEL";
}

public sealed record AcceptanceScenarioMatrix(IReadOnlyList<OpportunityScenario> Opportunities)
{
    public IReadOnlyList<BidScenario> AllBids =>
        Opportunities.SelectMany(row => row.Bids).ToArray();

    public static AcceptanceScenarioMatrix Create(
        AcceptanceScenarioKind? kind = null) => (kind ?? AcceptanceConstants.ScenarioKind) switch
        {
            AcceptanceScenarioKind.Comprehensive => Comprehensive(),
            AcceptanceScenarioKind.SingleLifecycle => SingleLifecycle(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static AcceptanceScenarioMatrix Comprehensive() => new(
    [
        new(
            "A",
            "AWAITING ALLOCATION -> COMPLETED",
            SqmsKeys.OpportunityStates.Completed,
            10,
            [
                new("01", 60m, "FAIR VALUE", SqmsKeys.BidStates.Accepted, true),
                new("02", 50m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
                new("03", 0m, "FAIR VALUE", SqmsKeys.BidStates.NotAccepted),
                new("04", 25m, "DISCOUNT", SqmsKeys.BidStates.NotAllocated),
                new("05", 0m, "FAIR VALUE", SqmsKeys.BidStates.Accepted)
            ]),
        new(
            "B",
            "PARTIALLY ALLOCATED -> COMPLETED",
            SqmsKeys.OpportunityStates.Completed,
            8,
            [
                new("06", 20m, "FAIR VALUE", SqmsKeys.BidStates.Accepted),
                new("07", 20m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
                new("08", 20m, "DISCOUNT", SqmsKeys.BidStates.NotAccepted),
                new("09", 20m, "FAIR VALUE", SqmsKeys.BidStates.Accepted),
                new("10", 20m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted)
            ]),
        new(
            "C",
            "NOT FULFILLED",
            SqmsKeys.OpportunityStates.NotFulfilled,
            7,
            [
                new("11", 20m, "FAIR VALUE", SqmsKeys.BidStates.Accepted),
                new("12", 20m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
                new("13", 20m, "DISCOUNT", SqmsKeys.BidStates.NotAccepted),
                new("14", 20m, "FAIR VALUE", SqmsKeys.BidStates.Accepted),
                new("15", 20m, "PREMIUM", SqmsKeys.BidStates.NotAccepted)
            ])
    ]);

    private static AcceptanceScenarioMatrix SingleLifecycle() => new(
    [
        new(
            "A",
            "AWAITING ALLOCATION -> COMPLETED",
            SqmsKeys.OpportunityStates.Completed,
            10,
            [
                new("01", 40m, "FAIR VALUE", SqmsKeys.BidStates.Accepted, true),
                new("02", 30m, "PREMIUM", SqmsKeys.BidStates.PartiallyAccepted),
                new("03", 20m, "DISCOUNT", SqmsKeys.BidStates.NotAccepted)
            ])
    ]);
}

public sealed record AcceptanceManifest(
    IReadOnlyDictionary<string, int> OpportunityIds,
    IReadOnlyDictionary<string, int> BidIds);

public sealed record AcceptanceScenarioDefinition(
    AcceptanceScenarioMatrix Matrix,
    IReadOnlyDictionary<string, int> ExpectedOpportunityStates,
    IReadOnlyDictionary<string, int> ExpectedBidStates,
    IReadOnlyDictionary<string, int> ExpectedEmailRoutes)
{
    public int ExpectedOpportunityCount => Matrix.Opportunities.Count;
    public int ExpectedBidCount => Matrix.AllBids.Count;
    public int ExpectedWorkflowInstanceCount => ExpectedOpportunityCount + ExpectedBidCount;
    public int ExpectedEmailCount => ExpectedEmailRoutes.Values.Sum();
    public int ExpectedBidsNotAllocated =>
        Matrix.AllBids.Count(bid => bid.TerminalState == SqmsKeys.BidStates.NotAllocated);
    public int ExpectedBidsMovedToIpgReview => ExpectedBidCount - ExpectedBidsNotAllocated;

    public static AcceptanceScenarioDefinition Create(AcceptanceScenarioKind kind)
    {
        var matrix = AcceptanceScenarioMatrix.Create(kind);
        return new(
            matrix,
            StateCounts(matrix.Opportunities.Select(row => row.TerminalState)),
            StateCounts(matrix.AllBids.Select(row => row.TerminalState)),
            BuildEmailRoutes(matrix));
    }

    private static IReadOnlyDictionary<string, int> StateCounts(IEnumerable<string> states) =>
        states.GroupBy(state => state, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> BuildEmailRoutes(AcceptanceScenarioMatrix matrix)
    {
        var routes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [SqmsKeys.Routes.OpportunityOpen] = 1,
            [SqmsKeys.Routes.OpportunityReminder] = 1,
            [SqmsKeys.Routes.OpportunityPendingSellerApproval] = matrix.Opportunities.Count
        };
        foreach (var group in matrix.AllBids
                     .Where(bid => bid.TerminalState != SqmsKeys.BidStates.NotAllocated)
                     .GroupBy(bid => BidRoute(bid.TerminalState), StringComparer.Ordinal))
        {
            routes[group.Key] = group.Count();
        }
        return routes;
    }

    private static string BidRoute(string terminalState) => terminalState switch
    {
        SqmsKeys.BidStates.Accepted => SqmsKeys.Routes.BidAccepted,
        SqmsKeys.BidStates.PartiallyAccepted => SqmsKeys.Routes.BidPartiallyAccepted,
        SqmsKeys.BidStates.NotAccepted => SqmsKeys.Routes.BidNotAccepted,
        _ => throw new ArgumentOutOfRangeException(nameof(terminalState), terminalState, null)
    };
}

public sealed record ExpectedEmailRoute(string RouteKey, int Count);

public static class AcceptanceEmailPlan
{
    public static IReadOnlyList<ExpectedEmailRoute> Create() =>
        AcceptanceConstants.Definition.ExpectedEmailRoutes
            .Select(route => new ExpectedEmailRoute(route.Key, route.Value))
            .ToArray();
}

public sealed record ExpectedOperation(
    string Method,
    string? ActionKey,
    string ExpectedState,
    int ExpectedVersion);

public sealed record EntityOperationPlan(
    IReadOnlyList<ExpectedOperation> PreSchedule,
    ExpectedOperation Final);

public sealed record AcceptanceOperationPlan(
    EntityOperationPlan OpportunityA,
    EntityOperationPlan OpportunityB,
    EntityOperationPlan OpportunityC,
    EntityOperationPlan Bid01,
    EntityOperationPlan Bid04)
{
    public static AcceptanceOperationPlan Create(AcceptanceScenarioMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new(
            new(
                [
                    Start("Opportunity", SqmsKeys.OpportunityStates.Submitted, 2),
                    Action(SqmsKeys.OpportunityActions.ReturnToDraft, SqmsKeys.OpportunityStates.Draft, 3),
                    Action(SqmsKeys.OpportunityActions.Submit, SqmsKeys.OpportunityStates.Submitted, 4),
                    Action(SqmsKeys.OpportunityActions.MarkPending, SqmsKeys.OpportunityStates.Pending, 5),
                    Action(SqmsKeys.OpportunityActions.OpenForBidding, SqmsKeys.OpportunityStates.OpenToBidding, 6)
                ],
                Action(SqmsKeys.OpportunityActions.Complete, SqmsKeys.OpportunityStates.Completed, 10)),
            StandardOpportunity(8),
            StandardOpportunity(7, SqmsKeys.OpportunityStates.NotFulfilled),
            new(
                [
                    Start("Bid", SqmsKeys.BidStates.Submitted, 2),
                    Action(SqmsKeys.BidActions.ReturnToDraft, SqmsKeys.BidStates.Draft, 3),
                    Action(SqmsKeys.BidActions.Submit, SqmsKeys.BidStates.Submitted, 4)
                ],
                Action(SqmsKeys.BidActions.Accept, SqmsKeys.BidStates.Accepted, 7)),
            new(
                [Start("Bid", SqmsKeys.BidStates.Submitted, 2)],
                Action(SqmsKeys.BidActions.MarkNotAllocated, SqmsKeys.BidStates.NotAllocated, 3)));
    }

    private static EntityOperationPlan StandardOpportunity(int finalVersion, string? state = null) =>
        new(
            [
                Start("Opportunity", SqmsKeys.OpportunityStates.Submitted, 2),
                Action(SqmsKeys.OpportunityActions.MarkPending, SqmsKeys.OpportunityStates.Pending, 3),
                Action(SqmsKeys.OpportunityActions.OpenForBidding, SqmsKeys.OpportunityStates.OpenToBidding, 4)
            ],
            Action(
                state == SqmsKeys.OpportunityStates.NotFulfilled
                    ? SqmsKeys.OpportunityActions.MarkNotFulfilled
                    : SqmsKeys.OpportunityActions.Complete,
                state ?? SqmsKeys.OpportunityStates.Completed,
                finalVersion));

    private static ExpectedOperation Start(string entity, string state, int version) =>
        new($"Start{entity}Async", null, state, version);

    private static ExpectedOperation Action(string action, string state, int version) =>
        new("ExecuteActionAsync", action, state, version);
}
