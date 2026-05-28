using System.Text.Json.Serialization;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed record WorkflowContextApiResponse(
    [property: JsonPropertyName("result")] WorkflowContextApiResult Result);

public sealed record WorkflowContextApiResult(
    [property: JsonPropertyName("workflowContext")] WorkflowContext WorkflowContext);
