namespace WorkflowEngine.Application.Commands;

public interface IRunSlaCommandHandler
{
    Task<CommandResult> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken);
}
