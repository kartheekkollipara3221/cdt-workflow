using System.Text.Json;

namespace WorkflowEngine.Application.Contracts;

public static class WorkflowJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
