using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Sqms;
using WorkflowEngine.Sqms.Persistence;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SqmsDataStoreTests
{
    [Fact]
    public void ApprovedDataStoreSurfaceExcludesCalculatedBidSummaryColumns()
    {
        var publicSurface = string.Join(
            Environment.NewLine,
            typeof(ISqmsDataStore).GetMethods().Select(method => method.ToString()));

        Assert.DoesNotContain("TotalBid", publicSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("LargestBid", publicSurface, StringComparison.Ordinal);
        Assert.DoesNotContain("LowestBid", publicSurface, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SqlStoreReadsApprovedRowsFiltersDeletedRecordsAndProjectsOnlyWorkflowState()
    {
        const string databaseName = "workflowengine_sqms_data_store_tests";
        var connectionString = TestSqlConnectionFactory.ConnectionString(databaseName);
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new WorkflowDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        try
        {
            await dbContext.Database.MigrateAsync();
            var root = SqmsDatabasePackageTests.RepositoryRoot();
            await SqmsDatabasePackageTests.ExecuteScriptAsync(
                connectionString,
                Path.Combine(root, "sql", "sqms", "testing", "000_create_sqms_test_tables.sql"));
            await SqmsDatabasePackageTests.ExecuteScriptAsync(
                connectionString,
                Path.Combine(root, "sql", "sqms", "002_sqms_workflow_metadata_dml.sql"),
                RecipientVariables());
            await InsertFixtureAsync(connectionString);

            var store = new SqlSqmsDataStore(dbContext);

            var opportunity = await store.GetOpportunityAsync(10000, CancellationToken.None);
            Assert.NotNull(opportunity);
            Assert.Equal("Seller Team", opportunity.SellerRmTeam);
            Assert.Equal("Open Account", opportunity.AccountName);
            Assert.Null(await store.GetOpportunityAsync(10002, CancellationToken.None));

            var bid = await store.GetBidAsync(10000, CancellationToken.None);
            Assert.NotNull(bid);
            Assert.Equal(10001, bid.OpportunityId);
            Assert.Null(await store.GetBidAsync(10001, CancellationToken.None));

            var open = Assert.Single(await store.GetOpenOpportunitiesAsync(CancellationToken.None));
            Assert.Equal(10000, open.Opportunity.Id);
            Assert.Equal(DateTimeOffset.Parse("2030-01-15T15:30:00Z"), open.OpenedAtUtc);

            var closed = Assert.Single(
                await store.GetClosedOpportunitiesWithSubmittedBidsAsync(CancellationToken.None));
            Assert.Equal(10001, closed.Id);

            var submitted = Assert.Single(await store.GetSubmittedBidsAsync(10001, CancellationToken.None));
            Assert.Equal(10000, submitted.Id);

            var modifiedAtUtc = new DateTime(2030, 1, 16, 12, 0, 0, DateTimeKind.Utc);
            await store.ProjectOpportunityStateAsync(
                10000,
                SqmsKeys.OpportunityStates.ClosedToBidding,
                "system-user",
                modifiedAtUtc,
                CancellationToken.None);
            await store.ProjectBidStateAsync(
                10000,
                SqmsKeys.BidStates.IpgReview,
                "system-user",
                modifiedAtUtc,
                CancellationToken.None);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Assert.Equal(
                "CLOSED TO BIDDING|CLOSED TO BIDDING|system-user|Open Account",
                await ScalarStringAsync(
                    connection,
                    "SELECT CONCAT([Status], '|', [WorkflowState], '|', [ModifiedBy], '|', [AccountName]) FROM [SSQMS].[Opportunity] WHERE [ID] = 10000;"));
            Assert.Equal(
                "IPG REVIEW|IPG REVIEW|system-user|Submitted Bidder",
                await ScalarStringAsync(
                    connection,
                    "SELECT CONCAT([Status], '|', [WorkflowState], '|', [ModifiedBy], '|', [AccountName]) FROM [SSQMS].[Bid] WHERE [ID] = 10000;"));
        }
        finally
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    private static IReadOnlyDictionary<string, string> RecipientVariables() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SqmsOpenToRecipientsJson"] = "[\"sqms-open@example.test\"]",
            ["SqmsOpenCcRecipientsJson"] = "[]",
            ["SqmsSellerApprovalCcRecipientsJson"] = "[]",
            ["SqmsBidDispositionCcRecipientsJson"] = "[]"
        };

    private static async Task InsertFixtureAsync(string connectionString)
    {
        const string sql = """
            SET IDENTITY_INSERT [SSQMS].[Opportunity] ON;
            INSERT INTO [SSQMS].[Opportunity]
                ([ID], [SellerRMTeam], [Account], [AccountName], [AccountNumber], [RelationshipManager],
                 [FundConsideredSecondarySale], [Vehicle], [CommitmentAmountSale], [EffectiveDateForSale],
                 [Status], [WorkflowState], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted])
            VALUES
                (10000, 'Seller Team', 'OPEN', 'Open Account', 'OA-1', 'Open RM', 'Open Fund', 'Open Vehicle', '$1,000.00', '2030-03-31', 'OPEN TO BIDDING', 'OPEN TO BIDDING', 'maker', '2030-01-15', 'ipg', '2030-01-15', 0),
                (10001, 'Seller Team', 'CLOSED', 'Closed Account', 'CA-1', 'Closed RM', 'Closed Fund', 'Closed Vehicle', '$2,000.00', '2030-06-30', 'CLOSED TO BIDDING', 'CLOSED TO BIDDING', 'maker', '2030-01-15', 'ipg', '2030-01-15', NULL),
                (10002, 'Seller Team', 'DELETED', 'Deleted Account', 'DA-1', NULL, NULL, NULL, NULL, NULL, 'OPEN TO BIDDING', 'OPEN TO BIDDING', 'maker', '2030-01-15', 'ipg', '2030-01-15', 1);
            SET IDENTITY_INSERT [SSQMS].[Opportunity] OFF;

            SET IDENTITY_INSERT [SSQMS].[Bid] ON;
            INSERT INTO [SSQMS].[Bid]
                ([ID], [OpportunityId], [BuyerRmteam], [BidForFullCommitment], [CommitmentAmountRequested],
                 [AccountName], [AccountNumber], [RelationshipManager], [BidExpressedAsFairValue],
                 [Status], [WorkflowState], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted])
            VALUES
                (10000, 10001, 'Buyer Team', 'Yes', '$500.00', 'Submitted Bidder', 'BA-1', 'Buyer RM', 'Premium', 'SUBMITTED', 'SUBMITTED', 'bidder', '2030-01-15', 'bidder', '2030-01-15', 0),
                (10001, 10001, 'Buyer Team', 'Yes', '$600.00', 'Deleted Bidder', 'BA-2', NULL, 'Discount', 'SUBMITTED', 'SUBMITTED', 'bidder', '2030-01-15', 'bidder', '2030-01-15', 1);
            SET IDENTITY_INSERT [SSQMS].[Bid] OFF;

            DECLARE @versionId bigint =
            (
                SELECT version.Id
                FROM [dbo].[WorkflowDefinitionVersion] AS version
                INNER JOIN [dbo].[WorkflowDefinition] AS definition
                    ON definition.Id = version.WorkflowDefinitionId
                WHERE definition.WorkflowKey = N'SqmsOpportunity'
                  AND version.VersionNumber = 1
            );
            INSERT INTO [dbo].[WorkflowInstance]
                ([WorkflowInstanceId], [WorkflowKey], [WorkflowDefinitionVersionId], [BusinessObjectType],
                 [BusinessObjectId], [CurrentStateKey], [Status], [StartedAtUtc], [StartedByUserId], [Version])
            VALUES
                (N'WF-SQMS-OPP-10000', N'SqmsOpportunity', @versionId, N'SqmsOpportunity',
                 N'SQMS-OPP-10000', N'OPEN TO BIDDING', N'Active', '2030-01-15T15:00:00Z', N'ipg', 4);
            INSERT INTO [dbo].[WorkflowAudit]
                ([WorkflowInstanceId], [BusinessObjectType], [BusinessObjectId], [ActionKey], [FromStateKey],
                 [ToStateKey], [PerformedByUserId], [PerformedByRoleKey], [CorrelationId], [CreatedAtUtc])
            VALUES
                (N'WF-SQMS-OPP-10000', N'SqmsOpportunity', N'SQMS-OPP-10000', N'OpenOpportunityForBidding',
                 N'PENDING', N'OPEN TO BIDDING', N'ipg', N'IPG', N'open-10000', '2030-01-15T15:30:00Z');
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
