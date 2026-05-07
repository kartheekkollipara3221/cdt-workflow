namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class IdempotencyRecord
{
    public long Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string ActionKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResultHash { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
