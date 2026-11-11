using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// Reads and writes the platform JSON files.
/// </summary>
/// <remarks>
/// The reader accepts comments and trailing commas. Property names are not case sensitive.
/// The reader reads enums by name. The current files, written for Newtonsoft.Json, load with no change.
/// </remarks>
public static class AtlasJson
{
    /// <summary>The shared, read-only serializer options of the platform.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Reads a JSON text into a value of type <typeparamref name="T"/>.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="source">The name of the source, for the error message (for example a file name).</param>
    /// <exception cref="InvalidOperationException">The text is empty or is not valid JSON.</exception>
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

    /// <summary>Writes a value as JSON text with the platform options.</summary>
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
