namespace WorkflowEngine.Application.Commands;

public interface IShowTasksCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
