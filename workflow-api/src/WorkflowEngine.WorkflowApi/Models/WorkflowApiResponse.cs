using System.Text.Json.Serialization;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed record WorkflowApiResponse<T>(
    [property: JsonPropertyName("result")] T Result);
