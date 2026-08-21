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
    IReadOnlyList<BidScenario> Bids);

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
