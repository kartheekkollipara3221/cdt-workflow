using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using WorkflowEngine.Application.Validation;

namespace WorkflowEngine.Application.Notifications;

public sealed partial class NotificationTemplateRenderer
{
    public NotificationTemplateRenderResult Render(
        string template,
        IReadOnlyDictionary<string, string?> values,
        string fieldPrefix)
    {
        return RenderCore(template, values, fieldPrefix, static value => value);
    }

    public NotificationTemplateRenderResult RenderHtml(
        string template,
        IReadOnlyDictionary<string, string?> values,
        string fieldPrefix)
    {
        return RenderCore(template, values, fieldPrefix, HtmlEncoder.Default.Encode);
    }

    private static NotificationTemplateRenderResult RenderCore(
        string template,
        IReadOnlyDictionary<string, string?> values,
        string fieldPrefix,
        Func<string, string> encode)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPrefix);
        ArgumentNullException.ThrowIfNull(encode);

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

            return encode(value.Trim());
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
