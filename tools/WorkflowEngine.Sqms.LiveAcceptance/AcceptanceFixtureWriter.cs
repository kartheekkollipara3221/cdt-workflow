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
}
