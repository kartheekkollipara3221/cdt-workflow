using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsTargetApiScenarioContractTests
{
    private static readonly string Root = Path.Combine(
        SqmsDatabasePackageTests.RepositoryRoot(),
        "docs",
        "sqms",
        "target-api-scenarios");

    [Fact]
    public void RootCatalogDeclaresTheDualContractAndOwnershipBoundary()
    {
        var readme = Read("README.md");

        Assert.Contains("Implemented in-process contract", readme, StringComparison.Ordinal);
        Assert.Contains("Recommended target facade", readme, StringComparison.Ordinal);
        Assert.Contains("not implemented in this repository", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request-driven", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scheduled", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target API persists", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("four columns", readme, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments]));
}
