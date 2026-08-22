using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed class AcceptanceFixtureWriter(WorkflowDbContext dbContext)
{
    public async Task<AcceptanceManifest> CreateAsync(
        AcceptanceScenarioMatrix matrix,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await EnsureMarkerIsUnusedAsync(connection, transaction, cancellationToken);
            await EnsureProjectionEvidenceTableAsync(connection, transaction, cancellationToken);

            var opportunityIds = new Dictionary<string, int>(StringComparer.Ordinal);
            var bidIds = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var opportunity in matrix.Opportunities)
            {
                var opportunityId = await InsertOpportunityAsync(
                    connection,
                    transaction,
                    opportunity,
                    cancellationToken);
                opportunityIds.Add(opportunity.Key, opportunityId);

                foreach (var bid in opportunity.Bids)
                {
                    var bidId = await InsertBidAsync(
                        connection,
                        transaction,
                        opportunity,
                        opportunityId,
                        bid,
                        cancellationToken);
                    bidIds.Add(bid.Key, bidId);
                }
            }

            if (opportunityIds.Count != AcceptanceConstants.Definition.ExpectedOpportunityCount ||
                bidIds.Count != AcceptanceConstants.Definition.ExpectedBidCount)
            {
                throw new InvalidOperationException(
                    $"Fixture count mismatch: {opportunityIds.Count} Opportunities and {bidIds.Count} Bids.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new AcceptanceManifest(opportunityIds, bidIds);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<string> ReadTargetRowSnapshotAsync(
        string entityType,
        int id,
        CancellationToken cancellationToken)
    {
        var table = TargetTable(entityType);
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT (SELECT * FROM [SSQMS].[{table}] WHERE [ID] = @id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES);";
            AddParameter(command, "@id", DbType.Int32, id);
            return Convert.ToString(
                    await command.ExecuteScalarAsync(cancellationToken),
                    System.Globalization.CultureInfo.InvariantCulture)
                ?? throw new InvalidOperationException($"Target {entityType} {id} was not found.");
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task PersistReturnedStateAsTargetOwnerAsync(
        string entityType,
        int id,
        string state,
        string modifiedBy,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        var sql = entityType switch
        {
            "Opportunity" =>
                "UPDATE [SSQMS].[Opportunity] SET [Status] = {0}, [WorkflowState] = {0}, [ModifiedBy] = {1}, [ModifiedDate] = {2} WHERE [ID] = {3}",
            "Bid" =>
                "UPDATE [SSQMS].[Bid] SET [Status] = {0}, [WorkflowState] = {0}, [ModifiedBy] = {1}, [ModifiedDate] = {2} WHERE [ID] = {3}",
            _ => throw new ArgumentOutOfRangeException(nameof(entityType))
        };
        _ = await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            [state, modifiedBy, modifiedAtUtc, id],
            cancellationToken);
    }

    public async Task RecordProjectionOwnershipEvidenceAsync(
        ProjectionOwnershipEvidence evidence,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO [dbo].[SqmsProjectionOwnershipEvidence]
                ([EvidenceKind], [EntityType], [TargetId], [Operation], [ReturnedState],
                 [ReturnedVersion], [BeforeJson], [AfterJson], [Unchanged], [RecordedAtUtc])
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, SYSUTCDATETIME())
            """,
            [
                evidence.EvidenceKind,
                evidence.EntityType,
                evidence.TargetId,
                evidence.Operation,
                evidence.ReturnedState,
                evidence.ReturnedVersion,
                evidence.BeforeJson,
                evidence.AfterJson,
                evidence.Unchanged
            ],
            cancellationToken);
    }

    private static async Task EnsureProjectionEvidenceTableAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF OBJECT_ID(N'[dbo].[SqmsProjectionOwnershipEvidence]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SqmsProjectionOwnershipEvidence]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [EvidenceKind] nvarchar(40) NOT NULL,
                    [EntityType] nvarchar(20) NOT NULL,
                    [TargetId] int NOT NULL,
                    [Operation] nvarchar(100) NOT NULL,
                    [ReturnedState] nvarchar(100) NOT NULL,
                    [ReturnedVersion] int NOT NULL,
                    [BeforeJson] nvarchar(max) NOT NULL,
                    [AfterJson] nvarchar(max) NOT NULL,
                    [Unchanged] bit NOT NULL,
                    [RecordedAtUtc] datetime2 NOT NULL
                );
            END;
            """;
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureMarkerIsUnusedAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM [SSQMS].[Opportunity]
                 WHERE [Account] LIKE @markerPrefix OR [AccountNumber] LIKE @markerPrefix)
              + (SELECT COUNT(*) FROM [SSQMS].[Bid]
                 WHERE [AccountName] LIKE @markerPrefix OR [AccountNumber] LIKE @markerPrefix);
            """;
        AddParameter(command, "@markerPrefix", DbType.String, AcceptanceConstants.RunMarker + "%");
        var count = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (count != 0)
        {
            throw new InvalidOperationException(
                $"Acceptance marker '{AcceptanceConstants.RunMarker}' already exists in {count} target rows.");
        }
    }

    private static async Task<int> InsertOpportunityAsync(
        DbConnection connection,
        DbTransaction transaction,
        OpportunityScenario scenario,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [SSQMS].[Opportunity]
                ([SellerRMTeam], [Account], [AccountName], [AccountNumber], [RelationshipManager],
                 [FundConsideredSecondarySale], [Vehicle], [ClientCommitmentToVehicle],
                 [CommitmentAmountSale], [ClientRelationshipLeavingBBH], [RationaleForSecondarySale],
                 [EffectiveDateForSale], [Status], [FundManagementFee], [MinimumBid],
                 [RecentDateOfValuation], [ValuationAsRecentDate], [ContributionSinceValuation],
                 [DistributionSinceValuation], [EstimatedFairValue], [RemainingCapitalCommitment],
                 [IsValuationForPrivateFundVehicle], [TotalBid], [LargestBid], [LowestBid],
                 [WorkflowState], [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted])
            VALUES
                (@sellerRmTeam, @account, @accountName, @accountNumber, @relationshipManager,
                 @fund, @vehicle, 'Yes', '$100.00', 'No', 'SQMS live acceptance',
                 @effectiveDate, 'Draft', '1.00%', '$1.00', @valuationDate, '$100.00', '$0.00',
                 '$0.00', '$100.00', '$0.00', 1, @totalBid, @largestBid, @lowestBid,
                 'Draft', @createdBy, @createdDate, NULL, NULL, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;
        var lowerKey = scenario.Key.ToLowerInvariant();
        AddParameter(command, "@sellerRmTeam", DbType.String, $"seller-{lowerKey}-team");
        AddParameter(command, "@account", DbType.String, scenario.Account);
        AddParameter(command, "@accountName", DbType.String, $"SQMS Live Acceptance Opportunity {scenario.Key}");
        AddParameter(command, "@accountNumber", DbType.String, $"{AcceptanceConstants.RunMarker}-{scenario.Key}");
        AddParameter(command, "@relationshipManager", DbType.String, $"rm-{lowerKey}");
        AddParameter(command, "@fund", DbType.String, $"SQMS Live Fund {scenario.Key}");
        AddParameter(command, "@vehicle", DbType.String, $"SQMS Live Vehicle {scenario.Key}");
        AddParameter(command, "@effectiveDate", DbType.DateTime, new DateTime(2030, 3, 31));
        AddParameter(command, "@valuationDate", DbType.DateTime, new DateTime(2029, 12, 31));
        AddParameter(command, "@totalBid", DbType.Int32, scenario.TotalBidSentinel);
        AddParameter(command, "@largestBid", DbType.String, scenario.LargestBidSentinel);
        AddParameter(command, "@lowestBid", DbType.String, scenario.LowestBidSentinel);
        AddParameter(command, "@createdBy", DbType.String, AcceptanceConstants.Mfo.UserId);
        AddParameter(command, "@createdDate", DbType.DateTime, AcceptanceConstants.OpeningUtc.UtcDateTime);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> InsertBidAsync(
        DbConnection connection,
        DbTransaction transaction,
        OpportunityScenario opportunity,
        int opportunityId,
        BidScenario bid,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [SSQMS].[Bid]
                ([OpportunityId], [BuyerRmteam], [BidForFullCommitment], [CommitmentAmountRequested],
                 [AccountName], [AccountNumber], [RelationshipManager], [BBHAffiliateAccount],
                 [BidFairValue], [BidExpressedAsFairValue], [Status], [WorkflowState],
                 [CreatedBy], [CreatedDate], [ModifiedBy], [ModifiedDate], [IsDeleted])
            VALUES
                (@opportunityId, @buyerRmTeam, 'No', @requestedAmount,
                 @accountName, @accountNumber, @relationshipManager, 'No',
                 @bidFairValue, @category, 'Draft', 'Draft',
                 @createdBy, @createdDate, NULL, NULL, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;
        AddParameter(command, "@opportunityId", DbType.Int32, opportunityId);
        AddParameter(command, "@buyerRmTeam", DbType.String, $"buyer-team-{bid.Key}");
        AddParameter(command, "@requestedAmount", DbType.String, $"${bid.RequestedAmount:0.00}");
        AddParameter(command, "@accountName", DbType.String, $"{AcceptanceConstants.RunMarker} Bid {bid.Key}");
        AddParameter(command, "@accountNumber", DbType.String, $"{AcceptanceConstants.RunMarker}-BID-{bid.Key}");
        AddParameter(command, "@relationshipManager", DbType.String, $"buyer-rm-{bid.Key}");
        AddParameter(command, "@bidFairValue", DbType.String, $"${bid.RequestedAmount:0.00}");
        AddParameter(command, "@category", DbType.String, bid.Category);
        AddParameter(command, "@createdBy", DbType.String, AcceptanceConstants.Mfo.UserId);
        AddParameter(command, "@createdDate", DbType.DateTime, AcceptanceConstants.OpeningUtc.UtcDateTime);
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string TargetTable(string entityType) => entityType switch
    {
        "Opportunity" => "Opportunity",
        "Bid" => "Bid",
        _ => throw new ArgumentOutOfRangeException(nameof(entityType))
    };
}
