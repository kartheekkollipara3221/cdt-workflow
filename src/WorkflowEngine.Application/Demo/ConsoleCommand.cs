namespace WorkflowEngine.Application.Demo;

public sealed record ConsoleCommand(string CommandName, params string[] Arguments)
{
    public string[] ToArgv()
    {
        var argv = new string[Arguments.Length + 1];
        argv[0] = CommandName;
        Array.Copy(Arguments, 0, argv, 1, Arguments.Length);
        return argv;
    }
}
