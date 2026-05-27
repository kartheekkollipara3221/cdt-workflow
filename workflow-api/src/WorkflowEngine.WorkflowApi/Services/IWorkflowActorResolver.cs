namespace WorkflowEngine.WorkflowApi.Services;

public interface IWorkflowActorResolver
{
    WorkflowActorSnapshot Resolve(HttpContext httpContext);
}
