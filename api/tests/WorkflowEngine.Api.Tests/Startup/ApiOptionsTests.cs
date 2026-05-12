using WorkflowEngine.Api.Options;

namespace WorkflowEngine.Api.Tests.Startup;

public sealed class ApiOptionsTests
{
    [Fact]
    public void WorkflowDatabaseStartupOptionsDefaultsAreSafe()
    {
        var options = new WorkflowDatabaseStartupOptions();

        Assert.False(options.ApplyMigrationsAndSeedOnStartup);
        Assert.False(options.VerifySeedOnStartup);
        Assert.True(options.FailStartupWhenSeedVerificationFails);
    }

    [Fact]
    public void WorkflowApiOptionsDefaultsAreSafe()
    {
        var options = new WorkflowApiOptions();

        Assert.False(options.AllowDevelopmentHeaderActorResolver);
        Assert.True(options.DisablePublicReset);
        Assert.True(options.DisablePublicAdminOverride);
    }
}
