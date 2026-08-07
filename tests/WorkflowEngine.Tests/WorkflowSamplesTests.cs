using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

public sealed class WorkflowSamplesTests
{
    [Fact]
    public void AllRequiredWorkflowActionSamplesDeserializeAndCarryExpectations()
    {
        var paths = Directory.GetFiles(Path.Combine(FindRepoRoot(), "samples", "workflow-actions"), "*.json")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(10, paths.Length);
        foreach (var path in paths)
        {
            var envelope = JsonSerializer.Deserialize<WorkflowActionEnvelope>(File.ReadAllText(path), WorkflowJson.Options);

            Assert.NotNull(envelope);
            Assert.Equal("PortfolioTrade", envelope.BusinessObject?.ObjectType);
            Assert.Equal("PT-1001", envelope.BusinessObject?.ObjectId);
            Assert.Equal("PortfolioTrade", envelope.Workflow?.WorkflowKey);
            Assert.Equal("WF-PT-1001", envelope.Workflow?.WorkflowInstanceId);
            Assert.Equal(1, envelope.Workflow?.WorkflowDefinitionVersion);
            Assert.NotNull(envelope.TestExpectations);
            Assert.Equal("Success", envelope.TestExpectations?.ExpectedResult);
        }
    }

    [Fact]
    public void NotificationProducingSamplesUseOrderedTypedRecipientArrays()
    {
        var sampleDirectory = Path.Combine(FindRepoRoot(), "samples", "workflow-actions");

        var saveDraft = ReadEnvelope(Path.Combine(sampleDirectory, "01-save-draft.json"));
        Assert.Null(saveDraft.Workflow?.NotificationRecipients);

        foreach (var path in Directory.GetFiles(sampleDirectory, "*.json")
                     .Where(path => !path.EndsWith("01-save-draft.json", StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            var envelope = ReadEnvelope(path);
            var recipients = Assert.IsType<NotificationRecipients>(envelope.Workflow?.NotificationRecipients);
            Assert.Equal(
                ["workflow.primary@example.test", "workflow.secondary@example.test"],
                recipients.ToEmailAddresses);
            Assert.Equal(
                ["workflow.manager@example.test", "workflow.operations@example.test"],
                recipients.CcEmailAddresses);
            Assert.False(envelope.Workflow?.NotificationContext?.ContainsKey("recipientEmail") ?? false);
            Assert.False(envelope.Workflow?.NotificationContext?.ContainsKey("ccEmailAddresses") ?? false);
        }
    }

    [Fact]
    public void CheckerSendToTradingDeskSampleCarriesCurrentTargetDate()
    {
        var sample = ReadEnvelope(Path.Combine(
            FindRepoRoot(),
            "samples",
            "workflow-actions",
            "05-checker-send-to-trading-desk.json"));

        Assert.Equal(new DateOnly(2030, 1, 20), sample.Workflow?.SlaInput?.TargetDate);
    }

    private static WorkflowActionEnvelope ReadEnvelope(string path)
    {
        return JsonSerializer.Deserialize<WorkflowActionEnvelope>(File.ReadAllText(path), WorkflowJson.Options)
            ?? throw new InvalidOperationException($"Could not deserialize workflow sample '{path}'.");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PROJECT.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
