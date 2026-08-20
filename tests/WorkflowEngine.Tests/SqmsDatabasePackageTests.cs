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
