namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowRoleDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

