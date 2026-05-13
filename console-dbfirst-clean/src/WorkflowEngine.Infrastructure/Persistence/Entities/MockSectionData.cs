namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class MockSectionData
{
    public long Id { get; set; }
    public string BusinessObjectType { get; set; } = string.Empty;
    public string BusinessObjectId { get; set; } = string.Empty;
    public string SectionKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string SavedByUserId { get; set; } = string.Empty;
    public DateTimeOffset SavedAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

