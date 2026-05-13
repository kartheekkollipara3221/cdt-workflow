namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowDefinitionVersion
{
    public long Id { get; set; }
    public long WorkflowDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public WorkflowDefinition? WorkflowDefinition { get; set; }
}

