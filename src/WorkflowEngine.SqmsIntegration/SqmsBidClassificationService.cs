using System.Collections.ObjectModel;

namespace WorkflowEngine.SqmsIntegration;

public enum SqmsBidDisposition
{
    IpgReview,
    NotAllocated
}

public sealed record SqmsBidClassificationInput(
    int BidId,
    string WorkflowState,
    decimal CommitmentAmountRequested,
    string BidPricingCategory);

public sealed class SqmsBidClassificationService
{
    public IReadOnlyDictionary<int, SqmsBidDisposition> Classify(
        decimal saleAmount,
        IReadOnlyList<SqmsBidClassificationInput> bids)
    {
        if (saleAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(saleAmount), "SQMS sale amount must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(bids);
        var submitted = bids
            .Where(bid => string.Equals(bid.WorkflowState, "Submitted", StringComparison.Ordinal))
            .ToArray();
        var duplicateBidId = submitted
            .GroupBy(bid => bid.BidId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateBidId is not null)
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{duplicateBidId.Key}' appeared more than once during classification.");
        }

        foreach (var bid in submitted)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bid.BidId);
            if (bid.CommitmentAmountRequested <= 0)
            {
                throw new SqmsIntegrationException(
                    $"SQMS Bid '{bid.BidId}' has an invalid CommitmentAmountRequested.");
            }

            if (bid.BidPricingCategory is not ("FairValue" or "Premium" or "Discount"))
            {
                throw new SqmsIntegrationException(
                    $"SQMS Bid '{bid.BidId}' has invalid BidPricingCategory '{bid.BidPricingCategory}'.");
            }
        }

        var fairValueAndPremiumTotal = submitted
            .Where(bid => bid.BidPricingCategory is "FairValue" or "Premium")
            .Sum(bid => bid.CommitmentAmountRequested);
        var excludeDiscount = fairValueAndPremiumTotal > saleAmount;
        var decisions = submitted.ToDictionary(
            bid => bid.BidId,
            bid => excludeDiscount && bid.BidPricingCategory == "Discount"
                ? SqmsBidDisposition.NotAllocated
                : SqmsBidDisposition.IpgReview);
        return new ReadOnlyDictionary<int, SqmsBidDisposition>(decisions);
    }
}
