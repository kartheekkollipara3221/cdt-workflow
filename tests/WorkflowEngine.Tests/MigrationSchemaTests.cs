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
        var recipientSnapshotSegment = script[deleteOutbox..(addTaskSnapshot +
            "ALTER TABLE [dbo].[WorkflowTask] ADD [NotificationRecipientsJson] nvarchar(max) NOT NULL;".Length)];
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowInstance]", recipientSnapshotSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowAudit]", recipientSnapshotSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[MockSectionData]", recipientSnapshotSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[IdempotencyRecord]", recipientSnapshotSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[NotificationRoute]", recipientSnapshotSegment, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationAnchoredSlaMigrationBackfillsExistingDurationPoliciesWithoutScheduleBackfill()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains("CREATE TABLE [dbo].[WorkflowTaskSlaSchedule]", script, StringComparison.Ordinal);
        Assert.Contains("[WorkflowTaskSlaScheduleId] bigint NULL", script, StringComparison.Ordinal);
        Assert.Contains("[SlaOccurrenceDate] date NULL", script, StringComparison.Ordinal);
        Assert.Contains("[SlaReminderKind] nvarchar(30) NULL", script, StringComparison.Ordinal);
        Assert.Contains("[CancelledAtUtc] datetimeoffset NULL", script, StringComparison.Ordinal);
        Assert.Contains("'Cancelled'", script, StringComparison.Ordinal);
        var calculationModeBackfill = script.IndexOf(
            "UPDATE [dbo].[SlaPolicy] SET [CalculationMode] = N'DurationFromTaskCreation' WHERE [CalculationMode] = N'';",
            StringComparison.Ordinal);
        var calculationModeConstraint = script.IndexOf(
            "ADD CONSTRAINT [CK_SlaPolicy_CalculationMode]",
            StringComparison.Ordinal);
        Assert.True(calculationModeBackfill >= 0, "Expected existing duration policies to receive a valid calculation mode.");
        Assert.True(
            calculationModeConstraint > calculationModeBackfill,
            "Expected the SLA calculation-mode backfill before its check constraint.");
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

    [Fact]
    public void DurationMinuteRecurrenceMigrationPreservesExistingSchedulesAndNotificationHistory()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        Assert.Contains(
            "ALTER TABLE [dbo].[SlaPolicy] ADD [RecurrenceIntervalMinutes] int NULL;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ALTER TABLE [dbo].[WorkflowTaskSlaSchedule] ADD [RecurrenceIntervalMinutes] int NULL;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "ALTER TABLE [dbo].[NotificationOutbox] ADD [SlaOccurrenceAtUtc] datetimeoffset NULL;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_NotificationOutbox_WorkflowTaskSlaScheduleId_SlaOccurrenceAtUtc_SlaReminderKind]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "WHERE [WorkflowTaskSlaScheduleId] IS NOT NULL AND [SlaOccurrenceAtUtc] IS NOT NULL",
            script,
            StringComparison.Ordinal);
        Assert.Contains("CK_SlaPolicy_RecurrenceIntervalMinutes", script, StringComparison.Ordinal);
        Assert.Contains("CK_SlaPolicy_RecurrenceIntervalsMutuallyExclusive", script, StringComparison.Ordinal);
        Assert.Contains("CK_WorkflowTaskSlaSchedule_RecurrenceIntervalMinutes", script, StringComparison.Ordinal);
        Assert.Contains("CK_WorkflowTaskSlaSchedule_RecurrenceIntervalsMutuallyExclusive", script, StringComparison.Ordinal);

        var migrationStart = script.IndexOf(
            "ALTER TABLE [dbo].[SlaPolicy] ADD [RecurrenceIntervalMinutes] int NULL;",
            StringComparison.Ordinal);
        Assert.True(migrationStart >= 0);
        var migrationSegment = script[migrationStart..];
        Assert.DoesNotContain("UPDATE [dbo].[WorkflowTaskSlaSchedule]", migrationSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE [dbo].[NotificationOutbox]", migrationSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO [dbo].[WorkflowTaskSlaSchedule]", migrationSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO [dbo].[NotificationOutbox]", migrationSegment, StringComparison.Ordinal);
    }

    [Fact]
    public void OptimizedEnvelopeMigrationCreatesMetadataParticipantsAndRecipientRules()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();

        var deleteOutbox = script.LastIndexOf("DELETE FROM [dbo].[NotificationOutbox];", StringComparison.Ordinal);
        var deleteSchedules = script.LastIndexOf("DELETE FROM [dbo].[WorkflowTaskSlaSchedule];", StringComparison.Ordinal);
        var deleteTasks = script.LastIndexOf("DELETE FROM [dbo].[WorkflowTask];", StringComparison.Ordinal);
        var deleteAudit = script.LastIndexOf("DELETE FROM [dbo].[WorkflowAudit];", StringComparison.Ordinal);
        var deleteIdempotency = script.LastIndexOf("DELETE FROM [dbo].[IdempotencyRecord];", StringComparison.Ordinal);
        var deleteInstances = script.LastIndexOf("DELETE FROM [dbo].[WorkflowInstance];", StringComparison.Ordinal);

        Assert.Contains("ALTER TABLE [dbo].[WorkflowDefinition] ADD [BusinessObjectType] nvarchar(100) NOT NULL", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowParticipantDefinition]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[WorkflowParticipant]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [dbo].[NotificationRecipientRule]", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE [dbo].[NotificationRoute] DROP COLUMN [RecipientRoleKey];", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE [dbo].[NotificationRoute] DROP COLUMN [RecipientUserId];", script, StringComparison.Ordinal);
        Assert.Contains("CK_WorkflowParticipantDefinition_Cardinality", script, StringComparison.Ordinal);
        Assert.Contains("CK_WorkflowParticipantDefinition_CaptureStage", script, StringComparison.Ordinal);
        Assert.Contains("CK_NotificationRecipientRule_RecipientKind", script, StringComparison.Ordinal);
        Assert.Contains("CK_NotificationRecipientRule_SourceType", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CK_NotificationRecipientRule_Email", script, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX [IX_NotificationRecipientRule_NotificationRouteId_SortOrder]",
            script,
            StringComparison.Ordinal);
        Assert.True(deleteOutbox >= 0, "Expected incompatible notification runtime rows to be cleared.");
        Assert.True(deleteSchedules > deleteOutbox, "Expected schedules to be cleared after outbox rows.");
        Assert.True(deleteTasks > deleteSchedules, "Expected tasks to be cleared after schedules.");
        Assert.True(deleteAudit > deleteTasks, "Expected audit rows to be cleared after tasks.");
        Assert.True(deleteIdempotency > deleteAudit, "Expected idempotency rows to be cleared after audit.");
        Assert.True(deleteInstances > deleteIdempotency, "Expected workflow instances to be cleared last.");
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowDefinition]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[WorkflowDefinitionVersion]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[NotificationRoute]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM [dbo].[MockSectionData]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignedRoleRecipientFilterMigrationAddsOnlyNullableMetadataColumn()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        var migrationStart = script.IndexOf(
            "ALTER TABLE [dbo].[NotificationRecipientRule] ADD [AssignedRoleKey] nvarchar(100) NULL;",
            StringComparison.Ordinal);

        Assert.True(migrationStart >= 0, "Expected the nullable assigned-role recipient filter column.");
        var migrationSegment = script[migrationStart..];
        Assert.DoesNotContain("DROP TABLE", migrationSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", migrationSegment, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE [dbo].[NotificationRecipientRule]", migrationSegment, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationOccurrenceOwnershipMigrationPreservesExistingRows()
    {
        using var dbContext = CreateDbContext();

        var script = dbContext.GetService<IMigrator>().GenerateScript();
        var migrationStart = script.IndexOf(
            "ALTER TABLE [dbo].[NotificationOutbox] ALTER COLUMN [WorkflowInstanceId] nvarchar(150) NULL;",
            StringComparison.Ordinal);

        Assert.True(migrationStart >= 0, "Expected WorkflowInstanceId to become nullable.");
        var migrationSegment = script[migrationStart..];
        Assert.Contains(
            "ALTER TABLE [dbo].[NotificationOutbox] ADD [NotificationOccurrenceKey] nvarchar(300) NULL;",
            migrationSegment,
            StringComparison.Ordinal);
        Assert.Contains("CK_NotificationOutbox_Owner", migrationSegment, StringComparison.Ordinal);
        Assert.Contains(
            "WHERE [NotificationOccurrenceKey] IS NOT NULL",
            migrationSegment,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", migrationSegment, StringComparison.OrdinalIgnoreCase);
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
