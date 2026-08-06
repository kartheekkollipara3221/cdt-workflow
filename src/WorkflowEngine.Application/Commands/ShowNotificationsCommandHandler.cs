namespace WorkflowEngine.Application.Commands;

using WorkflowEngine.Application.Inspection;

public sealed class ShowNotificationsCommandHandler(IWorkflowInspectionReader inspectionReader) : IShowNotificationsCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseWorkflowInstance(args, "Usage: show-notifications <workflowInstanceId>");
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

        var lines = new List<string> { $"Notifications for {workflowInstanceId}:" };
        if (snapshot.Notifications.Count == 0)
        {
            lines.Add("  [none]");
        }
        else
        {
            foreach (var notification in snapshot.Notifications)
            {
                var toRecipients = string.Join(", ", notification.ToEmailAddresses);
                var ccRecipients = string.Join(", ", notification.CcEmailAddresses);
                var recipientSnapshotStatus = notification.HasInvalidRecipientSnapshot
                    ? " | recipientSnapshot=invalid"
                    : string.Empty;
                lines.Add($"  #{notification.Id} {notification.NotificationType} | status={notification.Status} | to=[{toRecipients}] | cc=[{ccRecipients}]{recipientSnapshotStatus} | taskId={notification.WorkflowTaskId?.ToString() ?? "None"} | subject={notification.Subject} | created={ShowCommandHandler.FormatTimestamp(notification.CreatedAtUtc)}");
            }
        }

        return CommandResult.Success(string.Join(Environment.NewLine, lines));
    }
}
