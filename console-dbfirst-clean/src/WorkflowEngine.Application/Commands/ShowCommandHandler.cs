namespace WorkflowEngine.Application.Commands;

using WorkflowEngine.Application.Inspection;

public sealed class ShowCommandHandler(IWorkflowInspectionReader inspectionReader) : IShowCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseWorkflowInstance(args, "Usage: show <workflowInstanceId>");
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var workflowInstanceId = ((WorkflowInstanceCommandArguments)parseResult.Arguments!).WorkflowInstanceId;

        var snapshot = inspectionReader.Read(workflowInstanceId);
        if (snapshot.Instance is null)
        {
            return CommandResult.Failure($"Workflow instance '{workflowInstanceId}' was not found.");
        }

        var instance = snapshot.Instance;
        var currentTask = snapshot.Tasks.FirstOrDefault(task => string.Equals(task.Status, "Pending", StringComparison.OrdinalIgnoreCase));
        var lines = new List<string>
        {
            $"Workflow Instance: {instance.WorkflowInstanceId}",
            $"Workflow: {instance.WorkflowKey} v{instance.WorkflowDefinitionVersion}",
            $"Business Object: {instance.BusinessObjectType}/{instance.BusinessObjectId}",
            $"Status: {instance.Status}",
            $"Current State: {instance.CurrentStateKey}",
            $"Version: {instance.Version}",
            $"Started: {FormatTimestamp(instance.StartedAtUtc)} by {instance.StartedByUserId}",
            $"Completed: {FormatTimestamp(instance.CompletedAtUtc)}",
            $"Current Task: {(currentTask is null ? "None" : $"{currentTask.TaskKey} ({currentTask.AssignedRoleKey})")}",
            $"Tasks: {snapshot.Tasks.Count}",
            $"Notifications: {snapshot.Notifications.Count}",
            $"History entries: {snapshot.History.Count}"
        };

        return CommandResult.Success(string.Join(Environment.NewLine, lines));
    }

    internal static string? SingleWorkflowInstanceId(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseWorkflowInstance(args, "Usage: show <workflowInstanceId>");
        return parseResult.Success ? ((WorkflowInstanceCommandArguments)parseResult.Arguments!).WorkflowInstanceId : null;
    }

    internal static string FormatTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp?.ToUniversalTime().ToString("O") ?? "None";
    }
}
