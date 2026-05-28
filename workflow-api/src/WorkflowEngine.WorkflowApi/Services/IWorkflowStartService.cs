using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

public interface IWorkflowStartService
{
    WorkflowStartApiResponse StartWorkflow(HttpContext httpContext, WorkflowStartRequest? request);
}
