using System.Text.Json;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Demo;

public static class DemoScriptDefinitions
{
    public const string DemoBusinessObjectId = "PT-DEMO";
    public const string DemoWorkflowInstanceId = "WF-PT-DEMO";

    public static readonly DemoStep[] DemoHappyPathSteps =
    [
        new("Reset PT-DEMO", "start-workflow", ["PortfolioTrade", DemoBusinessObjectId, "--user", "maker1", "--reset"], null),
        Sample("Save draft", "./samples/workflow-actions/01-save-draft.json"),
        Sample("Submit for Checker", "./samples/workflow-actions/02-submit-for-checker.json"),
        new("Show Checker context", "show-context", [DemoWorkflowInstanceId, "--user", "checker1", "--roles", "Checker", "--format", "json"], null),
        new("Show workflow", "show", [DemoWorkflowInstanceId], null),
        new("Run SLA monitor", "run-sla", ["--now", "2030-01-15T16:00:00Z"], null),
        new("Show notifications", "show-notifications", [DemoWorkflowInstanceId], null),
        Sample("Checker sends to Trading Desk", "./samples/workflow-actions/05-checker-send-to-trading-desk.json"),
        new("Show Trading Desk context", "show-context", [DemoWorkflowInstanceId, "--user", "trader1", "--roles", "TradingDesk", "--format", "json"], null),
        Sample("Trading Desk marks trade executed", "./samples/workflow-actions/07-trading-trade-executed.json"),
        Sample("BDRM sends to Trading Desk rework", "./samples/workflow-actions/09-bdrm-trade-not-executed-properly.json"),
        new("Show Trading Desk rework context", "show-context", [DemoWorkflowInstanceId, "--user", "trader1", "--roles", "TradingDesk", "--format", "json"], null),
        Sample("Trading Desk rework trade executed", "./samples/workflow-actions/10-trading-rework-trade-executed.json"),
        Sample("BDRM marks trade fulfilled", "./samples/workflow-actions/08-bdrm-trade-fulfilled.json")
    ];

    private static DemoStep Sample(string name, string samplePath)
    {
        return new DemoStep(
            name,
            "execute-json",
            [samplePath, "--business-object", DemoBusinessObjectId, "--workflow-instance", DemoWorkflowInstanceId],
            ReadExpectations(samplePath));
    }

    private static DemoStepExpectations ReadExpectations(string samplePath)
    {
        var json = File.ReadAllText(ResolveSamplePath(samplePath));
        var envelope = JsonSerializer.Deserialize<WorkflowActionEnvelope>(json, WorkflowJson.Options)
            ?? throw new InvalidOperationException($"Sample envelope '{samplePath}' is empty.");
        var expectations = envelope.TestExpectations
            ?? throw new InvalidOperationException($"Sample envelope '{samplePath}' is missing testExpectations.");

        return new DemoStepExpectations(
            expectations.ExpectedResult,
            expectations.ExpectedOldState,
            expectations.ExpectedNewState,
            expectations.ExpectedClosedTaskKey,
            expectations.ExpectedCreatedTaskKey,
            expectations.ExpectedAssignedRole,
            expectations.ExpectedNotificationType,
            expectations.ExpectedAuditAction);
    }

    private static string ResolveSamplePath(string samplePath)
    {
        if (File.Exists(samplePath))
        {
            return samplePath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, samplePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return samplePath;
    }
}
