using System.Globalization;
using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Service.Platform.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Contracts;

namespace BBH.CP.Atlas.Service.Platform.Workflow;

/// <summary>A name of a recipient field that matched no single user with an email. The email goes to the others.</summary>
public sealed record AtlasSkippedRecipient(string Key, string Name, string Reason);

/// <summary>
/// Makes the email inputs of an engine action from the binding and the values of the record: the recipients and the
/// template data.
/// </summary>
/// <remarks>
/// A list address comes from AppSettings:Modules:&lt;Code&gt;:Workflow:Lists:&lt;Key&gt; (separated by ';'). A name is
/// found in the user directory; a name with no exact match is left out, with a warning log.
/// </remarks>
public sealed class AtlasWorkflowInputs(IConfiguration configuration, IAtlasUserDirectory directory, ILogger logger)
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Returns the setting path of the addresses of a list key.</summary>
    public static string ListSetting(string appCode, string listKey) => $"AppSettings:Modules:{appCode}:Workflow:Lists:{listKey}";

    /// <summary>The setting with the address of the client application, with no slash at the end.</summary>
    public const string HostedClientUrlSetting = "AppSettings:HostedClientURL";

    /// <summary>Returns the addresses of a list key from the settings, or an empty list.</summary>
    public IReadOnlyList<string> ListAddresses(string appCode, string listKey) =>
        [.. (configuration[ListSetting(appCode, listKey)] ?? "")
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>
    /// Returns the recipient inputs of the keys, from the record values. A key with no address and no Optional is an error.
    /// </summary>
    /// <param name="value">Returns the value of a field (column) of the record.</param>
    /// <param name="recordId">The record ID for the log, or 0 for a new record.</param>
    /// <param name="skipped">Null: each name with no match is logged now. Else the names are added to this list, and the
    /// caller logs them when the record ID is known (see <see cref="LogSkipped"/>).</param>
    public Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> Recipients(
        string appCode, CPWorkflow binding, IEnumerable<string> keys, Func<string, object?> value, int recordId, List<CPPageError> errors,
        List<AtlasSkippedRecipient>? skipped = null)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(errors);
        Dictionary<string, IReadOnlyList<WorkflowRecipientInput>> inputs = new(StringComparer.Ordinal);
        foreach (string key in keys.Distinct(StringComparer.Ordinal))
        {
            CPWorkflowRecipient source = binding.Recipients?.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal)).Value
                ?? throw AtlasProblemException.Config($"App '{appCode}': the recipient key '{key}' has no definition in Workflow.Recipients.");

            List<WorkflowRecipientInput> list = [];
            foreach (string listKey in ListKeys(source, value))
                list.AddRange(ListAddresses(appCode, listKey).Select(x => new WorkflowRecipientInput(null, x, null)));

            if (!string.IsNullOrWhiteSpace(source.FromNameField))
                foreach (string name in Text(value(source.FromNameField)).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    AtlasNameMatch match = directory.EmailOfDisplayName(name);
                    if (match.Email is { Length: > 0 } email)
                        list.Add(new WorkflowRecipientInput(null, email, name));
                    else if (skipped != null)
                        skipped.Add(new AtlasSkippedRecipient(key, name, match.Problem ?? "no match"));
                    else
                        AtlasLog.RecipientSkipped(logger, appCode, recordId, key, name, match.Problem ?? "no match");
                }

            List<WorkflowRecipientInput> distinct = [.. list.DistinctBy(x => x.EmailAddress, StringComparer.OrdinalIgnoreCase)];
            if (distinct.Count == 0 && !source.Optional)
                errors.Add(new CPPageError
                {
                    Code = "NoRecipient",
                    Message = $"The notification has no address for '{key}'. Contact support."
                });
            if (distinct.Count > 0)
                inputs[key] = distinct;
        }

        return inputs;
    }

    /// <summary>Writes the warning log Atlas.RecipientSkipped for each name that <see cref="Recipients"/> left out.</summary>
    /// <param name="recordId">The record ID, or 0 when the save made no record.</param>
    public void LogSkipped(string appCode, int recordId, IEnumerable<AtlasSkippedRecipient> skipped)
    {
        ArgumentNullException.ThrowIfNull(skipped);
        foreach (AtlasSkippedRecipient item in skipped)
            AtlasLog.RecipientSkipped(logger, appCode, recordId, item.Key, item.Name, item.Reason);
    }

    /// <summary>
    /// Returns the template data from the record values. An empty value is an error that names the field.
    /// </summary>
    /// <param name="recordId">The record ID for the record link, or 0 when the ID is not known yet (only the check).</param>
    /// <param name="label">Returns the label of a field for the error text.</param>
    public Dictionary<string, string?> TemplateData(
        string appCode, CPWorkflow binding, Func<string, object?> value, Func<string, string> label, int recordId, List<CPPageError> errors)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(errors);
        Dictionary<string, string?> data = new(StringComparer.Ordinal);
        foreach ((string token, CPWorkflowTemplateValue source) in binding.TemplateData ?? new Dictionary<string, CPWorkflowTemplateValue>())
        {
            if (source.RecordLink)
            {
                string host = (configuration[HostedClientUrlSetting] ?? "").TrimEnd('/');
                data[token] = $"{host}/form/{appCode}/{recordId.ToString(CultureInfo.InvariantCulture)}";
                continue;
            }

            string field = source.Field ?? token;
            string text = Format(value(field), source.Format);
            if (text.Length == 0)
                errors.Add(new CPPageError { Field = field, Code = "NotificationValue", Message = $"{label(field)} is necessary for the notification." });
            data[token] = text;
        }

        return data;
    }

    /// <summary>Returns a value as text: with the format and the en-US culture, or as invariant text.</summary>
    public static string Format(object? value, string? format) => value switch
    {
        null => "",
        IFormattable formattable when !string.IsNullOrWhiteSpace(format) => formattable.ToString(format, UsCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Text(value)
    };

    private static IEnumerable<string> ListKeys(CPWorkflowRecipient source, Func<string, object?> value)
    {
        if (!string.IsNullOrWhiteSpace(source.List))
            yield return source.List;

        if (!string.IsNullOrWhiteSpace(source.ListByField) && source.Map != null)
        {
            string selected = Text(value(source.ListByField)).Trim();
            foreach (string key in source.Map.FirstOrDefault(x => string.Equals(x.Key, selected, StringComparison.OrdinalIgnoreCase)).Value ?? [])
                yield return key;
        }
    }

    private static string Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
}
