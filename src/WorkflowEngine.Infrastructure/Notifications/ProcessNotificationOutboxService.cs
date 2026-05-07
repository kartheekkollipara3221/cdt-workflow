using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class ProcessNotificationOutboxService(
    WorkflowDbContext dbContext,
    INotificationSender notificationSender,
    TimeProvider timeProvider) : IProcessNotificationOutboxService
{
    public async Task<ProcessNotificationOutboxResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var pendingRows = await dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(row => row.Status == "Pending")
            .OrderBy(row => row.CreatedAtUtc)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

        var processed = new List<ProcessedNotification>();
        var warnings = new List<string>();

        for (var index = 0; index < pendingRows.Count; index++)
        {
            var row = pendingRows[index];
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await notificationSender.SendAsync(ToDelivery(row), cancellationToken);
                var sentAtUtc = timeProvider.GetUtcNow();
                var affected = await dbContext.NotificationOutbox
                    .Where(item => item.Id == row.Id && item.Status == "Pending")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, "Sent")
                        .SetProperty(item => item.SentAtUtc, sentAtUtc), cancellationToken);

                if (affected == 0)
                {
                    warnings.Add($"row {row.Id} was already marked Sent by another process");
                }
                else
                {
                    processed.Add(new ProcessedNotification(
                        row.Id,
                        row.NotificationType,
                        row.RecipientRoleKey,
                        row.Subject,
                        row.WorkflowInstanceId));
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new ProcessNotificationOutboxResult(
                    notificationSender.SenderName,
                    pendingRows.Count,
                    processed,
                    warnings,
                    new NotificationOutboxFailure(
                        row.Id,
                        row.NotificationType,
                        row.RecipientRoleKey,
                        row.Subject,
                        row.WorkflowInstanceId,
                        exception));
            }
        }

        return new ProcessNotificationOutboxResult(
            notificationSender.SenderName,
            pendingRows.Count,
            processed,
            warnings,
            null);
    }

    private static NotificationDelivery ToDelivery(NotificationOutbox row)
    {
        return new NotificationDelivery(
            row.Id,
            row.WorkflowInstanceId,
            row.WorkflowTaskId,
            row.BusinessObjectType,
            row.BusinessObjectId,
            row.NotificationType,
            row.RecipientRoleKey,
            row.RecipientUserId,
            row.Subject,
            row.PayloadJson,
            row.Status,
            row.CorrelationId,
            row.CreatedAtUtc);
    }
}
