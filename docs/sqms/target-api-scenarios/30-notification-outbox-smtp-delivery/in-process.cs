using WorkflowEngine.Application.Commands;

// This is the same implemented handler invoked by `process-notification-outbox`.
CommandResult result = await processNotificationOutboxCommandHandler.RunAsync([], cancellationToken);
if (result.ExitCode != 0)
{
    Console.Error.WriteLine(result.Message);
}
