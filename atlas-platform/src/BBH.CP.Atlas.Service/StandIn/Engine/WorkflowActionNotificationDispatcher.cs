// STAND-IN: do not copy to the target. A copy of WorkflowEngine.WorkflowApi.Services.WorkflowActionNotificationDispatcher
// (workflow-api) with the namespace of the target engine copy. The target registers its own implementation.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;
using WorkflowEngine.Infrastructure.Notifications;
using WorkflowEngine.Application.WorkflowNotification;

namespace WorkflowEngine.Infrastructure.WorkflowNotification;

public sealed class WorkflowActionNotificationDispatcher(
    WorkflowDbContext dbContext,
    INotificationSender notificationSender,
    TimeProvider timeProvider) : IWorkflowActionNotificationDispatcher
{
    private readonly OutboxProcessingOptions options = new();

    public WorkflowActionNotificationDispatcher(WorkflowDbContext dbContext, INotificationSender notificationSender,
        TimeProvider timeProvider, OutboxProcessingOptions options)
        : this(dbContext, notificationSender, timeProvider)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public WorkflowActionNotificationDispatchResult DispatchCreatedNotifications(
        string workflowInstanceId,
        string correlationId)
    {
        var now = timeProvider.GetUtcNow();
        if (dbContext.NotificationOutbox.Any(row => row.WorkflowInstanceId == workflowInstanceId
            && row.CorrelationId == correlationId && row.Status == "Pending" && row.NextAttemptAtUtc > now))
        {
            return WorkflowActionNotificationDispatchResult.Failed(
                $"Notification delivery is deferred until its scheduled retry for workflow action correlation id '{correlationId}'.");
        }
        var pendingRows = dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(row =>
                row.WorkflowInstanceId == workflowInstanceId
                && row.CorrelationId == correlationId
                && row.Status == "Pending"
                && (row.NextAttemptAtUtc == null || row.NextAttemptAtUtc <= now))
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
                    .Where(item => item.Id == row.Id && item.Status == "Pending"
                        && (item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= claimedAtUtc))
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
                        .SetProperty(item => item.NextAttemptAtUtc, (DateTimeOffset?)null)
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
        if (string.IsNullOrWhiteSpace(row.NotificationRecipientsJson))
        {
            throw new InvalidOperationException($"Notification {row.Id} recipient snapshot is missing.");
        }

        NotificationRecipients? recipients;
        try
        {
            recipients = JsonSerializer.Deserialize<NotificationRecipients>(
                row.NotificationRecipientsJson,
                WorkflowJson.Options);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Notification {row.Id} recipient snapshot is invalid.",
                exception);
        }

        if (recipients is null
            || recipients.ToEmailAddresses is null
            || recipients.CcEmailAddresses is null)
        {
            throw new InvalidOperationException($"Notification {row.Id} recipient snapshot is invalid.");
        }

        return new NotificationDelivery(
            row.Id,
            row.WorkflowInstanceId!,
            row.WorkflowTaskId,
            row.BusinessObjectType,
            row.BusinessObjectId,
            row.NotificationType,
            recipients.ToEmailAddresses,
            recipients.CcEmailAddresses,
            row.Subject,
            row.Body,
            row.HtmlBody,
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
        var retryCount = dbContext.NotificationOutbox
            .Where(item => item.Id == id && item.Status == "Processing" && item.ProcessingToken == processingToken)
            .Select(item => (int?)item.RetryCount).SingleOrDefault();
        if (retryCount is null)
            return;
        var nextAttempt = options.GetNextAttemptAtUtc(retryCount.Value, timeProvider.GetUtcNow());
        dbContext.NotificationOutbox
            .Where(item => item.Id == id && item.Status == "Processing" && item.ProcessingToken == processingToken)
            .ExecuteUpdate(setters => setters
                .SetProperty(item => item.Status, "Pending")
                .SetProperty(item => item.ProcessingToken, (string?)null)
                .SetProperty(item => item.ProcessingStartedAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.NextAttemptAtUtc, nextAttempt)
                .SetProperty(item => item.RetryCount, item => item.RetryCount < int.MaxValue ? item.RetryCount + 1 : item.RetryCount)
                .SetProperty(item => item.LastError, message));
    }
}
