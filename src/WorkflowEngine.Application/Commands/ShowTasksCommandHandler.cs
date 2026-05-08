namespace WorkflowEngine.Application.Commands;

using WorkflowEngine.Application.Inspection;

public sealed class ShowTasksCommandHandler(IWorkflowInspectionReader inspectionReader) : IShowTasksCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseWorkflowInstance(args, "Usage: show-tasks <workflowInstanceId>");
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

        var lines = new List<string> { $"Tasks for {workflowInstanceId}:" };
        if (snapshot.Tasks.Count == 0)
        {
            lines.Add("  [none]");
        }
        else
        {
            foreach (var task in snapshot.Tasks)
            {
                lines.Add($"  #{task.Id} {task.TaskKey} | status={task.Status} | role={task.AssignedRoleKey} | due={ShowCommandHandler.FormatTimestamp(task.DueAtUtc)} | completed={ShowCommandHandler.FormatTimestamp(task.CompletedAtUtc)} | closedBy={task.ClosedByActionKey ?? "None"}");
            }
        }

        return CommandResult.Success(string.Join(Environment.NewLine, lines));
    }
}
