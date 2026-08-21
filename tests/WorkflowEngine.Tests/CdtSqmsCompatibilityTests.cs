using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class CdtSqmsCompatibilityTests
{
    private const string DatabaseName = "workflowengine_cdt_sqms_compatibility_tests";

    [Fact]
    public async Task ExactSqmsDdlTwicePreservesLegacyCdtRowsAndEnforcesAdditiveOwnership()
    {
        using var host = new TestConsoleHost(DatabaseName);
        host.ResetDatabase();
        try
        {
            ExecuteAll(host, BranchScriptDefinitions.HappyPath_Section25_1);
            AssertSuccess(
                host.Execute("start-workflow", "PortfolioTrade", "PT-COMPAT-ACTIVE", "--user", "maker1", "--reset"),
                "start active compatibility workflow");
            AssertSuccess(
                host.Execute(
                    "execute-json",
                    "./samples/workflow-actions/02-submit-for-checker.json",
                    "--business-object",
                    "PT-COMPAT-ACTIVE",
                    "--workflow-instance",
                    "WF-PT-COMPAT-ACTIVE"),
                "submit active compatibility workflow");

            PrepareOutboxStatusCoverage(host);
            AssertLegacyFixtureCoverage(host);
            host.DbContext.ChangeTracker.Clear();

            await ReconstructLegacyOutboxSchemaAsync(host.ConnectionString);
            var beforeCount = await CountOutboxRowsAsync(host.ConnectionString);
            var beforeHash = await HashLegacyOutboxRowsAsync(host.ConnectionString);

            var ddlPath = Path.Combine(
                SqmsDatabasePackageTests.RepositoryRoot(),
                "sql",
                "sqms",
                "001_sqms_notification_outbox_ddl.sql");
            await SqmsDatabasePackageTests.ExecuteScriptAsync(host.ConnectionString, ddlPath);
            await SqmsDatabasePackageTests.ExecuteScriptAsync(host.ConnectionString, ddlPath);

            Assert.Equal(beforeCount, await CountOutboxRowsAsync(host.ConnectionString));
            Assert.Equal(beforeHash, await HashLegacyOutboxRowsAsync(host.ConnectionString));
            Assert.Equal(0, await CountOwnerlessRowsAsync(host.ConnectionString));

            await AssertOldStyleInsertSucceedsAsync(host.ConnectionString);
            await AssertOwnerlessInsertFailsAsync(host.ConnectionString);
            await AssertOccurrenceOnlyInsertSucceedsAsync(host.ConnectionString);
            await AssertDuplicateOccurrenceFailsAsync(host.ConnectionString);
        }
        finally
        {
            host.ResetDatabase();
        }
    }

    private static void ExecuteAll(TestConsoleHost host, IEnumerable<ConsoleCommand> commands)
    {
        foreach (var command in commands)
        {
            AssertSuccess(host.Execute(command), command.CommandName);
        }
    }

    private static void AssertSuccess(WorkflowEngine.Application.Commands.CommandResult result, string operation)
    {
        Assert.True(result.ExitCode == 0, $"{operation} failed: {result.Message}");
    }

    private static void PrepareOutboxStatusCoverage(TestConsoleHost host)
    {
        var rows = host.DbContext.NotificationOutbox.OrderBy(row => row.Id).Take(4).ToArray();
        Assert.Equal(4, rows.Length);

        rows[0].Status = "Pending";
        rows[0].ProcessingToken = null;
        rows[0].ProcessingStartedAtUtc = null;
        rows[0].SentAtUtc = null;
        rows[0].CancelledAtUtc = null;
        rows[0].CancellationReason = null;

        rows[1].Status = "Processing";
        rows[1].ProcessingToken = "compatibility-claim";
        rows[1].ProcessingStartedAtUtc = rows[1].CreatedAtUtc.AddMinutes(1);
        rows[1].SentAtUtc = null;
        rows[1].CancelledAtUtc = null;
        rows[1].CancellationReason = null;

        rows[2].Status = "Sent";
        rows[2].ProcessingToken = null;
        rows[2].ProcessingStartedAtUtc = null;
        rows[2].SentAtUtc = rows[2].CreatedAtUtc.AddMinutes(2);
        rows[2].CancelledAtUtc = null;
        rows[2].CancellationReason = null;

        rows[3].Status = "Cancelled";
        rows[3].ProcessingToken = null;
        rows[3].ProcessingStartedAtUtc = null;
        rows[3].SentAtUtc = null;
        rows[3].CancelledAtUtc = rows[3].CreatedAtUtc.AddMinutes(3);
        rows[3].CancellationReason = "CompatibilityFixture";

        host.DbContext.SaveChanges();
    }

    private static void AssertLegacyFixtureCoverage(TestConsoleHost host)
    {
        Assert.Contains(host.DbContext.WorkflowInstances, row => row.Status == "Active");
        Assert.Contains(host.DbContext.WorkflowInstances, row => row.Status == "Completed");
        Assert.Contains(host.DbContext.WorkflowTasks, row => row.Status == "Pending");
        Assert.Contains(host.DbContext.WorkflowTasks, row => row.Status == "Completed");
        Assert.Contains(host.DbContext.WorkflowTaskSlaSchedules, row => row.Status == "Active");
        Assert.Contains(host.DbContext.WorkflowTaskSlaSchedules, row => row.Status == "Completed");
        Assert.NotEmpty(host.DbContext.WorkflowAudits);
        Assert.NotEmpty(host.DbContext.IdempotencyRecords);
        Assert.Equal(
            ["Cancelled", "Pending", "Processing", "Sent"],
            host.DbContext.NotificationOutbox
                .Select(row => row.Status)
                .Distinct()
                .OrderBy(status => status)
                .ToArray());
        Assert.All(host.DbContext.NotificationOutbox, row => Assert.NotNull(row.WorkflowInstanceId));
    }

    private static async Task ReconstructLegacyOutboxSchemaAsync(string connectionString)
    {
        const string sql = """
            ALTER TABLE [dbo].[NotificationOutbox] DROP CONSTRAINT [CK_NotificationOutbox_Owner];
            DROP INDEX [UX_NotificationOutbox_NotificationOccurrenceKey] ON [dbo].[NotificationOutbox];
            ALTER TABLE [dbo].[NotificationOutbox] DROP COLUMN [NotificationOccurrenceKey];
            ALTER TABLE [dbo].[NotificationOutbox]
                DROP CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId];
            DROP INDEX [IX_NotificationOutbox_WorkflowInstanceId] ON [dbo].[NotificationOutbox];
            ALTER TABLE [dbo].[NotificationOutbox]
                ALTER COLUMN [WorkflowInstanceId] nvarchar(150) NOT NULL;
            CREATE INDEX [IX_NotificationOutbox_WorkflowInstanceId]
                ON [dbo].[NotificationOutbox] ([WorkflowInstanceId]);
            ALTER TABLE [dbo].[NotificationOutbox] WITH CHECK
                ADD CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId]
                FOREIGN KEY ([WorkflowInstanceId])
                REFERENCES [dbo].[WorkflowInstance] ([WorkflowInstanceId])
                ON DELETE CASCADE;
            ALTER TABLE [dbo].[NotificationOutbox]
                CHECK CONSTRAINT [FK_NotificationOutbox_WorkflowInstance_WorkflowInstanceId];
            """;
        await ExecuteNonQueryAsync(connectionString, sql);
    }

    private static async Task<int> CountOutboxRowsAsync(string connectionString)
    {
        return Convert.ToInt32(
            await ExecuteScalarAsync(connectionString, "SELECT COUNT(*) FROM [dbo].[NotificationOutbox];"),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<int> CountOwnerlessRowsAsync(string connectionString)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM [dbo].[NotificationOutbox]
            WHERE [WorkflowInstanceId] IS NULL;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> HashLegacyOutboxRowsAsync(string connectionString)
    {
        const string sql = """
            SELECT
                [Id], [WorkflowInstanceId], [WorkflowTaskId], [WorkflowTaskSlaScheduleId],
                [SlaOccurrenceAtUtc], [SlaOccurrenceDate], [SlaReminderKind], [BusinessObjectType],
                [BusinessObjectId], [NotificationType], [NotificationRecipientsJson], [Subject],
                [Body], [HtmlBody], [PayloadJson], [Status], [CorrelationId], [CreatedAtUtc],
                [ProcessingToken], [ProcessingStartedAtUtc], [RetryCount], [LastError], [SentAtUtc],
                [CancelledAtUtc], [CancellationReason]
            FROM [dbo].[NotificationOutbox]
            ORDER BY [Id]
            FOR JSON PATH, INCLUDE_NULL_VALUES;
            """;
        var canonicalRows = Convert.ToString(
            await ExecuteScalarAsync(connectionString, sql),
            System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRows)));
    }

    private static async Task AssertOldStyleInsertSucceedsAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO [dbo].[NotificationOutbox]
            (
                [WorkflowInstanceId], [BusinessObjectType], [BusinessObjectId], [NotificationType],
                [NotificationRecipientsJson], [Subject], [Body], [HtmlBody], [PayloadJson], [Status],
                [CorrelationId], [CreatedAtUtc], [RetryCount]
            )
            VALUES
            (
                N'WF-PT-COMPAT-ACTIVE', N'PortfolioTrade', N'PT-COMPAT-ACTIVE', N'CompatibilityOldStyle',
                N'{"toEmailAddresses":["compat@example.test"],"ccEmailAddresses":[]}', N'Old style',
                N'body', N'<p>body</p>', N'{}', N'Pending', N'compat-old-style', SYSUTCDATETIME(), 0
            );
            """;
        Assert.Equal(1, await ExecuteNonQueryAsync(connectionString, sql));
    }

    private static async Task AssertOwnerlessInsertFailsAsync(string connectionString)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteNonQueryAsync(
            connectionString,
            OwnerlessInsertSql("NULL", "NULL", "compat-ownerless")));
        Assert.Equal(547, exception.Number);
        Assert.Contains("CK_NotificationOutbox_Owner", exception.Message, StringComparison.Ordinal);
    }

    private static async Task AssertOccurrenceOnlyInsertSucceedsAsync(string connectionString)
    {
        Assert.Equal(
            1,
            await ExecuteNonQueryAsync(
                connectionString,
                OwnerlessInsertSql("NULL", "N'SQMS-COMPAT-OCCURRENCE'", "compat-occurrence")));
    }

    private static async Task AssertDuplicateOccurrenceFailsAsync(string connectionString)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(() => ExecuteNonQueryAsync(
            connectionString,
            OwnerlessInsertSql("NULL", "N'SQMS-COMPAT-OCCURRENCE'", "compat-occurrence-duplicate")));
        Assert.Contains(exception.Number, new[] { 2601, 2627 });
        Assert.Contains("UX_NotificationOutbox_NotificationOccurrenceKey", exception.Message, StringComparison.Ordinal);
    }

    private static string OwnerlessInsertSql(
        string workflowInstanceIdSql,
        string occurrenceKeySql,
        string correlationId)
    {
        return $$"""
            INSERT INTO [dbo].[NotificationOutbox]
            (
                [WorkflowInstanceId], [NotificationOccurrenceKey], [BusinessObjectType], [BusinessObjectId],
                [NotificationType], [NotificationRecipientsJson], [Subject], [Body], [HtmlBody], [PayloadJson],
                [Status], [CorrelationId], [CreatedAtUtc], [RetryCount]
            )
            VALUES
            (
                {{workflowInstanceIdSql}}, {{occurrenceKeySql}}, N'Sqms', N'10000', N'CompatibilityOccurrence',
                N'{"toEmailAddresses":["compat@example.test"],"ccEmailAddresses":[]}', N'Occurrence',
                N'body', N'<p>body</p>', N'{}', N'Pending', N'{{correlationId}}', SYSUTCDATETIME(), 0
            );
            """;
    }

    private static async Task<int> ExecuteNonQueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ExecuteScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return await command.ExecuteScalarAsync();
    }
}
