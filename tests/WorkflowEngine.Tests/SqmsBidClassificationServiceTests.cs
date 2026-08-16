using WorkflowEngine.SqmsIntegration;

namespace WorkflowEngine.Tests;

public sealed class SqmsBidClassificationServiceTests
{
    private readonly SqmsBidClassificationService service = new();

    [Theory]
    [InlineData(99, SqmsBidDisposition.IpgReview)]
    [InlineData(100, SqmsBidDisposition.IpgReview)]
    [InlineData(101, SqmsBidDisposition.NotAllocated)]
    public void DiscountBidDependsOnStrictFairValueAndPremiumThreshold(
        decimal fairAndPremiumAmount,
        SqmsBidDisposition expectedDiscountDisposition)
    {
        var result = service.Classify(100m,
        [
            new SqmsBidClassificationInput(1, "Submitted", fairAndPremiumAmount, "FairValue"),
            new SqmsBidClassificationInput(2, "Submitted", 25m, "Discount")
        ]);

        Assert.Equal(SqmsBidDisposition.IpgReview, result[1]);
        Assert.Equal(expectedDiscountDisposition, result[2]);
    }

    [Fact]
    public void PremiumContributesToThresholdAndRemainsInIpgReview()
    {
        var result = service.Classify(100m,
        [
            new SqmsBidClassificationInput(1, "Submitted", 60m, "FairValue"),
            new SqmsBidClassificationInput(2, "Submitted", 41m, "Premium"),
            new SqmsBidClassificationInput(3, "Submitted", 30m, "Discount")
        ]);

        Assert.Equal(SqmsBidDisposition.IpgReview, result[1]);
        Assert.Equal(SqmsBidDisposition.IpgReview, result[2]);
        Assert.Equal(SqmsBidDisposition.NotAllocated, result[3]);
    }

    [Fact]
    public void DraftBidIsExcludedEvenWhenItsValuesAreInvalid()
    {
        var result = service.Classify(100m,
        [
            new SqmsBidClassificationInput(1, "Draft", -1m, "Unknown"),
            new SqmsBidClassificationInput(2, "Submitted", 50m, "Discount")
        ]);

        Assert.DoesNotContain(1, result.Keys);
        Assert.Equal(SqmsBidDisposition.IpgReview, result[2]);
    }

    [Fact]
    public void NoSubmittedBidsReturnsNoDecisions()
    {
        Assert.Empty(service.Classify(100m,
        [
            new SqmsBidClassificationInput(1, "Draft", 50m, "FairValue")
        ]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSaleAmountIsRejected(decimal saleAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Classify(saleAmount, []));
    }

    [Theory]
    [InlineData(0, "FairValue")]
    [InlineData(-1, "FairValue")]
    [InlineData(1, "Unknown")]
    [InlineData(1, "fairvalue")]
    public void InvalidSubmittedBidAmountOrCategoryIsRejected(decimal amount, string category)
    {
        Assert.Throws<SqmsIntegrationException>(() => service.Classify(100m,
        [
            new SqmsBidClassificationInput(1, "Submitted", amount, category)
        ]));
    }
}
