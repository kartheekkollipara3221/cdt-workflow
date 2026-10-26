using BBH.CP.Atlas.App.StandIn;
using Microsoft.Extensions.Configuration;

namespace BBH.CP.Atlas.Platform.Tests.Api;

/// <summary>Review HO-10: the stand-in Fixed authentication (each caller = the API process user) is for Development only.</summary>
public class StandInHostTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("QC")]
    public void Fixed_authentication_outside_Development_stops_the_host(string environment)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => StandInHost.Create([$"--environment={environment}"], builder =>
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DBConnection"] = "Server=.;Database=None;Integrated Security=true;TrustServerCertificate=true",
                ["StandIn:Authentication"] = "Fixed"
            })));

        Assert.Contains("only in Development", error.Message, StringComparison.Ordinal);
    }
}
