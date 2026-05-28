using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Services;

public interface IWorkflowContextQueryService
{
    WorkflowContextResponse GetWorkflowContext(HttpContext httpContext, string workflowInstanceId);
}
