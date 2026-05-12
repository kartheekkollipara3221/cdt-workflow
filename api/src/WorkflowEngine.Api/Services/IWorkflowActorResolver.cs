namespace WorkflowEngine.Api.Services;

public interface IWorkflowActorResolver
{
    WorkflowActorSnapshot Resolve(HttpContext httpContext);
}
