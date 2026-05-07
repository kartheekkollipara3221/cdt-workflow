namespace WorkflowEngine.Application.Commands;

public interface IExecuteJsonCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}

