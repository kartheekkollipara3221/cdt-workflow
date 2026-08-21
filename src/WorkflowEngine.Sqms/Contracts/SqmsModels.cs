using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Sqms.Contracts;

public sealed record SqmsActor(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

public sealed record SqmsExecuteRequest(
    int EntityId,
    string ActionKey,
    int ExpectedWorkflowVersion,
    SqmsActor Actor,
    IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInputs);

public sealed record SqmsOperationResult(
    bool Success,
    string ResultCode,
    string? Message,
    WorkflowActionResultEnvelope? WorkflowResult,
    WorkflowContextResponse? Context);
