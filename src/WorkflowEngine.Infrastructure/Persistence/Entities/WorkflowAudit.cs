namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowAudit
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string ActionKey { get; set; } = string.Empty;
    public string FromStateKey { get; set; } = string.Empty;
    public string ToStateKey { get; set; } = string.Empty;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string PerformedByRoleKey { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

