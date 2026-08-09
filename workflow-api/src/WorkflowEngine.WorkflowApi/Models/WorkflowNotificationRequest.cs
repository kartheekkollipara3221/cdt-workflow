using System.Text.Json.Serialization;

namespace WorkflowEngine.WorkflowApi.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkflowNotificationRequest
{
    [JsonPropertyName("recipientInputs")]
    public Dictionary<string, List<WorkflowRecipientRequest>?>? RecipientInputs { get; set; }

    [JsonPropertyName("templateData")]
    public Dictionary<string, string?>? TemplateData { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class WorkflowRecipientRequest
{
    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("emailAddress")]
    public string? EmailAddress { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
