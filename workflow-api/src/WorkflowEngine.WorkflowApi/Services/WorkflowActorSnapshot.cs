namespace WorkflowEngine.WorkflowApi.Services;

public sealed record WorkflowActorSnapshot(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);
