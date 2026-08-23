using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsTargetApiScenarioContractTests
{
    private static readonly string[] ExpectedScenarioDirectories =
    [
        "01-opportunity-start-submit",
        "02-opportunity-get-context",
        "03-opportunity-submit-existing-draft",
        "04-opportunity-submitted-return-to-draft",
        "05-opportunity-submitted-mark-pending",
        "06-opportunity-pending-return-to-draft",
        "07-opportunity-pending-open-for-bidding",
        "08-opportunity-scheduled-close-bidding",
        "09-opportunity-request-seller-approval",
        "10-opportunity-mark-awaiting-allocation",
        "11-opportunity-mark-partially-allocated",
        "12-opportunity-mark-not-fulfilled",
        "13-opportunity-complete-awaiting-allocation",
        "14-opportunity-complete-partially-allocated",
        "15-bid-start-submit",
        "16-bid-get-context",
        "17-bid-submit-existing-draft",
        "18-bid-submitted-return-to-draft",
        "19-bid-scheduled-mark-not-allocated",
        "20-bid-scheduled-move-to-ipg-review",
        "21-bid-send-to-seller-review",
        "22-bid-seller-accept",
        "23-bid-seller-partially-accept",
        "24-bid-seller-reject",
        "25-scheduled-opportunity-open-notification",
        "26-scheduled-opportunity-reminder",
        "27-scheduled-catch-up-close-first",
        "28-scheduled-disabled",
        "29-scheduled-mixed-bid-classification",
        "30-notification-outbox-smtp-delivery",
        "31-failure-target-row-not-found",
        "32-failure-workflow-not-started",
        "33-failure-invalid-action-or-role",
        "34-failure-stale-workflow-version",
        "35-failure-seller-approval-enrichment",
        "36-failure-bid-disposition-enrichment",
        "37-replay-request-action",
        "38-replay-notification-occurrence",
        "39-rollback-target-persistence-after-workflow",
        "40-rollback-scheduled-target-projection",
        "41-scheduled-per-record-continuation",
        "42-request-versus-scheduled-projection-ownership",
        "43-complete-opportunity-and-four-bids"
    ];

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
        Assert.Contains(
            "Request-driven Start does not update the SQMS target row.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Request-driven Action does not update the SQMS target row.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "After a non-replay success (`replay = false`), the target API persists the returned `Status` and `WorkflowState`.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "After a replay success (`replay = true`), the target API performs no target write; it reads and verifies the already committed projection.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Scheduled projection updates exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`, and no other target columns.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "| `AlreadyExists` | 409 | Duplicate Start; GET the workflow context and use the current existing state |",
            readme,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RootCatalogUsesFullNumberedDirectoryNames()
    {
        var readme = Read("README.md");

        for (var index = 0; index < ExpectedScenarioDirectories.Length; index++)
        {
            Assert.Contains(
                $"| {index + 1:00} | `{ExpectedScenarioDirectories[index]}` |",
                readme,
                StringComparison.Ordinal);
        }
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments]));
}
