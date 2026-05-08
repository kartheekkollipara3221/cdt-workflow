namespace WorkflowEngine.Application.Demo;

public sealed record DemoStep(
    string Name,
    string CommandName,
    string[] Arguments,
    DemoStepExpectations? Expectations);

public sealed record DemoStepExpectations(
    string? ExpectedResult,
    string? ExpectedOldState,
    string? ExpectedNewState,
    string? ExpectedClosedTaskKey,
    string? ExpectedCreatedTaskKey,
    string? ExpectedAssignedRole,
    string? ExpectedNotificationType,
    string? ExpectedAuditAction);

public sealed record DemoStepResult(
    bool Success,
    string ResultCode,
    string Message,
    DemoStepExpectations? ExpectationOutcome);
