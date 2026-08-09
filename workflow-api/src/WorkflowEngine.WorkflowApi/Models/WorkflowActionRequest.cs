using System.Text.Json.Serialization;

namespace WorkflowEngine.WorkflowApi.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkflowActionRequest
{
    [JsonPropertyName("workflowInstanceId")]
    public string? WorkflowInstanceId { get; set; }

    [JsonPropertyName("actionKey")]
    public string? ActionKey { get; set; }

    [JsonPropertyName("assignedRoleKey")]
    public string? AssignedRoleKey { get; set; }

    [JsonPropertyName("slaTargetDate")]
    public DateOnly? SlaTargetDate { get; set; }

    [JsonPropertyName("notification")]
    public WorkflowNotificationRequest? Notification { get; set; }
}
