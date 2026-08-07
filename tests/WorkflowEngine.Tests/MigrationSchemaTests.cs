using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WorkflowEngine.Infrastructure.Persistence;

namespace WorkflowEngine.Tests;

public sealed class MigrationSchemaTests
{
    [Fact]
    public void MigrationScriptQualifiesWorkflowTablesWithDboSchema()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [dbo].[__EFMigrationsHistory]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowDefinition]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowInstance]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[NotificationOutbox]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE [WorkflowDefinition]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipientSnapshotMigrationDeletesOnlyDisposableRowsBeforeChangingColumns()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        var deleteOutbox = script.IndexOf("DELETE FROM [dbo].[NotificationOutbox];", StringComparison.Ordinal);
        var deleteTasks = script.IndexOf("DELETE FROM [dbo].[WorkflowTask];", StringComparison.Ordinal);
        var addOutboxSnapshot = script.IndexOf(
            "ALTER TABLE [dbo].[NotificationOutbox] ADD [NotificationRecipientsJson] nvarchar(max) NOT NULL;",
            StringComparison.Ordinal);
        var addTaskSnapshot = script.IndexOf(
            "ALTER TABLE [dbo].[WorkflowTask] ADD [NotificationRecipientsJson] nvarchar(max) NOT NULL;",
            StringComparison.Ordinal);

        Assert.True(deleteOutbox >= 0, "Expected NotificationOutbox rows to be deleted by the recipient snapshot migration.");
        Assert.True(deleteTasks > deleteOutbox, "Expected WorkflowTask rows to be deleted after NotificationOutbox rows.");
        Assert.True(addOutboxSnapshot > deleteTasks, "Expected the outbox recipient snapshot column after the scoped deletes.");
        Assert.True(addTaskSnapshot > deleteTasks, "Expected the task recipient snapshot column after the scoped deletes.");
        Assert.Contains("ALTER TABLE [dbo].[NotificationOutbox] DROP COLUMN [RecipientRoleKey];", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE [dbo].[NotificationOutbox] DROP COLUMN [RecipientUserId];", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowInstance]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowAudit]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[MockSectionData]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[IdempotencyRecord]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[NotificationRoute]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationAnchoredSlaMigrationCreatesScheduleAndOccurrenceSchemaWithoutBackfill()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [dbo].[WorkflowTaskSlaSchedule]", script, StringComparison.Ordinal);
        Assert.Contains("[WorkflowTaskSlaScheduleId] bigint NULL", script, StringComparison.Ordinal);
        Assert.Contains("[SlaOccurrenceDate] date NULL", script, StringComparison.Ordinal);
        Assert.Contains("[SlaReminderKind] nvarchar(30) NULL", script, StringComparison.Ordinal);
        Assert.Contains("[CancelledAtUtc] datetimeoffset NULL", script, StringComparison.Ordinal);
        Assert.Contains("'Cancelled'", script, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_WorkflowTaskSlaSchedule_WorkflowTaskId_ScheduleVersion]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceDate_SlaReminderKind]",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO [dbo].[WorkflowTaskSlaSchedule]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE [dbo].[WorkflowTask] SET", script, StringComparison.Ordinal);
    }

    private static WorkflowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=WorkflowSchemaScriptTest;Integrated Security=true;TrustServerCertificate=True;Encrypt=False",
                sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "dbo"))
            .Options;

        return new WorkflowDbContext(options);
    }
}
