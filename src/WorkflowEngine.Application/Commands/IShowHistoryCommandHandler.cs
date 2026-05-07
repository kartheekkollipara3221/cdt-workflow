namespace WorkflowEngine.Application.Commands;

public interface IShowHistoryCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
