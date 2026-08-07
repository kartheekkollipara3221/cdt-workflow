using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Tests;

public sealed class SchemaConstraintTests
{
    private readonly WorkflowDbContext dbContext = new(
        new DbContextOptionsBuilder<WorkflowDbContext>()
            .UseSqlServer(TestSqlConnectionFactory.ConnectionString("WorkflowEngineModelTests"))
            .Options);

    [Fact]
    public void WorkflowTaskHasSinglePendingTaskFilteredUniqueIndex()
    {
        var entityType = dbContext.Model.FindEntityType(typeof(WorkflowTask));
        Assert.NotNull(entityType);

        var index = Assert.Single(entityType.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(["WorkflowInstanceId"]));

        Assert.True(index.IsUnique);
        Assert.Equal("[Status] = 'Pending'", index.GetFilter());
    }

    [Fact]
    public void WorkflowTaskSlaScheduleHasVersionAndSingleActiveIndexes()
    {
        var entityType = dbContext.Model.FindEntityType(typeof(WorkflowTaskSlaSchedule));
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(["WorkflowTaskId", "ScheduleVersion"]));
        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(["WorkflowTaskId"])
            && index.GetFilter() == "[Status] = 'Active'");

        var designTimeEntity = dbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(WorkflowTaskSlaSchedule));
        Assert.NotNull(designTimeEntity);
        Assert.Contains(designTimeEntity.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_WorkflowTaskSlaSchedule_Status"
            && constraint.Sql.Contains("'Superseded'", StringComparison.Ordinal)
            && constraint.Sql.Contains("'Completed'", StringComparison.Ordinal));
        Assert.Contains(designTimeEntity.GetCheckConstraints(), constraint =>
            constraint.Name == "CK_WorkflowTaskSlaSchedule_ScheduleVersion"
            && constraint.Sql.Contains("> 0", StringComparison.Ordinal));
    }

    [Fact]
    public void NotificationOutboxHasSlaOccurrenceUniqueIndex()
    {
        var entityType = dbContext.Model.FindEntityType(typeof(NotificationOutbox));
        Assert.NotNull(entityType);

        var index = Assert.Single(entityType.GetIndexes(), item =>
            item.Properties.Select(property => property.Name).SequenceEqual(
                ["WorkflowTaskSlaScheduleId", "SlaOccurrenceDate", "SlaReminderKind"]));

        Assert.True(index.IsUnique);
        Assert.Equal("[WorkflowTaskSlaScheduleId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void NotificationOutboxStatusAllowsCancelled()
    {
        var entityType = dbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(NotificationOutbox));
        Assert.NotNull(entityType);

        var constraint = Assert.Single(entityType.GetCheckConstraints(), item =>
            item.Name == "CK_NotificationOutbox_Status");

        Assert.Contains("'Cancelled'", constraint.Sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(WorkflowTask))]
    [InlineData(typeof(NotificationOutbox))]
    public void NotificationRecipientSnapshotsAreRequiredUnboundedJsonColumns(Type entityClrType)
    {
        var entityType = dbContext.Model.FindEntityType(entityClrType);
        Assert.NotNull(entityType);

        var property = entityType.FindProperty("NotificationRecipientsJson");

        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        Assert.Equal("nvarchar(max)", property.GetColumnType());
    }

    [Fact]
    public void NotificationOutboxDoesNotMapLegacyRecipientScalarColumns()
    {
        var entityType = dbContext.Model.FindEntityType(typeof(NotificationOutbox));
        Assert.NotNull(entityType);

        Assert.Null(entityType.FindProperty("RecipientRoleKey"));
        Assert.Null(entityType.FindProperty("RecipientUserId"));
    }

    [Theory]
    [InlineData(typeof(WorkflowInstance), "CK_WorkflowInstance_Status")]
    [InlineData(typeof(WorkflowTask), "CK_WorkflowTask_Status")]
    [InlineData(typeof(NotificationOutbox), "CK_NotificationOutbox_Status")]
    public void RuntimeStatusCheckConstraintsExist(Type entityClrType, string constraintName)
    {
        var entityType = dbContext.GetService<IDesignTimeModel>().Model.FindEntityType(entityClrType);
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetCheckConstraints(), constraint => constraint.Name == constraintName);
    }

    [Theory]
    [InlineData(typeof(WorkflowTask))]
    [InlineData(typeof(WorkflowAudit))]
    [InlineData(typeof(NotificationOutbox))]
    [InlineData(typeof(IdempotencyRecord))]
    public void RuntimeRowsHaveWorkflowInstanceForeignKeys(Type entityClrType)
    {
        var entityType = dbContext.Model.FindEntityType(entityClrType);
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["WorkflowInstanceId"])
            && foreignKey.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(["WorkflowInstanceId"]));
    }

    [Fact]
    public void WorkflowTaskSlaScheduleReferencesWorkflowTask()
    {
        var entityType = dbContext.Model.FindEntityType(typeof(WorkflowTaskSlaSchedule));
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["WorkflowTaskId"])
            && foreignKey.PrincipalEntityType.ClrType == typeof(WorkflowTask));
    }

    [Theory]
    [InlineData(typeof(WorkflowDefinition), "WorkflowKey")]
    [InlineData(typeof(IdempotencyRecord), "IdempotencyKey")]
    public void RequiredSingleColumnUniqueIndexesExist(Type entityClrType, string propertyName)
    {
        var entityType = dbContext.Model.FindEntityType(entityClrType);
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual([propertyName]));
    }

    [Theory]
    [InlineData(typeof(WorkflowDefinitionVersion), "WorkflowDefinitionId", "VersionNumber")]
    [InlineData(typeof(WorkflowStateDefinition), "WorkflowDefinitionVersionId", "StateKey")]
    [InlineData(typeof(WorkflowActionDefinition), "WorkflowDefinitionVersionId", "ActionKey")]
    [InlineData(typeof(WorkflowRoleDefinition), "WorkflowDefinitionVersionId", "RoleKey")]
    [InlineData(typeof(WorkflowTaskDefinition), "WorkflowDefinitionVersionId", "TaskKey")]
    [InlineData(typeof(SlaPolicy), "WorkflowDefinitionVersionId", "SlaPolicyKey")]
    [InlineData(typeof(NotificationRoute), "WorkflowDefinitionVersionId", "RouteKey")]
    [InlineData(typeof(WorkflowTransitionDefinition), "WorkflowDefinitionVersionId", "FromStateKey", "ActionKey", "AllowedRoleKey")]
    [InlineData(typeof(WorkflowInstance), "WorkflowKey", "BusinessObjectType", "BusinessObjectId")]
    public void RequiredCompositeUniqueIndexesExist(Type entityClrType, params string[] propertyNames)
    {
        var entityType = dbContext.Model.FindEntityType(entityClrType);
        Assert.NotNull(entityType);

        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
    }
}
