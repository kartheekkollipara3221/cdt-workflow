namespace WorkflowEngine.Application.Validation;

public sealed record WorkflowValidationError(string Field, string Message);

