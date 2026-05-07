using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Context;

public interface IWorkflowContextReader
{
    WorkflowContextResponse Read(WorkflowContextReadRequest request);
}

public sealed record WorkflowContextReadRequest(
    string WorkflowInstanceId,
    string UserId,
    IReadOnlyList<string> Roles);

