using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class ProcessNotificationOutboxService(
    DatabaseFirstWorkflowStore store,
    INotificationSender notificationSender,
    TimeProvider timeProvider) : IProcessNotificationOutboxService
{
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    public async Task<ProcessNotificationOutboxResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var leaseCutoff = timeProvider.GetUtcNow().Subtract(ProcessingLease);
        var pendingRows = store.GetClaimableNotifications(leaseCutoff);

        var processed = new List<ProcessedNotification>();
        var warnings = new List<string>();

        for (var index = 0; index < pendingRows.Count; index++)
        {
            var row = pendingRows[index];
            var processingToken = Guid.NewGuid().ToString("N");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var claimedAtUtc = timeProvider.GetUtcNow();
                var claimed = store.ClaimNotification(row.Id, processingToken, claimedAtUtc, leaseCutoff);

                if (claimed == 0)
                {
                    warnings.Add($"row {row.Id} was already claimed by another process");
                    continue;
                }

                var claimedRow = store.GetClaimedNotification(row.Id, processingToken)
                    ?? throw new InvalidOperationException($"Notification outbox row {row.Id} was claimed but could not be re-read.");

                await notificationSender.SendAsync(ToDelivery(claimedRow), cancellationToken);
                var sentAtUtc = timeProvider.GetUtcNow();
                var markedSent = store.MarkNotificationSent(row.Id, processingToken, sentAtUtc);

                if (markedSent == 0)
                {
                    warnings.Add($"row {row.Id} was claimed but could not be marked Sent");
                    continue;
                }

                processed.Add(new ProcessedNotification(
                    row.Id,
                    row.NotificationType,
                    row.RecipientRoleKey,
                    row.Subject,
                    row.WorkflowInstanceId));
            }
            catch (Exception exception)
            {
                ReleaseClaimAfterFailure(row.Id, processingToken, exception);
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

    private void ReleaseClaimAfterFailure(
        long id,
        string processingToken,
        Exception exception)
    {
        var message = exception.Message.Length <= 1000
            ? exception.Message
            : exception.Message[..1000];
        store.ReleaseNotificationClaimAfterFailure(id, processingToken, message);
    }
}
