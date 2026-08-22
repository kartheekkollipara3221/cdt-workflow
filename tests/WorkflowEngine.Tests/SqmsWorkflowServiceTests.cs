using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;
using WorkflowEngine.Application.Results;
using WorkflowEngine.Infrastructure.DependencyInjection;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Contracts;
using WorkflowEngine.Sqms.DependencyInjection;
using WorkflowEngine.Sqms.Persistence;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsWorkflowServiceTests
{
    private static readonly SqmsActor MfoActor = new("mfo-user", "MFO User", [SqmsKeys.Roles.Mfo]);
    private static readonly SqmsActor IpgActor = new("ipg-user", "IPG User", [SqmsKeys.Roles.Ipg]);
    private static readonly SqmsActor SystemActor = new("system", "System", [SqmsKeys.Roles.System]);
    private static readonly SqmsActor SellerActor = new("seller-user", "Seller User", [SqmsKeys.Roles.Seller]);

    [Fact]
    public async Task ContextDoesNotLazilyStartMissingOpportunityOrBidWorkflow()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("context_missing");
        var beforeCount = await fixture.DbContext.WorkflowInstances.CountAsync();

        var opportunity = await fixture.Service.GetOpportunityContextAsync(10000, MfoActor, CancellationToken.None);
        var bid = await fixture.Service.GetBidContextAsync(10000, MfoActor, CancellationToken.None);

        Assert.False(opportunity.Success);
        Assert.Equal("NotWorkflowManaged", opportunity.ResultCode);
        Assert.Null(opportunity.Context);
        Assert.False(bid.Success);
        Assert.Equal("NotWorkflowManaged", bid.ResultCode);
        Assert.Null(bid.Context);
        Assert.Equal(beforeCount, await fixture.DbContext.WorkflowInstances.CountAsync());
    }

    [Fact]
    public async Task StartOpportunityAndBidReturnSubmittedWithoutUpdatingTargetRows()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("start_submit");
        var opportunityBefore = await fixture.TargetRowSnapshotAsync("Opportunity", 10000);
        var bidBefore = await fixture.TargetRowSnapshotAsync("Bid", 10000);

        var opportunity = await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None);
        var bid = await fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None);

        Assert.True(opportunity.Success, opportunity.Message);
        Assert.Equal("SUBMITTED", opportunity.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(2, opportunity.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);
        Assert.True(bid.Success, bid.Message);
        Assert.Equal("SUBMITTED", bid.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(2, bid.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);

        fixture.DbContext.ChangeTracker.Clear();
        Assert.Equal(2, await fixture.DbContext.WorkflowInstances.CountAsync());
        Assert.Equal(2, await fixture.DbContext.WorkflowAudits.CountAsync());
        Assert.Equal(2, await fixture.DbContext.IdempotencyRecords.CountAsync());
        Assert.Empty(fixture.DbContext.WorkflowTasks);
        Assert.Empty(fixture.DbContext.WorkflowTaskSlaSchedules);
        Assert.Equal(opportunityBefore, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
        Assert.Equal(bidBefore, await fixture.TargetRowSnapshotAsync("Bid", 10000));
    }

    [Theory]
    [InlineData("Opportunity")]
    [InlineData("Bid")]
    public async Task RequestDrivenStartDoesNotCallTargetProjection(string entityType)
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync(
            $"projection_failure_{entityType.ToLowerInvariant()}",
            failProjection: true);

        Func<Task<SqmsOperationResult>> action = entityType == "Opportunity"
            ? () => fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)
            : () => fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None);

        var result = await action();

        Assert.True(result.Success, result.Message);
        Assert.Equal(SqmsKeys.OpportunityStates.Submitted, result.Context?.WorkflowContext.Workflow.CurrentState);
        fixture.DbContext.ChangeTracker.Clear();
        Assert.Single(fixture.DbContext.WorkflowInstances);
        Assert.Single(fixture.DbContext.WorkflowAudits);
        Assert.Single(fixture.DbContext.IdempotencyRecords);
    }

    [Fact]
    public async Task ActionsUseOnlyActionKeyActorAndExpectedVersionForBothEntities()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("actions");
        Assert.True((await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)).Success);
        Assert.True((await fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None)).Success);

        var forbiddenOpportunity = await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.MarkPending, 2, MfoActor),
            CancellationToken.None);
        var staleOpportunity = await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.MarkPending, 1, IpgActor),
            CancellationToken.None);
        var opportunityBefore = await fixture.TargetRowSnapshotAsync("Opportunity", 10000);
        var returnedOpportunity = await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.ReturnToDraft, 2, IpgActor),
            CancellationToken.None);

        Assert.Equal("Forbidden", forbiddenOpportunity.ResultCode);
        Assert.Equal("ConcurrencyConflict", staleOpportunity.ResultCode);
        Assert.True(returnedOpportunity.Success, returnedOpportunity.Message);
        Assert.Equal("Draft", returnedOpportunity.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(3, returnedOpportunity.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);

        var forbiddenBid = await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.ReturnToDraft, 2, MfoActor),
            CancellationToken.None);
        var staleBid = await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.ReturnToDraft, 1, IpgActor),
            CancellationToken.None);
        var bidBefore = await fixture.TargetRowSnapshotAsync("Bid", 10000);
        var returnedBid = await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.ReturnToDraft, 2, IpgActor),
            CancellationToken.None);

        Assert.Equal("Forbidden", forbiddenBid.ResultCode);
        Assert.Equal("ConcurrencyConflict", staleBid.ResultCode);
        Assert.True(returnedBid.Success, returnedBid.Message);
        Assert.Equal("Draft", returnedBid.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(3, returnedBid.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);
        Assert.Equal(opportunityBefore, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
        Assert.Equal(bidBefore, await fixture.TargetRowSnapshotAsync("Bid", 10000));
    }

    [Fact]
    public async Task RequestDrivenIdempotentReplayDoesNotUpdateTargetRowOrRepeatSideEffects()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("manual_replay");
        Assert.True((await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)).Success);
        var request = Request(10000, SqmsKeys.OpportunityActions.ReturnToDraft, 2, IpgActor);
        var targetBefore = await fixture.TargetRowSnapshotAsync("Opportunity", 10000);

        var first = await fixture.Service.ExecuteOpportunityActionAsync(request, CancellationToken.None);
        var auditCount = await fixture.DbContext.WorkflowAudits.CountAsync();
        var idempotencyCount = await fixture.DbContext.IdempotencyRecords.CountAsync();
        var replay = await fixture.Service.ExecuteOpportunityActionAsync(request, CancellationToken.None);

        Assert.True(first.Success, first.Message);
        Assert.True(replay.Success, replay.Message);
        Assert.Equal(first.WorkflowResult?.Result.NewState, replay.WorkflowResult?.Result.NewState);
        Assert.Equal(targetBefore, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
        Assert.Equal(auditCount, await fixture.DbContext.WorkflowAudits.CountAsync());
        Assert.Equal(idempotencyCount, await fixture.DbContext.IdempotencyRecords.CountAsync());
        Assert.Empty(fixture.DbContext.NotificationOutbox);
    }

    [Fact]
    public void PublicSqmsActionContractHasNoClientNextStateOrTargetHttpDto()
    {
        var propertyNames = typeof(SqmsExecuteRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(
            ["EntityId", "ActionKey", "ExpectedWorkflowVersion", "Actor", "RecipientInputs"],
            propertyNames);
        Assert.DoesNotContain(propertyNames, name => name.Contains("State", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SellerApprovalEmailUsesTrustedOpportunityDataResolvedRecipientsAndConfiguredLink()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("seller_email");
        Assert.True((await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.MarkPending, 2, IpgActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.OpenForBidding, 3, IpgActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.CloseBidding, 4, SystemActor), CancellationToken.None)).Success);

        var result = await fixture.Service.ExecuteOpportunityActionAsync(
            Request(
                10000,
                SqmsKeys.OpportunityActions.RequestSellerApproval,
                5,
                IpgActor,
                RecipientInputs(
                    ("relationshipManager", "relationship.manager@example.test"),
                    ("sellerRmTeam", "seller.team@example.test"))),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var row = Assert.Single(fixture.DbContext.NotificationOutbox);
        Assert.Equal(SqmsKeys.Routes.OpportunityPendingSellerApproval, row.NotificationType);
        Assert.Equal(
            "Q1 2030 Secondary Sale Bids - Vehicle One/OA-1 - Opportunity One: Response Required by 01/21/2030",
            row.Subject);
        Assert.Equal(
            string.Join(
                "\r\n",
                "Please click the following link to review secondary sale bids for the following opportunity.",
                "",
                "Account Number: OA-1",
                "Account Name: Opportunity One",
                "Name of Fund: Fund One",
                "Name of Vehicle: Vehicle One",
                "Total Amount Requested to Be Sold: $1,250.00",
                "",
                "Link to approve or reject bids received: https://dashboard.example.test/bids?view=seller&opportunityId=10000"),
            row.Body);
        Assert.Contains(">Click Here</a>", row.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("relationship.manager@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        Assert.Contains("seller.team@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        SqmsKeys.BidActions.Accept,
        SqmsKeys.Routes.BidAccepted,
        SqmsKeys.BidStates.Accepted,
        "The bid placed for the following opportunity has been accepted by the Seller for an effective date of 03/31/2030.",
        true)]
    [InlineData(
        SqmsKeys.BidActions.PartiallyAccept,
        SqmsKeys.Routes.BidPartiallyAccepted,
        SqmsKeys.BidStates.PartiallyAccepted,
        "The bid placed for the following opportunity has been partially accepted by the Seller for an effective date of 03/31/2030.",
        true)]
    [InlineData(
        SqmsKeys.BidActions.Reject,
        SqmsKeys.Routes.BidNotAccepted,
        SqmsKeys.BidStates.NotAccepted,
        "The bid placed for the following opportunity has not been accepted by the Seller for an effective date of 03/31/2030.",
        false)]
    public async Task BidDispositionEmailsUseDistinctRoutesAndTrustedBidAndParentData(
        string actionKey,
        string routeKey,
        string expectedState,
        string expectedOpening,
        bool expectFollowUp)
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync($"bid_email_{actionKey}");
        Assert.True((await fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.MoveToIpgReview, 2, SystemActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.SendToSellerReview, 3, IpgActor), CancellationToken.None)).Success);

        var result = await fixture.Service.ExecuteBidActionAsync(
            Request(
                10000,
                actionKey,
                4,
                SellerActor,
                RecipientInputs(
                    ("relationshipManager", "buyer.manager@example.test"),
                    ("buyerRmTeam", "buyer.team@example.test"))),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedState, result.Context?.WorkflowContext.Workflow.CurrentState);
        var row = Assert.Single(fixture.DbContext.NotificationOutbox);
        Assert.Equal(routeKey, row.NotificationType);
        Assert.Equal("Q1 2030 Secondary Sale Bids - Vehicle One/BA-1 - Bid One", row.Subject);
        Assert.StartsWith(expectedOpening + "\r\n", row.Body, StringComparison.Ordinal);
        Assert.Contains("Buyer Account Number: BA-1", row.Body, StringComparison.Ordinal);
        Assert.Contains("Buyer Account Name: Bid One", row.Body, StringComparison.Ordinal);
        Assert.Contains("Name of Fund: Fund One", row.Body, StringComparison.Ordinal);
        Assert.Contains("Name of Vehicle: Vehicle One", row.Body, StringComparison.Ordinal);
        Assert.Contains("Total Amount Requested to Be Purchased: $500.00", row.Body, StringComparison.Ordinal);
        Assert.EndsWith(
            "Link to View: https://forms.example.test/bid?mode=edit&bidId=10000",
            row.Body,
            StringComparison.Ordinal);
        Assert.Equal(expectFollowUp, row.Body.Contains("Please confirm your buyer", StringComparison.Ordinal));
        Assert.Equal(
            expectFollowUp,
            row.Body.Contains("documentation required to effect this sale", StringComparison.Ordinal));
        Assert.Contains(">Click Here</a>", row.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("buyer.manager@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
        Assert.Contains("buyer.team@example.test", row.NotificationRecipientsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingEffectiveDateFailsSellerApprovalWithoutAnyActionSideEffects()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("seller_missing_date");
        await fixture.DbContext.Database.ExecuteSqlRawAsync(
            "UPDATE [SSQMS].[Opportunity] SET [EffectiveDateForSale] = NULL WHERE [ID] = 10000");
        Assert.True((await fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.MarkPending, 2, IpgActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.OpenForBidding, 3, IpgActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteOpportunityActionAsync(
            Request(10000, SqmsKeys.OpportunityActions.CloseBidding, 4, SystemActor), CancellationToken.None)).Success);
        var auditCount = await fixture.DbContext.WorkflowAudits.CountAsync();
        var targetBefore = await fixture.TargetRowSnapshotAsync("Opportunity", 10000);

        var result = await fixture.Service.ExecuteOpportunityActionAsync(
            Request(
                10000,
                SqmsKeys.OpportunityActions.RequestSellerApproval,
                5,
                IpgActor,
                RecipientInputs(
                    ("relationshipManager", "relationship.manager@example.test"),
                    ("sellerRmTeam", "seller.team@example.test"))),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ResultCode);
        Assert.Empty(fixture.DbContext.NotificationOutbox);
        Assert.Equal(auditCount, await fixture.DbContext.WorkflowAudits.CountAsync());
        Assert.Equal(targetBefore, await fixture.TargetRowSnapshotAsync("Opportunity", 10000));
    }

    [Theory]
    [InlineData("missingAmount")]
    [InlineData("missingParent")]
    [InlineData("unresolvedRecipients")]
    [InlineData("invalidUrl")]
    public async Task BidDispositionEnrichmentFailureRollsBackAllActionSideEffects(string scenario)
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync(
            $"bid_atomic_{scenario}",
            invalidBidUrl: scenario == "invalidUrl");
        if (scenario == "missingAmount")
        {
            await fixture.DbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [SSQMS].[Bid] SET [CommitmentAmountRequested] = NULL WHERE [ID] = 10000");
        }
        else if (scenario == "missingParent")
        {
            await fixture.DbContext.Database.ExecuteSqlRawAsync(
                "UPDATE [SSQMS].[Opportunity] SET [IsDeleted] = 1 WHERE [ID] = 10000");
        }

        Assert.True((await fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.MoveToIpgReview, 2, SystemActor), CancellationToken.None)).Success);
        Assert.True((await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.SendToSellerReview, 3, IpgActor), CancellationToken.None)).Success);
        var auditCount = await fixture.DbContext.WorkflowAudits.CountAsync();
        var idempotencyCount = await fixture.DbContext.IdempotencyRecords.CountAsync();
        var recipients = scenario == "unresolvedRecipients"
            ? new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            : RecipientInputs(
                ("relationshipManager", "buyer.manager@example.test"),
                ("buyerRmTeam", "buyer.team@example.test"));

        var targetBefore = await fixture.TargetRowSnapshotAsync("Bid", 10000);
        var result = await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.Accept, 4, SellerActor, recipients),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ValidationFailed", result.ResultCode);
        fixture.DbContext.ChangeTracker.Clear();
        var instance = await fixture.DbContext.WorkflowInstances.SingleAsync(row =>
            row.WorkflowInstanceId == SqmsKeys.BidWorkflowInstanceId(10000));
        Assert.Equal(SqmsKeys.BidStates.UnderReviewBySeller, instance.CurrentStateKey);
        Assert.Equal(4, instance.Version);
        Assert.Equal(auditCount, await fixture.DbContext.WorkflowAudits.CountAsync());
        Assert.Equal(idempotencyCount, await fixture.DbContext.IdempotencyRecords.CountAsync());
        Assert.Empty(fixture.DbContext.NotificationOutbox);
        Assert.Equal(targetBefore, await fixture.TargetRowSnapshotAsync("Bid", 10000));
    }

    private static SqmsExecuteRequest Request(
        int entityId,
        string actionKey,
        int expectedVersion,
        SqmsActor actor,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>>? recipientInputs = null) =>
        new(
            entityId,
            actionKey,
            expectedVersion,
            actor,
            recipientInputs ?? new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal));

    private static Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInputs(
        params (string Key, string Address)[] values) =>
        values.ToDictionary(
            value => value.Key,
            value => (IReadOnlyList<WorkflowRecipientInput>)[new(value.Key, value.Address, value.Key)],
            StringComparer.Ordinal);

    private sealed class SqmsServiceFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly IServiceScope scope;

        private SqmsServiceFixture(ServiceProvider provider, IServiceScope scope)
        {
            this.provider = provider;
            this.scope = scope;
        }

        public WorkflowDbContext DbContext => scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();

        public ISqmsWorkflowService Service => scope.ServiceProvider.GetRequiredService<ISqmsWorkflowService>();

        public static async Task<SqmsServiceFixture> CreateAsync(
            string suffix,
            bool failProjection = false,
            bool invalidBidUrl = false)
        {
            var databaseName = $"workflowengine_sqms_service_{suffix}_tests";
            var connectionString = TestSqlConnectionFactory.ConnectionString(databaseName);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Notifications:Sender"] = "NoOp",
                    ["Sqms:Enabled"] = "true",
                    ["Sqms:Links:OpenBiddingDashboardUrl"] = "https://dashboard.example.test/open",
                    ["Sqms:Links:SellerApprovalBidsDashboardUrl"] = "https://dashboard.example.test/bids?view=seller",
                    ["Sqms:Links:BidFormUrl"] = invalidBidUrl
                        ? "not-a-url"
                        : "https://forms.example.test/bid?mode=edit",
                    ["Sqms:Scheduling:TimeZoneId"] = "Eastern Standard Time",
                    ["Sqms:Scheduling:SellerResponseDays"] = "5"
                })
                .Build();
            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .AddSingleton<TimeProvider>(new FixedTimeProvider(DateTimeOffset.Parse("2030-01-16T12:00:00Z")))
                .AddScoped<WorkflowActionEnvelopeNormalizer>()
                .AddScoped<CanonicalRequestHasher>()
                .AddScoped<WorkflowActionResultBuilder>()
                .AddScoped<CanonicalWorkflowResultSerializer>()
                .AddWorkflowInfrastructure(configuration)
                .AddSqmsWorkflowIntegration(configuration);
            if (failProjection)
            {
                services.AddScoped<ISqmsDataStore, ProjectionFailingSqmsDataStore>();
            }

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var fixture = new SqmsServiceFixture(provider, scope);
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
                    ["SqmsOpenCcRecipientsJson"] = "[]",
                    ["SqmsSellerApprovalCcRecipientsJson"] = "[]",
                    ["SqmsBidDispositionCcRecipientsJson"] = "[]"
                });
            await fixture.InsertTargetRowsAsync();
            return fixture;
        }

        public async Task<string> TargetRowSnapshotAsync(string tableName, int id)
        {
            var query = tableName switch
            {
                "Opportunity" => DbContext.Database.SqlQueryRaw<string>(
                    "SELECT (SELECT * FROM [SSQMS].[Opportunity] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
                    id),
                "Bid" => DbContext.Database.SqlQueryRaw<string>(
                    "SELECT (SELECT * FROM [SSQMS].[Bid] WHERE [ID] = {0} FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) AS [Value]",
                    id),
                _ => throw new ArgumentOutOfRangeException(nameof(tableName))
            };
            return await query.SingleAsync();
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
                     [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (10000, 'Seller Team', 'OPP-1', 'Opportunity One', 'OA-1', 'Relationship Manager',
                     'Fund One', 'Vehicle One', '$1,250.00', '2030-03-31',
                     'Draft', 'Draft', 'mfo-user', '2030-01-16', 0);
                SET IDENTITY_INSERT [SSQMS].[Opportunity] OFF;
                SET IDENTITY_INSERT [SSQMS].[Bid] ON;
                INSERT INTO [SSQMS].[Bid]
                    ([ID], [OpportunityId], [BuyerRmteam], [BidForFullCommitment], [AccountName], [AccountNumber],
                     [RelationshipManager], [CommitmentAmountRequested], [BidExpressedAsFairValue],
                     [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (10000, 10000, 'Buyer Team', 'Yes', 'Bid One', 'BA-1',
                     'Buyer Relationship Manager', '$500.00', 'Fair Value',
                     'Draft', 'Draft', 'mfo-user', '2030-01-16', 0);
                SET IDENTITY_INSERT [SSQMS].[Bid] OFF;
                """);
        }
    }

    private sealed class ProjectionFailingSqmsDataStore(WorkflowDbContext dbContext) : ISqmsDataStore
    {
        private readonly SqlSqmsDataStore inner = new(dbContext);

        public Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken) =>
            inner.GetOpportunityAsync(id, cancellationToken);

        public Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken) =>
            inner.GetBidAsync(id, cancellationToken);

        public Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(CancellationToken cancellationToken) =>
            inner.GetOpenOpportunitiesAsync(cancellationToken);

        public Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken cancellationToken) =>
            inner.GetClosedOpportunitiesWithSubmittedBidsAsync(cancellationToken);

        public Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(int opportunityId, CancellationToken cancellationToken) =>
            inner.GetSubmittedBidsAsync(opportunityId, cancellationToken);

        public Task ProjectOpportunityStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("forced projection failure");

        public Task ProjectBidStateAsync(int id, string state, string modifiedBy, DateTime modifiedAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("forced projection failure");
    }
}
