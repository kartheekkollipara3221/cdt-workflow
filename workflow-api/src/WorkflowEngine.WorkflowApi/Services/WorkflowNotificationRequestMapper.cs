using WorkflowEngine.Application.Contracts;
using WorkflowEngine.WorkflowApi.Models;

namespace WorkflowEngine.WorkflowApi.Services;

internal static class WorkflowNotificationRequestMapper
{
    public static WorkflowNotificationInput Map(WorkflowNotificationRequest? request)
    {
        var recipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(
            StringComparer.Ordinal);

        if (request?.RecipientInputs is not null)
        {
            foreach (var pair in request.RecipientInputs)
            {
                recipientInputs[pair.Key] = pair.Value is null
                    ? []
                    : pair.Value
                        .Select(recipient => new WorkflowRecipientInput(
                            recipient.UserId,
                            recipient.EmailAddress ?? string.Empty,
                            recipient.DisplayName))
                        .ToArray();
            }
        }

        var templateData = request?.TemplateData is null
            ? new Dictionary<string, string?>(StringComparer.Ordinal)
            : new Dictionary<string, string?>(request.TemplateData, StringComparer.Ordinal);

        return new WorkflowNotificationInput
        {
            RecipientInputs = recipientInputs,
            TemplateData = templateData
        };
    }
}
