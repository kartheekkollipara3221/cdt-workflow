namespace WorkflowEngine.Api.Options;

public sealed class WorkflowApiOptions
{
    public const string SectionName = "WorkflowApi";

    public bool AllowDevelopmentHeaderActorResolver { get; set; }

    public bool DisablePublicReset { get; set; } = true;

    public bool DisablePublicAdminOverride { get; set; } = true;
}
