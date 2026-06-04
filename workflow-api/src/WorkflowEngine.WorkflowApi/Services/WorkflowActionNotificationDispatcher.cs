using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActionNotificationDispatcher(
    WorkflowDbContext dbContext,
    INotificationSender notificationSender,
    TimeProvider timeProvider) : IWorkflowActionNotificationDispatcher
{
    public WorkflowActionNotificationDispatchResult DispatchCreatedNotifications(
        string workflowInstanceId,
        string correlationId)
    {
        var pendingRows = dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(row =>
                row.WorkflowInstanceId == workflowInstanceId
                && row.CorrelationId == correlationId
                && row.Status == "Pending")
            .OrderBy(row => row.CreatedAtUtc)
            .ThenBy(row => row.Id)
            .ToList();

        if (pendingRows.Count == 0)
        {
            return WorkflowActionNotificationDispatchResult.Failed(
                $"No pending notification rows were found for workflow action correlation id '{correlationId}'.");
        }

        var delivered = new List<long>();
        foreach (var row in pendingRows)
        {
            var processingToken = Guid.NewGuid().ToString("N");
            try
            {
                var claimedAtUtc = timeProvider.GetUtcNow();
                var claimed = dbContext.NotificationOutbox
                    .Where(item => item.Id == row.Id && item.Status == "Pending")
                    .ExecuteUpdate(setters => setters
                        .SetProperty(item => item.Status, "Processing")
                        .SetProperty(item => item.ProcessingToken, processingToken)
                        .SetProperty(item => item.ProcessingStartedAtUtc, claimedAtUtc)
                        .SetProperty(item => item.LastError, (string?)null));

                if (claimed == 0)
                {
                    return WorkflowActionNotificationDispatchResult.Failed(
                        $"Notification row {row.Id} could not be claimed for SMTP delivery.");
                }

                var claimedRow = dbContext.NotificationOutbox
                    .AsNoTracking()
                    .Single(item => item.Id == row.Id && item.ProcessingToken == processingToken);

                notificationSender.SendAsync(ToDelivery(claimedRow), CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                var sentAtUtc = timeProvider.GetUtcNow();
                var markedSent = dbContext.NotificationOutbox
                    .Where(item => item.Id == row.Id && item.Status == "Processing" && item.ProcessingToken == processingToken)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(item => item.Status, "Sent")
                        .SetProperty(item => item.SentAtUtc, sentAtUtc)
                        .SetProperty(item => item.ProcessingToken, (string?)null)
                        .SetProperty(item => item.ProcessingStartedAtUtc, (DateTimeOffset?)null)
                        .SetProperty(item => item.LastError, (string?)null));

                if (markedSent == 0)
                {
                    return WorkflowActionNotificationDispatchResult.Failed(
                        $"Notification row {row.Id} was delivered but could not be marked Sent.");
                }

                delivered.Add(row.Id);
            }
            catch (Exception exception)
            {
                ReleaseClaimAfterFailure(row.Id, processingToken, exception);
                return WorkflowActionNotificationDispatchResult.Failed(
                    $"SMTP delivery failed for notification {row.Id}: {exception.Message}");
            }
        }

        return WorkflowActionNotificationDispatchResult.Delivered(delivered);
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

    private void ReleaseClaimAfterFailure(
        long id,
        string processingToken,
        Exception exception)
    {
        var message = exception.Message.Length <= 1000
            ? exception.Message
            : exception.Message[..1000];
        dbContext.NotificationOutbox
            .Where(item => item.Id == id && item.Status == "Processing" && item.ProcessingToken == processingToken)
            .ExecuteUpdate(setters => setters
                .SetProperty(item => item.Status, "Pending")
                .SetProperty(item => item.ProcessingToken, (string?)null)
                .SetProperty(item => item.ProcessingStartedAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.RetryCount, item => item.RetryCount + 1)
                .SetProperty(item => item.LastError, message));
    }
}
