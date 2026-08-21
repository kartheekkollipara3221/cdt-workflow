using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Configuration;
using WorkflowEngine.Sqms.DependencyInjection;

namespace WorkflowEngine.Tests;

public sealed class SqmsProjectStructureTests
{
    [Fact]
    public void SqmsKeysUseApprovedStableIdentifiers()
    {
        Assert.Equal("SqmsOpportunity", SqmsKeys.OpportunityWorkflow);
        Assert.Equal("SqmsBid", SqmsKeys.BidWorkflow);
        Assert.Equal("SQMS-OPP-42", SqmsKeys.OpportunityBusinessObjectId(42));
        Assert.Equal("WF-SQMS-OPP-42", SqmsKeys.OpportunityWorkflowInstanceId(42));
        Assert.Equal("SQMS-BID-84", SqmsKeys.BidBusinessObjectId(84));
        Assert.Equal("WF-SQMS-BID-84", SqmsKeys.BidWorkflowInstanceId(84));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SqmsIdentityMethodsRejectNonPositiveIds(int id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsKeys.OpportunityBusinessObjectId(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsKeys.OpportunityWorkflowInstanceId(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsKeys.BidBusinessObjectId(id));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqmsKeys.BidWorkflowInstanceId(id));
    }

    [Fact]
    public void DisabledSqmsDoesNotRequireLinks()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Sqms:Enabled"] = "false"
        });

        var options = provider.GetRequiredService<IOptions<SqmsOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.Null(options.Links.OpenBiddingDashboardUrl);
    }

    [Theory]
    [InlineData("Sqms:Scheduling:BiddingDays", "0")]
    [InlineData("Sqms:Scheduling:SellerResponseDays", "0")]
    [InlineData("Sqms:Scheduling:TimeZoneId", "Not/A-Time-Zone")]
    public void EnabledSqmsRejectsInvalidSchedulingOptions(string key, string value)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Sqms:Enabled"] = "true",
            [key] = value
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SqmsOptions>>().Value);
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddSqmsWorkflowIntegration(configuration);
        return services.BuildServiceProvider();
    }
}
