namespace WorkflowEngine.Application.Commands;

public interface IStartWorkflowCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
