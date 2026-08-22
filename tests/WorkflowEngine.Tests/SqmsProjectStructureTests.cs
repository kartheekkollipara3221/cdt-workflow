using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Application.WorkflowExecution;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Configuration;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.DependencyInjection;
using WorkflowEngine.Infrastructure.DependencyInjection;

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

    [Fact]
    public void WorkflowAndScheduledTransitionContractsResolveToSameScopedService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\MSSQLLocalDB;Database=SqmsRegistrationTest;Integrated Security=true;TrustServerCertificate=true",
                ["Notifications:Sender"] = "NoOp",
                ["Sqms:Enabled"] = "false"
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(TimeProvider.System)
            .AddScoped<WorkflowActionEnvelopeNormalizer>()
            .AddScoped<CanonicalRequestHasher>()
            .AddScoped<WorkflowActionResultBuilder>()
            .AddScoped<CanonicalWorkflowResultSerializer>()
            .AddWorkflowInfrastructure(configuration)
            .AddSqmsWorkflowIntegration(configuration)
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var workflow = scope.ServiceProvider.GetRequiredService<ISqmsWorkflowService>();
        var scheduled = scope.ServiceProvider.GetRequiredService<ISqmsScheduledTransitionService>();

        Assert.Same(workflow, scheduled);
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
