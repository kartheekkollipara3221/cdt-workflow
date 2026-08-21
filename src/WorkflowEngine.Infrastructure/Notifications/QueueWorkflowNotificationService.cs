using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Notifications;

public sealed class QueueWorkflowNotificationService(
    WorkflowDbContext dbContext,
    NotificationRecipientResolver recipientResolver,
    NotificationTemplateRenderer templateRenderer,
    TimeProvider timeProvider) : IQueueWorkflowNotificationService
{
    public async Task<QueueWorkflowNotificationResult> QueueAsync(
        QueueWorkflowNotificationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var replay = await FindReplayAsync(request.NotificationOccurrenceKey, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var route = await (
            from definition in dbContext.WorkflowDefinitions.AsNoTracking()
            join version in dbContext.WorkflowDefinitionVersions.AsNoTracking()
                on definition.Id equals version.WorkflowDefinitionId
            join candidate in dbContext.NotificationRoutes.AsNoTracking()
                on version.Id equals candidate.WorkflowDefinitionVersionId
            where definition.WorkflowKey == request.WorkflowKey
                && definition.IsActive
                && version.VersionNumber == request.WorkflowDefinitionVersion
                && version.IsActive
                && candidate.RouteKey == request.NotificationRouteKey
                && candidate.IsActive
            select candidate)
            .SingleOrDefaultAsync(cancellationToken);
        if (route is null)
        {
            return Failure(
                "NotFound",
                $"Active notification route '{request.NotificationRouteKey}' was not found for workflow '{request.WorkflowKey}' version {request.WorkflowDefinitionVersion}.");
        }

        var recipientResolution = recipientResolver.Resolve(
            route.Id,
            workflowInstanceId: null,
            request.NotificationInput);
        if (recipientResolution.Errors.Count > 0)
        {
            return Failure("ValidationFailed", recipientResolution.Errors);
        }

        var values = request.NotificationInput.TemplateData;
        var subject = templateRenderer.Render(route.SubjectTemplate, values, "notificationInput.templateData");
        var body = templateRenderer.Render(route.BodyTemplate, values, "notificationInput.templateData");
        var htmlBody = templateRenderer.RenderHtml(route.HtmlBodyTemplate, values, "notificationInput.templateData");
        var templateErrors = subject.Errors
            .Concat(body.Errors)
            .Concat(htmlBody.Errors)
            .Select(error => $"{error.Field}: {error.Message}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (templateErrors.Length > 0)
        {
            return Failure("ValidationFailed", templateErrors);
        }

        var row = new NotificationOutbox
        {
            WorkflowInstanceId = null,
            NotificationOccurrenceKey = request.NotificationOccurrenceKey,
            BusinessObjectType = request.BusinessObjectType,
            BusinessObjectId = request.BusinessObjectId,
            NotificationType = route.NotificationType,
            NotificationRecipientsJson = JsonSerializer.Serialize(recipientResolution.Recipients, WorkflowJson.Options),
            Subject = subject.Rendered,
            Body = body.Rendered,
            HtmlBody = htmlBody.Rendered,
            PayloadJson = JsonSerializer.Serialize(values, WorkflowJson.Options),
            Status = "Pending",
            CorrelationId = request.CorrelationId,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
        dbContext.NotificationOutbox.Add(row);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOccurrenceKeyUniqueViolation(exception))
        {
            dbContext.Entry(row).State = EntityState.Detached;
            var concurrentReplay = await FindReplayAsync(request.NotificationOccurrenceKey, cancellationToken);
            if (concurrentReplay is not null)
            {
                return concurrentReplay;
            }

            throw;
        }

        return new QueueWorkflowNotificationResult(true, "Success", row.Id, false, []);
    }

    private async Task<QueueWorkflowNotificationResult?> FindReplayAsync(
        string occurrenceKey,
        CancellationToken cancellationToken)
    {
        var existingId = await dbContext.NotificationOutbox
            .AsNoTracking()
            .Where(row => row.NotificationOccurrenceKey == occurrenceKey)
            .Select(row => (long?)row.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return existingId is null
            ? null
            : new QueueWorkflowNotificationResult(true, "Success", existingId, true, []);
    }

    private static QueueWorkflowNotificationResult Failure(string resultCode, params string[] errors)
    {
        return new QueueWorkflowNotificationResult(false, resultCode, null, false, errors);
    }

    private static QueueWorkflowNotificationResult Failure(string resultCode, IReadOnlyList<string> errors)
    {
        return new QueueWorkflowNotificationResult(false, resultCode, null, false, errors);
    }

    private static bool IsOccurrenceKeyUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error =>
                (error.Number == 2601 || error.Number == 2627)
                && error.Message.Contains(
                    "UX_NotificationOutbox_NotificationOccurrenceKey",
                    StringComparison.OrdinalIgnoreCase));
    }
}
