using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// The platform JSON loader (spec 11, 4.2): comments and trailing commas are accepted, names are not case
/// sensitive, enums are read by name. The current files (written for Newtonsoft.Json) load with no change.
/// </summary>
public static class AtlasJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static T Deserialize<T>(string json, string source)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                ?? throw new InvalidOperationException($"{source} is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source} is not valid JSON: {ex.Message}", ex);
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Newtonsoft.Json ignores the C# "required" keyword; System.Text.Json enforces it. The current files
            // (for example SQMSO.json with no Workflow.Roles, CDT.json with no Label) depend on the Newtonsoft rule.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    static typeInfo =>
                    {
                        foreach (JsonPropertyInfo property in typeInfo.Properties)
                            property.IsRequired = false;
                    }
                }
            }
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly();
        return options;
    }
}
