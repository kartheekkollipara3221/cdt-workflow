using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Configuration;

namespace WorkflowEngine.Tests;

public sealed class SqmsLinkBuilderTests
{
    [Fact]
    public void OpenBiddingDashboardLinkIsReturnedUnchanged()
    {
        const string configured = "https://sqms.example.test/open?view=active#results";
        var builder = Builder(openUrl: configured);

        var result = builder.BuildOpenBiddingDashboardLink();

        Assert.True(result.Success, result.Error);
        Assert.Equal(configured, result.Url);
    }

    [Fact]
    public void SellerApprovalLinkAddsOpportunityId()
    {
        var builder = Builder(sellerUrl: "https://sqms.example.test/bids");

        var result = builder.BuildSellerApprovalBidsDashboardLink(10000);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://sqms.example.test/bids?opportunityId=10000", result.Url);
    }

    [Fact]
    public void SellerApprovalLinkPreservesExistingQueryAndKeepsFragmentLast()
    {
        var builder = Builder(sellerUrl: "https://sqms.example.test/bids?view=all#results");

        var result = builder.BuildSellerApprovalBidsDashboardLink(10000);

        Assert.True(result.Success, result.Error);
        Assert.Equal(
            "https://sqms.example.test/bids?view=all&opportunityId=10000#results",
            result.Url);
    }

    [Fact]
    public void BidFormLinkAddsBidId()
    {
        var builder = Builder(bidUrl: "https://sqms.example.test/bid-form");

        var result = builder.BuildBidFormLink(10001);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://sqms.example.test/bid-form?bidId=10001", result.Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative")]
    [InlineData("ftp://sqms.example.test/open")]
    [InlineData("not a url")]
    public void InvalidConfiguredUrlsReturnStructuredFailure(string? configured)
    {
        var builder = Builder(openUrl: configured);

        var result = builder.BuildOpenBiddingDashboardLink();

        Assert.False(result.Success);
        Assert.Null(result.Url);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void NonPositiveTargetIdReturnsStructuredFailure()
    {
        var builder = Builder(sellerUrl: "https://sqms.example.test/bids");

        var result = builder.BuildSellerApprovalBidsDashboardLink(0);

        Assert.False(result.Success);
        Assert.Null(result.Url);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static SqmsLinkBuilder Builder(
        string? openUrl = "https://sqms.example.test/open",
        string? sellerUrl = "https://sqms.example.test/seller",
        string? bidUrl = "https://sqms.example.test/bid")
    {
        return new SqmsLinkBuilder(new SqmsLinkOptions
        {
            OpenBiddingDashboardUrl = openUrl,
            SellerApprovalBidsDashboardUrl = sellerUrl,
            BidFormUrl = bidUrl
        });
    }
}
