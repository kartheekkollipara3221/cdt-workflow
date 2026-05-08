using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Tests;

[Collection("SqlServerWorkflowExecution")]
public sealed class SampleExpectationTests
{
    [Fact]
    public void ExecuteJson_SamplesProduceTheirTestExpectations()
    {
        using var host = new TestConsoleHost("burrito_phase6_sample_expectation_tests");
        host.ResetDatabase();
        Assert.Equal(0, host.Execute("init-db").ExitCode);

        Start(host, "PT-SAMPLE-HAPPY");
        AssertSample(host, "01-save-draft.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "02-submit-for-checker.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "05-checker-send-to-trading-desk.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "07-trading-trade-executed.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "09-bdrm-trade-not-executed-properly.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "10-trading-rework-trade-executed.json", "PT-SAMPLE-HAPPY");
        AssertSample(host, "08-bdrm-trade-fulfilled.json", "PT-SAMPLE-HAPPY");

        Start(host, "PT-SAMPLE-CHECKER");
        AssertSample(host, "02-submit-for-checker.json", "PT-SAMPLE-CHECKER");
        AssertSample(host, "03-checker-return-to-maker.json", "PT-SAMPLE-CHECKER");
        AssertSample(host, "04-maker-resubmit-for-checker.json", "PT-SAMPLE-CHECKER");

        Start(host, "PT-SAMPLE-TRADING");
        AssertSample(host, "02-submit-for-checker.json", "PT-SAMPLE-TRADING");
        AssertSample(host, "05-checker-send-to-trading-desk.json", "PT-SAMPLE-TRADING");
        AssertSample(host, "06-trading-return-to-maker.json", "PT-SAMPLE-TRADING");
    }

    private static void Start(TestConsoleHost host, string businessObjectId)
    {
        var result = host.Execute("start-workflow", "PortfolioTrade", businessObjectId, "--user", "maker1", "--reset");
        Assert.True(result.ExitCode == 0, result.Message);
    }

    private static void AssertSample(TestConsoleHost host, string sampleFile, string businessObjectId)
    {
        var path = $"./samples/workflow-actions/{sampleFile}";
        var workflowInstanceId = $"WF-{businessObjectId}";
        var result = host.Execute("execute-json", path, "--business-object", businessObjectId, "--workflow-instance", workflowInstanceId, "--format", "json");
        Assert.True(result.ExitCode == 0, result.Message);

        var envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);
        Assert.NotNull(envelope);
        var expectations = ReadExpectations(path);
        var actual = envelope.Result;

        AssertIfPresent(expectations.ExpectedResult, actual.ResultCode);
        AssertIfPresent(expectations.ExpectedOldState, actual.OldState);
        AssertIfPresent(expectations.ExpectedNewState, actual.NewState);
        AssertIfPresent(expectations.ExpectedClosedTaskKey, actual.ClosedTask?.TaskKey);
        AssertIfPresent(expectations.ExpectedCreatedTaskKey, actual.CreatedTask?.TaskKey);
        AssertIfPresent(expectations.ExpectedAssignedRole, actual.CreatedTask?.AssignedRole);
        AssertIfPresent(expectations.ExpectedNotificationType, actual.Notification?.NotificationType);
        AssertIfPresent(expectations.ExpectedAuditAction, actual.Audit?.ActionKey);
    }

    private static void AssertIfPresent(string? expected, string? actual)
    {
        if (expected is not null)
        {
            Assert.Equal(expected, actual);
        }
    }

    private static WorkflowTestExpectations ReadExpectations(string path)
    {
        var resolved = ResolveSampleArgument(path);
        var sample = JsonSerializer.Deserialize<WorkflowActionEnvelope>(File.ReadAllText(resolved), WorkflowJson.Options);
        Assert.NotNull(sample?.TestExpectations);
        return sample.TestExpectations;
    }

    private static string ResolveSampleArgument(string arg)
    {
        if (File.Exists(arg))
        {
            return arg;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, arg);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return arg;
    }
}
