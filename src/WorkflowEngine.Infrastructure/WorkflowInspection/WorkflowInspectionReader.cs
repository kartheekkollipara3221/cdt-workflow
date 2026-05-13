namespace WorkflowEngine.Infrastructure.WorkflowInspection;

using WorkflowEngine.Application.Inspection;
using WorkflowEngine.Infrastructure.Persistence.DatabaseFirst;

public sealed class WorkflowInspectionReader(DatabaseFirstWorkflowStore store) : IWorkflowInspectionReader
{
    public WorkflowInspectionSnapshot Read(string workflowInstanceId)
    {
        return store.GetInspectionSnapshot(workflowInstanceId);
    }
}
