using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Sqms.Persistence;

public sealed class SqlSqmsDataStore(WorkflowDbContext dbContext) : ISqmsDataStore
{
    private const string OpportunityColumns = """
        opportunity.[ID], opportunity.[SellerRMTeam], opportunity.[AccountName], opportunity.[AccountNumber],
        opportunity.[RelationshipManager], opportunity.[FundConsideredSecondarySale], opportunity.[Vehicle],
        opportunity.[CommitmentAmountSale], opportunity.[EffectiveDateForSale], opportunity.[Status],
        opportunity.[WorkflowState]
        """;

    private const string BidColumns = """
        bid.[ID], bid.[OpportunityId], bid.[BuyerRmteam], bid.[AccountName], bid.[AccountNumber],
        bid.[RelationshipManager], bid.[CommitmentAmountRequested], bid.[BidExpressedAsFairValue],
        bid.[Status], bid.[WorkflowState], bid.[BidFairValue]
        """;

    private readonly WorkflowDbContext dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<SqmsOpportunityRow?> GetOpportunityAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id, nameof(id));
        var rows = await QueryOpportunitiesAsync(
            $"""
            SELECT {OpportunityColumns}
            FROM [SSQMS].[Opportunity] AS opportunity
            WHERE opportunity.[ID] = @id
              AND (opportunity.[IsDeleted] IS NULL OR opportunity.[IsDeleted] = 0);
            """,
            [Parameter("@id", DbType.Int32, id)],
            cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<SqmsBidRow?> GetBidAsync(int id, CancellationToken cancellationToken)
    {
        ValidateId(id, nameof(id));
        var rows = await QueryBidsAsync(
            $"""
            SELECT {BidColumns}
            FROM [SSQMS].[Bid] AS bid
            WHERE bid.[ID] = @id
              AND (bid.[IsDeleted] IS NULL OR bid.[IsDeleted] = 0);
            """,
            [Parameter("@id", DbType.Int32, id)],
            cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<IReadOnlyList<SqmsOpenOpportunityRow>> GetOpenOpportunitiesAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await OpenIfNeededAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(
                connection,
                $"""
                SELECT {OpportunityColumns}, openingAudit.[CreatedAtUtc]
                FROM [SSQMS].[Opportunity] AS opportunity
                INNER JOIN [dbo].[WorkflowAudit] AS openingAudit
                    ON openingAudit.[WorkflowInstanceId] = CONCAT(N'WF-SQMS-OPP-', opportunity.[ID])
                   AND openingAudit.[ActionKey] = @actionKey
                WHERE opportunity.[WorkflowState] = @state
                  AND (opportunity.[IsDeleted] IS NULL OR opportunity.[IsDeleted] = 0)
                ORDER BY opportunity.[ID];
                """,
                [
                    Parameter("@actionKey", DbType.String, SqmsKeys.OpportunityActions.OpenForBidding, 100),
                    Parameter("@state", DbType.String, SqmsKeys.OpportunityStates.OpenToBidding, 100)
                ]);
            var rows = new List<SqmsOpenOpportunityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new SqmsOpenOpportunityRow(
                    ReadOpportunity(reader),
                    reader.GetFieldValue<DateTimeOffset>(11)));
            }

            return rows;
        }
        finally
        {
            await CloseIfOpenedHereAsync(connection, openedHere);
        }
    }

    public Task<IReadOnlyList<SqmsOpportunityRow>> GetClosedOpportunitiesWithSubmittedBidsAsync(
        CancellationToken cancellationToken) =>
        QueryOpportunitiesAsync(
            $"""
            SELECT {OpportunityColumns}
            FROM [SSQMS].[Opportunity] AS opportunity
            WHERE opportunity.[WorkflowState] = @opportunityState
              AND (opportunity.[IsDeleted] IS NULL OR opportunity.[IsDeleted] = 0)
              AND EXISTS
              (
                  SELECT 1
                  FROM [SSQMS].[Bid] AS submittedBid
                  WHERE submittedBid.[OpportunityId] = opportunity.[ID]
                    AND submittedBid.[WorkflowState] = @bidState
                    AND (submittedBid.[IsDeleted] IS NULL OR submittedBid.[IsDeleted] = 0)
              )
            ORDER BY opportunity.[ID];
            """,
            [
                Parameter("@opportunityState", DbType.String, SqmsKeys.OpportunityStates.ClosedToBidding, 100),
                Parameter("@bidState", DbType.String, SqmsKeys.BidStates.Submitted, 100)
            ],
            cancellationToken);

    public Task<IReadOnlyList<SqmsBidRow>> GetSubmittedBidsAsync(
        int opportunityId,
        CancellationToken cancellationToken)
    {
        ValidateId(opportunityId, nameof(opportunityId));
        return QueryBidsAsync(
            $"""
            SELECT {BidColumns}
            FROM [SSQMS].[Bid] AS bid
            WHERE bid.[OpportunityId] = @opportunityId
              AND bid.[WorkflowState] = @state
              AND (bid.[IsDeleted] IS NULL OR bid.[IsDeleted] = 0)
            ORDER BY bid.[ID];
            """,
            [
                Parameter("@opportunityId", DbType.Int32, opportunityId),
                Parameter("@state", DbType.String, SqmsKeys.BidStates.Submitted, 100)
            ],
            cancellationToken);
    }

    public Task ProjectOpportunityStateAsync(
        int id,
        string state,
        string modifiedBy,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken) =>
        ProjectStateAsync(
            "Opportunity",
            id,
            state,
            modifiedBy,
            modifiedAtUtc,
            cancellationToken);

    public Task ProjectBidStateAsync(
        int id,
        string state,
        string modifiedBy,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken) =>
        ProjectStateAsync("Bid", id, state, modifiedBy, modifiedAtUtc, cancellationToken);

    private async Task ProjectStateAsync(
        string tableName,
        int id,
        string state,
        string modifiedBy,
        DateTime modifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateId(id, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(modifiedBy);
        if (state.Length > 100)
        {
            throw new ArgumentException("SQMS workflow state cannot exceed 100 characters.", nameof(state));
        }

        if (modifiedBy.Length > 50)
        {
            throw new ArgumentException("SQMS modified actor cannot exceed 50 characters.", nameof(modifiedBy));
        }

        if (modifiedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("SQMS modified timestamp must be UTC.", nameof(modifiedAtUtc));
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await OpenIfNeededAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(
                connection,
                $"""
                UPDATE [SSQMS].[{tableName}]
                SET [Status] = @state,
                    [WorkflowState] = @state,
                    [ModifiedBy] = @modifiedBy,
                    [ModifiedDate] = @modifiedAtUtc
                WHERE [ID] = @id
                  AND ([IsDeleted] IS NULL OR [IsDeleted] = 0);
                """,
                [
                    Parameter("@state", DbType.String, state, 100),
                    Parameter("@modifiedBy", DbType.String, modifiedBy, 50),
                    Parameter("@modifiedAtUtc", DbType.DateTime, modifiedAtUtc),
                    Parameter("@id", DbType.Int32, id)
                ]);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Expected to project exactly one SSQMS {tableName} row for ID {id}, but affected {affected} rows.");
            }
        }
        finally
        {
            await CloseIfOpenedHereAsync(connection, openedHere);
        }
    }

    private async Task<IReadOnlyList<SqmsOpportunityRow>> QueryOpportunitiesAsync(
        string sql,
        IReadOnlyList<CommandParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await OpenIfNeededAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection, sql, parameters);
            var rows = new List<SqmsOpportunityRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadOpportunity(reader));
            }

            return rows;
        }
        finally
        {
            await CloseIfOpenedHereAsync(connection, openedHere);
        }
    }

    private async Task<IReadOnlyList<SqmsBidRow>> QueryBidsAsync(
        string sql,
        IReadOnlyList<CommandParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = await OpenIfNeededAsync(connection, cancellationToken);
        try
        {
            await using var command = CreateCommand(connection, sql, parameters);
            var rows = new List<SqmsBidRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadBid(reader));
            }

            return rows;
        }
        finally
        {
            await CloseIfOpenedHereAsync(connection, openedHere);
        }
    }

    private DbCommand CreateCommand(
        DbConnection connection,
        string sql,
        IReadOnlyList<CommandParameter> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var parameterValue in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterValue.Name;
            parameter.DbType = parameterValue.Type;
            parameter.Value = parameterValue.Value;
            if (parameterValue.Size is not null)
            {
                parameter.Size = parameterValue.Size.Value;
            }

            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static SqmsOpportunityRow ReadOpportunity(DbDataReader reader) =>
        new(
            reader.GetInt32(0),
            NullableString(reader, 1),
            reader.GetString(2),
            reader.GetString(3),
            NullableString(reader, 4),
            NullableString(reader, 5),
            NullableString(reader, 6),
            NullableString(reader, 7),
            NullableDateTime(reader, 8),
            NullableString(reader, 9),
            NullableString(reader, 10));

    private static SqmsBidRow ReadBid(DbDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetInt32(1),
            NullableString(reader, 2),
            reader.GetString(3),
            reader.GetString(4),
            NullableString(reader, 5),
            NullableString(reader, 6),
            NullableString(reader, 7),
            NullableString(reader, 8),
            NullableString(reader, 9))
        {
            BidFairValue = NullableString(reader, 10)
        };

    private static string? NullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateTime? NullableDateTime(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);

    private static async Task<bool> OpenIfNeededAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (connection.State == ConnectionState.Open)
        {
            return false;
        }

        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private static Task CloseIfOpenedHereAsync(DbConnection connection, bool openedHere) =>
        openedHere ? connection.CloseAsync() : Task.CompletedTask;

    private static CommandParameter Parameter(string name, DbType type, object value, int? size = null) =>
        new(name, type, value, size);

    private static void ValidateId(int id, string parameterName) =>
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1, parameterName);

    private sealed record CommandParameter(string Name, DbType Type, object Value, int? Size);
}
