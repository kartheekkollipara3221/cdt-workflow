namespace WorkflowEngine.Application.Commands;

public interface IDemoCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
