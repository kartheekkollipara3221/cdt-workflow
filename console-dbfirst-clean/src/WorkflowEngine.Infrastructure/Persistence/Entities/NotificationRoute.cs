namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationRoute
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string RouteKey { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string? RecipientRoleKey { get; set; }
    public string? RecipientUserId { get; set; }
    public string SubjectTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

