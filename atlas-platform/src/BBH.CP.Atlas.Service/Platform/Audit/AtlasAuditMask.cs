using System.Text.Json;

namespace BBH.CP.Atlas.Service.Platform.Audit;

/// <summary>
/// Review 3 RV-02: the History of a record shows *** in place of the old and new value of a field that the rules hide
/// from the reader. The entry and the field name stay, so the reader sees that a change was made.
/// </summary>
public static class AtlasAuditMask
{
    public const string Hidden = "***";

    /// <summary>
    /// The LogMessage with the values of the hidden fields masked. A message that is not a JSON list (old text format)
    /// is given as it is: it has no field names to match.
    /// </summary>
    public static object? Mask(object? logMessage, IReadOnlySet<string> hiddenFields)
    {
        ArgumentNullException.ThrowIfNull(hiddenFields);
        if (hiddenFields.Count == 0 || logMessage is not JsonElement { ValueKind: JsonValueKind.Array } items)
            return logMessage;

        List<Dictionary<string, JsonElement>> result = [];
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return logMessage; // not the platform format: no change

            Dictionary<string, JsonElement> copy = [];
            bool hide = item.TryGetProperty("Name", out JsonElement name) && name.ValueKind == JsonValueKind.String
                && name.GetString() != AtlasAuditWriter.EventName && hiddenFields.Contains(name.GetString()!);
            foreach (JsonProperty property in item.EnumerateObject())
            {
                copy[property.Name] = hide && property.Name is "Old" or "Value" && property.Value.ValueKind != JsonValueKind.Null
                    ? JsonSerializer.SerializeToElement(Hidden)
                    : property.Value.Clone();
            }
            result.Add(copy);
        }

        return JsonSerializer.SerializeToElement(result);
    }
}
