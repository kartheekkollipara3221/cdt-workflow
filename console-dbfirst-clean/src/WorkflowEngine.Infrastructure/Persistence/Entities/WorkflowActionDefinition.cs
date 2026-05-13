namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowActionDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string ActionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? SectionKey { get; set; }
    public string? ButtonKey { get; set; }
    public string? ButtonLabel { get; set; }
    public string? Intent { get; set; }
    public bool RequiresReason { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

