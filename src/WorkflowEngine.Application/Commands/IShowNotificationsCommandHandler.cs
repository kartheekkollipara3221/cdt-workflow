namespace WorkflowEngine.Application.Commands;

public interface IShowNotificationsCommandHandler
{
    CommandResult Execute(IReadOnlyList<string> args);
}
