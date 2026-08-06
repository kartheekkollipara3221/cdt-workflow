namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTask
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string TaskKey { get; set; } = string.Empty;
    public string AssignedRoleKey { get; set; } = string.Empty;
    public string? AssignedUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DueAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? CompletedByUserId { get; set; }
    public string? ClosedByActionKey { get; set; }
    public string? NotificationRouteKey { get; set; }
    public string? NotificationContextJson { get; set; }
    public string NotificationRecipientsJson { get; set; } = string.Empty;
}
