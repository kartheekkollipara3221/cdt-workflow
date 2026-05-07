namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTransitionDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string FromStateKey { get; set; } = string.Empty;
    public string ActionKey { get; set; } = string.Empty;
    public string AllowedRoleKey { get; set; } = string.Empty;
    public string ToStateKey { get; set; } = string.Empty;
    public string? TaskToCloseKey { get; set; }
    public string? TaskToCreateKey { get; set; }
    public string? NotificationRouteKey { get; set; }
    public string AuditLabel { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

