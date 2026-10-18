namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class SlaPolicy
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string SlaPolicyKey { get; set; } = string.Empty;
    public string CalculationMode { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public bool SendDueDateReminder { get; set; }
    public TimeOnly? ReminderLocalTime { get; set; }
    public string? TimeZoneId { get; set; }
    public int OverdueStartOffsetDays { get; set; }
    public int? RecurrenceIntervalMinutes { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    public string? DueNotificationRouteKey { get; set; }
    public string? OverdueNotificationRouteKey { get; set; }
    public TimeOnly? DueReminderLocalTime { get; set; }
    public bool SkipMissedDueReminder { get; set; }
    public bool OverdueStartsNextLocalDay { get; set; }
    public bool IsActive { get; set; }
}

