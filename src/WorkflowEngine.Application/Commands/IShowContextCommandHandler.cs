namespace WorkflowEngine.Application.Commands;

public interface IShowContextCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}

