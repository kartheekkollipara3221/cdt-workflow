namespace WorkflowEngine.Infrastructure.WorkflowInspection;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Inspection;
using WorkflowEngine.Infrastructure.Persistence;

public sealed class WorkflowInspectionReader(WorkflowDbContext dbContext) : IWorkflowInspectionReader
{
    public WorkflowInspectionSnapshot Read(string workflowInstanceId)
    {
        var instance = (
            from workflowInstance in dbContext.WorkflowInstances.AsNoTracking()
            join version in dbContext.WorkflowDefinitionVersions.AsNoTracking()
                on workflowInstance.WorkflowDefinitionVersionId equals version.Id
            where workflowInstance.WorkflowInstanceId == workflowInstanceId
            select new WorkflowInstanceInspection(
                workflowInstance.WorkflowInstanceId,
                workflowInstance.WorkflowKey,
                version.VersionNumber,
                workflowInstance.BusinessObjectType,
                workflowInstance.BusinessObjectId,
                workflowInstance.CurrentStateKey,
                workflowInstance.Status,
                workflowInstance.StartedAtUtc,
                workflowInstance.StartedByUserId,
                workflowInstance.CompletedAtUtc,
                workflowInstance.Version))
            .SingleOrDefault();

        if (instance is null)
        {
            return new WorkflowInspectionSnapshot(null, [], [], []);
        }

        var tasks = dbContext.WorkflowTasks
            .AsNoTracking()
            .Where(task => task.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id)
            .Select(task => new WorkflowTaskInspection(
                task.Id,
                task.TaskKey,
                task.AssignedRoleKey,
                task.AssignedUserId,
                task.Status,
                task.CreatedAtUtc,
                task.DueAtUtc,
                task.CompletedAtUtc,
                task.CompletedByUserId,
                task.ClosedByActionKey))
            .ToList();

        var history = dbContext.WorkflowAudits
            .AsNoTracking()
            .Where(audit => audit.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(audit => audit.CreatedAtUtc)
            .ThenBy(audit => audit.Id)
            .Select(audit => new WorkflowAuditInspection(
                audit.Id,
                audit.ActionKey,
                audit.FromStateKey,
                audit.ToStateKey,
                audit.PerformedByUserId,
                audit.PerformedByRoleKey,
                audit.Reason,
                audit.CorrelationId,
                audit.CreatedAtUtc))
            .ToList();

        var notificationRows = dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(notification => notification.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(notification => notification.CreatedAtUtc)
            .ThenBy(notification => notification.Id)
            .ToList();
        var notifications = notificationRows
            .Select(notification =>
            {
                var recipientSnapshot = ReadRecipients(notification.NotificationRecipientsJson);

                return new NotificationInspection(
                    notification.Id,
                    notification.WorkflowTaskId,
                    notification.NotificationType,
                    recipientSnapshot.Recipients.ToEmailAddresses,
                    recipientSnapshot.Recipients.CcEmailAddresses,
                    recipientSnapshot.HasInvalidRecipientSnapshot,
                    notification.Subject,
                    notification.Status,
                    notification.CorrelationId,
                    notification.CreatedAtUtc,
                    notification.SentAtUtc);
            })
            .ToList();

        return new WorkflowInspectionSnapshot(instance, tasks, history, notifications);
    }

    private static RecipientSnapshotInspection ReadRecipients(string notificationRecipientsJson)
    {
        if (string.IsNullOrWhiteSpace(notificationRecipientsJson))
        {
            return RecipientSnapshotInspection.Invalid;
        }

        try
        {
            var recipients = JsonSerializer.Deserialize<NotificationRecipients>(
                notificationRecipientsJson,
                WorkflowJson.Options);

            return recipients is null
                || recipients.ToEmailAddresses is null
                || recipients.CcEmailAddresses is null
                ? RecipientSnapshotInspection.Invalid
                : new RecipientSnapshotInspection(recipients, false);
        }
        catch (JsonException)
        {
            return RecipientSnapshotInspection.Invalid;
        }
    }

    private sealed record RecipientSnapshotInspection(
        NotificationRecipients Recipients,
        bool HasInvalidRecipientSnapshot)
    {
        public static RecipientSnapshotInspection Invalid => new(new NotificationRecipients(), true);
    }
}
