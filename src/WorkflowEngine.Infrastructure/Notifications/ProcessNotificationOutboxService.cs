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
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(15);

    public async Task<ProcessNotificationOutboxResult> ProcessAsync(CancellationToken cancellationToken)
    {
        var leaseCutoff = timeProvider.GetUtcNow().Subtract(ProcessingLease);
        var pendingRows = await dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(row =>
                row.Status == "Pending"
                || (row.Status == "Processing"
                    && row.ProcessingStartedAtUtc != null
                    && row.ProcessingStartedAtUtc <= leaseCutoff))
            .OrderBy(row => row.CreatedAtUtc)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

        var processed = new List<ProcessedNotification>();
        var warnings = new List<string>();

        if (pendingRows.Count == 0)
        {
            var activeClaimedRows = await dbContext.NotificationOutbox
                .AsNoTracking()
                .Where(row =>
                    row.Status == "Processing"
                    && row.ProcessingStartedAtUtc != null
                    && row.ProcessingStartedAtUtc > leaseCutoff)
                .OrderBy(row => row.CreatedAtUtc)
                .ThenBy(row => row.Id)
                .Select(row => row.Id)
                .ToListAsync(cancellationToken);

            warnings.AddRange(activeClaimedRows.Select(id => $"row {id} was already claimed by another process"));
        }

        for (var index = 0; index < pendingRows.Count; index++)
        {
            var row = pendingRows[index];
            var processingToken = Guid.NewGuid().ToString("N");
            try
            {
                var claimedAtUtc = timeProvider.GetUtcNow();
                var claimed = await dbContext.NotificationOutbox
                    .Where(item =>
                        item.Id == row.Id
                        && (item.Status == "Pending"
                            || (item.Status == "Processing"
                                && item.ProcessingStartedAtUtc != null
                                && item.ProcessingStartedAtUtc <= leaseCutoff)))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, "Processing")
                        .SetProperty(item => item.ProcessingToken, processingToken)
                        .SetProperty(item => item.ProcessingStartedAtUtc, claimedAtUtc)
                        .SetProperty(item => item.LastError, (string?)null), cancellationToken);

                if (claimed == 0)
                {
                    warnings.Add($"row {row.Id} was already claimed by another process");
                    continue;
                }

                var claimedRow = await dbContext.NotificationOutbox
                    .AsNoTracking()
                    .SingleAsync(item => item.Id == row.Id && item.ProcessingToken == processingToken, cancellationToken);

                await notificationSender.SendAsync(ToDelivery(claimedRow), cancellationToken);
                var sentAtUtc = timeProvider.GetUtcNow();
                var markedSent = await dbContext.NotificationOutbox
                    .Where(item => item.Id == row.Id && item.Status == "Processing" && item.ProcessingToken == processingToken)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, "Sent")
                        .SetProperty(item => item.SentAtUtc, sentAtUtc)
                        .SetProperty(item => item.ProcessingToken, (string?)null)
                        .SetProperty(item => item.ProcessingStartedAtUtc, (DateTimeOffset?)null)
                        .SetProperty(item => item.LastError, (string?)null), cancellationToken);

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
                await ReleaseClaimAfterFailureAsync(row.Id, processingToken, exception, cancellationToken);
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
            row.Body,
            row.PayloadJson,
            row.Status,
            row.CorrelationId,
            row.CreatedAtUtc);
    }

    private async Task ReleaseClaimAfterFailureAsync(
        long id,
        string processingToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = exception.Message.Length <= 1000
            ? exception.Message
            : exception.Message[..1000];
        await dbContext.NotificationOutbox
            .Where(item => item.Id == id && item.Status == "Processing" && item.ProcessingToken == processingToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "Pending")
                .SetProperty(item => item.ProcessingToken, (string?)null)
                .SetProperty(item => item.ProcessingStartedAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.RetryCount, item => item.RetryCount + 1)
                .SetProperty(item => item.LastError, message), CancellationToken.None);
    }
}
