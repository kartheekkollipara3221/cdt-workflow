using WorkflowEngine.Application.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Seed;

namespace WorkflowEngine.Infrastructure.Persistence.Verification;

public sealed class SeedVerificationService(WorkflowDbContext dbContext) : ISeedVerificationService
{
    public SeedVerificationResult Verify()
    {
        var errors = new List<string>();
        var definition = dbContext.WorkflowDefinitions.SingleOrDefault(row => row.WorkflowKey == PortfolioTradeSeedData.WorkflowKey);
        if (definition is null)
        {
            return new SeedVerificationResult(["Missing workflow definition 'PortfolioTrade'."]);
        }

        if (!string.Equals(
                definition.BusinessObjectType,
                PortfolioTradeSeedData.BusinessObjectType,
                StringComparison.Ordinal))
        {
            errors.Add($"Workflow definition 'PortfolioTrade' must use BusinessObjectType '{PortfolioTradeSeedData.BusinessObjectType}'.");
        }

        var version = dbContext.WorkflowDefinitionVersions.SingleOrDefault(row =>
            row.WorkflowDefinitionId == definition.Id && row.VersionNumber == PortfolioTradeSeedData.VersionNumber);
        if (version is null)
        {
            return new SeedVerificationResult(["Missing PortfolioTrade workflow definition version 1."]);
        }

        VerifyKeySet(
            errors,
            "state",
            PortfolioTradeSeedData.States.Select(row => row.Key),
            dbContext.WorkflowStateDefinitions
                .Where(row => row.WorkflowDefinitionVersionId == version.Id)
                .Select(row => row.StateKey));
        VerifyKeySet(
            errors,
            "role",
            PortfolioTradeSeedData.Roles.Select(row => row.Key),
            dbContext.WorkflowRoleDefinitions
                .Where(row => row.WorkflowDefinitionVersionId == version.Id)
                .Select(row => row.RoleKey));
        VerifyKeySet(
            errors,
            "action",
            PortfolioTradeSeedData.Actions.Select(row => row.Key),
            dbContext.WorkflowActionDefinitions
                .Where(row => row.WorkflowDefinitionVersionId == version.Id)
                .Select(row => row.ActionKey));
        VerifyKeySet(
            errors,
            "task definition",
            PortfolioTradeSeedData.Tasks.Select(row => row.Key),
            dbContext.WorkflowTaskDefinitions
                .Where(row => row.WorkflowDefinitionVersionId == version.Id)
                .Select(row => row.TaskKey));

        VerifyNoAdminDemoOverride(errors, version.Id);
        VerifyParticipantDefinitions(errors, version.Id);
        VerifyActionHints(errors, version.Id);
        VerifyTaskReferences(errors, version.Id);
        VerifySlaPolicies(errors, version.Id);
        VerifyTaskAssignmentRoles(errors, version.Id);
        VerifyNotificationRoutes(errors, version.Id);
        VerifyNotificationRecipientRules(errors, version.Id);
        VerifyTransitions(errors, version.Id);
        VerifyNoDuplicateTransitions(errors, version.Id);

        return new SeedVerificationResult(errors);
    }

    private static void VerifyKeySet(
        ICollection<string> errors,
        string name,
        IEnumerable<string> expected,
        IEnumerable<string> actual)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);

        foreach (var missing in expectedSet.Except(actualSet, StringComparer.Ordinal).Order())
        {
            errors.Add($"Missing {name} '{missing}'.");
        }

        foreach (var extra in actualSet.Except(expectedSet, StringComparer.Ordinal).Order())
        {
            errors.Add($"Unexpected {name} '{extra}'.");
        }
    }

    private void VerifyNoAdminDemoOverride(ICollection<string> errors, long versionId)
    {
        if (dbContext.WorkflowRoleDefinitions.Any(row =>
            row.WorkflowDefinitionVersionId == versionId && row.RoleKey == "AdminDemoOverride"))
        {
            errors.Add("AdminDemoOverride must not be seeded as a workflow role.");
        }

        if (dbContext.WorkflowTransitionDefinitions.Any(row =>
            row.WorkflowDefinitionVersionId == versionId && row.AllowedRoleKey == "AdminDemoOverride"))
        {
            errors.Add("AdminDemoOverride must not be seeded as a transition AllowedRoleKey.");
        }
    }

    private void VerifyParticipantDefinitions(ICollection<string> errors, long versionId)
    {
        var definitions = dbContext.WorkflowParticipantDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.ParticipantKey, StringComparer.Ordinal);
        VerifyKeySet(
            errors,
            "participant definition",
            PortfolioTradeSeedData.ParticipantDefinitions.Select(row => row.ParticipantKey),
            definitions.Keys);

        foreach (var seed in PortfolioTradeSeedData.ParticipantDefinitions)
        {
            if (!definitions.TryGetValue(seed.ParticipantKey, out var row))
            {
                continue;
            }

            if (!string.Equals(row.Cardinality, seed.Cardinality, StringComparison.Ordinal)
                || !string.Equals(row.CaptureStage, seed.CaptureStage, StringComparison.Ordinal)
                || row.IsRequired != seed.IsRequired
                || row.IsImmutable != seed.IsImmutable
                || !row.IsActive)
            {
                errors.Add($"Participant definition '{seed.ParticipantKey}' does not match required metadata.");
            }
        }
    }

    private void VerifyActionHints(ICollection<string> errors, long versionId)
    {
        var actions = dbContext.WorkflowActionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.ActionKey, StringComparer.Ordinal);

        foreach (var action in PortfolioTradeSeedData.Actions)
        {
            if (!actions.TryGetValue(action.Key, out var row))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SectionKey)
                || string.IsNullOrWhiteSpace(row.ButtonKey)
                || string.IsNullOrWhiteSpace(row.ButtonLabel))
            {
                errors.Add($"Action '{action.Key}' is missing presentation hints.");
            }
        }

        foreach (var makerActionKey in new[] { "SaveDraft", "SubmitForChecker", "ResubmitForChecker" })
        {
            if (actions.TryGetValue(makerActionKey, out var row)
                && (!string.Equals(row.ButtonKey, "MakerPrimary", StringComparison.Ordinal)
                    || !string.Equals(row.SectionKey, "Maker", StringComparison.Ordinal)))
            {
                errors.Add($"Action '{makerActionKey}' must use section 'Maker' and buttonKey 'MakerPrimary'.");
            }
        }
    }

    private void VerifyTaskReferences(ICollection<string> errors, long versionId)
    {
        var roleKeys = dbContext.WorkflowRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RoleKey)
            .ToHashSet(StringComparer.Ordinal);
        var slaKeys = dbContext.SlaPolicies
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.SlaPolicyKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var task in dbContext.WorkflowTaskDefinitions.Where(row => row.WorkflowDefinitionVersionId == versionId))
        {
            if (!roleKeys.Contains(task.OwnerRoleKey))
            {
                errors.Add($"Task definition '{task.TaskKey}' references missing owner role '{task.OwnerRoleKey}'.");
            }

            if (task.SlaPolicyKey is not null && !slaKeys.Contains(task.SlaPolicyKey))
            {
                errors.Add($"Task definition '{task.TaskKey}' references missing SLA policy '{task.SlaPolicyKey}'.");
            }
        }

        var makerRework = dbContext.WorkflowTaskDefinitions.SingleOrDefault(row =>
            row.WorkflowDefinitionVersionId == versionId && row.TaskKey == "MakerReworkTask");
        if (makerRework?.SlaPolicyKey is not null)
        {
            errors.Add("MakerReworkTask must not have an SLA policy in v1.");
        }
    }

    private void VerifyTaskAssignmentRoles(ICollection<string> errors, long versionId)
    {
        var roleKeys = dbContext.WorkflowRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RoleKey)
            .ToHashSet(StringComparer.Ordinal);
        var taskKeys = dbContext.WorkflowTaskDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.TaskKey)
            .ToHashSet(StringComparer.Ordinal);

        var expected = PortfolioTradeSeedData.TaskAssignmentRoles
            .Select(row => $"{row.TaskKey}/{row.RoleKey}");
        var actual = dbContext.WorkflowTaskAssignmentRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId && row.IsActive)
            .Select(row => new { row.TaskKey, row.RoleKey })
            .ToList()
            .Select(row => $"{row.TaskKey}/{row.RoleKey}");
        VerifyKeySet(errors, "task assignment role", expected, actual);

        foreach (var row in dbContext.WorkflowTaskAssignmentRoleDefinitions.Where(row => row.WorkflowDefinitionVersionId == versionId))
        {
            if (!taskKeys.Contains(row.TaskKey))
            {
                errors.Add($"Task assignment role references missing TaskKey '{row.TaskKey}'.");
            }

            if (!roleKeys.Contains(row.RoleKey))
            {
                errors.Add($"Task assignment role references missing RoleKey '{row.RoleKey}'.");
            }
        }
    }

    private void VerifySlaPolicies(ICollection<string> errors, long versionId)
    {
        var policies = dbContext.SlaPolicies
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToList();
        VerifyKeySet(
            errors,
            "SLA policy",
            PortfolioTradeSeedData.SlaPolicies.Select(row => row.Key),
            policies.Select(row => row.SlaPolicyKey));

        var routeKeys = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RouteKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var policy in policies)
        {
            if (policy.CalculationMode == "DurationFromTaskCreation")
            {
                if (policy.DurationMinutes is null or <= 0
                    || policy.SendDueDateReminder
                    || policy.ReminderLocalTime is not null
                    || policy.TimeZoneId is not null)
                {
                    errors.Add($"SLA policy '{policy.SlaPolicyKey}' has invalid duration-mode metadata.");
                }
            }
            else if (policy.CalculationMode == "ApplicationTargetDate")
            {
                if (policy.DurationMinutes is not null
                    || !policy.SendDueDateReminder
                    || policy.ReminderLocalTime is null
                    || string.IsNullOrWhiteSpace(policy.TimeZoneId)
                    || policy.OverdueStartOffsetDays < 0
                    || policy.RecurrenceIntervalDays is null or <= 0)
                {
                    errors.Add($"SLA policy '{policy.SlaPolicyKey}' has invalid application-target-date metadata.");
                }
            }
            else
            {
                errors.Add($"SLA policy '{policy.SlaPolicyKey}' has unknown calculation mode '{policy.CalculationMode}'.");
            }

            if (policy.DueNotificationRouteKey is not null
                && !routeKeys.Contains(policy.DueNotificationRouteKey))
            {
                errors.Add($"SLA policy '{policy.SlaPolicyKey}' references missing due notification route '{policy.DueNotificationRouteKey}'.");
            }

            if (policy.OverdueNotificationRouteKey is not null
                && !routeKeys.Contains(policy.OverdueNotificationRouteKey))
            {
                errors.Add($"SLA policy '{policy.SlaPolicyKey}' references missing overdue notification route '{policy.OverdueNotificationRouteKey}'.");
            }
        }
    }

    private void VerifyNotificationRoutes(ICollection<string> errors, long versionId)
    {
        var routes = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.RouteKey, StringComparer.Ordinal);
        VerifyKeySet(errors, "notification route", PortfolioTradeSeedData.NotificationRoutes.Select(row => row.Key), routes.Keys);

        foreach (var seed in PortfolioTradeSeedData.NotificationRoutes)
        {
            if (!routes.TryGetValue(seed.Key, out var row))
            {
                continue;
            }

            if (!string.Equals(row.NotificationType, seed.NotificationType, StringComparison.Ordinal)
                || !string.Equals(row.SubjectTemplate, seed.SubjectTemplate, StringComparison.Ordinal)
                || !string.Equals(row.BodyTemplate, seed.BodyTemplate, StringComparison.Ordinal)
                || !string.Equals(row.HtmlBodyTemplate, seed.HtmlBodyTemplate, StringComparison.Ordinal)
                || !string.Equals(row.OverdueSubjectPrefix, seed.OverdueSubjectPrefix, StringComparison.Ordinal))
            {
                errors.Add($"Notification route '{seed.Key}' does not match required template metadata.");
            }
        }
    }

    private void VerifyNotificationRecipientRules(ICollection<string> errors, long versionId)
    {
        var routes = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToDictionary(row => row.RouteKey, StringComparer.Ordinal);
        var routeKeysById = routes.Values.ToDictionary(row => row.Id, row => row.RouteKey);
        var rules = dbContext.NotificationRecipientRules
            .Where(row => routeKeysById.Keys.Contains(row.NotificationRouteId))
            .ToList();

        var actualKeys = rules.Select(row => $"{routeKeysById[row.NotificationRouteId]}/{row.SortOrder}");
        var expectedKeys = PortfolioTradeSeedData.NotificationRecipientRules
            .Select(row => $"{row.RouteKey}/{row.SortOrder}");
        VerifyKeySet(errors, "notification recipient rule", expectedKeys, actualKeys);

        var participantKeys = dbContext.WorkflowParticipantDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId && row.IsActive)
            .Select(row => row.ParticipantKey)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var seed in PortfolioTradeSeedData.NotificationRecipientRules)
        {
            if (!routes.TryGetValue(seed.RouteKey, out var route))
            {
                continue;
            }

            var row = rules.SingleOrDefault(item =>
                item.NotificationRouteId == route.Id && item.SortOrder == seed.SortOrder);
            if (row is null)
            {
                continue;
            }

            if (!string.Equals(row.RecipientKind, seed.RecipientKind, StringComparison.Ordinal)
                || !string.Equals(row.SourceType, seed.SourceType, StringComparison.Ordinal)
                || !string.Equals(row.SourceKey, seed.SourceKey, StringComparison.Ordinal)
                || !string.Equals(row.StaticAddress, seed.StaticAddress, StringComparison.Ordinal)
                || row.IsRequired != seed.IsRequired
                || !row.IsActive)
            {
                errors.Add($"Notification recipient rule '{seed.RouteKey}/{seed.SortOrder}' does not match required metadata.");
            }

            if (row.SourceType == "StaticAddress")
            {
                if (row.StaticAddress is null || row.SourceKey is not null)
                {
                    errors.Add($"Static recipient rule '{seed.RouteKey}/{seed.SortOrder}' has inconsistent source fields.");
                }
            }
            else
            {
                if (row.SourceKey is null || row.StaticAddress is not null)
                {
                    errors.Add($"Dynamic recipient rule '{seed.RouteKey}/{seed.SortOrder}' has inconsistent source fields.");
                }

                if (row.SourceType == "WorkflowParticipant"
                    && row.SourceKey is not null
                    && !participantKeys.Contains(row.SourceKey))
                {
                    errors.Add($"Recipient rule '{seed.RouteKey}/{seed.SortOrder}' references missing participant '{row.SourceKey}'.");
                }
            }
        }

        var transitionRouteKeys = dbContext.WorkflowTransitionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId && row.NotificationRouteKey != null)
            .Select(row => row.NotificationRouteKey!)
            .Distinct()
            .ToList();
        foreach (var routeKey in transitionRouteKeys)
        {
            if (!routes.TryGetValue(routeKey, out var route)
                || !rules.Any(row =>
                    row.NotificationRouteId == route.Id
                    && row.IsActive
                    && row.RecipientKind == "To"))
            {
                errors.Add($"Notification route '{routeKey}' must have at least one active To recipient rule.");
            }
        }
    }

    private void VerifyTransitions(ICollection<string> errors, long versionId)
    {
        var states = dbContext.WorkflowStateDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.StateKey)
            .ToHashSet(StringComparer.Ordinal);
        var actions = dbContext.WorkflowActionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.ActionKey)
            .ToHashSet(StringComparer.Ordinal);
        var roles = dbContext.WorkflowRoleDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RoleKey)
            .ToHashSet(StringComparer.Ordinal);
        var tasks = dbContext.WorkflowTaskDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.TaskKey)
            .ToHashSet(StringComparer.Ordinal);
        var routes = dbContext.NotificationRoutes
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .Select(row => row.RouteKey)
            .ToHashSet(StringComparer.Ordinal);
        var transitions = dbContext.WorkflowTransitionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .ToList();

        foreach (var seed in PortfolioTradeSeedData.Transitions)
        {
            var row = transitions.SingleOrDefault(item =>
                item.FromStateKey == seed.FromStateKey
                && item.ActionKey == seed.ActionKey
                && item.AllowedRoleKey == seed.AllowedRoleKey);
            if (row is null)
            {
                errors.Add($"Missing transition {seed.FromStateKey} -> {seed.ActionKey} -> {seed.AllowedRoleKey}.");
                continue;
            }

            if (!string.Equals(row.ToStateKey, seed.ToStateKey, StringComparison.Ordinal)
                || !string.Equals(row.TaskToCloseKey, seed.TaskToCloseKey, StringComparison.Ordinal)
                || !string.Equals(row.TaskToCreateKey, seed.TaskToCreateKey, StringComparison.Ordinal)
                || !string.Equals(row.NotificationRouteKey, seed.NotificationRouteKey, StringComparison.Ordinal))
            {
                errors.Add($"Transition {seed.FromStateKey} -> {seed.ActionKey} -> {seed.AllowedRoleKey} does not match the required matrix.");
            }
        }

        foreach (var row in transitions)
        {
            if (!states.Contains(row.FromStateKey))
            {
                errors.Add($"Transition references missing FromStateKey '{row.FromStateKey}'.");
            }

            if (!states.Contains(row.ToStateKey))
            {
                errors.Add($"Transition references missing ToStateKey '{row.ToStateKey}'.");
            }

            if (!actions.Contains(row.ActionKey))
            {
                errors.Add($"Transition references missing ActionKey '{row.ActionKey}'.");
            }

            if (!roles.Contains(row.AllowedRoleKey))
            {
                errors.Add($"Transition references missing AllowedRoleKey '{row.AllowedRoleKey}'.");
            }

            if (row.TaskToCloseKey is not null && !tasks.Contains(row.TaskToCloseKey))
            {
                errors.Add($"Transition references missing TaskToCloseKey '{row.TaskToCloseKey}'.");
            }

            if (row.TaskToCreateKey is not null && !tasks.Contains(row.TaskToCreateKey))
            {
                errors.Add($"Transition references missing TaskToCreateKey '{row.TaskToCreateKey}'.");
            }

            if (row.NotificationRouteKey is not null && !routes.Contains(row.NotificationRouteKey))
            {
                errors.Add($"Transition references missing NotificationRouteKey '{row.NotificationRouteKey}'.");
            }
        }

        if (transitions.Count != PortfolioTradeSeedData.Transitions.Length)
        {
            errors.Add($"Portfolio Trade transition count is {transitions.Count}; expected {PortfolioTradeSeedData.Transitions.Length}.");
        }
    }

    private void VerifyNoDuplicateTransitions(ICollection<string> errors, long versionId)
    {
        var duplicates = dbContext.WorkflowTransitionDefinitions
            .Where(row => row.WorkflowDefinitionVersionId == versionId)
            .GroupBy(row => new { row.FromStateKey, row.ActionKey, row.AllowedRoleKey })
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.FromStateKey}/{group.Key.ActionKey}/{group.Key.AllowedRoleKey}")
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate transition key found: {duplicate}.");
        }
    }
}
