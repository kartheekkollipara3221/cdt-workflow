using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.LiveAcceptance;

namespace WorkflowEngine.Tests;

public sealed class SqmsLiveAcceptanceContractTests
{
    [Fact]
    public void ConstantsPinTheIsolatedDatabaseAndEvidenceCounts()
    {
        Assert.Equal("workflowengine_sqms_live_acceptance", AcceptanceConstants.DatabaseName);
        Assert.Equal("SQMS-LIVE-20260821", AcceptanceConstants.RunMarker);
        Assert.Equal(3, AcceptanceConstants.ExpectedOpportunityCount);
        Assert.Equal(15, AcceptanceConstants.ExpectedBidCount);
        Assert.Equal(19, AcceptanceConstants.ExpectedEmailCount);
        Assert.Equal(
            new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero),
            AcceptanceConstants.OpeningUtc);
    }

    [Fact]
    public void ScenarioMatrixContainsEveryTerminalOutcome()
    {
        var matrix = AcceptanceScenarioMatrix.Create();

        Assert.Equal(3, matrix.Opportunities.Count);
        Assert.Equal(15, matrix.AllBids.Count);
        Assert.Equal(6, matrix.AllBids.Count(row => row.TerminalState == "ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "PARTIALLY ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "NOT ACCEPTED"));
        Assert.Single(matrix.AllBids, row => row.TerminalState == "NOT ALLOCATED");
        Assert.Single(matrix.AllBids, row => row.ReturnBeforeResubmit);
    }

    [Fact]
    public void AcceptanceClockAdvancesWithoutUsingSystemTime()
    {
        var clock = new AcceptanceTimeProvider(AcceptanceConstants.OpeningUtc);

        clock.SetUtcNow(AcceptanceConstants.OpeningUtc.AddDays(14));

        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), clock.GetUtcNow());
    }

    [Fact]
    public void FixturesHaveExactParentsAmountsAndSentinels()
    {
        var matrix = AcceptanceScenarioMatrix.Create();

        Assert.All(matrix.Opportunities, row =>
        {
            Assert.Contains(AcceptanceConstants.RunMarker, row.Account, StringComparison.Ordinal);
            Assert.Equal(777, row.TotalBidSentinel);
            Assert.Equal("L-SENTINEL", row.LargestBidSentinel);
            Assert.Equal("S-SENTINEL", row.LowestBidSentinel);
            Assert.Equal(5, row.Bids.Count);
        });
        Assert.Equal(
            110m,
            matrix.Opportunities[0].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
        Assert.Equal(
            80m,
            matrix.Opportunities[1].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
        Assert.Equal(
            80m,
            matrix.Opportunities[2].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
    }

    [Fact]
    public void OperationPlanHasExactReturnAndTerminalVersions()
    {
        var plan = AcceptanceOperationPlan.Create(AcceptanceScenarioMatrix.Create());

        Assert.Equal(
            ["SUBMITTED", "Draft", "SUBMITTED", "PENDING", "OPEN TO BIDDING"],
            plan.OpportunityA.PreSchedule.Select(row => row.ExpectedState));
        Assert.Equal(10, plan.OpportunityA.Final.ExpectedVersion);
        Assert.Equal(8, plan.OpportunityB.Final.ExpectedVersion);
        Assert.Equal(7, plan.OpportunityC.Final.ExpectedVersion);
        Assert.Equal(7, plan.Bid01.Final.ExpectedVersion);
        Assert.Equal(3, plan.Bid04.Final.ExpectedVersion);
    }

    [Fact]
    public void CalendarAndEmailPlanAreExact()
    {
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(1), AcceptanceConstants.OpenRunUtc);
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), AcceptanceConstants.ReminderRunUtc);
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(16), AcceptanceConstants.CloseRunUtc);
        var expected = AcceptanceEmailPlan.Create();
        Assert.Equal(19, expected.Sum(row => row.Count));
        Assert.Equal(6, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidAccepted).Count);
        Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidPartiallyAccepted).Count);
        Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidNotAccepted).Count);
    }
}
