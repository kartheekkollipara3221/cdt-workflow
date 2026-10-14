namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationOutbox
{
    public long Id { get; set; }
    public string? WorkflowInstanceId { get; set; }
    public string? NotificationOccurrenceKey { get; set; }
    public long? WorkflowTaskId { get; set; }
    public long? WorkflowTaskSlaScheduleId { get; set; }
    public DateTimeOffset? SlaOccurrenceAtUtc { get; set; }
    public DateOnly? SlaOccurrenceDate { get; set; }
    public string? SlaReminderKind { get; set; }
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string NotificationRecipientsJson { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? ProcessingToken { get; set; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
}
