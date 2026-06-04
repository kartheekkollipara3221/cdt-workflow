using System.Text.RegularExpressions;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Application.Notifications;

public sealed partial class NotificationTemplateRenderer
{
    public NotificationTemplateRenderResult Render(
        string template,
        IReadOnlyDictionary<string, string?> values,
        string fieldPrefix)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPrefix);

        var errors = new List<WorkflowValidationError>();
        var rendered = TokenRegex().Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                errors.Add(new WorkflowValidationError(
                    $"{fieldPrefix}.{key}",
                    $"Template token '{key}' requires {fieldPrefix}.{key}."));
                return match.Value;
            }

            return value.Trim();
        });

        return new NotificationTemplateRenderResult(errors.Count == 0, rendered, errors);
    }

    [GeneratedRegex(@"\{\{(?<key>[A-Za-z0-9_]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}

public sealed record NotificationTemplateRenderResult(
    bool Success,
    string Rendered,
    IReadOnlyList<WorkflowValidationError> Errors);
