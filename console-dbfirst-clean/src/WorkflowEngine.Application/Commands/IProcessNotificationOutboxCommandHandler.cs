namespace WorkflowEngine.Application.Commands;

public interface IProcessNotificationOutboxCommandHandler
{
    Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken);
}
