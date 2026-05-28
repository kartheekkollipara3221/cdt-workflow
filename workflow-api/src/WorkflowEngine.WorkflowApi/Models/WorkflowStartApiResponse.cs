using System.Text.Json.Serialization;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.WorkflowApi.Models;

public sealed record WorkflowStartApiResponse(
    [property: JsonPropertyName("result")] WorkflowStartApiResult Result);

public sealed record WorkflowStartApiResult(
    [property: JsonPropertyName("actionResult")] WorkflowActionResult ActionResult,
    [property: JsonPropertyName("workflowContext")] WorkflowContext? WorkflowContext);
