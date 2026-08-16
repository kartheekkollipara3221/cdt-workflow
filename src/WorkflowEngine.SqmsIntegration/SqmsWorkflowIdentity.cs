using System.Globalization;

namespace WorkflowEngine.SqmsIntegration;

public static class SqmsWorkflowIdentity
{
    public static string OpportunityBusinessObjectId(int opportunityId)
    {
        return $"SQMS-OPP-{PositiveId(opportunityId)}";
    }

    public static string OpportunityWorkflowInstanceId(int opportunityId)
    {
        return $"WF-{OpportunityBusinessObjectId(opportunityId)}";
    }

    public static string BidBusinessObjectId(int bidId)
    {
        return $"SQMS-BID-{PositiveId(bidId)}";
    }

    public static string BidWorkflowInstanceId(int bidId)
    {
        return $"WF-{BidBusinessObjectId(bidId)}";
    }

    private static string PositiveId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return id.ToString(CultureInfo.InvariantCulture);
    }
}
