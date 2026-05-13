using System.Globalization;
using System.Text;
using WorkflowEngine.Application.Notifications;
using WorkflowEngine.Application.Sla;

namespace WorkflowEngine.Application.Commands;

public sealed class RunSlaCommandHandler(
    IRunSlaService runSlaService,
    TimeProvider timeProvider,
    INotificationSender notificationSender) : IRunSlaCommandHandler
{
    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parseResult = CommandArgumentParser.ParseRunSla(args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var options = (RunSlaCommandArguments)parseResult.Arguments!;
        var now = options.Now ?? timeProvider.GetUtcNow();
        var result = await runSlaService.RunAsync(new RunSlaRequest(now), cancellationToken);

        var output = new StringBuilder();
        output.AppendLine("Command: RunSla");
        output.AppendLine(CultureInfo.InvariantCulture, $"Sender: {notificationSender.SenderName}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Now: {FormatUtc(result.Now)}");
        output.AppendLine();
        output.AppendLine("Scanning pending tasks...");
        output.AppendLine(CultureInfo.InvariantCulture, $"Pending tasks with SLA: {result.PendingTasksWithSla}");
        output.AppendLine(CultureInfo.InvariantCulture, $"  Already-queued (skipped): {result.AlreadyQueued}");
        output.AppendLine(CultureInfo.InvariantCulture, $"  Overdue (to process):     {result.QueuedNotifications.Count}");
        output.AppendLine();
        output.AppendLine("Processing overdue tasks...");

        foreach (var queued in result.QueuedNotifications)
        {
            output.AppendLine(
                CultureInfo.InvariantCulture,
                $"[Queued] #{queued.WorkflowTaskId} {queued.TaskKey} | {queued.AssignedRoleKey ?? "(unassigned)"} | due {FormatUtc(queued.DueAtUtc)} | notification {queued.NotificationOutboxId}");
        }

        foreach (var warning in result.Warnings)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"WARN: {warning}");
        }

        output.AppendLine();
        output.AppendLine(CultureInfo.InvariantCulture, $"{result.QueuedNotifications.Count} SLA overdue notifications queued.");
        return CommandResult.Success(output.ToString().TrimEnd());
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
