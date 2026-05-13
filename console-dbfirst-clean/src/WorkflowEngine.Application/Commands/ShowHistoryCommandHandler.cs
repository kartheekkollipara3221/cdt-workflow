namespace WorkflowEngine.Application.Commands;

using WorkflowEngine.Application.Inspection;

public sealed class ShowHistoryCommandHandler(IWorkflowInspectionReader inspectionReader) : IShowHistoryCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseWorkflowInstance(args, "Usage: show-history <workflowInstanceId>");
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var workflowInstanceId = ((WorkflowInstanceCommandArguments)parseResult.Arguments!).WorkflowInstanceId;

        var snapshot = inspectionReader.Read(workflowInstanceId);
        if (!snapshot.Found)
        {
            return CommandResult.Failure($"Workflow instance '{workflowInstanceId}' was not found.");
        }

        var lines = new List<string> { $"History for {workflowInstanceId}:" };
        if (snapshot.History.Count == 0)
        {
            lines.Add("  [none]");
        }
        else
        {
            foreach (var entry in snapshot.History)
            {
                lines.Add($"  #{entry.Id} {entry.ActionKey} | {entry.FromStateKey} -> {entry.ToStateKey} | by={entry.PerformedByUserId}/{entry.PerformedByRoleKey} | at={ShowCommandHandler.FormatTimestamp(entry.CreatedAtUtc)} | reason={entry.Reason ?? "None"}");
            }
        }

        return CommandResult.Success(string.Join(Environment.NewLine, lines));
    }
}
