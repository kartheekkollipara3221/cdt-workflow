namespace WorkflowEngine.Infrastructure.Persistence.Entities;

public sealed class WorkflowParticipantDefinition
{
    public long Id { get; set; }
    public long WorkflowDefinitionVersionId { get; set; }
    public string ParticipantKey { get; set; } = string.Empty;
    public string Cardinality { get; set; } = string.Empty;
    public string CaptureStage { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsImmutable { get; set; }
    public bool IsActive { get; set; }
}

public static class WorkflowParticipantCardinalities
{
    public const string Single = "Single";
    public const string Many = "Many";
}

public static class WorkflowParticipantCaptureStages
{
    public const string Start = "Start";
}
