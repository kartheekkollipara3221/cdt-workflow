namespace WorkflowEngine.Application.Sla;

public interface IRunSlaService
{
    Task<RunSlaResult> RunAsync(RunSlaRequest request, CancellationToken cancellationToken);
}

public sealed record RunSlaRequest(DateTimeOffset Now);

public sealed record RunSlaResult(
    DateTimeOffset Now,
    int PendingTasksWithSla,
    int AlreadyQueued,
    IReadOnlyList<RunSlaQueuedNotification> QueuedNotifications,
    IReadOnlyList<string> Warnings);

public sealed record RunSlaQueuedNotification(
    long WorkflowTaskId,
    string WorkflowInstanceId,
    string TaskKey,
    string? AssignedRoleKey,
    DateTimeOffset DueAtUtc,
    long NotificationOutboxId,
    long WorkflowTaskSlaScheduleId,
    int ScheduleVersion,
    DateTimeOffset OccurrenceAtUtc,
    DateOnly OccurrenceDate,
    string ReminderKind);
