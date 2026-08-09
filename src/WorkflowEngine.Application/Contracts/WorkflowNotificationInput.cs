namespace WorkflowEngine.Application.Contracts;

public sealed record WorkflowRecipientInput(
    string? UserId,
    string EmailAddress,
    string? DisplayName);

public sealed class WorkflowNotificationInput
{
    public IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>> RecipientInputs { get; init; }
        = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string?> TemplateData { get; init; }
        = new Dictionary<string, string?>(StringComparer.Ordinal);
}
