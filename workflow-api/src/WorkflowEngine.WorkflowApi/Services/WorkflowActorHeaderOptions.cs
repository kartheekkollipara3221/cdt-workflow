namespace WorkflowEngine.WorkflowApi.Services;

public sealed class WorkflowActorHeaderOptions
{
    public const string SectionName = "WorkflowApi:ActorHeaders";

    public string UserIdHeaderName { get; set; } = "X-Workflow-UserId";

    public string RolesHeaderName { get; set; } = "X-Workflow-Roles";

    public string DisplayNameHeaderName { get; set; } = "X-Workflow-DisplayName";
}
