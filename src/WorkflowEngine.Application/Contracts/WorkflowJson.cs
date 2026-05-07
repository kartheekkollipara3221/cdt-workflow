using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowEngine.Application.Contracts;

public static class WorkflowJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

