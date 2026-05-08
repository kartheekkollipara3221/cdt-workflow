using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Tests;

public static class BranchScriptDefinitions
{
    public static readonly ConsoleCommand[] HappyPath_Section25_1 =
    [
        new("init-db"),
        new("verify-seed"),
        new("start-workflow", "PortfolioTrade", "PT-1001", "--user", "maker1", "--reset"),
        new("execute-json", "./samples/workflow-actions/01-save-draft.json"),
        new("execute-json", "./samples/workflow-actions/02-submit-for-checker.json"),
        new("show-context", "WF-PT-1001", "--user", "checker1", "--roles", "Checker", "--format", "json"),
        new("show", "WF-PT-1001"),
        new("run-sla", "--now", "2030-01-15T16:00:00Z"),
        new("show-notifications", "WF-PT-1001"),
        new("execute-json", "./samples/workflow-actions/05-checker-send-to-trading-desk.json"),
        new("show-context", "WF-PT-1001", "--user", "trader1", "--roles", "TradingDesk", "--format", "json"),
        new("execute-json", "./samples/workflow-actions/07-trading-trade-executed.json"),
        new("execute-json", "./samples/workflow-actions/09-bdrm-trade-not-executed-properly.json"),
        new("show-context", "WF-PT-1001", "--user", "trader1", "--roles", "TradingDesk", "--format", "json"),
        new("execute-json", "./samples/workflow-actions/10-trading-rework-trade-executed.json"),
        new("execute-json", "./samples/workflow-actions/08-bdrm-trade-fulfilled.json"),
        new("show-history", "WF-PT-1001")
    ];

    public static readonly ConsoleCommand[] CheckerReturnBranch_Section25_2 =
    [
        new("start-workflow", "PortfolioTrade", "PT-1002", "--user", "maker1", "--reset"),
        new("execute-json", "./samples/workflow-actions/02-submit-for-checker.json", "--business-object", "PT-1002", "--workflow-instance", "WF-PT-1002"),
        new("execute-json", "./samples/workflow-actions/03-checker-return-to-maker.json", "--business-object", "PT-1002", "--workflow-instance", "WF-PT-1002"),
        new("show", "WF-PT-1002"),
        new("show-tasks", "WF-PT-1002"),
        new("show-notifications", "WF-PT-1002")
    ];

    public static readonly ConsoleCommand[] TradingReturnBranch_Section25_3 =
    [
        new("start-workflow", "PortfolioTrade", "PT-1003", "--user", "maker1", "--reset"),
        new("execute-json", "./samples/workflow-actions/02-submit-for-checker.json", "--business-object", "PT-1003", "--workflow-instance", "WF-PT-1003"),
        new("execute-json", "./samples/workflow-actions/05-checker-send-to-trading-desk.json", "--business-object", "PT-1003", "--workflow-instance", "WF-PT-1003"),
        new("execute-json", "./samples/workflow-actions/06-trading-return-to-maker.json", "--business-object", "PT-1003", "--workflow-instance", "WF-PT-1003"),
        new("show", "WF-PT-1003"),
        new("show-tasks", "WF-PT-1003"),
        new("show-notifications", "WF-PT-1003")
    ];

    public static string ToReadmeLine(ConsoleCommand command)
    {
        return "dotnet run --project src/WorkflowEngine.Console -- " +
            string.Join(" ", new[] { command.CommandName }.Concat(command.Arguments.Select(QuoteIfNeeded)));
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) || (value.Contains('T', StringComparison.Ordinal) && value.EndsWith('Z'))
            ? $"\"{value}\""
            : value;
    }
}
