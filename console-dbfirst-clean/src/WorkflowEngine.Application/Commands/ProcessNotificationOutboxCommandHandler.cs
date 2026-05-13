using System.Globalization;
using System.Text;
using WorkflowEngine.Application.Notifications;

namespace WorkflowEngine.Application.Commands;

public sealed class ProcessNotificationOutboxCommandHandler(
    IProcessNotificationOutboxService outboxService,
    INotificationSender notificationSender) : IProcessNotificationOutboxCommandHandler
{
    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var parseResult = CommandArgumentParser.Parse("process-notification-outbox", args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        Console.Out.WriteLine("Command: ProcessNotificationOutbox");
        Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture, "Sender: {0}", notificationSender.SenderName));
        Console.Out.WriteLine();
        Console.Out.WriteLine("Loading pending notifications...");

        var result = await outboxService.ProcessAsync(cancellationToken);
        var output = new StringBuilder();
        output.AppendLine(CultureInfo.InvariantCulture, $"Pending: {result.PendingCount}");
        output.AppendLine();

        foreach (var warning in result.Warnings)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"WARN: {warning}");
        }

        if (result.Failure is not null)
        {
            var failure = result.Failure;
            output.AppendLine(
                CultureInfo.InvariantCulture,
                $"[FAIL] {failure.NotificationType} | {failure.RecipientRoleKey ?? "(none)"} | {failure.Subject} | {failure.WorkflowInstanceId}");
            output.AppendLine(CultureInfo.InvariantCulture, $"       Exception: {failure.Exception}");
            output.AppendLine();
            output.AppendLine(
                CultureInfo.InvariantCulture,
                $"{result.Processed.Count} notification{Plural(result.Processed.Count)} processed, 1 failed, {result.NotAttemptedCount} not attempted.");
            return CommandResult.Failure(output.ToString().TrimEnd());
        }

        output.AppendLine(
            CultureInfo.InvariantCulture,
            $"{result.Processed.Count} notification{Plural(result.Processed.Count)} processed via {result.SenderName}.");
        return CommandResult.Success(output.ToString().TrimEnd());
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";
}
