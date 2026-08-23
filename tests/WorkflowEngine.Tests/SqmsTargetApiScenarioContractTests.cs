using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsTargetApiScenarioContractTests
{
    private static readonly string[] ExpectedCatalogSlugs =
    [
        "opportunity-start-submit",
        "opportunity-get-context",
        "opportunity-submit-existing-draft",
        "opportunity-submitted-return-to-draft",
        "opportunity-submitted-mark-pending",
        "opportunity-pending-return-to-draft",
        "opportunity-pending-open-for-bidding",
        "opportunity-scheduled-close-bidding",
        "opportunity-request-seller-approval",
        "opportunity-mark-awaiting-allocation",
        "opportunity-mark-partially-allocated",
        "opportunity-mark-not-fulfilled",
        "opportunity-complete-awaiting-allocation",
        "opportunity-complete-partially-allocated",
        "bid-start-submit",
        "bid-get-context",
        "bid-submit-existing-draft",
        "bid-submitted-return-to-draft",
        "bid-scheduled-mark-not-allocated",
        "bid-scheduled-move-to-ipg-review",
        "bid-send-to-seller-review",
        "bid-seller-accept",
        "bid-seller-partially-accept",
        "bid-seller-reject",
        "scheduled-opportunity-open-notification",
        "scheduled-opportunity-reminder",
        "scheduled-catch-up-close-first",
        "scheduled-disabled",
        "scheduled-mixed-bid-classification",
        "notification-outbox-smtp-delivery",
        "failure-target-row-not-found",
        "failure-workflow-not-started",
        "failure-invalid-action-or-role",
        "failure-stale-workflow-version",
        "failure-seller-approval-enrichment",
        "failure-bid-disposition-enrichment",
        "replay-request-action",
        "replay-notification-occurrence",
        "rollback-target-persistence-after-workflow",
        "rollback-scheduled-target-projection",
        "scheduled-per-record-continuation",
        "request-versus-scheduled-projection-ownership",
        "complete-opportunity-and-four-bids"
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
            "The standalone recommended Start route loads the already-saved target row identified by `{id}` and never inserts another target row.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Target-owned create-and-submit orchestration is not the standalone empty-body Start route; it inserts and saves the new target row inside the outer transaction to obtain its ID before invoking Start.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Action loads the existing target row.",
            readme,
            StringComparison.Ordinal);
        Assert.Contains(
            "Before calling `ISqmsWorkflowService`, the target API persists prerequisite target business changes inside the outer transaction so workflow enrichment reads persisted Opportunity or Bid columns.",
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
            "On a replay success (`replay = true`), the target API rolls back the outer transaction, reloads the previously committed target row, verifies its projection, and issues no update.",
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
    public void RootCatalogUsesExactSlugsAndDeclaresNumberedPhysicalPaths()
    {
        var readme = Read("README.md");

        Assert.Contains(
            "Physical pack paths use `NN-{catalog-slug}` (for example, `01-opportunity-start-submit`); later packs will use numbered directories.",
            readme,
            StringComparison.Ordinal);

        for (var index = 0; index < ExpectedCatalogSlugs.Length; index++)
        {
            Assert.Contains(
                $"| {index + 1:00} | `{ExpectedCatalogSlugs[index]}` |",
                readme,
                StringComparison.Ordinal);
        }
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments]));
}
