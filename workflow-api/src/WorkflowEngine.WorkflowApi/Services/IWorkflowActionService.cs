using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

public interface IWorkflowActionService
{
    WorkflowActionApiResponse ExecuteWorkflowAction(HttpContext httpContext, WorkflowActionRequest? request);
}
