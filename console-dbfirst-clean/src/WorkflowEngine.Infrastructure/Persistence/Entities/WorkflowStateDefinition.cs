namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowStateDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string StateKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsTerminal { get; set; }
    public bool IsActive { get; set; }
}

