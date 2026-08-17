using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.SqmsIntegration;

public sealed class SqmsApplicationRepository(
    WorkflowDbContext dbContext)
{
    internal IReadOnlyList<SqmsSaleCycleCandidate> ReadDueSaleCycles(
        DateTimeOffset now,
        int maxCycles)
    {
        if (maxCycles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCycles));
        }

        using var command = CreateCommand("""
            SELECT TOP (@maxCycles)
                CONVERT(date, [EffectiveDateForSale]),
                [OpenBiddingNoticeAt],
                [OpenBiddingReminderAt],
                [BidResponseDueDate]
            FROM [SSQMS].[Opportunity]
            WHERE ISNULL([IsDeleted], 0) = 0
              AND [WorkflowState] = N'OpenToBidding'
              AND [EffectiveDateForSale] IS NOT NULL
              AND [OpenBiddingNoticeAt] IS NOT NULL
              AND [OpenBiddingReminderAt] IS NOT NULL
              AND [BidResponseDueDate] IS NOT NULL
              AND [OpenBiddingNoticeAt] <= @now
            GROUP BY CONVERT(date, [EffectiveDateForSale]),
                [OpenBiddingNoticeAt], [OpenBiddingReminderAt], [BidResponseDueDate]
            ORDER BY [OpenBiddingNoticeAt], CONVERT(date, [EffectiveDateForSale]), [BidResponseDueDate];
            """);
        command.Parameters.Add(new SqlParameter("@maxCycles", SqlDbType.Int) { Value = maxCycles });
        command.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTimeOffset) { Value = now });

        var result = new List<SqmsSaleCycleCandidate>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SqmsSaleCycleCandidate(
                DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetFieldValue<DateTimeOffset>(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                DateOnly.FromDateTime(reader.GetDateTime(3))));
        }

        return result;
    }

    internal IReadOnlyList<int> ReadOpportunityIdsDueForClose(
        DateTimeOffset now,
        int maxOpportunities)
    {
        return ReadOpportunityIds("""
            SELECT TOP (@maxOpportunities) [ID]
            FROM [SSQMS].[Opportunity]
            WHERE ISNULL([IsDeleted], 0) = 0
              AND [WorkflowState] = N'OpenToBidding'
              AND [BiddingCloseAt] IS NOT NULL
              AND [BiddingCloseAt] <= @now
            ORDER BY [BiddingCloseAt], [ID];
            """, maxOpportunities, now);
    }

    internal IReadOnlyList<int> ReadClosedOpportunityIds(int maxOpportunities)
    {
        return ReadOpportunityIds("""
            SELECT TOP (@maxOpportunities) opportunity.[ID]
            FROM [SSQMS].[Opportunity] AS opportunity
            WHERE ISNULL(opportunity.[IsDeleted], 0) = 0
              AND opportunity.[WorkflowState] = N'ClosedToBidding'
              AND
              (
                  EXISTS
                  (
                      SELECT 1 FROM [SSQMS].[Bid] AS submittedBid
                      WHERE submittedBid.[OpportunityId] = opportunity.[ID]
                        AND ISNULL(submittedBid.[IsDeleted], 0) = 0
                        AND submittedBid.[WorkflowState] = N'Submitted'
                  )
                  OR NOT EXISTS
                  (
                      SELECT 1 FROM [SSQMS].[Bid] AS reviewedBid
                      WHERE reviewedBid.[OpportunityId] = opportunity.[ID]
                        AND ISNULL(reviewedBid.[IsDeleted], 0) = 0
                        AND reviewedBid.[WorkflowState] = N'IpgReview'
                  )
              )
            ORDER BY opportunity.[ID];
            """, maxOpportunities, now: null);
    }

    internal SqmsOpportunityClassificationRow ReadOpportunityForClassification(int opportunityId)
    {
        using var command = CreateCommand("""
            SELECT [ID], [WorkflowState], [CommitmentAmountSale]
            FROM [SSQMS].[Opportunity]
            WHERE [ID] = @opportunityId AND ISNULL([IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"SQMS Opportunity row '{opportunityId}' was not found or is deleted.");
        }

        return new SqmsOpportunityClassificationRow(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    internal IReadOnlyList<SqmsBidClassificationRow> ReadBidsForClassification(int opportunityId)
    {
        using var command = CreateCommand("""
            SELECT [ID], [WorkflowState], [CommitmentAmountRequested], [BidPricingCategory]
            FROM [SSQMS].[Bid]
            WHERE [OpportunityId] = @opportunityId AND ISNULL([IsDeleted], 0) = 0
            ORDER BY [ID];
            """);
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        var result = new List<SqmsBidClassificationRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new SqmsBidClassificationRow(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return result;
    }

    internal SqmsBidSubmissionGate ReadBidSubmissionGate(int bidId)
    {
        using var command = CreateCommand("""
            SELECT bid.[OpportunityId], opportunity.[WorkflowState], opportunity.[BiddingCloseAt],
                CONVERT(date, opportunity.[EffectiveDateForSale]),
                opportunity.[OpenBiddingNoticeAt], opportunity.[OpenBiddingReminderAt],
                opportunity.[BidResponseDueDate], bid.[CommitmentAmountRequested],
                bid.[BidPricingCategory]
            FROM [SSQMS].[Bid] AS bid
            INNER JOIN [SSQMS].[Opportunity] AS opportunity
                ON opportunity.[ID] = bid.[OpportunityId]
            WHERE bid.[ID] = @bidId
              AND ISNULL(bid.[IsDeleted], 0) = 0
              AND ISNULL(opportunity.[IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@bidId", SqlDbType.Int) { Value = bidId });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new SqmsIntegrationException(
                $"SQMS Bid '{bidId}' does not have an active parent Opportunity.");
        }

        return new SqmsBidSubmissionGate(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : DateOnly.FromDateTime(reader.GetDateTime(3)),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : DateOnly.FromDateTime(reader.GetDateTime(6)),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    internal IReadOnlyList<int> ReadIpgReviewBidIds(int opportunityId)
    {
        using var command = CreateCommand("""
            SELECT [ID]
            FROM [SSQMS].[Bid]
            WHERE [OpportunityId] = @opportunityId
              AND ISNULL([IsDeleted], 0) = 0
              AND [WorkflowState] = N'IpgReview'
            ORDER BY [ID];
            """);
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        var result = new List<int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    internal SqmsOpportunityNotificationRow ReadOpportunityNotificationRow(int opportunityId)
    {
        using var command = CreateCommand("""
            SELECT [ID], CONVERT(date, [EffectiveDateForSale]), [SellerResponseDueDate],
                [AccountNumber], [AccountName], [FundConsideredSecondarySale], [Vehicle],
                [CommitmentAmountSale], [OpportunityRelationshipManagerEmail],
                [SellerRmTeamEmailsJson]
            FROM [SSQMS].[Opportunity]
            WHERE [ID] = @opportunityId AND ISNULL([IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"SQMS Opportunity row '{opportunityId}' was not found or is deleted.");
        }

        return new SqmsOpportunityNotificationRow(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : DateOnly.FromDateTime(reader.GetDateTime(1)),
            reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    internal SqmsBidNotificationRow ReadBidNotificationRow(int bidId)
    {
        using var command = CreateCommand("""
            SELECT bid.[ID], bid.[OpportunityId], CONVERT(date, opportunity.[EffectiveDateForSale]),
                bid.[AccountNumber], bid.[AccountName], opportunity.[FundConsideredSecondarySale],
                opportunity.[Vehicle], bid.[CommitmentAmountRequested],
                bid.[BidRelationshipManagerEmail], bid.[BuyerRmTeamEmailsJson]
            FROM [SSQMS].[Bid] AS bid
            INNER JOIN [SSQMS].[Opportunity] AS opportunity
                ON opportunity.[ID] = bid.[OpportunityId]
            WHERE bid.[ID] = @bidId
              AND ISNULL(bid.[IsDeleted], 0) = 0
              AND ISNULL(opportunity.[IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@bidId", SqlDbType.Int) { Value = bidId });
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"SQMS Bid row '{bidId}' was not found or is deleted.");
        }

        return new SqmsBidNotificationRow(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2)),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    internal SqmsBidRollupCounts ReadBidRollupCounts(int opportunityId)
    {
        using var command = CreateCommand("""
            SELECT
                SUM(CASE WHEN [WorkflowState] = N'UnderReviewBySeller' THEN 1 ELSE 0 END),
                SUM(CASE WHEN [WorkflowState] = N'Accepted' THEN 1 ELSE 0 END),
                SUM(CASE WHEN [WorkflowState] = N'PartiallyAccepted' THEN 1 ELSE 0 END),
                SUM(CASE WHEN [WorkflowState] = N'NotAccepted' THEN 1 ELSE 0 END)
            FROM [SSQMS].[Bid]
            WHERE [OpportunityId] = @opportunityId AND ISNULL([IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        using var reader = command.ExecuteReader();
        reader.Read();
        return new SqmsBidRollupCounts(
            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
            reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
            reader.IsDBNull(3) ? 0 : reader.GetInt32(3));
    }

    internal void UpdateOpportunityBidSummary(
        int opportunityId,
        int totalBid,
        string largestBid,
        string lowestBid,
        string modifiedBy,
        DateTime modifiedDateUtc)
    {
        using var command = CreateCommand("""
            UPDATE [SSQMS].[Opportunity]
            SET [TotalBid] = @totalBid,
                [LargestBid] = @largestBid,
                [LowestBid] = @lowestBid,
                [ModifiedBy] = @modifiedBy,
                [ModifiedDate] = @modifiedDateUtc
            WHERE [ID] = @opportunityId
              AND ISNULL([IsDeleted], 0) = 0
              AND [WorkflowState] = N'ClosedToBidding';
            """);
        command.Parameters.Add(new SqlParameter("@totalBid", SqlDbType.Int) { Value = totalBid });
        command.Parameters.Add(new SqlParameter("@largestBid", SqlDbType.VarChar, 100) { Value = largestBid });
        command.Parameters.Add(new SqlParameter("@lowestBid", SqlDbType.VarChar, 100) { Value = lowestBid });
        command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.NVarChar, 50) { Value = modifiedBy });
        command.Parameters.Add(new SqlParameter("@modifiedDateUtc", SqlDbType.DateTime) { Value = modifiedDateUtc });
        command.Parameters.Add(new SqlParameter("@opportunityId", SqlDbType.Int) { Value = opportunityId });
        if (command.ExecuteNonQuery() != 1)
        {
            throw new SqmsIntegrationException(
                $"SQMS Opportunity row '{opportunityId}' changed before its Bid summary was projected.");
        }
    }

    private IReadOnlyList<int> ReadOpportunityIds(
        string sql,
        int maxOpportunities,
        DateTimeOffset? now)
    {
        if (maxOpportunities <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOpportunities));
        }

        using var command = CreateCommand(sql);
        command.Parameters.Add(new SqlParameter("@maxOpportunities", SqlDbType.Int) { Value = maxOpportunities });
        if (now is not null)
        {
            command.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTimeOffset) { Value = now.Value });
        }

        var result = new List<int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    internal SqmsApplicationState ReadState(SqmsEntityKind entityKind, int entityId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entityId);
        var tableName = TableName(entityKind);

        using var command = CreateCommand($"""
            SELECT [WorkflowState], [Status]
            FROM [SSQMS].[{tableName}]
            WHERE [ID] = @entityId AND ISNULL([IsDeleted], 0) = 0;
            """);
        command.Parameters.Add(new SqlParameter("@entityId", SqlDbType.Int) { Value = entityId });

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"SQMS {tableName} row '{entityId}' was not found or is deleted.");
        }

        return new SqmsApplicationState(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    internal void ProjectWorkflowState(
        SqmsEntityKind entityKind,
        int entityId,
        string? expectedWorkflowState,
        string workflowState,
        string status,
        string modifiedBy,
        DateTime modifiedDateUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entityId);
        var tableName = TableName(entityKind);

        using var command = CreateCommand($"""
            UPDATE [SSQMS].[{tableName}]
            SET [WorkflowState] = @workflowState,
                [Status] = @status,
                [ModifiedBy] = @modifiedBy,
                [ModifiedDate] = @modifiedDateUtc
            WHERE [ID] = @entityId
              AND ISNULL([IsDeleted], 0) = 0
              AND (([WorkflowState] = @expectedWorkflowState)
                   OR ([WorkflowState] IS NULL AND @expectedWorkflowState IS NULL));
            """);
        command.Parameters.Add(new SqlParameter("@workflowState", SqlDbType.NVarChar, 100) { Value = workflowState });
        command.Parameters.Add(new SqlParameter("@status", SqlDbType.VarChar, 200) { Value = status });
        command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.NVarChar, 50) { Value = modifiedBy });
        command.Parameters.Add(new SqlParameter("@modifiedDateUtc", SqlDbType.DateTime) { Value = modifiedDateUtc });
        command.Parameters.Add(new SqlParameter("@entityId", SqlDbType.Int) { Value = entityId });
        command.Parameters.Add(new SqlParameter("@expectedWorkflowState", SqlDbType.NVarChar, 100)
        {
            Value = (object?)expectedWorkflowState ?? DBNull.Value
        });

        if (command.ExecuteNonQuery() != 1)
        {
            throw new SqmsIntegrationException(
                $"SQMS {tableName} row '{entityId}' changed while its workflow action was executing.");
        }
    }

    private SqlCommand CreateCommand(string commandText)
    {
        dbContext.Database.OpenConnection();
        var command = (SqlCommand)dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = 30;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;
        return command;
    }

    private static string TableName(SqmsEntityKind entityKind)
    {
        return entityKind switch
        {
            SqmsEntityKind.Opportunity => "Opportunity",
            SqmsEntityKind.Bid => "Bid",
            _ => throw new ArgumentOutOfRangeException(nameof(entityKind), entityKind, "Unknown SQMS entity kind.")
        };
    }
}
