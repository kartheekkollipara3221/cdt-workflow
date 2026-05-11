using System.Diagnostics;

namespace WorkflowEngine.Tests;

[Trait("Category", "Performance")]
[Collection("SqlServerWorkflowExecution")]
public sealed class WorkflowPerformanceTests
{
    [Fact]
    public void ExecuteJsonSubmitForChecker_PerformanceSmoke()
    {
        const int warmupRuns = 3;
        const int measuredRuns = 25;
        var durations = new List<TimeSpan>(measuredRuns);

        using var host = new TestConsoleHost("burrito_performance_tests");
        host.ResetDatabase();
        AssertSuccess(host.Execute("init-db"), "init-db");

        for (var index = 0; index < warmupRuns + measuredRuns; index++)
        {
            var businessObjectId = $"PT-PERF-{index:0000}";
            var workflowInstanceId = $"WF-{businessObjectId}";

            AssertSuccess(
                host.Execute("start-workflow", "PortfolioTrade", businessObjectId, "--user", "maker1"),
                $"start-workflow {businessObjectId}");

            var stopwatch = Stopwatch.StartNew();
            var result = host.Execute(
                "execute-json",
                "./samples/workflow-actions/02-submit-for-checker.json",
                "--business-object",
                businessObjectId,
                "--workflow-instance",
                workflowInstanceId,
                "--format",
                "json");
            stopwatch.Stop();

            AssertSuccess(result, $"execute-json {businessObjectId}");

            if (index >= warmupRuns)
            {
                durations.Add(stopwatch.Elapsed);
            }
        }

        var averageMilliseconds = durations.Average(duration => duration.TotalMilliseconds);
        var maxMilliseconds = durations.Max(duration => duration.TotalMilliseconds);

        Assert.True(
            averageMilliseconds < 500,
            $"Average execute-json SubmitForChecker time was {averageMilliseconds:N1} ms; expected < 500 ms. Max was {maxMilliseconds:N1} ms.");
        Assert.True(
            maxMilliseconds < 2_000,
            $"Slowest execute-json SubmitForChecker time was {maxMilliseconds:N1} ms; expected < 2,000 ms. Average was {averageMilliseconds:N1} ms.");
    }

    private static void AssertSuccess(WorkflowEngine.Application.Commands.CommandResult result, string operation)
    {
        Assert.True(result.ExitCode == 0, $"{operation} failed: {result.Message}");
    }
}
