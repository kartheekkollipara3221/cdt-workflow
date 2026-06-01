namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowTaskAssignmentRoleDefinition
{
    public long Id { get; set; }

    public long WorkflowDefinitionVersionId { get; set; }

    public string TaskKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
