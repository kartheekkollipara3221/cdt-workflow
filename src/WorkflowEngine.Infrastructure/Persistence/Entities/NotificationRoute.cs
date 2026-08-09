namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationRoute
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string RouteKey { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string HtmlBodyTemplate { get; set; } = string.Empty;
    public string OverdueSubjectPrefix { get; set; } = "Overdue: ";
    public bool IsActive { get; set; }
}
