using System.Text.Json;
using WorkflowEngine.Application.Context;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Commands;

public sealed class ShowContextCommandHandler(IWorkflowContextReader contextReader) : IShowContextCommandHandler
{
    public CommandResult Execute(IReadOnlyList<string> args)
    {
        var parseResult = CommandArgumentParser.ParseShowContext(args);
        if (!parseResult.Success)
        {
            return CommandResult.Failure(parseResult.ErrorMessage!);
        }

        var options = (ShowContextCommandArguments)parseResult.Arguments!;

        try
        {
            var response = contextReader.Read(new WorkflowContextReadRequest(
                options.WorkflowInstanceId,
                options.UserId,
                options.Roles));

            return CommandResult.Success(JsonSerializer.Serialize(response, WorkflowJson.Options));
        }
        catch (KeyNotFoundException exception)
        {
            return CommandResult.Failure(exception.Message);
        }
    }

}
