using System.Text.Json;
using WorkflowEngine.Application.Contracts;
using WorkflowEngine.Application.Normalization;

namespace WorkflowEngine.Tests;

public sealed class WorkflowActionEnvelopeNormalizationTests
{
    private readonly WorkflowActionEnvelopeNormalizer normalizer = new();
    private readonly CanonicalRequestHasher hasher = new();

    [Fact]
    public void OverridesApplyBeforeHashing()
    {
        var baseline = WorkflowActionEnvelopeTestData.DeserializeValid();
        var changed = WorkflowActionEnvelopeTestData.DeserializeValid();

        normalizer.Normalize(changed, "PT-2002", "WF-PT-2002");

        Assert.Equal("PT-2002", changed.BusinessObject?.ObjectId);
        Assert.Equal("WF-PT-2002", changed.Workflow?.WorkflowInstanceId);
        Assert.NotEqual(hasher.ComputeHash(baseline), hasher.ComputeHash(changed));
    }

    [Fact]
    public void IdempotencyKeyWinsOverClientActionId()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();

        var normalized = normalizer.Normalize(envelope, null, null);

        Assert.Equal("idem-submit-1", normalized.EffectiveIdempotencyKey);
    }

    [Fact]
    public void HashIgnoresExcludedRequestFields()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        right.Request!.ClientActionId = "different-client-action";
        right.Request.CorrelationId = "different-correlation";
        right.Request.RequestedAtUtc = DateTimeOffset.Parse("2040-01-01T00:00:00Z");
        right.Workflow!.IdempotencyKey = "different-idem";

        Assert.Equal(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashCanonicalizesPayloadPropertyOrder()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        left.DataSave!.Payload = JsonDocument.Parse("""{"a":1,"b":2}""").RootElement.Clone();
        right.DataSave!.Payload = JsonDocument.Parse("""{"b":2,"a":1}""").RootElement.Clone();

        Assert.Equal(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashPreservesActorRoleArrayOrder()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        left.Actor!.Roles = ["Maker", "Checker"];
        right.Actor!.Roles = ["Checker", "Maker"];

        Assert.NotEqual(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }
}
