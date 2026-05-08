using System.Text;
using WorkflowEngine.Application.Demo;

namespace WorkflowEngine.Application.Commands;

public sealed class DemoCommandHandler(IDemoStepRunner demoStepRunner, IShowHistoryCommandHandler showHistoryCommandHandler) : IDemoCommandHandler
{
    private const string BranchGuidance = """
=== Demo completed successfully. ===

To explore Return-to-Maker branches, run these commands manually:

# Checker Return to Maker (uses PT-1002)
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1002 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/03-checker-return-to-maker.json --business-object PT-1002 --workflow-instance WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1002
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1002

# Trading Desk Return to Maker (uses PT-1003)
dotnet run --project src/WorkflowEngine.Console -- start-workflow PortfolioTrade PT-1003 --user maker1 --reset
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/02-submit-for-checker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/05-checker-send-to-trading-desk.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- execute-json ./samples/workflow-actions/06-trading-return-to-maker.json --business-object PT-1003 --workflow-instance WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-tasks WF-PT-1003
dotnet run --project src/WorkflowEngine.Console -- show-notifications WF-PT-1003

For SLA overdue demonstration, run:
dotnet run --project src/WorkflowEngine.Console -- run-sla --now "2030-01-15T16:00:00Z"
dotnet run --project src/WorkflowEngine.Console -- process-notification-outbox
""";

    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.Parse("demo", args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var output = new StringBuilder();
        var steps = DemoScriptDefinitions.DemoHappyPathSteps;

        for (var index = 0; index < steps.Length; index++)
        {
            var step = steps[index];
            output.AppendLine($"--- Step {index}: {step.Name} ---");

            var result = demoStepRunner.RunStepAsync(step, CancellationToken.None).GetAwaiter().GetResult();
            output.AppendLine(result.Message);

            if (result.Success)
            {
                output.AppendLine("Expectation: PASS");
            }
            else
            {
                output.AppendLine("Expectation: FAIL");
                output.AppendLine($"Demo failed at step {index}: {result.ResultCode} {result.Message}");
                return CommandResult.Failure(output.ToString().TrimEnd());
            }

            if (index < steps.Length - 1)
            {
                output.AppendLine("--- Pause: 100ms ---");
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }

        output.AppendLine($"Demo completed successfully. {steps.Length} steps executed.");
        output.AppendLine();
        output.AppendLine(showHistoryCommandHandler.Execute([DemoScriptDefinitions.DemoWorkflowInstanceId]).Message);
        output.AppendLine();
        output.AppendLine(BranchGuidance);

        return CommandResult.Success(output.ToString().TrimEnd());
    }
}
