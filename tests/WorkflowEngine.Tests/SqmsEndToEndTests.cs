using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.DependencyInjection;
using WorkflowEngine.Sqms.Persistence;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsEndToEndTests
{
    private static readonly SqmsActor Mfo = new("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
    private static readonly SqmsActor Ipg = new("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
    private static readonly SqmsActor Seller = new("seller-user", "Seller User", [SqmsKeys.Roles.Seller]);

    [Fact]
    public async Task CompleteOpportunityPathsPreserveExactStatesRuntimeAndExcludedColumns()
    {
        await using var fixture = await Fixture.CreateAsync("opportunity_paths");
        fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2030-01-01T15:00:00Z"));

        var first = await fixture.StartOpportunity(10000, Mfo);
        AssertState(first, SqmsKeys.OpportunityStates.Submitted, 2);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.ReturnToDraft, 2, Ipg), SqmsKeys.OpportunityStates.Draft, 3);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.Submit, 3, Mfo), SqmsKeys.OpportunityStates.Submitted, 4);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.MarkPending, 4, Ipg), SqmsKeys.OpportunityStates.Pending, 5);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.OpenForBidding, 5, Ipg), SqmsKeys.OpportunityStates.OpenToBidding, 6);

        foreach (var id in new[] { 10001, 10002 })
        {
            AssertState(await fixture.StartOpportunity(id, Mfo), SqmsKeys.OpportunityStates.Submitted, 2);
            AssertState(await fixture.OpportunityAction(id, SqmsKeys.OpportunityActions.MarkPending, 2, Ipg), SqmsKeys.OpportunityStates.Pending, 3);
            AssertState(await fixture.OpportunityAction(id, SqmsKeys.OpportunityActions.OpenForBidding, 3, Ipg), SqmsKeys.OpportunityStates.OpenToBidding, 4);
        }

        fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2030-01-17T15:00:00Z"));
        var scheduled = await fixture.Scheduled.RunAsync(CancellationToken.None);
        Assert.True(scheduled.Success);
        Assert.Equal(3, scheduled.OpportunitiesClosed);
        Assert.Equal(0, scheduled.OpenNotificationsQueued);
        Assert.Equal(0, scheduled.ReminderNotificationsQueued);

        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.RequestSellerApproval, 7, Ipg, SellerRecipients()), SqmsKeys.OpportunityStates.PendingSellerApproval, 8);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.MarkAwaitingAllocation, 8, Ipg), SqmsKeys.OpportunityStates.AwaitingAllocation, 9);
        AssertState(await fixture.OpportunityAction(10000, SqmsKeys.OpportunityActions.Complete, 9, Ipg), SqmsKeys.OpportunityStates.Completed, 10);

        AssertState(await fixture.OpportunityAction(10001, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, Ipg, SellerRecipients()), SqmsKeys.OpportunityStates.PendingSellerApproval, 6);
        AssertState(await fixture.OpportunityAction(10001, SqmsKeys.OpportunityActions.MarkPartiallyAllocated, 6, Ipg), SqmsKeys.OpportunityStates.PartiallyAllocated, 7);
        AssertState(await fixture.OpportunityAction(10001, SqmsKeys.OpportunityActions.Complete, 7, Ipg), SqmsKeys.OpportunityStates.Completed, 8);

        AssertState(await fixture.OpportunityAction(10002, SqmsKeys.OpportunityActions.RequestSellerApproval, 5, Ipg, SellerRecipients()), SqmsKeys.OpportunityStates.PendingSellerApproval, 6);
        AssertState(await fixture.OpportunityAction(10002, SqmsKeys.OpportunityActions.MarkNotFulfilled, 6, Ipg), SqmsKeys.OpportunityStates.NotFulfilled, 7);

        fixture.DbContext.ChangeTracker.Clear();
        var instances = await fixture.DbContext.WorkflowInstances
            .Where(row => row.WorkflowKey == SqmsKeys.OpportunityWorkflow)
            .OrderBy(row => row.BusinessObjectId)
            .ToListAsync();
        Assert.Equal(3, instances.Count);
        Assert.All(instances, instance =>
        {
            Assert.Equal("Completed", instance.Status);
            Assert.NotNull(instance.CompletedAtUtc);
        });
        Assert.Equal(9, await fixture.AuditCountAsync(SqmsKeys.OpportunityWorkflowInstanceId(10000)));
        Assert.Empty(fixture.DbContext.WorkflowTasks);
        Assert.Empty(fixture.DbContext.WorkflowTaskSlaSchedules);
        Assert.Equal(3, await fixture.DbContext.NotificationOutbox.CountAsync(row =>
            row.NotificationType == SqmsKeys.Routes.OpportunityPendingSellerApproval));
        Assert.All(await fixture.DbContext.NotificationOutbox.ToListAsync(), row =>
        {
            Assert.Contains("opportunityId=", row.Body, StringComparison.Ordinal);
            Assert.Contains("relationship.manager@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
            Assert.Contains("seller.team@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        });
        await fixture.AssertSentinelsAsync([10000, 10001, 10002]);
    }

    [Fact]
    public async Task CompleteBidPathsClassifyAndDisposeWithoutChangingParentOpportunity()
    {
        await using var fixture = await Fixture.CreateAsync("bid_paths");
        await fixture.SetOpportunityStateAsync(10000, SqmsKeys.OpportunityStates.ClosedToBidding);
        await fixture.SetBidDataAsync(20000, "$60.00", "Fair Value");
        await fixture.SetBidDataAsync(20001, "$50.00", "Premium");
        await fixture.SetBidDataAsync(20002, "$25.00", "Discount");
        await fixture.SetBidDataAsync(20003, "$0.00", "Fair Value");

        AssertState(await fixture.StartBid(20000, Mfo), SqmsKeys.BidStates.Submitted, 2);
        AssertState(await fixture.BidAction(20000, SqmsKeys.BidActions.ReturnToDraft, 2, Ipg), SqmsKeys.BidStates.Draft, 3);
        AssertState(await fixture.BidAction(20000, SqmsKeys.BidActions.Submit, 3, Mfo), SqmsKeys.BidStates.Submitted, 4);
        foreach (var id in new[] { 20001, 20002, 20003 })
        {
            AssertState(await fixture.StartBid(id, Mfo), SqmsKeys.BidStates.Submitted, 2);
        }

        var scheduled = await fixture.Scheduled.RunAsync(CancellationToken.None);
        Assert.True(scheduled.Success);
        Assert.Equal(3, scheduled.BidsMovedToIpgReview);
        Assert.Equal(1, scheduled.BidsNotAllocated);
        Assert.Equal(SqmsKeys.BidStates.NotAllocated, (await fixture.Workflow.GetBidContextAsync(20002, Seller, CancellationToken.None)).Context?.WorkflowContext.Workflow.CurrentState);

        AssertState(await fixture.BidAction(20000, SqmsKeys.BidActions.SendToSellerReview, 5, Ipg), SqmsKeys.BidStates.UnderReviewBySeller, 6);
        AssertState(await fixture.BidAction(20000, SqmsKeys.BidActions.Accept, 6, Seller, BuyerRecipients()), SqmsKeys.BidStates.Accepted, 7);
        AssertState(await fixture.BidAction(20001, SqmsKeys.BidActions.SendToSellerReview, 3, Ipg), SqmsKeys.BidStates.UnderReviewBySeller, 4);
        AssertState(await fixture.BidAction(20001, SqmsKeys.BidActions.PartiallyAccept, 4, Seller, BuyerRecipients()), SqmsKeys.BidStates.PartiallyAccepted, 5);
        AssertState(await fixture.BidAction(20003, SqmsKeys.BidActions.SendToSellerReview, 3, Ipg), SqmsKeys.BidStates.UnderReviewBySeller, 4);
        AssertState(await fixture.BidAction(20003, SqmsKeys.BidActions.Reject, 4, Seller, BuyerRecipients()), SqmsKeys.BidStates.NotAccepted, 5);

        Assert.Equal(
            SqmsKeys.OpportunityStates.ClosedToBidding,
            await fixture.TargetWorkflowStateAsync("Opportunity", 10000));
        var routes = await fixture.DbContext.NotificationOutbox
            .OrderBy(row => row.Id)
            .Select(row => row.NotificationType)
            .ToArrayAsync();
        Assert.Equal(
            [SqmsKeys.Routes.BidAccepted, SqmsKeys.Routes.BidPartiallyAccepted, SqmsKeys.Routes.BidNotAccepted],
            routes);
        Assert.All(await fixture.DbContext.NotificationOutbox.ToListAsync(), row =>
        {
            Assert.Contains("bidId=", row.Body, StringComparison.Ordinal);
            Assert.Contains("buyer.manager@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
            Assert.Contains("buyer.team@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        });
        await fixture.AssertSentinelsAsync([10000]);
    }

    [Fact]
    public async Task CatchUpRunClosesFirstQueuesOneGroupAndDispatchesImmutableMessagesOnce()
    {
        await using var fixture = await Fixture.CreateAsync("catchup_dispatch");
        fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2029-12-15T15:00:00Z"));
        await fixture.DriveOpportunityToOpenAsync(10000);
        fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2030-01-01T15:00:00Z"));
        await fixture.DriveOpportunityToOpenAsync(10001);
        fixture.Clock.SetUtcNow(DateTimeOffset.Parse("2030-01-15T15:00:00Z"));

        var first = await fixture.Scheduled.RunAsync(CancellationToken.None);
        var second = await fixture.Scheduled.RunAsync(CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(1, first.OpportunitiesClosed);
        Assert.Equal(1, first.OpenNotificationsQueued);
        Assert.Equal(1, first.ReminderNotificationsQueued);
        Assert.Equal(2, second.AlreadyQueued);
        Assert.Equal(SqmsKeys.OpportunityStates.ClosedToBidding, await fixture.TargetWorkflowStateAsync("Opportunity", 10000));
        Assert.Equal(SqmsKeys.OpportunityStates.OpenToBidding, await fixture.TargetWorkflowStateAsync("Opportunity", 10001));

        var sender = new RecordingNotificationSender();
        var processed = await new ProcessNotificationOutboxService(fixture.DbContext, sender, fixture.Clock)
            .ProcessAsync(CancellationToken.None);

        Assert.Null(processed.Failure);
        Assert.Equal(2, processed.Processed.Count);
        Assert.Equal(2, sender.SentRows.Count);
        Assert.All(sender.SentRows, delivery =>
        {
            Assert.Null(delivery.WorkflowInstanceId);
            Assert.StartsWith("SQMS:Opportunity", delivery.DisplayReference, StringComparison.Ordinal);
            Assert.Contains("https://dashboard.example.test/open?static=true", delivery.Body, StringComparison.Ordinal);
            Assert.Contains("<a href=\"https://dashboard.example.test/open?static=true\"", delivery.HtmlBody, StringComparison.Ordinal);
            Assert.Contains("sqms-open@example.test", delivery.ToEmailAddresses);
            Assert.Contains("sqms-open-cc@example.test", delivery.CcEmailAddresses);
        });
        fixture.DbContext.ChangeTracker.Clear();
        Assert.All(fixture.DbContext.NotificationOutbox, row =>
        {
            Assert.Equal("Sent", row.Status);
            Assert.Equal(0, row.RetryCount);
        });
        await fixture.AssertSentinelsAsync([10000, 10001]);
    }

    [Fact]
    public async Task ClassificationRetryAfterSqlFailureUsesCommittedHistoryInFreshScope()
    {
        await using var fixture = await Fixture.CreateAsync("classification_retry");
        await fixture.SetOpportunityStateAsync(10000, SqmsKeys.OpportunityStates.ClosedToBidding);
        await fixture.SetBidDataAsync(20000, "$60.00", "Fair Value");
        await fixture.SetBidDataAsync(20001, "$50.00", "Premium");
        await fixture.SetBidDataAsync(20002, "$25.00", "Discount");
        // A never-submitted draft must not contribute to the threshold.
        await fixture.SetBidDataAsync(20003, "$500.00", "Fair Value");
        foreach (var id in new[] { 20000, 20001, 20002 })
        {
            AssertState(await fixture.StartBid(id, Mfo), SqmsKeys.BidStates.Submitted, 2);
        }

        await fixture.DbContext.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER [SSQMS].[FailClassificationForRetryTest] ON [SSQMS].[Bid] AFTER UPDATE AS
            BEGIN
                IF EXISTS (SELECT 1 FROM inserted WHERE [ID] = 20002 AND [WorkflowState] <> N'SUBMITTED')
                    THROW 51000, 'Injected classification projection failure', 1;
            END
            """);
        try
        {
            var failure = await Assert.ThrowsAsync<SqlException>(() => fixture.Scheduled.RunAsync(CancellationToken.None));
            Assert.Equal(51000, failure.Number);
        }
        finally
        {
            await fixture.DbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER [SSQMS].[FailClassificationForRetryTest]");
        }

        Assert.Equal(SqmsKeys.BidStates.IpgReview, await fixture.TargetWorkflowStateAsync("Bid", 20000));
        Assert.Equal(SqmsKeys.BidStates.IpgReview, await fixture.TargetWorkflowStateAsync("Bid", 20001));
        Assert.Equal(SqmsKeys.BidStates.Submitted, await fixture.TargetWorkflowStateAsync("Bid", 20002));
        Assert.Equal(1, await fixture.AuditCountAsync(SqmsKeys.BidWorkflowInstanceId(20002)));
        AssertState(await fixture.BidAction(20000, SqmsKeys.BidActions.SendToSellerReview, 3, Ipg),
            SqmsKeys.BidStates.UnderReviewBySeller, 4);

        using var retryScope = fixture.CreateScope();
        var store = retryScope.ServiceProvider.GetRequiredService<ISqmsDataStore>();
        var basis = await store.GetBidClassificationBasisAsync(10000, CancellationToken.None);
        Assert.Equal([20000, 20001, 20002], basis.Select(row => row.Id));
        Assert.Equal(20002, Assert.Single(await store.GetSubmittedBidsAsync(10000, CancellationToken.None)).Id);
        var scheduler = retryScope.ServiceProvider.GetRequiredService<ISqmsScheduledService>();
        var retry = await scheduler.RunAsync(CancellationToken.None);
        Assert.True(retry.Success);
        Assert.Equal(1, retry.BidsNotAllocated);
        Assert.Equal(0, retry.BidsMovedToIpgReview);
        Assert.Equal(SqmsKeys.BidStates.NotAllocated, await fixture.TargetWorkflowStateAsync("Bid", 20002));
        Assert.Equal(2, await fixture.AuditCountAsync(SqmsKeys.BidWorkflowInstanceId(20002)));
        Assert.Equal(3, await fixture.AuditCountAsync(SqmsKeys.BidWorkflowInstanceId(20000)));
        Assert.Equal(2, await fixture.AuditCountAsync(SqmsKeys.BidWorkflowInstanceId(20001)));
        var replay = await scheduler.RunAsync(CancellationToken.None);
        Assert.True(replay.Success);
        Assert.Equal(0, replay.BidsNotAllocated + replay.BidsMovedToIpgReview);
    }

    private static void AssertState(SqmsOperationResult result, string state, int version)
    {
        Assert.True(result.Success, result.Message);
        Assert.Equal(state, result.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(version, result.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);
    }

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> SellerRecipients() =>
        Recipients(
            ("relationshipManager", "relationship.manager@example.test"),
            ("sellerRmTeam", "seller.team@example.test"));

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> BuyerRecipients() =>
        Recipients(
            ("relationshipManager", "buyer.manager@example.test"),
            ("buyerRmTeam", "buyer.team@example.test"));

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> Recipients(
        params (string Key, string Address)[] values) =>
        values.ToDictionary(
            value => value.Key,
            value => (IReadOnlyList<WorkflowRecipientInput>)[new(value.Key, value.Address, value.Key)],
            StringComparer.Ordinal);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;

        private Fixture(ServiceProvider provider, IServiceScope scope, AdjustableTimeProvider clock)
        {
            this.provider = provider;
            this.scope = scope;
            Clock = clock;
        }

        public AdjustableTimeProvider Clock { get; }
        public WorkflowDbContext DbContext => scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        public ISqmsWorkflowService Workflow => scope.ServiceProvider.GetRequiredService<ISqmsWorkflowService>();
        public ISqmsScheduledService Scheduled => scope.ServiceProvider.GetRequiredService<ISqmsScheduledService>();
        public IServiceScope CreateScope() => provider.CreateScope();

        public static async Task<Fixture> CreateAsync(string suffix)
        {
            var databaseName = $"workflowengine_sqms_e2e_{suffix}_tests";
            var connectionString = TestSqlConnectionFactory.ConnectionString(databaseName);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Notifications:Sender"] = "NoOp",
                    ["Sqms:Enabled"] = "true",
                    ["Sqms:Links:OpenBiddingDashboardUrl"] = "https://dashboard.example.test/open?static=true",
                    ["Sqms:Links:SellerApprovalBidsDashboardUrl"] = "https://dashboard.example.test/bids?view=seller",
                    ["Sqms:Links:BidFormUrl"] = "https://forms.example.test/bid?mode=edit",
                    ["Sqms:Scheduling:TimeZoneId"] = "UTC",
                    ["Sqms:Scheduling:BiddingDays"] = "15",
                    ["Sqms:Scheduling:SellerResponseDays"] = "5"
                })
                .Build();
            var clock = new AdjustableTimeProvider(DateTimeOffset.Parse("2030-01-01T15:00:00Z"));
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<TimeProvider>(clock)
                .AddScoped<WorkflowActionEnvelopeNormalizer>()
                .AddScoped<CanonicalRequestHasher>()
                .AddScoped<WorkflowActionResultBuilder>()
                .AddScoped<CanonicalWorkflowResultSerializer>()
                .AddWorkflowInfrastructure(configuration)
                .AddSqmsWorkflowIntegration(configuration);
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var fixture = new Fixture(provider, scope, clock);
            await fixture.DbContext.Database.EnsureDeletedAsync();
            await fixture.DbContext.Database.MigrateAsync();
            var root = SqmsDatabasePackageTests.RepositoryRoot();
            await SqmsDatabasePackageTests.ExecuteScriptAsync(
                connectionString,
                Path.Combine(root, "sql", "sqms", "testing", "000_create_sqms_test_tables.sql"));
            await SqmsDatabasePackageTests.ExecuteScriptAsync(
                connectionString,
                Path.Combine(root, "sql", "sqms", "002_sqms_workflow_metadata_dml.sql"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["SqmsOpenToRecipientsJson"] = "[\"sqms-open@example.test\"]",
                    ["SqmsOpenCcRecipientsJson"] = "[\"sqms-open-cc@example.test\"]",
                    ["SqmsSellerApprovalCcRecipientsJson"] = "[\"sqms-seller-cc@example.test\"]",
                    ["SqmsBidDispositionCcRecipientsJson"] = "[\"sqms-bid-cc@example.test\"]"
                });
            await fixture.InsertTargetRowsAsync();
            return fixture;
        }

        public Task<SqmsOperationResult> StartOpportunity(int id, SqmsActor actor) =>
            ExecuteRequestDrivenOperationAsync(
                "Opportunity",
                id,
                actor,
                () => Workflow.StartOpportunityAsync(id, actor, CancellationToken.None));

        public Task<SqmsOperationResult> StartBid(int id, SqmsActor actor) =>
            ExecuteRequestDrivenOperationAsync(
                "Bid",
                id,
                actor,
                () => Workflow.StartBidAsync(id, actor, CancellationToken.None));

        public Task<SqmsOperationResult> OpportunityAction(
            int id,
            string action,
            int version,
            SqmsActor actor,
            IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>>? recipients = null) =>
            ExecuteRequestDrivenOperationAsync(
                "Opportunity",
                id,
                actor,
                () => Workflow.ExecuteOpportunityActionAsync(
                    new SqmsExecuteRequest(id, action, version, actor, recipients ?? EmptyRecipients()),
                    CancellationToken.None));

        public Task<SqmsOperationResult> BidAction(
            int id,
            string action,
            int version,
            SqmsActor actor,
            IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>>? recipients = null) =>
            ExecuteRequestDrivenOperationAsync(
                "Bid",
                id,
                actor,
                () => Workflow.ExecuteBidActionAsync(
                    new SqmsExecuteRequest(id, action, version, actor, recipients ?? EmptyRecipients()),
                    CancellationToken.None));

        public async Task DriveOpportunityToOpenAsync(int id)
        {
            AssertState(await StartOpportunity(id, Mfo), SqmsKeys.OpportunityStates.Submitted, 2);
            AssertState(await OpportunityAction(id, SqmsKeys.OpportunityActions.MarkPending, 2, Ipg), SqmsKeys.OpportunityStates.Pending, 3);
            AssertState(await OpportunityAction(id, SqmsKeys.OpportunityActions.OpenForBidding, 3, Ipg), SqmsKeys.OpportunityStates.OpenToBidding, 4);
        }

        public Task<int> AuditCountAsync(string workflowInstanceId) =>
            DbContext.WorkflowAudits.CountAsync(row => row.WorkflowInstanceId == workflowInstanceId);

        public Task SetOpportunityStateAsync(int id, string state) =>
            DbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [SSQMS].[Opportunity] SET [Status] = {0}, [WorkflowState] = {0} WHERE [ID] = {1}",
                state,
                id);

        public Task SetBidDataAsync(int id, string amount, string category) =>
            DbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [SSQMS].[Bid] SET [CommitmentAmountRequested] = {0}, [BidExpressedAsFairValue] = {1} WHERE [ID] = {2}",
                amount,
                category,
                id);

        public async Task<string?> TargetWorkflowStateAsync(string table, int id)
        {
            return table switch
            {
                "Opportunity" => await DbContext.Database.SqlQueryRaw<string>(
                    "SELECT [WorkflowState] AS [Value] FROM [SSQMS].[Opportunity] WHERE [ID] = {0}", id).SingleAsync(),
                "Bid" => await DbContext.Database.SqlQueryRaw<string>(
                    "SELECT [WorkflowState] AS [Value] FROM [SSQMS].[Bid] WHERE [ID] = {0}", id).SingleAsync(),
                _ => throw new ArgumentOutOfRangeException(nameof(table))
            };
        }

        public Task<string> TargetRowSnapshotAsync(string table, int id) => table switch
        {
            "Opportunity" => DbContext.Database.SqlQueryRaw<string>(
                "SELECT (SELECT * FROM [SSQMS].[Opportunity] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
                id).SingleAsync(),
            "Bid" => DbContext.Database.SqlQueryRaw<string>(
                "SELECT (SELECT * FROM [SSQMS].[Bid] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
                id).SingleAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };

        public async Task PersistReturnedStateAsTargetOwnerAsync(
            string entityType,
            int id,
            SqmsOperationResult result,
            string modifiedBy)
        {
            var state = result.Context?.WorkflowContext.Workflow.CurrentState
                ?? throw new InvalidOperationException("A successful request operation must return authoritative workflow state.");
            var modifiedAtUtc = Clock.GetUtcNow().UtcDateTime;
            _ = entityType switch
            {
                "Opportunity" => await DbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE [SSQMS].[Opportunity] SET [Status] = {0}, [WorkflowState] = {0}, [ModifiedBy] = {1}, [ModifiedDate] = {2} WHERE [ID] = {3}",
                    state,
                    modifiedBy,
                    modifiedAtUtc,
                    id),
                "Bid" => await DbContext.Database.ExecuteSqlRawAsync(
                    "UPDATE [SSQMS].[Bid] SET [Status] = {0}, [WorkflowState] = {0}, [ModifiedBy] = {1}, [ModifiedDate] = {2} WHERE [ID] = {3}",
                    state,
                    modifiedBy,
                    modifiedAtUtc,
                    id),
                _ => throw new ArgumentOutOfRangeException(nameof(entityType))
            };
        }

        public async Task AssertSentinelsAsync(int[] opportunityIds)
        {
            foreach (var id in opportunityIds)
            {
                var value = await DbContext.Database.SqlQueryRaw<string>(
                    "SELECT CONCAT([TotalBid], '|', [LargestBid], '|', [LowestBid]) AS [Value] FROM [SSQMS].[Opportunity] WHERE [ID] = {0}",
                    id).SingleAsync();
                Assert.Equal("777|L-SENTINEL|S-SENTINEL", value);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.Database.EnsureDeletedAsync();
            scope.Dispose();
            await provider.DisposeAsync();
        }

        private async Task InsertTargetRowsAsync()
        {
            await DbContext.Database.ExecuteSqlRawAsync("""
                SET IDENTITY_INSERT [SSQMS].[Opportunity] ON;
                INSERT INTO [SSQMS].[Opportunity]
                    ([ID], [SellerRMTeam], [Account], [AccountName], [AccountNumber], [RelationshipManager],
                     [FundConsideredSecondarySale], [Vehicle], [CommitmentAmountSale], [EffectiveDateForSale],
                     [TotalBid], [LargestBid], [LowestBid], [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (10000, 'Seller Team', 'OPP-1', 'Opportunity One', 'OA-1', 'Relationship Manager', 'Fund One', 'Vehicle One', '$100.00', '2030-03-31', 777, 'L-SENTINEL', 'S-SENTINEL', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (10001, 'Seller Team', 'OPP-2', 'Opportunity Two', 'OA-2', 'Relationship Manager', 'Fund One', 'Vehicle One', '$100.00', '2030-02-15', 777, 'L-SENTINEL', 'S-SENTINEL', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (10002, 'Seller Team', 'OPP-3', 'Opportunity Three', 'OA-3', 'Relationship Manager', 'Fund One', 'Vehicle One', '$100.00', '2030-03-01', 777, 'L-SENTINEL', 'S-SENTINEL', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (10003, 'Seller Team', 'OPP-4', 'Opportunity Four', 'OA-4', 'Relationship Manager', 'Fund One', 'Vehicle One', '$100.00', '2030-06-30', 777, 'L-SENTINEL', 'S-SENTINEL', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0);
                SET IDENTITY_INSERT [SSQMS].[Opportunity] OFF;

                SET IDENTITY_INSERT [SSQMS].[Bid] ON;
                INSERT INTO [SSQMS].[Bid]
                    ([ID], [OpportunityId], [BuyerRmteam], [BidForFullCommitment], [AccountName], [AccountNumber],
                     [RelationshipManager], [CommitmentAmountRequested], [BidExpressedAsFairValue],
                     [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (20000, 10000, 'Buyer Team', 'Yes', 'Bid One', 'BA-1', 'Buyer RM', '$10.00', 'Fair Value', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (20001, 10000, 'Buyer Team', 'Yes', 'Bid Two', 'BA-2', 'Buyer RM', '$10.00', 'Premium', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (20002, 10000, 'Buyer Team', 'Yes', 'Bid Three', 'BA-3', 'Buyer RM', '$10.00', 'Discount', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0),
                    (20003, 10000, 'Buyer Team', 'Yes', 'Bid Four', 'BA-4', 'Buyer RM', '$10.00', 'Fair Value', 'Draft', 'Draft', 'mfo-user', '2030-01-01', 0);
                SET IDENTITY_INSERT [SSQMS].[Bid] OFF;
                """);
        }

        private async Task<SqmsOperationResult> ExecuteRequestDrivenOperationAsync(
            string entityType,
            int id,
            SqmsActor actor,
            Func<Task<SqmsOperationResult>> operation)
        {
            var before = await TargetRowSnapshotAsync(entityType, id);
            var result = await operation();
            Assert.Equal(before, await TargetRowSnapshotAsync(entityType, id));
            if (result.Success)
            {
                await PersistReturnedStateAsTargetOwnerAsync(entityType, id, result, actor.UserId);
                Assert.Equal(
                    result.Context?.WorkflowContext.Workflow.CurrentState,
                    await TargetWorkflowStateAsync(entityType, id));
            }

            return result;
        }

        private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> EmptyRecipients() =>
            new(StringComparer.Ordinal);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => now;

        public void SetUtcNow(DateTimeOffset value) => now = value.ToUniversalTime();
    }
}
