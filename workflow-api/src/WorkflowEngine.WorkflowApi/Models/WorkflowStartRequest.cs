using System.Text.Json.Serialization;

namespace WorkflowEngine.WorkflowApi.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkflowStartRequest
{
    [JsonPropertyName("workflowKey")]
    public string? WorkflowKey { get; set; }

    [JsonPropertyName("businessObjectId")]
    public string? BusinessObjectId { get; set; }

    [JsonPropertyName("actionKey")]
    public string? ActionKey { get; set; }

    [JsonPropertyName("slaTargetDate")]
    public DateOnly? SlaTargetDate { get; set; }

    [JsonPropertyName("notification")]
    public WorkflowNotificationRequest? Notification { get; set; }
}
