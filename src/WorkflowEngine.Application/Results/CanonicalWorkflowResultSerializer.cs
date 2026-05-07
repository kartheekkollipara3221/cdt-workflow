using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Results;

public sealed class CanonicalWorkflowResultSerializer
{
    public string Serialize(WorkflowActionResultEnvelope envelope, bool excludeDiagnostics = false)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var element = JsonSerializer.SerializeToElement(envelope, WorkflowJson.Options);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(element, writer, excludeDiagnostics);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public string ComputeResultHash(WorkflowActionResultEnvelope envelope)
    {
        var canonicalJson = Serialize(envelope, excludeDiagnostics: true);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer, bool excludeDiagnostics)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (excludeDiagnostics && string.Equals(property.Name, "diagnostics", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer, excludeDiagnostics);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer, excludeDiagnostics);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
