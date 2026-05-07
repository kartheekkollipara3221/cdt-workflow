namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationOutbox
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public long? WorkflowTaskId { get; set; }
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? RecipientRoleKey { get; set; }
    public string? RecipientUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
}

