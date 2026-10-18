using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Sla;

// Resolves the recipients of an SLA reminder route when its schedule is created.
// A route with any rule row (active or inactive) owns its recipients. A route
// without rule rows keeps the task snapshot, so the result is null for it.
internal sealed class SlaReminderAudienceResolver(
    WorkflowDbContext dbContext,
    NotificationRecipientResolver recipientResolver)
{
    public SlaReminderAudience Resolve(
        long workflowDefinitionVersionId,
        string workflowInstanceId,
        SlaPolicy policy,
        string? assignedRoleKey)
    {
        var errors = new List<string>();
        var dueRecipientsJson = policy.SendDueDateReminder
            ? ResolveRoute(workflowDefinitionVersionId, workflowInstanceId, policy.DueNotificationRouteKey, assignedRoleKey, errors)
            : null;
        var overdueRecipientsJson = ResolveRoute(
            workflowDefinitionVersionId,
            workflowInstanceId,
            policy.OverdueNotificationRouteKey,
            assignedRoleKey,
            errors);
        return new SlaReminderAudience(dueRecipientsJson, overdueRecipientsJson, errors);
    }

    private string? ResolveRoute(
        long workflowDefinitionVersionId,
        string workflowInstanceId,
        string? routeKey,
        string? assignedRoleKey,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(routeKey))
        {
            return null;
        }

        // A missing or inactive route is reported by the SLA scan, as before.
        var route = dbContext.NotificationRoutes
            .AsNoTracking()
            .SingleOrDefault(row =>
                row.WorkflowDefinitionVersionId == workflowDefinitionVersionId
                && row.RouteKey == routeKey
                && row.IsActive);
        if (route is null)
        {
            return null;
        }

        var rules = dbContext.NotificationRecipientRules
            .AsNoTracking()
            .Where(row => row.NotificationRouteId == route.Id)
            .ToList();
        if (rules.Count == 0)
        {
            return null;
        }

        if (rules.Any(row => row.IsActive && row.SourceType == RecipientSourceTypes.RequestInput))
        {
            errors.Add($"SLA reminder route '{routeKey}' must not use RequestInput recipient rules.");
            return null;
        }

        var resolution = recipientResolver.Resolve(route.Id, workflowInstanceId, null, assignedRoleKey);
        if (resolution.Errors.Count > 0)
        {
            errors.AddRange(resolution.Errors.Select(error => $"SLA reminder route '{routeKey}': {error}"));
            return null;
        }

        return JsonSerializer.Serialize(resolution.Recipients, WorkflowJson.Options);
    }
}

internal sealed record SlaReminderAudience(
    string? DueRecipientsJson,
    string? OverdueRecipientsJson,
    IReadOnlyList<string> Errors);
