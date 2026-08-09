namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class NotificationRecipientRule
{
    public long Id { get; set; }
    public long NotificationRouteId { get; set; }
    public string RecipientKind { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceKey { get; set; }
    public string? StaticAddress { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
}

public static class RecipientKinds
{
    public const string To = "To";
    public const string Cc = "Cc";
}

public static class RecipientSourceTypes
{
    public const string StaticAddress = "StaticAddress";
    public const string WorkflowParticipant = "WorkflowParticipant";
    public const string RequestInput = "RequestInput";
}
