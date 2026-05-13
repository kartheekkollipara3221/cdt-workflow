using System.Security.Cryptography;
using System.Text;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Normalization;

public sealed class WorkflowActionEnvelopeNormalizer
{
    public NormalizedWorkflowAction Normalize(
        WorkflowActionEnvelope envelope,
        string? businessObjectOverride,
        string? workflowInstanceOverride)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var overrideApplied = false;
        if (!string.IsNullOrWhiteSpace(businessObjectOverride))
        {
            envelope.BusinessObject ??= new WorkflowBusinessObject();
            envelope.BusinessObject.ObjectId = businessObjectOverride;
            overrideApplied = true;
        }

        if (!string.IsNullOrWhiteSpace(workflowInstanceOverride))
        {
            envelope.Workflow ??= new WorkflowActionBlock();
            envelope.Workflow.WorkflowInstanceId = workflowInstanceOverride;
            overrideApplied = true;
        }

        var baseClientActionId = envelope.Request?.ClientActionId ?? string.Empty;
        var baseCorrelationId = envelope.Request?.CorrelationId ?? string.Empty;
        var baseIdempotencyKey = !string.IsNullOrWhiteSpace(envelope.Workflow?.IdempotencyKey)
            ? envelope.Workflow.IdempotencyKey!
            : baseClientActionId;

        var scope = $"{envelope.BusinessObject?.ObjectType}|{envelope.BusinessObject?.ObjectId}|{envelope.Workflow?.WorkflowInstanceId}|{envelope.Workflow?.ActionKey}";
        var suffix = ShortHash(scope);
        var effectiveClientActionId = overrideApplied && !string.IsNullOrWhiteSpace(baseClientActionId)
            ? $"{baseClientActionId}:{suffix}"
            : baseClientActionId;
        var effectiveCorrelationId = overrideApplied && !string.IsNullOrWhiteSpace(baseCorrelationId)
            ? $"{baseCorrelationId}:{suffix}"
            : baseCorrelationId;
        var effectiveIdempotencyKey = overrideApplied && !string.IsNullOrWhiteSpace(baseIdempotencyKey)
            ? $"{baseIdempotencyKey}:{suffix}"
            : baseIdempotencyKey;

        return new NormalizedWorkflowAction(
            envelope,
            effectiveClientActionId,
            effectiveCorrelationId,
            effectiveIdempotencyKey,
            overrideApplied);
    }

    private static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash[..6]).ToLowerInvariant();
    }
}

public sealed record NormalizedWorkflowAction(
    WorkflowActionEnvelope Envelope,
    string EffectiveClientActionId,
    string EffectiveCorrelationId,
    string EffectiveIdempotencyKey,
    bool OverrideApplied);

