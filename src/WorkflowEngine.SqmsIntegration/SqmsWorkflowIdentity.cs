using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

    public static string SaleCycleBusinessObjectId(
        DateOnly effectiveDateForSale,
        DateTimeOffset noticeAt,
        DateTimeOffset reminderAt,
        DateOnly responseDueDate)
    {
        var canonical = FormattableString.Invariant(
            $"{effectiveDateForSale:yyyy-MM-dd}|{noticeAt.ToUniversalTime():O}|{reminderAt.ToUniversalTime():O}|{responseDueDate:yyyy-MM-dd}");
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical))[..12])
            .ToLowerInvariant();
        return FormattableString.Invariant(
            $"SQMS-CYCLE-{effectiveDateForSale:yyyyMMdd}-{responseDueDate:yyyyMMdd}-{hash}");
    }

    public static string SaleCycleWorkflowInstanceId(
        DateOnly effectiveDateForSale,
        DateTimeOffset noticeAt,
        DateTimeOffset reminderAt,
        DateOnly responseDueDate)
    {
        return $"WF-{SaleCycleBusinessObjectId(effectiveDateForSale, noticeAt, reminderAt, responseDueDate)}";
    }

    private static string PositiveId(int id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return id.ToString(CultureInfo.InvariantCulture);
    }
}
