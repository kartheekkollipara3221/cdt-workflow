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
    public async Task StartOpportunityAndBidCommitDirectlyToSubmittedWithoutTasksOrSla()
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync("start_submit");

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
        Assert.Equal(
            "SUBMITTED|SUBMITTED|mfo-user",
            await fixture.TargetStateAsync("Opportunity", 10000));
        Assert.Equal(
            "SUBMITTED|SUBMITTED|mfo-user",
            await fixture.TargetStateAsync("Bid", 10000));
    }

    [Theory]
    [InlineData("Opportunity")]
    [InlineData("Bid")]
    public async Task ProjectionFailureRollsBackStartRuntimeRows(string entityType)
    {
        await using var fixture = await SqmsServiceFixture.CreateAsync(
            $"projection_failure_{entityType.ToLowerInvariant()}",
            failProjection: true);

        Func<Task<SqmsOperationResult>> action = entityType == "Opportunity"
            ? () => fixture.Service.StartOpportunityAsync(10000, MfoActor, CancellationToken.None)
            : () => fixture.Service.StartBidAsync(10000, MfoActor, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Contains("forced projection failure", exception.Message, StringComparison.Ordinal);
        fixture.DbContext.ChangeTracker.Clear();
        Assert.Empty(fixture.DbContext.WorkflowInstances);
        Assert.Empty(fixture.DbContext.WorkflowAudits);
        Assert.Empty(fixture.DbContext.IdempotencyRecords);
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
        var returnedBid = await fixture.Service.ExecuteBidActionAsync(
            Request(10000, SqmsKeys.BidActions.ReturnToDraft, 2, IpgActor),
            CancellationToken.None);

        Assert.Equal("Forbidden", forbiddenBid.ResultCode);
        Assert.Equal("ConcurrencyConflict", staleBid.ResultCode);
        Assert.True(returnedBid.Success, returnedBid.Message);
        Assert.Equal("Draft", returnedBid.Context?.WorkflowContext.Workflow.CurrentState);
        Assert.Equal(3, returnedBid.Context?.WorkflowContext.Workflow.WorkflowInstanceVersion);
        Assert.Equal("Draft|Draft|ipg-user", await fixture.TargetStateAsync("Opportunity", 10000));
        Assert.Equal("Draft|Draft|ipg-user", await fixture.TargetStateAsync("Bid", 10000));
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

    private static SqmsExecuteRequest Request(
        int entityId,
        string actionKey,
        int expectedVersion,
        SqmsActor actor) =>
        new(
            entityId,
            actionKey,
            expectedVersion,
            actor,
            new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal));

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

        public static async Task<SqmsServiceFixture> CreateAsync(string suffix, bool failProjection = false)
        {
            var databaseName = $"workflowengine_sqms_service_{suffix}_tests";
            var connectionString = TestSqlConnectionFactory.ConnectionString(databaseName);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Notifications:Sender"] = "NoOp"
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

        public async Task<string?> TargetStateAsync(string tableName, int id)
        {
            var query = tableName switch
            {
                "Opportunity" => DbContext.Database.SqlQueryRaw<string>(
                    "SELECT CONCAT([Status], '|', [WorkflowState], '|', [ModifiedBy]) AS [Value] FROM [SSQMS].[Opportunity] WHERE [ID] = {0}",
                    id),
                "Bid" => DbContext.Database.SqlQueryRaw<string>(
                    "SELECT CONCAT([Status], '|', [WorkflowState], '|', [ModifiedBy]) AS [Value] FROM [SSQMS].[Bid] WHERE [ID] = {0}",
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
                    ([ID], [SellerRMTeam], [Account], [AccountName], [AccountNumber], [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (10000, 'Seller Team', 'OPP-1', 'Opportunity One', 'OA-1', 'Draft', 'Draft', 'mfo-user', '2030-01-16', 0);
                SET IDENTITY_INSERT [SSQMS].[Opportunity] OFF;
                SET IDENTITY_INSERT [SSQMS].[Bid] ON;
                INSERT INTO [SSQMS].[Bid]
                    ([ID], [OpportunityId], [BuyerRmteam], [BidForFullCommitment], [AccountName], [AccountNumber], [Status], [WorkflowState], [CreatedBy], [CreatedDate], [IsDeleted])
                VALUES
                    (10000, 10000, 'Buyer Team', 'Yes', 'Bid One', 'BA-1', 'Draft', 'Draft', 'mfo-user', '2030-01-16', 0);
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
