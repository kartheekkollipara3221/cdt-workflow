using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Seed;

public sealed class WorkflowSeedService(WorkflowDbContext dbContext, TimeProvider timeProvider)
{
    public void SeedPortfolioTrade()
    {
        var definition = UpsertWorkflowDefinition();
        var version = UpsertVersion(definition.Id, PortfolioTradeSeedData.Definition);

        // Version 1 is the default active version unless another version is active.
        if (!dbContext.WorkflowDefinitionVersions.Any(row =>
                row.WorkflowDefinitionId == definition.Id && row.Id != version.Id && row.IsActive))
        {
            version.IsActive = true;
        }

        dbContext.SaveChanges();
        UpsertVersionRows(version.Id, PortfolioTradeSeedData.Definition);
    }

    // Inserts or updates PortfolioTrade version 2. A new version 2 starts inactive.
    // With activate, version 2 becomes the only active version in one transaction.
    public void SeedPortfolioTradeVersion2(bool activate)
    {
        var definition = UpsertWorkflowDefinition();
        var version = UpsertVersion(definition.Id, PortfolioTradeSeedDataV2.Definition);
        dbContext.SaveChanges();
        UpsertVersionRows(version.Id, PortfolioTradeSeedDataV2.Definition);
        if (activate)
        {
            ActivateVersion(definition.Id, version.Id);
        }
    }

    // Makes the given seeded PortfolioTrade version the only active version.
    public void ActivatePortfolioTradeVersion(int versionNumber)
    {
        var definition = dbContext.WorkflowDefinitions.Single(row => row.WorkflowKey == PortfolioTradeSeedData.WorkflowKey);
        var version = dbContext.WorkflowDefinitionVersions.Single(row =>
            row.WorkflowDefinitionId == definition.Id && row.VersionNumber == versionNumber);
        ActivateVersion(definition.Id, version.Id);
    }

    private WorkflowDefinition UpsertWorkflowDefinition()
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
        return definition;
    }

    private WorkflowDefinitionVersion UpsertVersion(long workflowDefinitionId, PortfolioTradeSeedDefinition seedSet)
    {
        var version = dbContext.WorkflowDefinitionVersions.SingleOrDefault(row =>
            row.WorkflowDefinitionId == workflowDefinitionId && row.VersionNumber == seedSet.VersionNumber);
        if (version is null)
        {
            version = new WorkflowDefinitionVersion
            {
                WorkflowDefinitionId = workflowDefinitionId,
                VersionNumber = seedSet.VersionNumber,
                IsActive = false,
                CreatedAtUtc = timeProvider.GetUtcNow()
            };
            dbContext.WorkflowDefinitionVersions.Add(version);
        }

        return version;
    }

    private void UpsertVersionRows(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        UpsertStates(versionId, seedSet);
        UpsertParticipantDefinitions(versionId, seedSet);
        UpsertRoles(versionId, seedSet);
        UpsertActions(versionId, seedSet);
        UpsertSlaPolicies(versionId, seedSet);
        UpsertTasks(versionId, seedSet);
        UpsertTaskAssignmentRoles(versionId, seedSet);
        UpsertNotificationRoutes(versionId, seedSet);
        dbContext.SaveChanges();
        UpsertNotificationRecipientRules(versionId, seedSet);
        UpsertTransitions(versionId, seedSet);
        dbContext.SaveChanges();
    }

    private void ActivateVersion(long workflowDefinitionId, long versionId)
    {
        using var transaction = dbContext.Database.CurrentTransaction is null
            ? dbContext.Database.BeginTransaction()
            : null;
        foreach (var row in dbContext.WorkflowDefinitionVersions.Where(row => row.WorkflowDefinitionId == workflowDefinitionId))
        {
            row.IsActive = row.Id == versionId;
        }

        dbContext.SaveChanges();
        transaction?.Commit();
    }

    private void UpsertParticipantDefinitions(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.ParticipantDefinitions)
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

    private void UpsertStates(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.States)
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

    private void UpsertRoles(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.Roles)
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

    private void UpsertActions(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.Actions)
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

    private void UpsertSlaPolicies(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.SlaPolicies)
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
            row.RecurrenceIntervalMinutes = seed.RecurrenceIntervalMinutes;
            row.RecurrenceIntervalDays = seed.RecurrenceIntervalDays;
            row.DueNotificationRouteKey = seed.DueNotificationRouteKey;
            row.OverdueNotificationRouteKey = seed.OverdueNotificationRouteKey;
            row.DueReminderLocalTime = seed.DueReminderLocalTime;
            row.SkipMissedDueReminder = seed.SkipMissedDueReminder;
            row.OverdueStartsNextLocalDay = seed.OverdueStartsNextLocalDay;
            row.IsActive = true;
        }
    }

    private void UpsertTasks(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.Tasks)
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

    private void UpsertTaskAssignmentRoles(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.TaskAssignmentRoles)
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

    private void UpsertNotificationRoutes(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.NotificationRoutes)
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

    private void UpsertNotificationRecipientRules(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        var routes = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.RouteKey, StringComparer.Ordinal);

        foreach (var seed in seedSet.NotificationRecipientRules)
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
            row.AssignedRoleKey = seed.AssignedRoleKey;
            row.IsRequired = seed.IsRequired;
            row.IsActive = true;
        }

        // A rule removed from the seed is deactivated, never deleted. An own-rules
        // SLA reminder route is recognized by any rule row, active or inactive.
        foreach (var routeGroup in seedSet.NotificationRecipientRules.GroupBy(seed => seed.RouteKey, StringComparer.Ordinal))
        {
            var route = routes[routeGroup.Key];
            var seededSortOrders = routeGroup.Select(seed => seed.SortOrder).ToHashSet();
            foreach (var stale in dbContext.NotificationRecipientRules.Where(item =>
                         item.NotificationRouteId == route.Id && item.IsActive))
            {
                if (!seededSortOrders.Contains(stale.SortOrder))
                {
                    stale.IsActive = false;
                }
            }
        }
    }

    private void UpsertTransitions(long versionId, PortfolioTradeSeedDefinition seedSet)
    {
        foreach (var seed in seedSet.Transitions)
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
