using System.Text.Json.Serialization;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed record WorkflowActionApiResponse(
    [property: JsonPropertyName("result")] WorkflowActionApiResult Result);

public sealed record WorkflowActionApiResult(
    [property: JsonPropertyName("actionResult")] WorkflowActionResult ActionResult,
    [property: JsonPropertyName("workflowContext")] WorkflowContext? WorkflowContext);
