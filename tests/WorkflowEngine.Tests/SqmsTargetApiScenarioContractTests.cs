using WorkflowEngine.Sqms;

namespace WorkflowEngine.Tests;

public sealed class SqmsTargetApiScenarioContractTests
{
    private static readonly string[] OpportunityPacks =
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
        "14-opportunity-complete-partially-allocated"
    ];

    private static readonly string[] BidPacks =
    [
        "15-bid-start-submit",
        "16-bid-get-context",
        "17-bid-submit-existing-draft",
        "18-bid-submitted-return-to-draft",
        "19-bid-scheduled-mark-not-allocated",
        "20-bid-scheduled-move-to-ipg-review",
        "21-bid-send-to-seller-review",
        "22-bid-seller-accept",
        "23-bid-seller-partially-accept",
        "24-bid-seller-reject"
    ];

    private static readonly string[] SchedulerPacks =
    [
        "25-scheduled-opportunity-open-notification",
        "26-scheduled-opportunity-reminder",
        "27-scheduled-catch-up-close-first",
        "28-scheduled-disabled",
        "29-scheduled-mixed-bid-classification",
        "30-notification-outbox-smtp-delivery"
    ];

    private static readonly string[] FailurePacks =
    [
        "31-failure-target-row-not-found",
        "32-failure-workflow-not-started",
        "33-failure-invalid-action-or-role",
        "34-failure-stale-workflow-version",
        "35-failure-seller-approval-enrichment",
        "36-failure-bid-disposition-enrichment"
    ];

    private static readonly string[] OwnershipPacks =
    [
        "37-replay-request-action",
        "38-replay-notification-occurrence",
        "39-rollback-target-persistence-after-workflow",
        "40-rollback-scheduled-target-projection",
        "41-scheduled-per-record-continuation",
        "42-request-versus-scheduled-projection-ownership"
    ];

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

    [Fact]
    public void OpportunityCatalogContainsEveryApprovedPath()
    {
        Assert.All(OpportunityPacks, pack =>
        {
            Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
        });
    }

    [Fact]
    public void BidCatalogContainsEveryApprovedPath()
    {
        Assert.All(BidPacks, pack =>
        {
            Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
        });
    }

    [Fact]
    public void SchedulerCatalogContainsEveryApprovedPath()
    {
        Assert.All(SchedulerPacks, pack =>
        {
            Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
        });
    }

    [Fact]
    public void SchedulerPacksPinOccurrenceOrderingClassificationAndDeliveryContracts()
    {
        var open = Read(SchedulerPacks[0], "README.md");
        Assert.Contains("SQMS:OpportunityOpen:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}", open, StringComparison.Ordinal);
        Assert.Contains("SQMS:OpportunityOpen:2030:Q1:2030-01-02", open, StringComparison.Ordinal);
        Assert.Contains("WorkflowInstanceId IS NULL", open, StringComparison.Ordinal);
        Assert.Contains("immutable", open, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("filtered unique", open, StringComparison.OrdinalIgnoreCase);

        var reminder = Read(SchedulerPacks[1], "README.md");
        Assert.Contains("SQMS:OpportunityReminder:{year}:Q{quarter}:{occurrenceDate:yyyy-MM-dd}", reminder, StringComparison.Ordinal);
        Assert.Contains("SQMS:OpportunityReminder:2030:Q1:2030-01-15", reminder, StringComparison.Ordinal);
        Assert.Contains("WorkflowInstanceId IS NULL", reminder, StringComparison.Ordinal);

        var catchUp = Read(SchedulerPacks[2], "README.md");
        Assert.Contains("close every due candidate", catchUp, StringComparison.Ordinal);
        Assert.Contains("classify their valid Bids", catchUp, StringComparison.Ordinal);
        Assert.Contains("queue due open/reminder", catchUp, StringComparison.Ordinal);
        Assert.True(
            catchUp.IndexOf("close every due candidate", StringComparison.Ordinal)
            < catchUp.IndexOf("classify their valid Bids", StringComparison.Ordinal));
        Assert.True(
            catchUp.IndexOf("classify their valid Bids", StringComparison.Ordinal)
            < catchUp.IndexOf("queue due open/reminder", StringComparison.Ordinal));

        var disabled = Read(SchedulerPacks[3], "README.md");
        Assert.Contains("before `GetOpenOpportunitiesAsync`", disabled, StringComparison.Ordinal);
        Assert.Contains("successful zero work", disabled, StringComparison.Ordinal);

        var mixed = Read(SchedulerPacks[4], "README.md");
        Assert.Contains("Fair Value", mixed, StringComparison.Ordinal);
        Assert.Contains("Premium", mixed, StringComparison.Ordinal);
        Assert.Contains("Discount", mixed, StringComparison.Ordinal);
        Assert.Contains("strictly greater", mixed, StringComparison.Ordinal);

        var smtp = Read(SchedulerPacks[5], "README.md");
        Assert.Contains("process-notification-outbox", smtp, StringComparison.Ordinal);
        Assert.Contains("Status = Sent", smtp, StringComparison.Ordinal);
        Assert.Contains("RetryCount = 0", smtp, StringComparison.Ordinal);
        Assert.Contains("does not start smtp4dev", smtp, StringComparison.Ordinal);
        foreach (var route in new[]
                 {
                     SqmsKeys.Routes.OpportunityOpen,
                     SqmsKeys.Routes.OpportunityReminder,
                     SqmsKeys.Routes.OpportunityPendingSellerApproval,
                     SqmsKeys.Routes.BidAccepted,
                     SqmsKeys.Routes.BidPartiallyAccepted,
                     SqmsKeys.Routes.BidNotAccepted
                 })
        {
            Assert.Contains(route, smtp, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FailureCatalogContainsEveryApprovedPath()
    {
        Assert.All(FailurePacks, pack =>
        {
            Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "in-process.cs")), $"Missing in-process example for {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "verify.sql")), $"Missing verifier for {pack}");
        });
    }

    [Fact]
    public void FailurePacksPinMappingsNoLazyStartAndAtomicEnrichmentFailures()
    {
        var notFound = Read(FailurePacks[0], "README.md");
        Assert.Contains("`TargetNotFound` to HTTP 404", notFound, StringComparison.Ordinal);
        Assert.Contains("No workflow instance is created", notFound, StringComparison.Ordinal);

        var notStarted = Read(FailurePacks[1], "README.md");
        Assert.Contains("`NotWorkflowManaged` to HTTP 409", notStarted, StringComparison.Ordinal);
        Assert.Contains("Context and Action do not lazily start a workflow", notStarted, StringComparison.Ordinal);

        var invalid = Read(FailurePacks[2], "README.md");
        Assert.Contains("Map `Forbidden` to HTTP 403", invalid, StringComparison.Ordinal);
        Assert.Contains("`InvalidTransition` to HTTP 422", invalid, StringComparison.Ordinal);
        Assert.Contains("No target-owned state save", invalid, StringComparison.Ordinal);

        var stale = Read(FailurePacks[3], "README.md");
        Assert.Contains("`ConcurrencyConflict` to HTTP 409", stale, StringComparison.Ordinal);
        Assert.Contains("must not automatically retry", stale, StringComparison.Ordinal);

        var seller = Read(FailurePacks[4], "README.md");
        Assert.Contains("**Testing only:**", seller, StringComparison.Ordinal);
        Assert.Contains("Seller approval does not transition", seller, StringComparison.Ordinal);
        Assert.Contains("No `SqmsOpportunityPendingSellerApproval` email/outbox row", seller, StringComparison.Ordinal);

        var bid = Read(FailurePacks[5], "README.md");
        Assert.Contains("**Testing only:**", bid, StringComparison.Ordinal);
        Assert.Contains("Missing required Bid or parent data rolls back transition, audit, idempotency, target projection, and outbox side effects", bid, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnershipCatalogContainsEveryApprovedPathAndPinsAtomicBoundaries()
    {
        Assert.All(OwnershipPacks, pack =>
        {
            Assert.True(Directory.Exists(Path.Combine(Root, pack)), $"Missing {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "README.md")), $"Missing README for {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "in-process.cs")), $"Missing in-process example for {pack}");
            Assert.True(File.Exists(Path.Combine(Root, pack, "verify.sql")), $"Missing verifier for {pack}");
        });

        var requestReplay = Read(OwnershipPacks[0], "README.md");
        Assert.Contains("the second result has `replay = true`", requestReplay, StringComparison.Ordinal);
        Assert.Contains("one transition audit, one idempotency record, and one notification outbox row", requestReplay, StringComparison.Ordinal);
        Assert.Contains("rolls back prerequisite writes, reloads the committed target row, and issues no second target update", requestReplay, StringComparison.Ordinal);

        var occurrenceReplay = Read(OwnershipPacks[1], "README.md");
        Assert.Contains("same `NotificationOutboxId`", occurrenceReplay, StringComparison.Ordinal);
        Assert.Contains("exactly one outbox row", occurrenceReplay, StringComparison.Ordinal);

        var requestRollback = Read(OwnershipPacks[2], "README.md");
        Assert.Contains("after the workflow call succeeds but before commit", requestRollback, StringComparison.Ordinal);
        Assert.Contains("target row, workflow state, audit, idempotency, and outbox all remain at their pre-request values", requestRollback, StringComparison.Ordinal);

        var scheduledRollback = Read(OwnershipPacks[3], "README.md");
        Assert.Contains("**Testing only:**", scheduledRollback, StringComparison.Ordinal);
        Assert.Contains("scheduled target projection throws", scheduledRollback, StringComparison.Ordinal);
        Assert.Contains("workflow transition, audit, idempotency, outbox, and target projection roll back together", scheduledRollback, StringComparison.Ordinal);

        var continuation = Read(OwnershipPacks[4], "README.md");
        Assert.Contains("first and third candidates succeed", continuation, StringComparison.Ordinal);
        Assert.Contains("middle candidate fails", continuation, StringComparison.Ordinal);
        Assert.Contains("nonzero exit", continuation, StringComparison.Ordinal);

        var ownership = Read(OwnershipPacks[5], "README.md");
        Assert.Contains("complete target row remains byte-for-byte unchanged after the request adapter call", ownership, StringComparison.Ordinal);
        Assert.Contains("target API then persists the authoritative returned `Status` and `WorkflowState`", ownership, StringComparison.Ordinal);
        Assert.Contains("exactly `Status`, `WorkflowState`, `ModifiedBy`, and `ModifiedDate`", ownership, StringComparison.Ordinal);
        Assert.Contains("every other target column remains unchanged", ownership, StringComparison.Ordinal);
    }

    [Fact]
    public void CompositePackCoversFourBidOutcomesAndSixRoutes()
    {
        const string pack = "43-complete-opportunity-and-four-bids";
        var expectedFiles = new[]
        {
            "README.md",
            "sequence.http",
            "in-process.cs",
            "expected-results.sql",
            "expected-emails.md"
        };
        Assert.Equal(
            expectedFiles.Order(StringComparer.Ordinal),
            Directory.GetFiles(Path.Combine(Root, pack)).Select(Path.GetFileName).Order(StringComparer.Ordinal));

        var readme = Read(pack, "README.md");
        foreach (var state in new[] { "ACCEPTED", "PARTIALLY ACCEPTED", "NOT ACCEPTED", "NOT ALLOCATED" })
        {
            Assert.Contains(state, readme, StringComparison.Ordinal);
        }

        foreach (var route in new[]
                 {
                     SqmsKeys.Routes.OpportunityOpen,
                     SqmsKeys.Routes.OpportunityReminder,
                     SqmsKeys.Routes.OpportunityPendingSellerApproval,
                     SqmsKeys.Routes.BidAccepted,
                     SqmsKeys.Routes.BidPartiallyAccepted,
                     SqmsKeys.Routes.BidNotAccepted
                 })
        {
            Assert.Contains(route, readme, StringComparison.Ordinal);
        }

        Assert.Contains("Opportunity | 10000 | `COMPLETED` | 10", readme, StringComparison.Ordinal);
        Assert.Contains("Bid | 10000 | `ACCEPTED` | 7", readme, StringComparison.Ordinal);
        Assert.Contains("Bid | 10001 | `PARTIALLY ACCEPTED` | 5", readme, StringComparison.Ordinal);
        Assert.Contains("Bid | 10002 | `NOT ACCEPTED` | 5", readme, StringComparison.Ordinal);
        Assert.Contains("Bid | 10003 | `NOT ALLOCATED` | 3", readme, StringComparison.Ordinal);
        Assert.Contains("20 | Request-driven target rows remained unchanged", readme, StringComparison.Ordinal);
        Assert.Contains("20 | Target owner persisted the returned state", readme, StringComparison.Ordinal);
        Assert.Contains("5 | Scheduled adapter projected the four owned columns", readme, StringComparison.Ordinal);
        Assert.Contains("25 | Workflow audit rows", readme, StringComparison.Ordinal);
        Assert.Contains("25 | Idempotency rows", readme, StringComparison.Ordinal);
        Assert.Contains("6 | Sent outbox/SMTP messages with retry count zero", readme, StringComparison.Ordinal);
        Assert.Contains("zero SQMS tasks and zero SLA schedules", readme, StringComparison.Ordinal);

        var sequence = Read(pack, "sequence.http");
        Assert.Equal(20, sequence.Split('\n').Count(line => line.StartsWith("POST ", StringComparison.Ordinal)));
        Assert.DoesNotContain("/workflow/scheduled", sequence, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([Root, .. segments]));
}
