namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class SlaPolicy
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string SlaPolicyKey { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; }
}

