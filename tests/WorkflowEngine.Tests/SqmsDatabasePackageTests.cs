using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests;

public sealed class SqmsDatabasePackageTests
{
    [Fact]
    public void NotificationOutboxDdlHasRequiredTransactionAndOwnershipGuards()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "sql",
            "sqms",
            "001_sqms_notification_outbox_ddl.sql");

        Assert.True(File.Exists(path), $"Missing DBA DDL file: {path}");
        var sql = File.ReadAllText(path);

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotificationOccurrenceKey", sql, StringComparison.Ordinal);
        Assert.Contains("UX_NotificationOutbox_NotificationOccurrenceKey", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE NotificationOccurrenceKey IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CK_NotificationOutbox_Owner", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE [SSQMS]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE [SSQMS]", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqmsMetadataPackageIsRerunnableAndHasExactActiveCounts()
    {
        var root = RepositoryRoot();
        var tableDdlPath = Path.Combine(root, "sql", "sqms", "testing", "000_create_sqms_test_tables.sql");
        var metadataDmlPath = Path.Combine(root, "sql", "sqms", "002_sqms_workflow_metadata_dml.sql");
        var verifierPath = Path.Combine(root, "sql", "sqms", "003_verify_sqms_deployment.sql");

        Assert.True(File.Exists(tableDdlPath), $"Missing SQMS testing DDL: {tableDdlPath}");
        Assert.True(File.Exists(metadataDmlPath), $"Missing SQMS metadata DML: {metadataDmlPath}");
        Assert.True(File.Exists(verifierPath), $"Missing SQMS verifier: {verifierPath}");

        var connectionString = TestSqlConnectionFactory.ConnectionString("workflowengine_sqms_package_tests");
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo"))
            .Options;

        await using var dbContext = new WorkflowDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
        try
        {
            await dbContext.Database.MigrateAsync();
            await ExecuteScriptAsync(connectionString, tableDdlPath);

            var variables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SqmsOpenToRecipientsJson"] = "[\"sqms-open@example.test\"]",
                ["SqmsOpenCcRecipientsJson"] = "[\"sqms-open-cc@example.test\"]",
                ["SqmsSellerApprovalCcRecipientsJson"] = "[\"sqms-seller-cc@example.test\"]",
                ["SqmsBidDispositionCcRecipientsJson"] = "[\"sqms-bid-cc@example.test\"]"
            };
            await ExecuteScriptAsync(connectionString, metadataDmlPath, variables);
            await ExecuteScriptAsync(connectionString, metadataDmlPath, variables);
            await ExecuteScriptAsync(connectionString, verifierPath);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Assert.Equal(10, await CountAsync(connection, "WorkflowStateDefinition", "SqmsOpportunity"));
            Assert.Equal(10, await CountAsync(connection, "WorkflowActionDefinition", "SqmsOpportunity"));
            Assert.Equal(3, await CountAsync(connection, "WorkflowRoleDefinition", "SqmsOpportunity"));
            Assert.Equal(12, await CountAsync(connection, "WorkflowTransitionDefinition", "SqmsOpportunity"));
            Assert.Equal(3, await CountAsync(connection, "NotificationRoute", "SqmsOpportunity"));
            Assert.Equal(8, await CountAsync(connection, "WorkflowStateDefinition", "SqmsBid"));
            Assert.Equal(8, await CountAsync(connection, "WorkflowActionDefinition", "SqmsBid"));
            Assert.Equal(4, await CountAsync(connection, "WorkflowRoleDefinition", "SqmsBid"));
            Assert.Equal(8, await CountAsync(connection, "WorkflowTransitionDefinition", "SqmsBid"));
            Assert.Equal(3, await CountAsync(connection, "NotificationRoute", "SqmsBid"));
            Assert.Equal(0, await CountAsync(connection, "WorkflowTaskDefinition", "SqmsOpportunity", "SqmsBid"));
            Assert.Equal(0, await CountAsync(connection, "SlaPolicy", "SqmsOpportunity", "SqmsBid"));
        }
        finally
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<int> CountAsync(
        SqlConnection connection,
        string tableName,
        params string[] workflowKeys)
    {
        var keyParameters = workflowKeys
            .Select((_, index) => $"@workflowKey{index}")
            .ToArray();
        var sql = $"""
            SELECT COUNT(*)
            FROM [dbo].[{tableName}] AS item
            INNER JOIN [dbo].[WorkflowDefinitionVersion] AS version
                ON version.Id = item.WorkflowDefinitionVersionId
            INNER JOIN [dbo].[WorkflowDefinition] AS definition
                ON definition.Id = version.WorkflowDefinitionId
            WHERE item.IsActive = 1
              AND definition.WorkflowKey IN ({string.Join(", ", keyParameters)});
            """;
        await using var command = new SqlCommand(sql, connection);
        for (var index = 0; index < workflowKeys.Length; index++)
        {
            command.Parameters.AddWithValue(keyParameters[index], workflowKeys[index]);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteScriptAsync(
        string connectionString,
        string path,
        IReadOnlyDictionary<string, string>? variables = null)
    {
        var sql = await File.ReadAllTextAsync(path);
        if (variables is not null)
        {
            foreach (var variable in variables)
            {
                sql = sql.Replace($"$({variable.Key})", variable.Value, StringComparison.Ordinal);
            }
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in Regex.Split(sql, @"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = new SqlCommand(batch, connection)
            {
                CommandTimeout = 120
            };
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
