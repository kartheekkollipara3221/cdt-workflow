namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowDefinition
{
    public long Id { get; set; }
    public string WorkflowKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public List<WorkflowDefinitionVersion> Versions { get; } = [];
}

