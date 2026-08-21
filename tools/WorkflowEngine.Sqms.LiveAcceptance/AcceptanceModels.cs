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

    public static AcceptanceScenarioMatrix Create() => new(
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
}

public sealed record AcceptanceManifest(
    IReadOnlyDictionary<string, int> OpportunityIds,
    IReadOnlyDictionary<string, int> BidIds);

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
