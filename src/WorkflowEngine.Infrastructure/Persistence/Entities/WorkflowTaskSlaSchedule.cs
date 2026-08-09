namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTaskSlaSchedule
{
    public long Id { get; set; }
    public long WorkflowTaskId { get; set; }
    public int ScheduleVersion { get; set; }
    public string SlaPolicyKey { get; set; } = string.Empty;
    public DateOnly? TargetDate { get; set; }
    public DateTimeOffset? DueReminderAtUtc { get; set; }
    public DateTimeOffset OverdueAtUtc { get; set; }
    public DateTimeOffset? NextReminderAtUtc { get; set; }
    public string CalculationMode { get; set; } = string.Empty;
    public string? TimeZoneId { get; set; }
    public TimeOnly? ReminderLocalTime { get; set; }
    public int OverdueStartOffsetDays { get; set; }
    public int? RecurrenceIntervalMinutes { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    public string? DueNotificationRouteKey { get; set; }
    public string? OverdueNotificationRouteKey { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset? SupersededAtUtc { get; set; }
    public string? SupersededByUserId { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
