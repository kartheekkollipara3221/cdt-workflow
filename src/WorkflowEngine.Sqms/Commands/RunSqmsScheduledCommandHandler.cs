using System.Globalization;
using System.Text;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Sqms.Contracts;

namespace WorkflowEngine.Sqms.Commands;

public sealed class RunSqmsScheduledCommandHandler(ISqmsScheduledService scheduledService)
    : IRunSqmsScheduledCommandHandler
{
    public async Task<CommandResult> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var parse = CommandArgumentParser.Parse("run-sqms-scheduled", args);
        if (!parse.Success)
        {
            return CommandResult.Failure(parse.ErrorMessage!);
        }

        var result = await scheduledService.RunAsync(cancellationToken);
        var output = new StringBuilder();
        output.AppendLine("Command: RunSqmsScheduled");
        output.AppendLine(CultureInfo.InvariantCulture, $"Now: {result.Now.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Opportunities closed: {result.OpportunitiesClosed}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Bids moved to IPG review: {result.BidsMovedToIpgReview}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Bids not allocated: {result.BidsNotAllocated}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Open notifications queued: {result.OpenNotificationsQueued}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Reminder notifications queued: {result.ReminderNotificationsQueued}");
        output.AppendLine(CultureInfo.InvariantCulture, $"Already queued: {result.AlreadyQueued}");
        foreach (var failure in result.Failures)
        {
            output.AppendLine(
                CultureInfo.InvariantCulture,
                $"[FAIL] {failure.EntityType} {failure.EntityId} | {failure.Operation} | {failure.Error}");
        }

        if (result.Success)
        {
            output.AppendLine("SQMS scheduled run succeeded.");
            return CommandResult.Success(output.ToString().TrimEnd());
        }

        output.AppendLine(
            CultureInfo.InvariantCulture,
            $"SQMS scheduled run failed with {result.Failures.Count} candidate failures.");
        return CommandResult.Failure(output.ToString().TrimEnd());
    }
}
