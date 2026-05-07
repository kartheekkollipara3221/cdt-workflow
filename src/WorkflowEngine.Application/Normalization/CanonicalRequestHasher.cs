using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Normalization;

public sealed class CanonicalRequestHasher
{
    public string ComputeHash(WorkflowActionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["actor.roles"] = envelope.Actor?.Roles is null
                ? null
                : envelope.Actor.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).Order(StringComparer.Ordinal).ToArray(),
            ["actor.userId"] = envelope.Actor?.UserId,
            ["businessObject.objectId"] = envelope.BusinessObject?.ObjectId,
            ["businessObject.objectType"] = envelope.BusinessObject?.ObjectType,
            ["dataSave.payload"] = envelope.DataSave is null || envelope.DataSave.Payload.ValueKind == JsonValueKind.Undefined
                ? null
                : Canonicalize(envelope.DataSave.Payload),
            ["dataSave.sectionKey"] = envelope.DataSave?.SectionKey,
            ["workflow.actionKey"] = envelope.Workflow?.ActionKey,
            ["workflow.actionReason"] = envelope.Workflow?.ActionReason,
            ["workflow.expectedCurrentState"] = envelope.Workflow?.ExpectedCurrentState,
            ["workflow.expectedCurrentTaskId"] = envelope.Workflow?.ExpectedCurrentTaskId,
            ["workflow.expectedCurrentTaskKey"] = envelope.Workflow?.ExpectedCurrentTaskKey,
            ["workflow.expectedInstanceVersion"] = envelope.Workflow?.ExpectedInstanceVersion,
            ["workflow.workflowDefinitionVersion"] = envelope.Workflow?.WorkflowDefinitionVersion,
            ["workflow.workflowInstanceId"] = envelope.Workflow?.WorkflowInstanceId,
            ["workflow.workflowKey"] = envelope.Workflow?.WorkflowKey
        };

        var json = JsonSerializer.Serialize(canonical, WorkflowJson.Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static object? Canonicalize(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => Canonicalize(property.Value), StringComparer.Ordinal),
            JsonValueKind.Array => element.EnumerateArray().Select(Canonicalize).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => null
        };
    }
}
