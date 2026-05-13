namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowInstance
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string WorkflowKey { get; set; } = string.Empty;
    public long WorkflowDefinitionVersionId { get; set; }
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string CurrentStateKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public string StartedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int Version { get; set; }
}

