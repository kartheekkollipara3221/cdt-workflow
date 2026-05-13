namespace WorkflowEngine.Application.Commands;

public interface IShowCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
