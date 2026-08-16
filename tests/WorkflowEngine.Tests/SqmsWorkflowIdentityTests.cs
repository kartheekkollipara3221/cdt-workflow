using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

public sealed class SqmsWorkflowIdentityTests
{
    [Theory]
    [InlineData(10000, "SQMS-OPP-10000", "WF-SQMS-OPP-10000")]
    [InlineData(42, "SQMS-OPP-42", "WF-SQMS-OPP-42")]
    public void OpportunityIdentityIsNamespaced(
        int id,
        string expectedBusinessObjectId,
        string expectedWorkflowInstanceId)
    {
        Assert.Equal(expectedBusinessObjectId, SqmsWorkflowIdentity.OpportunityBusinessObjectId(id));
        Assert.Equal(expectedWorkflowInstanceId, SqmsWorkflowIdentity.OpportunityWorkflowInstanceId(id));
    }

    [Theory]
    [InlineData(10000, "SQMS-BID-10000", "WF-SQMS-BID-10000")]
    [InlineData(42, "SQMS-BID-42", "WF-SQMS-BID-42")]
    public void BidIdentityIsNamespaced(
        int id,
        string expectedBusinessObjectId,
        string expectedWorkflowInstanceId)
    {
        Assert.Equal(expectedBusinessObjectId, SqmsWorkflowIdentity.BidBusinessObjectId(id));
        Assert.Equal(expectedWorkflowInstanceId, SqmsWorkflowIdentity.BidWorkflowInstanceId(id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IdentityRejectsNonPositiveApplicationId(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsWorkflowIdentity.OpportunityBusinessObjectId(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsWorkflowIdentity.BidBusinessObjectId(id));
    }

    [Fact]
    public void SaleCycleIdentityIsStableAndIncludesBusinessDates()
    {
        var noticeAt = DateTimeOffset.Parse("2026-05-01T08:00:00-04:00");
        var reminderAt = DateTimeOffset.Parse("2026-05-14T08:00:00-04:00");

        var first = SqmsWorkflowIdentity.SaleCycleBusinessObjectId(
            new DateOnly(2026, 7, 1),
            noticeAt,
            reminderAt,
            new DateOnly(2026, 5, 15));
        var second = SqmsWorkflowIdentity.SaleCycleBusinessObjectId(
            new DateOnly(2026, 7, 1),
            noticeAt,
            reminderAt,
            new DateOnly(2026, 5, 15));

        Assert.Equal(first, second);
        Assert.StartsWith("SQMS-CYCLE-20260701-20260515-", first, StringComparison.Ordinal);
        Assert.Equal($"WF-{first}", SqmsWorkflowIdentity.SaleCycleWorkflowInstanceId(
            new DateOnly(2026, 7, 1), noticeAt, reminderAt, new DateOnly(2026, 5, 15)));
    }
}
