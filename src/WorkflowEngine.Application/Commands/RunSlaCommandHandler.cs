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
        var parseResult = ParseNow(args);
        if (parseResult.Error is not null)
        {
            return CommandResult.Failure(parseResult.Error);
        }

        var now = parseResult.Now ?? timeProvider.GetUtcNow();
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

    private static (DateTimeOffset? Now, string? Error) ParseNow(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return (null, null);
        }

        if (args.Count != 2 || !string.Equals(args[0], "--now", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Usage: run-sla [--now <iso8601-utc>]");
        }

        if (!DateTimeOffset.TryParse(args[1], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var now))
        {
            return (null, $"Invalid --now value '{args[1]}'. Use an ISO-8601 UTC timestamp.");
        }

        return (now.ToUniversalTime(), null);
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
