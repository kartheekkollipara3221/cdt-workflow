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

    [Fact]
    public void HashChangesWhenNotificationRecipientValueChanges()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        right.Workflow!.NotificationRecipients!.ToEmailAddresses[1] = "different@example.com";

        Assert.NotEqual(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashPreservesNotificationRecipientArrayOrder()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        right.Workflow!.NotificationRecipients!.ToEmailAddresses.Reverse();

        Assert.NotEqual(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashDistinguishesToAndCcRecipientPlacement()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        var moved = right.Workflow!.NotificationRecipients!.ToEmailAddresses[0];
        right.Workflow.NotificationRecipients.ToEmailAddresses.RemoveAt(0);
        right.Workflow.NotificationRecipients.CcEmailAddresses.Insert(0, moved);

        Assert.NotEqual(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashDoesNotNormalizeNotificationRecipientValues()
    {
        var left = WorkflowActionEnvelopeTestData.DeserializeValid();
        var right = WorkflowActionEnvelopeTestData.DeserializeValid();
        right.Workflow!.NotificationRecipients!.ToEmailAddresses[0] = " checker.primary@example.com ";

        Assert.NotEqual(hasher.ComputeHash(left), hasher.ComputeHash(right));
    }

    [Fact]
    public void HashMergesDuplicateNotificationContextKeysAfterTrim()
    {
        var duplicateKeys = WorkflowActionEnvelopeTestData.DeserializeValid();
        var normalized = WorkflowActionEnvelopeTestData.DeserializeValid();
        duplicateKeys.Workflow!.NotificationContext = new Dictionary<string, string?>
        {
            ["deskName"] = "Original",
            [" deskName "] = " Taxable ",
            ["emptyValue"] = " "
        };
        normalized.Workflow!.NotificationContext = new Dictionary<string, string?>
        {
            ["deskName"] = "Taxable"
        };

        Assert.Equal(hasher.ComputeHash(normalized), hasher.ComputeHash(duplicateKeys));
    }

    [Fact]
    public void HashChangesWhenSlaTargetDateChanges()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        envelope.Workflow!.SlaInput = new WorkflowSlaInput
        {
            TargetDate = new DateOnly(2030, 1, 20)
        };
        var first = hasher.ComputeHash(envelope);

        envelope.Workflow.SlaInput.TargetDate = new DateOnly(2030, 1, 21);

        Assert.NotEqual(first, hasher.ComputeHash(envelope));
    }

    [Fact]
    public void NotificationInputHashSortsKeysButPreservesRecipientInputOrderAndValues()
    {
        var envelope = WorkflowActionEnvelopeTestData.DeserializeValid();
        var first = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["zInput"] = [new("z", "first", null), new("z2", "second", null)],
                ["aInput"] = [new("a", "opaque", "A")]
            },
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["z"] = "last",
                ["a"] = "first"
            }
        };
        var reorderedKeys = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["aInput"] = [new("a", "opaque", "A")],
                ["zInput"] = [new("z", "first", null), new("z2", "second", null)]
            },
            TemplateData = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["a"] = "first",
                ["z"] = "last"
            }
        };
        var reversedRecipients = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["aInput"] = [new("a", "opaque", "A")],
                ["zInput"] = [new("z2", "second", null), new("z", "first", null)]
            },
            TemplateData = reorderedKeys.TemplateData
        };

        Assert.Equal(hasher.ComputeHash(envelope, first), hasher.ComputeHash(envelope, reorderedKeys));
        Assert.NotEqual(hasher.ComputeHash(envelope, first), hasher.ComputeHash(envelope, reversedRecipients));

        var changedValue = new WorkflowNotificationInput
        {
            RecipientInputs = new Dictionary<string, IReadOnlyList<WorkflowRecipientInput>>(StringComparer.Ordinal)
            {
                ["aInput"] = [new("a", "opaque", "A")],
                ["zInput"] = [new("z", "FIRST", null), new("z2", "second", null)]
            },
            TemplateData = reorderedKeys.TemplateData
        };
        Assert.NotEqual(hasher.ComputeHash(envelope, reorderedKeys), hasher.ComputeHash(envelope, changedValue));
    }
}
