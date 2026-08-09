using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowSeedService(WorkflowDbContext dbContext, TimeProvider timeProvider)
{
    public void SeedPortfolioTrade()
    {
        var definition = dbContext.WorkflowDefinitions.SingleOrDefault(row => row.WorkflowKey == PortfolioTradeSeedData.WorkflowKey);
        if (definition is null)
        {
            definition = new WorkflowDefinition { WorkflowKey = PortfolioTradeSeedData.WorkflowKey };
            dbContext.WorkflowDefinitions.Add(definition);
        }

        definition.DisplayName = "Portfolio Trade";
        definition.BusinessObjectType = PortfolioTradeSeedData.BusinessObjectType;
        definition.IsActive = true;
        dbContext.SaveChanges();

        var version = dbContext.WorkflowDefinitionVersions.SingleOrDefault(row =>
            row.WorkflowDefinitionId == definition.Id && row.VersionNumber == PortfolioTradeSeedData.VersionNumber);
        if (version is null)
        {
            version = new WorkflowDefinitionVersion
            {
                WorkflowDefinitionId = definition.Id,
                VersionNumber = PortfolioTradeSeedData.VersionNumber,
                CreatedAtUtc = timeProvider.GetUtcNow()
            };
            dbContext.WorkflowDefinitionVersions.Add(version);
        }

        version.IsActive = true;
        dbContext.SaveChanges();

        UpsertStates(version.Id);
        UpsertParticipantDefinitions(version.Id);
        UpsertRoles(version.Id);
        UpsertActions(version.Id);
        UpsertSlaPolicies(version.Id);
        UpsertTasks(version.Id);
        UpsertTaskAssignmentRoles(version.Id);
        UpsertNotificationRoutes(version.Id);
        dbContext.SaveChanges();
        UpsertNotificationRecipientRules(version.Id);
        UpsertTransitions(version.Id);
        dbContext.SaveChanges();
    }

    private void UpsertParticipantDefinitions(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.ParticipantDefinitions)
        {
            var row = dbContext.WorkflowParticipantDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId
                && item.ParticipantKey == seed.ParticipantKey);
            if (row is null)
            {
                row = new WorkflowParticipantDefinition
                {
                    WorkflowDefinitionVersionId = versionId,
                    ParticipantKey = seed.ParticipantKey
                };
                dbContext.WorkflowParticipantDefinitions.Add(row);
            }

            row.Cardinality = seed.Cardinality;
            row.CaptureStage = seed.CaptureStage;
            row.IsRequired = seed.IsRequired;
            row.IsImmutable = seed.IsImmutable;
            row.IsActive = true;
        }
    }

    private void UpsertStates(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.States)
        {
            var row = dbContext.WorkflowStateDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.StateKey == seed.Key);
            if (row is null)
            {
                row = new WorkflowStateDefinition { WorkflowDefinitionVersionId = versionId, StateKey = seed.Key };
                dbContext.WorkflowStateDefinitions.Add(row);
            }

            row.DisplayName = seed.DisplayName;
            row.IsTerminal = seed.IsTerminal;
            row.IsActive = true;
        }
    }

    private void UpsertRoles(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.Roles)
        {
            var row = dbContext.WorkflowRoleDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.RoleKey == seed.Key);
            if (row is null)
            {
                row = new WorkflowRoleDefinition { WorkflowDefinitionVersionId = versionId, RoleKey = seed.Key };
                dbContext.WorkflowRoleDefinitions.Add(row);
            }

            row.DisplayName = seed.DisplayName;
            row.IsActive = true;
        }
    }

    private void UpsertActions(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.Actions)
        {
            var row = dbContext.WorkflowActionDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.ActionKey == seed.Key);
            if (row is null)
            {
                row = new WorkflowActionDefinition { WorkflowDefinitionVersionId = versionId, ActionKey = seed.Key };
                dbContext.WorkflowActionDefinitions.Add(row);
            }

            row.DisplayName = seed.DisplayName;
            row.SectionKey = seed.SectionKey;
            row.ButtonKey = seed.ButtonKey;
            row.ButtonLabel = seed.ButtonLabel;
            row.Intent = seed.Intent;
            row.RequiresReason = seed.RequiresReason;
            row.SortOrder = seed.SortOrder;
            row.IsActive = true;
        }
    }

    private void UpsertSlaPolicies(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.SlaPolicies)
        {
            var row = dbContext.SlaPolicies.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.SlaPolicyKey == seed.Key);
            if (row is null)
            {
                row = new SlaPolicy { WorkflowDefinitionVersionId = versionId, SlaPolicyKey = seed.Key };
                dbContext.SlaPolicies.Add(row);
            }

            row.CalculationMode = seed.CalculationMode;
            row.DurationMinutes = seed.DurationMinutes;
            row.SendDueDateReminder = seed.SendDueDateReminder;
            row.ReminderLocalTime = seed.ReminderLocalTime;
            row.TimeZoneId = seed.TimeZoneId;
            row.OverdueStartOffsetDays = seed.OverdueStartOffsetDays;
            row.RecurrenceIntervalDays = seed.RecurrenceIntervalDays;
            row.DueNotificationRouteKey = seed.DueNotificationRouteKey;
            row.OverdueNotificationRouteKey = seed.OverdueNotificationRouteKey;
            row.IsActive = true;
        }
    }

    private void UpsertTasks(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.Tasks)
        {
            var row = dbContext.WorkflowTaskDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.TaskKey == seed.Key);
            if (row is null)
            {
                row = new WorkflowTaskDefinition { WorkflowDefinitionVersionId = versionId, TaskKey = seed.Key };
                dbContext.WorkflowTaskDefinitions.Add(row);
            }

            row.DisplayName = seed.DisplayName;
            row.OwnerRoleKey = seed.OwnerRoleKey;
            row.SlaPolicyKey = seed.SlaPolicyKey;
            row.IsActive = true;
        }
    }

    private void UpsertTaskAssignmentRoles(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.TaskAssignmentRoles)
        {
            var row = dbContext.WorkflowTaskAssignmentRoleDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId
                && item.TaskKey == seed.TaskKey
                && item.RoleKey == seed.RoleKey);
            if (row is null)
            {
                row = new WorkflowTaskAssignmentRoleDefinition
                {
                    WorkflowDefinitionVersionId = versionId,
                    TaskKey = seed.TaskKey,
                    RoleKey = seed.RoleKey
                };
                dbContext.WorkflowTaskAssignmentRoleDefinitions.Add(row);
            }

            row.IsActive = true;
        }
    }

    private void UpsertNotificationRoutes(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.NotificationRoutes)
        {
            var row = dbContext.NotificationRoutes.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId && item.RouteKey == seed.Key);
            if (row is null)
            {
                row = new NotificationRoute { WorkflowDefinitionVersionId = versionId, RouteKey = seed.Key };
                dbContext.NotificationRoutes.Add(row);
            }

            row.NotificationType = seed.NotificationType;
            row.SubjectTemplate = seed.SubjectTemplate;
            row.BodyTemplate = seed.BodyTemplate;
            row.HtmlBodyTemplate = seed.HtmlBodyTemplate;
            row.OverdueSubjectPrefix = seed.OverdueSubjectPrefix;
            row.IsActive = true;
        }
    }

    private void UpsertNotificationRecipientRules(long versionId)
    {
        var routes = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.RouteKey, StringComparer.Ordinal);

        foreach (var seed in PortfolioTradeSeedData.NotificationRecipientRules)
        {
            var route = routes[seed.RouteKey];
            var row = dbContext.NotificationRecipientRules.SingleOrDefault(item =>
                item.NotificationRouteId == route.Id && item.SortOrder == seed.SortOrder);
            if (row is null)
            {
                row = new NotificationRecipientRule
                {
                    NotificationRouteId = route.Id,
                    SortOrder = seed.SortOrder
                };
                dbContext.NotificationRecipientRules.Add(row);
            }

            row.RecipientKind = seed.RecipientKind;
            row.SourceType = seed.SourceType;
            row.SourceKey = seed.SourceKey;
            row.StaticAddress = seed.StaticAddress;
            row.IsRequired = seed.IsRequired;
            row.IsActive = true;
        }
    }

    private void UpsertTransitions(long versionId)
    {
        foreach (var seed in PortfolioTradeSeedData.Transitions)
        {
            var row = dbContext.WorkflowTransitionDefinitions.SingleOrDefault(item =>
                item.WorkflowDefinitionVersionId == versionId
                && item.FromStateKey == seed.FromStateKey
                && item.ActionKey == seed.ActionKey
                && item.AllowedRoleKey == seed.AllowedRoleKey);
            if (row is null)
            {
                row = new WorkflowTransitionDefinition
                {
                    WorkflowDefinitionVersionId = versionId,
                    FromStateKey = seed.FromStateKey,
                    ActionKey = seed.ActionKey,
                    AllowedRoleKey = seed.AllowedRoleKey
                };
                dbContext.WorkflowTransitionDefinitions.Add(row);
            }

            row.ToStateKey = seed.ToStateKey;
            row.TaskToCloseKey = seed.TaskToCloseKey;
            row.TaskToCreateKey = seed.TaskToCreateKey;
            row.NotificationRouteKey = seed.NotificationRouteKey;
            row.AuditLabel = seed.AuditLabel;
            row.IsActive = true;
        }
    }
}
