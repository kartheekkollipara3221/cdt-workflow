using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.LiveAcceptance;

namespace WorkflowEngine.Tests;

public sealed class SqmsLiveAcceptanceContractTests
{
    [Fact]
    public void ConstantsPinTheIsolatedDatabaseAndEvidenceCounts()
    {
        Assert.Equal("workflowengine_sqms_live_acceptance", AcceptanceConstants.DatabaseName);
        Assert.Equal("SQMS-LIVE-20260821", AcceptanceConstants.RunMarker);
        Assert.Equal(3, AcceptanceConstants.ExpectedOpportunityCount);
        Assert.Equal(15, AcceptanceConstants.ExpectedBidCount);
        Assert.Equal(19, AcceptanceConstants.ExpectedEmailCount);
        Assert.Equal(
            new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero),
            AcceptanceConstants.OpeningUtc);
    }

    [Fact]
    public void AcceptanceProfileDefaultsPreserveThePriorRetainedRun()
    {
        var profile = AcceptanceProfile.FromValues(null, null, null);

        Assert.Equal("workflowengine_sqms_live_acceptance", profile.DatabaseName);
        Assert.Equal("SQMS-LIVE-20260821", profile.RunMarker);
        Assert.Equal(
            new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero),
            profile.OpeningUtc);
    }

    [Fact]
    public void AcceptanceProfileAcceptsAnExplicitIsolatedRun()
    {
        var profile = AcceptanceProfile.FromValues(
            "workflowengine_sqms_email_live_acceptance",
            "SQMS-EMAIL-LIVE-20260821",
            "2030-01-01T15:00:00Z");

        Assert.Equal("workflowengine_sqms_email_live_acceptance", profile.DatabaseName);
        Assert.Equal("SQMS-EMAIL-LIVE-20260821", profile.RunMarker);
        Assert.Equal(
            new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero),
            profile.OpeningUtc);
    }

    [Fact]
    public void SingleLifecycleProfilePinsOneOpportunityThreeBidsAndSeparateSmtp4Dev()
    {
        var profile = AcceptanceProfile.FromValues(
            "workflowengine_sqms_single_lifecycle_live_acceptance",
            "SQMS-SINGLE-LIVE-20260821",
            "2030-01-01T15:00:00Z",
            "single-lifecycle",
            "http://localhost:5001");

        Assert.Equal(AcceptanceScenarioKind.SingleLifecycle, profile.ScenarioKind);
        Assert.Equal(new Uri("http://localhost:5001"), profile.Smtp4DevBaseAddress);

        var matrix = AcceptanceScenarioMatrix.Create(profile.ScenarioKind);
        Assert.Single(matrix.Opportunities);
        Assert.Equal(3, matrix.AllBids.Count);
        Assert.Collection(
            matrix.AllBids,
            bid =>
            {
                Assert.Equal(40m, bid.RequestedAmount);
                Assert.Equal(SqmsKeys.BidStates.Accepted, bid.TerminalState);
            },
            bid =>
            {
                Assert.Equal(30m, bid.RequestedAmount);
                Assert.Equal(SqmsKeys.BidStates.PartiallyAccepted, bid.TerminalState);
            },
            bid =>
            {
                Assert.Equal(20m, bid.RequestedAmount);
                Assert.Equal("DISCOUNT", bid.Category);
                Assert.Equal(SqmsKeys.BidStates.NotAccepted, bid.TerminalState);
            });
        Assert.All(matrix.AllBids, bid => Assert.True(bid.RequestedAmount > 0));
        Assert.True(
            matrix.AllBids
                .Where(bid => bid.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(bid => bid.RequestedAmount) <= 100m);
    }

    [Fact]
    public void ProjectionOwnershipProfilePinsOneOpportunityFourBidsAndSixEmails()
    {
        var profile = AcceptanceProfile.FromValues(
            "workflowengine_sqms_projection_ownership_live_acceptance",
            "SQMS-PROJECTION-OWNERSHIP-LIVE-20260822",
            "2030-01-01T15:00:00Z",
            "projection-ownership",
            "http://localhost:5002");

        Assert.Equal(AcceptanceScenarioKind.ProjectionOwnership, profile.ScenarioKind);
        Assert.Equal(new Uri("http://localhost:5002"), profile.Smtp4DevBaseAddress);

        var definition = AcceptanceScenarioDefinition.Create(profile.ScenarioKind);
        Assert.Single(definition.Matrix.Opportunities);
        Assert.Equal(4, definition.ExpectedBidCount);
        Assert.Equal(5, definition.ExpectedWorkflowInstanceCount);
        Assert.Equal(6, definition.ExpectedEmailCount);
        Assert.Equal(3, definition.ExpectedBidsMovedToIpgReview);
        Assert.Equal(1, definition.ExpectedBidsNotAllocated);
        Assert.Equal(
            [
                SqmsKeys.BidStates.Accepted,
                SqmsKeys.BidStates.PartiallyAccepted,
                SqmsKeys.BidStates.NotAccepted,
                SqmsKeys.BidStates.NotAllocated
            ],
            definition.Matrix.AllBids.Select(bid => bid.TerminalState));
    }

    [Fact]
    public void ScenarioRunnerRecordsRequestAndScheduledProjectionOwnership()
    {
        var root = SqmsDatabasePackageTests.RepositoryRoot();
        var runner = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "WorkflowEngine.Sqms.LiveAcceptance",
            "AcceptanceScenarioRunner.cs"));

        Assert.Contains("RequestUnchanged", runner, StringComparison.Ordinal);
        Assert.Contains("PersistReturnedStateAsTargetOwnerAsync", runner, StringComparison.Ordinal);
        Assert.Contains("TargetOwnerPersisted", runner, StringComparison.Ordinal);
        Assert.Contains("ScheduledProjected", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectOpportunityStateAsync", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectBidStateAsync", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultProfileStillSelectsTheComprehensiveRetainedDataset()
    {
        var profile = AcceptanceProfile.FromValues(null, null, null, null, null);

        Assert.Equal(AcceptanceScenarioKind.Comprehensive, profile.ScenarioKind);
        Assert.Equal(new Uri("http://localhost:5000"), profile.Smtp4DevBaseAddress);
        Assert.Equal(15, AcceptanceScenarioMatrix.Create(profile.ScenarioKind).AllBids.Count);
    }

    [Fact]
    public void ScenarioMatrixContainsEveryTerminalOutcome()
    {
        var matrix = AcceptanceScenarioMatrix.Create();

        Assert.Equal(3, matrix.Opportunities.Count);
        Assert.Equal(15, matrix.AllBids.Count);
        Assert.Equal(6, matrix.AllBids.Count(row => row.TerminalState == "ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "PARTIALLY ACCEPTED"));
        Assert.Equal(4, matrix.AllBids.Count(row => row.TerminalState == "NOT ACCEPTED"));
        Assert.Single(matrix.AllBids, row => row.TerminalState == "NOT ALLOCATED");
        Assert.Single(matrix.AllBids, row => row.ReturnBeforeResubmit);
    }

    [Fact]
    public void AcceptanceClockAdvancesWithoutUsingSystemTime()
    {
        var clock = new AcceptanceTimeProvider(AcceptanceConstants.OpeningUtc);

        clock.SetUtcNow(AcceptanceConstants.OpeningUtc.AddDays(14));

        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), clock.GetUtcNow());
    }

    [Fact]
    public void FixturesHaveExactParentsAmountsAndSentinels()
    {
        var matrix = AcceptanceScenarioMatrix.Create();

        Assert.All(matrix.Opportunities, row =>
        {
            Assert.Contains(AcceptanceConstants.RunMarker, row.Account, StringComparison.Ordinal);
            Assert.Equal(777, row.TotalBidSentinel);
            Assert.Equal("L-SENTINEL", row.LargestBidSentinel);
            Assert.Equal("S-SENTINEL", row.LowestBidSentinel);
            Assert.Equal(5, row.Bids.Count);
        });
        Assert.Equal(
            110m,
            matrix.Opportunities[0].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
        Assert.Equal(
            80m,
            matrix.Opportunities[1].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
        Assert.Equal(
            80m,
            matrix.Opportunities[2].Bids
                .Where(row => row.Category is "FAIR VALUE" or "PREMIUM")
                .Sum(row => row.RequestedAmount));
    }

    [Fact]
    public void OperationPlanHasExactReturnAndTerminalVersions()
    {
        var plan = AcceptanceOperationPlan.Create(AcceptanceScenarioMatrix.Create());

        Assert.Equal(
            ["SUBMITTED", "Draft", "SUBMITTED", "PENDING", "OPEN TO BIDDING"],
            plan.OpportunityA.PreSchedule.Select(row => row.ExpectedState));
        Assert.Equal(10, plan.OpportunityA.Final.ExpectedVersion);
        Assert.Equal(8, plan.OpportunityB.Final.ExpectedVersion);
        Assert.Equal(7, plan.OpportunityC.Final.ExpectedVersion);
        Assert.Equal(7, plan.Bid01.Final.ExpectedVersion);
        Assert.Equal(3, plan.Bid04.Final.ExpectedVersion);
    }

    [Fact]
    public void CalendarAndEmailPlanAreExact()
    {
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(1), AcceptanceConstants.OpenRunUtc);
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(14), AcceptanceConstants.ReminderRunUtc);
        Assert.Equal(AcceptanceConstants.OpeningUtc.AddDays(16), AcceptanceConstants.CloseRunUtc);
        var expected = AcceptanceEmailPlan.Create();
        Assert.Equal(19, expected.Sum(row => row.Count));
        Assert.Equal(6, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidAccepted).Count);
        Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidPartiallyAccepted).Count);
        Assert.Equal(4, expected.Single(row => row.RouteKey == SqmsKeys.Routes.BidNotAccepted).Count);
    }

    [Fact]
    public void SingleLifecycleDefinitionDerivesExactRuntimeAndEmailExpectations()
    {
        var definition = AcceptanceScenarioDefinition.Create(AcceptanceScenarioKind.SingleLifecycle);

        Assert.Equal(1, definition.ExpectedOpportunityCount);
        Assert.Equal(3, definition.ExpectedBidCount);
        Assert.Equal(4, definition.ExpectedWorkflowInstanceCount);
        Assert.Equal(6, definition.ExpectedEmailCount);
        Assert.Equal(1, definition.ExpectedOpportunityStates[SqmsKeys.OpportunityStates.Completed]);
        Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.Accepted]);
        Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.PartiallyAccepted]);
        Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.NotAccepted]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityOpen]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityReminder]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.OpportunityPendingSellerApproval]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidAccepted]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidPartiallyAccepted]);
        Assert.Equal(1, definition.ExpectedEmailRoutes[SqmsKeys.Routes.BidNotAccepted]);
        Assert.Equal(3, definition.ExpectedBidsMovedToIpgReview);
        Assert.Equal(0, definition.ExpectedBidsNotAllocated);
    }

    [Fact]
    public void ComprehensiveDefinitionPreservesPriorRetainedExpectations()
    {
        var definition = AcceptanceScenarioDefinition.Create(AcceptanceScenarioKind.Comprehensive);

        Assert.Equal(3, definition.ExpectedOpportunityCount);
        Assert.Equal(15, definition.ExpectedBidCount);
        Assert.Equal(18, definition.ExpectedWorkflowInstanceCount);
        Assert.Equal(19, definition.ExpectedEmailCount);
        Assert.Equal(2, definition.ExpectedOpportunityStates[SqmsKeys.OpportunityStates.Completed]);
        Assert.Equal(1, definition.ExpectedOpportunityStates[SqmsKeys.OpportunityStates.NotFulfilled]);
        Assert.Equal(6, definition.ExpectedBidStates[SqmsKeys.BidStates.Accepted]);
        Assert.Equal(4, definition.ExpectedBidStates[SqmsKeys.BidStates.PartiallyAccepted]);
        Assert.Equal(4, definition.ExpectedBidStates[SqmsKeys.BidStates.NotAccepted]);
        Assert.Equal(1, definition.ExpectedBidStates[SqmsKeys.BidStates.NotAllocated]);
        Assert.Equal(19, definition.ExpectedEmailRoutes.Values.Sum());
        Assert.Equal(14, definition.ExpectedBidsMovedToIpgReview);
        Assert.Equal(1, definition.ExpectedBidsNotAllocated);
    }

    [Fact]
    public void PowerShellRunnerPinsSafeDatabasePackageAndHiddenSmtp4Dev()
    {
        var path = Path.Combine(
            SqmsDatabasePackageTests.RepositoryRoot(),
            "scripts",
            "run-sqms-live-acceptance.ps1");

        Assert.True(File.Exists(path), $"Missing live acceptance PowerShell runner: {path}");
        var script = File.ReadAllText(path);
        Assert.Contains("workflowengine_sqms_live_acceptance", script, StringComparison.Ordinal);
        Assert.Contains("000_create_sqms_test_tables.sql", script, StringComparison.Ordinal);
        Assert.Contains("001_sqms_notification_outbox_ddl.sql", script, StringComparison.Ordinal);
        Assert.Contains("002_sqms_workflow_metadata_dml.sql", script, StringComparison.Ordinal);
        Assert.Contains("003_verify_sqms_deployment.sql", script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", script, StringComparison.Ordinal);
        Assert.Contains("sqms-live-acceptance-20260821", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--reset", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorrectedEmailRunnerPinsNewEvidenceWithoutOverwritingThePriorRun()
    {
        var path = Path.Combine(
            SqmsDatabasePackageTests.RepositoryRoot(),
            "scripts",
            "run-sqms-email-template-live-acceptance.ps1");

        Assert.True(File.Exists(path), $"Missing corrected-email acceptance runner: {path}");
        var script = File.ReadAllText(path);
        Assert.Contains("workflowengine_sqms_email_live_acceptance", script, StringComparison.Ordinal);
        Assert.Contains("SQMS-EMAIL-LIVE-20260821", script, StringComparison.Ordinal);
        Assert.Contains("sqms-email-live-acceptance-20260821", script, StringComparison.Ordinal);
        Assert.Contains("006_verify_sqms_email_template_live_acceptance.sql", script, StringComparison.Ordinal);
        Assert.Contains("SQMS_LIVE_ACCEPTANCE_DATABASE", script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--reset", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SingleLifecycleRunnerPinsSeparateEvidenceAndNeverDropsIt()
    {
        var root = SqmsDatabasePackageTests.RepositoryRoot();
        var scriptPath = Path.Combine(
            root,
            "scripts",
            "run-sqms-single-lifecycle-live-acceptance.ps1");
        var verifierPath = Path.Combine(
            root,
            "sql",
            "sqms",
            "testing",
            "007_verify_sqms_single_lifecycle_live_acceptance.sql");

        Assert.True(File.Exists(scriptPath));
        Assert.True(File.Exists(verifierPath));
        var script = File.ReadAllText(scriptPath);
        var verifier = File.ReadAllText(verifierPath);

        Assert.Contains("workflowengine_sqms_single_lifecycle_live_acceptance", script, StringComparison.Ordinal);
        Assert.Contains("SQMS-SINGLE-LIVE-20260821", script, StringComparison.Ordinal);
        Assert.Contains("sqms-single-lifecycle-live-acceptance-20260821", script, StringComparison.Ordinal);
        Assert.Contains("SQMS_LIVE_ACCEPTANCE_SCENARIO = 'single-lifecycle'", script, StringComparison.Ordinal);
        Assert.Contains("SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL = 'http://localhost:5001'", script, StringComparison.Ordinal);
        Assert.Contains("Smtp__Port = '3026'", script, StringComparison.Ordinal);
        Assert.Contains("'--imapport='", script, StringComparison.Ordinal);
        Assert.Contains("'--pop3port='", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "DECLARE @RunMarker varchar(100) = 'SQMS-SINGLE-LIVE-20260821'",
            verifier,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectionOwnershipRunnerPinsFreshEvidenceAndIndependentProof()
    {
        var root = SqmsDatabasePackageTests.RepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "run-sqms-projection-ownership-live-acceptance.ps1");
        var verifierPath = Path.Combine(root, "sql", "sqms", "testing", "008_verify_sqms_projection_ownership_live_acceptance.sql");
        var guidePath = Path.Combine(root, "docs", "sqms", "SQMS-PROJECTION-OWNERSHIP-LIVE-ACCEPTANCE.md");

        Assert.True(File.Exists(scriptPath));
        Assert.True(File.Exists(verifierPath));
        Assert.True(File.Exists(guidePath));
        var script = File.ReadAllText(scriptPath);
        var verifier = File.ReadAllText(verifierPath);

        Assert.Contains("workflowengine_sqms_projection_ownership_live_acceptance", script, StringComparison.Ordinal);
        Assert.Contains("SQMS-PROJECTION-OWNERSHIP-LIVE-20260822", script, StringComparison.Ordinal);
        Assert.Contains("sqms-projection-ownership-live-acceptance-20260822", script, StringComparison.Ordinal);
        Assert.Contains("SQMS_LIVE_ACCEPTANCE_SCENARIO = 'projection-ownership'", script, StringComparison.Ordinal);
        Assert.Contains("SQMS_LIVE_ACCEPTANCE_SMTP4DEV_URL = 'http://localhost:5002'", script, StringComparison.Ordinal);
        Assert.Contains("Smtp__Port = '3027'", script, StringComparison.Ordinal);
        Assert.Contains("008_verify_sqms_projection_ownership_live_acceptance.sql", script, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", script, StringComparison.Ordinal);
        Assert.Contains("'--imapport='", script, StringComparison.Ordinal);
        Assert.Contains("'--pop3port='", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--reset", script, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("SqmsProjectionOwnershipEvidence", verifier, StringComparison.Ordinal);
        Assert.Contains("RequestUnchanged", verifier, StringComparison.Ordinal);
        Assert.Contains("TargetOwnerPersisted", verifier, StringComparison.Ordinal);
        Assert.Contains("ScheduledProjected", verifier, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceVerifierJoinsSlaScheduleToTheActualTaskPrimaryKey()
    {
        Assert.Contains(
            "task.[Id] = schedule.[WorkflowTaskId]",
            AcceptanceEvidenceVerifier.SqmsSlaCountSql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "task.[WorkflowTaskId]",
            AcceptanceEvidenceVerifier.SqmsSlaCountSql,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AcceptanceRunMode.Full)]
    [InlineData(AcceptanceRunMode.VerifyOnly, "--verify-only")]
    public void RunModeParserAllowsOnlyFullOrVerificationOnly(
        AcceptanceRunMode expected,
        params string[] args)
    {
        Assert.Equal(expected, AcceptanceRunModeParser.Parse(args));
    }

    [Fact]
    public void RunModeParserRejectsUnknownArguments()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            AcceptanceRunModeParser.Parse(["--unexpected"]));

        Assert.Contains("--verify-only", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SmtpVerifierRequestsOnePageLargeEnoughForAllExpectedMessages()
    {
        Assert.Equal(
            "/api/Messages?pageSize=100",
            AcceptanceEvidenceVerifier.MessageListPath);
    }

    [Fact]
    public void SmtpVerifierDownloadsTheCompleteRfc822MessageForEvidence()
    {
        Assert.Equal(
            "/api/Messages/message-id/download",
            AcceptanceEvidenceVerifier.MessageDownloadPath("message-id"));
    }

    [Fact]
    public void SmtpVerifierCombinesDecodedMetadataWithTheDownloadedBody()
    {
        const string subject = "SQMS Opportunity Pending Seller Approval - SQMS Live Acceptance Opportunity A";
        var evidence = AcceptanceEvidenceVerifier.CombineMessageEvidence(
            $$"""{"subject":"{{subject}}","to":["seller@example.test"]}""",
            "Subject: SQMS Opportunity Pending Seller Approval - SQMS Live Acceptance\r\n Opportunity A\r\n\r\nhttps://sqms.example.test/bids");

        Assert.Contains(subject, evidence, StringComparison.Ordinal);
        Assert.Contains("seller@example.test", evidence, StringComparison.Ordinal);
        Assert.Contains("https://sqms.example.test/bids", evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void SmtpVerifierDoesNotTreatTheReminderSubjectAsTheOpenSubject()
    {
        const string open =
            "Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030";
        const string reminder =
            "REMINDER Q1 2030 Secondary Sale Opportunities - Open to Bidding: Response Required by 01/16/2030";

        Assert.True(AcceptanceEvidenceVerifier.SubjectMatches(open, open));
        Assert.False(AcceptanceEvidenceVerifier.SubjectMatches(open, reminder));
    }
}
