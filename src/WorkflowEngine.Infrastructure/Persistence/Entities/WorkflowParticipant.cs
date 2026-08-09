namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowParticipant
{
    public long Id { get; set; }
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public string ParticipantKey { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
