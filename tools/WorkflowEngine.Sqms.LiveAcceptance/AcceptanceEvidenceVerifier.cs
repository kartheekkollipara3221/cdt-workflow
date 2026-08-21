using System.Data;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Sqms.LiveAcceptance;

public sealed partial class AcceptanceEvidenceVerifier(WorkflowDbContext dbContext)
{
    public async Task VerifyAsync(CancellationToken cancellationToken)
    {
        await VerifyDatabaseAsync(cancellationToken);
        await VerifySmtp4DevAsync(cancellationToken);
    }

    private async Task VerifyDatabaseAsync(CancellationToken cancellationToken)
    {
        await RequireCountAsync(
            "Opportunity fixtures",
            "SELECT COUNT(*) FROM [SSQMS].[Opportunity] WHERE [Account] LIKE @marker;",
            3,
            cancellationToken);
        await RequireCountAsync(
            "Bid fixtures",
            "SELECT COUNT(*) FROM [SSQMS].[Bid] WHERE [AccountNumber] LIKE @marker;",
            15,
            cancellationToken);
        await RequireCountAsync(
            "workflow instances",
            "SELECT COUNT(*) FROM [dbo].[WorkflowInstance] WHERE [WorkflowKey] IN (N'SqmsOpportunity', N'SqmsBid');",
            18,
            cancellationToken,
            useMarker: false);
        await RequireCountAsync(
            "SQMS tasks",
            "SELECT COUNT(*) FROM [dbo].[WorkflowTask] WHERE [WorkflowInstanceId] LIKE N'WF-SQMS-%';",
            0,
            cancellationToken,
            useMarker: false);
        await RequireCountAsync(
            "SQMS SLA schedules",
            """
            SELECT COUNT(*)
            FROM [dbo].[WorkflowTaskSlaSchedule] AS schedule
            INNER JOIN [dbo].[WorkflowTask] AS task ON task.[WorkflowTaskId] = schedule.[WorkflowTaskId]
            WHERE task.[WorkflowInstanceId] LIKE N'WF-SQMS-%';
            """,
            0,
            cancellationToken,
            useMarker: false);
        await RequireCountAsync(
            "Sent outbox rows",
            "SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] = N'Sent' AND [RetryCount] = 0;",
            AcceptanceConstants.ExpectedEmailCount,
            cancellationToken,
            useMarker: false);
        await RequireCountAsync(
            "non-Sent or retried outbox rows",
            "SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [Status] <> N'Sent' OR [RetryCount] <> 0;",
            0,
            cancellationToken,
            useMarker: false);
        await RequireCountAsync(
            "target/workflow mismatches",
            """
            SELECT COUNT(*)
            FROM
            (
                SELECT opportunity.[Status], opportunity.[WorkflowState], instance.[CurrentStateKey]
                FROM [SSQMS].[Opportunity] AS opportunity
                INNER JOIN [dbo].[WorkflowInstance] AS instance
                    ON instance.[WorkflowKey] = N'SqmsOpportunity'
                   AND instance.[BusinessObjectId] = CONCAT(N'SQMS-OPP-', opportunity.[ID])
                WHERE opportunity.[Account] LIKE @marker
                UNION ALL
                SELECT bid.[Status], bid.[WorkflowState], instance.[CurrentStateKey]
                FROM [SSQMS].[Bid] AS bid
                INNER JOIN [dbo].[WorkflowInstance] AS instance
                    ON instance.[WorkflowKey] = N'SqmsBid'
                   AND instance.[BusinessObjectId] = CONCAT(N'SQMS-BID-', bid.[ID])
                WHERE bid.[AccountNumber] LIKE @marker
            ) AS states
            WHERE states.[Status] <> states.[CurrentStateKey]
               OR states.[WorkflowState] <> states.[CurrentStateKey];
            """,
            0,
            cancellationToken);
        await RequireCountAsync(
            "changed sentinel rows",
            """
            SELECT COUNT(*) FROM [SSQMS].[Opportunity]
            WHERE [Account] LIKE @marker
              AND ([TotalBid] <> 777 OR [LargestBid] <> 'L-SENTINEL' OR [LowestBid] <> 'S-SENTINEL');
            """,
            0,
            cancellationToken);

        foreach (var route in AcceptanceEmailPlan.Create())
        {
            await RequireCountAsync(
                $"route {route.RouteKey}",
                "SELECT COUNT(*) FROM [dbo].[NotificationOutbox] WHERE [NotificationType] = @route;",
                route.Count,
                cancellationToken,
                useMarker: false,
                route.RouteKey);
        }

        await RequireTargetStateCountAsync("Opportunity", SqmsKeys.OpportunityStates.Completed, 2, cancellationToken);
        await RequireTargetStateCountAsync("Opportunity", SqmsKeys.OpportunityStates.NotFulfilled, 1, cancellationToken);
        await RequireTargetStateCountAsync("Bid", SqmsKeys.BidStates.Accepted, 6, cancellationToken);
        await RequireTargetStateCountAsync("Bid", SqmsKeys.BidStates.PartiallyAccepted, 4, cancellationToken);
        await RequireTargetStateCountAsync("Bid", SqmsKeys.BidStates.NotAccepted, 4, cancellationToken);
        await RequireTargetStateCountAsync("Bid", SqmsKeys.BidStates.NotAllocated, 1, cancellationToken);
    }

    private async Task VerifySmtp4DevAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        using var listResponse = await client.GetAsync("/api/Messages", cancellationToken);
        listResponse.EnsureSuccessStatusCode();
        var listJson = await listResponse.Content.ReadAsStringAsync(cancellationToken);
        using var listDocument = JsonDocument.Parse(listJson);
        var items = MessageArray(listDocument.RootElement).EnumerateArray().ToArray();
        if (items.Length != AcceptanceConstants.ExpectedEmailCount)
        {
            throw new InvalidOperationException(
                $"smtp4dev expected {AcceptanceConstants.ExpectedEmailCount} messages but returned {items.Length}.");
        }

        var details = new List<string>(items.Length);
        foreach (var item in items)
        {
            var id = RequiredString(item, "id");
            using var detailResponse = await client.GetAsync($"/api/Messages/{Uri.EscapeDataString(id)}", cancellationToken);
            detailResponse.EnsureSuccessStatusCode();
            using var detailDocument = JsonDocument.Parse(
                await detailResponse.Content.ReadAsStringAsync(cancellationToken));
            details.Add(WebUtility.HtmlDecode(string.Join('\n', AllStrings(detailDocument.RootElement))));
        }

        var outbox = await ReadOutboxAsync(cancellationToken);
        if (outbox.Count != AcceptanceConstants.ExpectedEmailCount)
        {
            throw new InvalidOperationException($"Expected 19 outbox evidence rows but found {outbox.Count}.");
        }

        foreach (var row in outbox)
        {
            var expectedAddresses = EmailPattern().Matches(row.RecipientsJson)
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var matching = details.Where(detail =>
                    detail.Contains(row.Subject, StringComparison.Ordinal) &&
                    expectedAddresses.All(address => detail.Contains(address, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (matching.Length != 1)
            {
                throw new InvalidOperationException(
                    $"smtp4dev expected one message for outbox {row.Id} '{row.Subject}' but matched {matching.Length}.");
            }

            foreach (var requiredLink in HttpPattern().Matches(row.Body + " " + row.HtmlBody).Select(match => match.Value))
            {
                if (!matching[0].Contains(WebUtility.HtmlDecode(requiredLink), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"smtp4dev message for outbox {row.Id} is missing link '{requiredLink}'.");
                }
            }
        }
    }

    private async Task RequireTargetStateCountAsync(
        string table,
        string state,
        int expected,
        CancellationToken cancellationToken)
    {
        var markerColumn = table == "Opportunity" ? "Account" : "AccountNumber";
        await RequireCountAsync(
            $"{table} state {state}",
            $"SELECT COUNT(*) FROM [SSQMS].[{table}] WHERE [{markerColumn}] LIKE @marker AND [Status] = @state;",
            expected,
            cancellationToken,
            useMarker: true,
            state: state);
    }

    private async Task RequireCountAsync(
        string label,
        string sql,
        int expected,
        CancellationToken cancellationToken,
        bool useMarker = true,
        string? route = null,
        string? state = null)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            if (useMarker)
            {
                AddParameter(command, "@marker", DbType.String, AcceptanceConstants.RunMarker + "%");
            }
            if (route is not null)
            {
                AddParameter(command, "@route", DbType.String, route);
            }
            if (state is not null)
            {
                AddParameter(command, "@state", DbType.String, state);
            }
            var actual = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (actual != expected)
            {
                throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}.");
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyList<OutboxEvidence>> ReadOutboxAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT [Id], [Subject], [NotificationRecipientsJson], [Body], [HtmlBody]
                FROM [dbo].[NotificationOutbox]
                ORDER BY [Id];
                """;
            var rows = new List<OutboxEvidence>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
            return rows;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static JsonElement MessageArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }
        if (root.ValueKind == JsonValueKind.Object &&
            TryGetProperty(root, "results", out var results) &&
            results.ValueKind == JsonValueKind.Array)
        {
            return results;
        }
        throw new InvalidOperationException($"Unexpected smtp4dev message-list JSON kind {root.ValueKind}.");
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            throw new InvalidOperationException($"smtp4dev message is missing '{name}'.");
        }
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.ToString();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static IEnumerable<string> AllStrings(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString()!;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                foreach (var value in AllStrings(item))
                {
                    yield return value;
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                foreach (var value in AllStrings(property.Value))
                {
                    yield return value;
                }
                break;
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"https://[^\s\""'<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex HttpPattern();

    private sealed record OutboxEvidence(
        long Id,
        string Subject,
        string RecipientsJson,
        string Body,
        string HtmlBody);
}
