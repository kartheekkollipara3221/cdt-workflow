using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Api.Services;

public sealed record WorkflowActorSnapshot(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles)
{
    public WorkflowActor ToWorkflowActor()
    {
        return new WorkflowActor
        {
            UserId = UserId,
            DisplayName = DisplayName,
            Roles = Roles.ToList()
        };
    }
}
