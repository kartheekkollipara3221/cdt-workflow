using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;

namespace WorkflowEngine.Application.WorkflowExecution;

public interface IWorkflowExecutionService
{
    WorkflowActionResultEnvelope Execute(WorkflowExecutionRequest request);
}

public sealed record WorkflowExecutionRequest(
    NormalizedWorkflowAction Normalized,
    string RequestHash,
    bool AdminOverride)
{
    public bool DeriveIdentityFromPersistence { get; init; }

    public bool ValidateActionEcho { get; init; } = true;

    public bool PersistSectionData { get; init; } = true;
}
