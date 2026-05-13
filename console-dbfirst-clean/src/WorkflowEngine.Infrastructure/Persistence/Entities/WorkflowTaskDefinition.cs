namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTaskDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OwnerRoleKey { get; set; } = string.Empty;
    public string? SlaPolicyKey { get; set; }
    public bool IsActive { get; set; }
}

