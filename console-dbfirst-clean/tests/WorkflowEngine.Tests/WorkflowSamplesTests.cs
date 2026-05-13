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
