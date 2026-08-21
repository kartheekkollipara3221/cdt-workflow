using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class NotificationRecipientResolver(WorkflowDbContext dbContext)
{
    public NotificationRecipientResolution Resolve(
        long notificationRouteId,
        string? workflowInstanceId,
        WorkflowNotificationInput? notificationInput,
        string? assignedRoleKey = null)
    {
        var input = notificationInput ?? new WorkflowNotificationInput();
        var rules = dbContext.NotificationRecipientRules
            .AsNoTracking()
            .Where(row => row.NotificationRouteId == notificationRouteId && row.IsActive)
            .ToList()
            .Where(row =>
                row.AssignedRoleKey is null
                || string.Equals(row.AssignedRoleKey, assignedRoleKey, StringComparison.Ordinal))
            .OrderBy(row => row.SortOrder)
            .ToList();
        var errors = new List<string>();

        if (!dbContext.NotificationRoutes.Any(row => row.Id == notificationRouteId))
        {
            errors.Add($"Notification route '{notificationRouteId}' was not found.");
        }

        var allowedRequestKeys = rules
            .Where(row => row.SourceType == RecipientSourceTypes.RequestInput && row.SourceKey != null)
            .Select(row => row.SourceKey!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var unexpectedKey in input.RecipientInputs.Keys
                     .Where(key => !allowedRequestKeys.Contains(key))
                     .OrderBy(key => key, StringComparer.Ordinal))
        {
            errors.Add($"Recipient input '{unexpectedKey}' is not declared by the notification route.");
        }

        var participantKeys = rules
            .Where(row => row.SourceType == RecipientSourceTypes.WorkflowParticipant && row.SourceKey != null)
            .Select(row => row.SourceKey!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var participants = string.IsNullOrWhiteSpace(workflowInstanceId)
            ? new Dictionary<string, List<WorkflowParticipant>>(StringComparer.Ordinal)
            : dbContext.WorkflowParticipants
                .AsNoTracking()
                .Where(row =>
                    row.WorkflowInstanceId == workflowInstanceId
                    && participantKeys.Contains(row.ParticipantKey))
                .OrderBy(row => row.SortOrder)
                .ToList()
                .GroupBy(row => row.ParticipantKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var rawTo = new List<string>();
        var rawCc = new List<string>();
        foreach (var rule in rules)
        {
            var values = ResolveValues(rule, input, participants);
            if (rule.IsRequired && values.Count == 0)
            {
                errors.Add($"Required recipient source '{rule.SourceKey ?? rule.StaticAddress}' is missing.");
            }

            var target = rule.RecipientKind == RecipientKinds.To ? rawTo : rawCc;
            target.AddRange(values);
        }

        var finalTo = Deduplicate(rawTo);
        var toSet = finalTo.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var finalCc = Deduplicate(rawCc)
            .Where(address => !toSet.Contains(address))
            .ToList();
        if (finalTo.Count == 0)
        {
            errors.Add("Notification recipient resolution produced no To recipient.");
        }

        return new NotificationRecipientResolution(
            new NotificationRecipients
            {
                ToEmailAddresses = finalTo,
                CcEmailAddresses = finalCc
            },
            errors);
    }

    private static List<string> ResolveValues(
        NotificationRecipientRule rule,
        WorkflowNotificationInput input,
        IReadOnlyDictionary<string, List<WorkflowParticipant>> participants)
    {
        return rule.SourceType switch
        {
            RecipientSourceTypes.StaticAddress => rule.StaticAddress is null
                ? []
                : [rule.StaticAddress],
            RecipientSourceTypes.WorkflowParticipant =>
                rule.SourceKey is not null && participants.TryGetValue(rule.SourceKey, out var stored)
                    ? stored.Select(row => row.EmailAddress ?? string.Empty).ToList()
                    : [],
            RecipientSourceTypes.RequestInput =>
                rule.SourceKey is not null && input.RecipientInputs.TryGetValue(rule.SourceKey, out var supplied)
                    ? supplied.Select(row => row.EmailAddress ?? string.Empty).ToList()
                    : [],
            _ => []
        };
    }

    private static List<string> Deduplicate(IEnumerable<string> addresses)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var address in addresses)
        {
            if (seen.Add(address))
            {
                result.Add(address);
            }
        }

        return result;
    }
}

public sealed record NotificationRecipientResolution(
    NotificationRecipients Recipients,
    IReadOnlyList<string> Errors);
