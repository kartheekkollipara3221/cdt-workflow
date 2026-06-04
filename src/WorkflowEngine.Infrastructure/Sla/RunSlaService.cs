using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Sla;
using WorkflowEngine.Infrastructure.Persistence;
using WorkflowEngine.Infrastructure.Persistence.Entities;

namespace WorkflowEngine.Infrastructure.Sla;

public sealed class RunSlaService(
    WorkflowDbContext dbContext,
    NotificationTemplateRenderer templateRenderer) : IRunSlaService
{
    public async Task<RunSlaResult> RunAsync(RunSlaRequest request, CancellationToken cancellationToken)
    {
        var now = request.Now.ToUniversalTime();
        var baseQuery =
            from task in dbContext.WorkflowTasks.AsNoTracking()
            join instance in dbContext.WorkflowInstances.AsNoTracking()
                on task.WorkflowInstanceId equals instance.WorkflowInstanceId
            where task.Status == "Pending"
                && task.DueAtUtc != null
                && instance.Status == "Active"
            select new
            {
                Task = task,
                Instance = instance
            };

        var pendingTasksWithSla = await baseQuery
            .CountAsync(cancellationToken);

        var alreadyQueued = await baseQuery
            .Where(row => row.Task.DueAtUtc < now)
            .Where(row => dbContext.NotificationOutbox.Any(outbox =>
                outbox.WorkflowTaskId == row.Task.Id
                && outbox.NotificationType == "SlaTaskOverdue"))
            .CountAsync(cancellationToken);

        var overdueRows = await baseQuery
            .Where(row => row.Task.DueAtUtc < now)
            .Where(row => !dbContext.NotificationOutbox.Any(outbox =>
                outbox.WorkflowTaskId == row.Task.Id
                && outbox.NotificationType == "SlaTaskOverdue"))
            .OrderBy(row => row.Task.DueAtUtc)
            .ThenBy(row => row.Task.Id)
            .Select(row => new SlaScanRow(row.Task, row.Instance))
            .ToListAsync(cancellationToken);

        var queued = new List<RunSlaQueuedNotification>();
        var warnings = new List<string>();

        foreach (var row in overdueRows)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var task = await dbContext.WorkflowTasks.SingleAsync(item => item.Id == row.Task.Id, cancellationToken);
                var instance = await dbContext.WorkflowInstances.SingleAsync(
                    item => item.WorkflowInstanceId == task.WorkflowInstanceId,
                    cancellationToken);

                var dueAtUtc = task.DueAtUtc;
                if (task.Status != "Pending" || dueAtUtc is null || dueAtUtc >= now || instance.Status != "Active")
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }
                var taskDueAtUtc = dueAtUtc.Value;

                var exists = await dbContext.NotificationOutbox.AnyAsync(outbox =>
                    outbox.WorkflowTaskId == task.Id
                    && outbox.NotificationType == "SlaTaskOverdue", cancellationToken);
                if (exists)
                {
                    await transaction.CommitAsync(cancellationToken);
                    continue;
                }

                var route = !string.IsNullOrWhiteSpace(task.NotificationRouteKey)
                    ? await dbContext.NotificationRoutes.AsNoTracking().SingleAsync(item =>
                        item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                        && item.RouteKey == task.NotificationRouteKey
                        && item.IsActive, cancellationToken)
                    : await dbContext.NotificationRoutes.AsNoTracking().SingleAsync(item =>
                        item.WorkflowDefinitionVersionId == instance.WorkflowDefinitionVersionId
                        && item.RouteKey == "RouteSlaTaskOverdue"
                        && item.IsActive, cancellationToken);

                var context = string.IsNullOrWhiteSpace(task.NotificationContextJson)
                    ? new Dictionary<string, string?>(StringComparer.Ordinal)
                    : JsonSerializer.Deserialize<Dictionary<string, string?>>(task.NotificationContextJson)
                        ?? new Dictionary<string, string?>(StringComparer.Ordinal);
                context["workflowInstanceId"] = instance.WorkflowInstanceId;
                context["taskKey"] = task.TaskKey;
                context["assignedRoleKey"] = task.AssignedRoleKey;
                context["dueAtUtc"] = taskDueAtUtc.ToString("O");
                context["overdueAtUtc"] = now.ToString("O");

                var renderedSubject = templateRenderer.Render(
                    route.SubjectTemplate,
                    context,
                    "workflow.notificationContext");
                var renderedBody = templateRenderer.Render(
                    route.BodyTemplate,
                    context,
                    "workflow.notificationContext");
                var templateErrors = renderedSubject.Errors.Concat(renderedBody.Errors).ToArray();
                if (templateErrors.Length > 0)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    warnings.Add(
                        $"task {task.Id} overdue notification template missing: {string.Join("; ", templateErrors.Select(error => error.Message))}");
                    continue;
                }

                var outbox = new NotificationOutbox
                {
                    WorkflowInstanceId = instance.WorkflowInstanceId,
                    WorkflowTaskId = task.Id,
                    BusinessObjectType = instance.BusinessObjectType,
                    BusinessObjectId = instance.BusinessObjectId,
                    NotificationType = "SlaTaskOverdue",
                    RecipientRoleKey = route.RecipientRoleKey ?? task.AssignedRoleKey,
                    RecipientUserId = route.RecipientUserId,
                    Subject = route.OverdueSubjectPrefix + renderedSubject.Rendered,
                    Body = renderedBody.Rendered,
                    PayloadJson = JsonSerializer.Serialize(context),
                    Status = "Pending",
                    CorrelationId = $"sla-{task.Id}",
                    CreatedAtUtc = now
                };
                dbContext.NotificationOutbox.Add(outbox);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                queued.Add(new RunSlaQueuedNotification(
                    task.Id,
                    instance.WorkflowInstanceId,
                    task.TaskKey,
                    task.AssignedRoleKey,
                    taskDueAtUtc,
                    outbox.Id));
            }
            catch (DbUpdateException exception) when (IsSlaOutboxUniqueViolation(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                warnings.Add($"task {row.Task.Id} already has an SlaTaskOverdue notification");
            }
        }

        return new RunSlaResult(now, pendingTasksWithSla, alreadyQueued, queued, warnings);
    }

    private static bool IsSlaOutboxUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException sqlException
            && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);
    }

    private sealed record SlaScanRow(WorkflowTask Task, WorkflowInstance Instance);
}
