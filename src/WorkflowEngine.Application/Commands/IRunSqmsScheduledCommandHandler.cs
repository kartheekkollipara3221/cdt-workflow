namespace WorkflowEngine.Application.Commands;

public interface IRunSqmsScheduledCommandHandler
{
    Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken);
}
