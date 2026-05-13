namespace WorkflowEngine.Application.Commands;

public sealed record CommandResult(int ExitCode, string Message)
{
    public static CommandResult Success(string message) => new(0, message);

    public static CommandResult Failure(string message) => new(1, message);
}
