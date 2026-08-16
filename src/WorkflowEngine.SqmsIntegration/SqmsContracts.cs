using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.SqmsIntegration;

public sealed record SqmsActor(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record SqmsActionRequest(
    string ActionKey,
    string? Reason = null);

public sealed record SqmsWorkflowResult(
    int EntityId,
    string WorkflowState,
    string Status,
    WorkflowContext WorkflowContext);

public sealed class SqmsIntegrationException : Exception
{
    public SqmsIntegrationException(string message)
        : base(message)
    {
    }

    public SqmsIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

internal enum SqmsEntityKind
{
    Opportunity,
    Bid
}

internal sealed record SqmsApplicationState(
    string? WorkflowState,
    string? Status);
