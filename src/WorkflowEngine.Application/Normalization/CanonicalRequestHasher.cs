using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Normalization;

public sealed class CanonicalRequestHasher
{
    public string ComputeHash(WorkflowActionEnvelope envelope)
    {
        return ComputeHash(envelope, null);
    }

    public string ComputeHash(
        WorkflowActionEnvelope envelope,
        WorkflowNotificationInput? notificationInput)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var canonical = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["actor.roles"] = envelope.Actor?.Roles is null
                ? null
                : envelope.Actor.Roles.Where(role => !string.IsNullOrWhiteSpace(role)).ToArray(),
            ["actor.userId"] = envelope.Actor?.UserId,
            ["businessObject.objectId"] = envelope.BusinessObject?.ObjectId,
            ["businessObject.objectType"] = envelope.BusinessObject?.ObjectType,
            ["dataSave.payload"] = envelope.DataSave is null || envelope.DataSave.Payload.ValueKind == JsonValueKind.Undefined
                ? null
                : Canonicalize(envelope.DataSave.Payload),
            ["dataSave.sectionKey"] = envelope.DataSave?.SectionKey,
            ["workflow.actionKey"] = envelope.Workflow?.ActionKey,
            ["workflow.actionReason"] = envelope.Workflow?.ActionReason,
            ["workflow.notificationContext"] = NormalizeNotificationContext(envelope.Workflow?.NotificationContext),
            ["workflow.notificationRecipients"] = SnapshotNotificationRecipients(envelope.Workflow?.NotificationRecipients),
            ["workflow.notificationInput.recipientInputs"] = SnapshotRecipientInputs(notificationInput?.RecipientInputs),
            ["workflow.notificationInput.templateData"] = notificationInput?.TemplateData,
            ["workflow.slaInput.targetDate"] = envelope.Workflow?.SlaInput?.TargetDate,
            ["workflow.expectedCurrentState"] = envelope.Workflow?.ExpectedCurrentState,
            ["workflow.expectedCurrentTaskId"] = envelope.Workflow?.ExpectedCurrentTaskId,
            ["workflow.expectedCurrentTaskKey"] = envelope.Workflow?.ExpectedCurrentTaskKey,
            ["workflow.expectedInstanceVersion"] = envelope.Workflow?.ExpectedInstanceVersion,
            ["workflow.workflowDefinitionVersion"] = envelope.Workflow?.WorkflowDefinitionVersion,
            ["workflow.workflowInstanceId"] = envelope.Workflow?.WorkflowInstanceId,
            ["workflow.workflowKey"] = envelope.Workflow?.WorkflowKey
        };

        var json = SerializeCanonical(canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, object?>? SnapshotRecipientInputs(
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowRecipientInput>>? recipientInputs)
    {
        if (recipientInputs is null || recipientInputs.Count == 0)
        {
            return null;
        }

        return recipientInputs.ToDictionary(
            pair => pair.Key,
            pair => (object?)pair.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?>? SnapshotNotificationRecipients(
        NotificationRecipients? recipients)
    {
        if (recipients is null)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["toEmailAddresses"] = recipients.ToEmailAddresses,
            ["ccEmailAddresses"] = recipients.CcEmailAddresses
        };
    }

    private static string SerializeCanonical(object? value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(element, writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static Dictionary<string, string?>? NormalizeNotificationContext(
        IReadOnlyDictionary<string, string?>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            normalized[pair.Key.Trim()] = pair.Value!.Trim();
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
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
