using System.Text.Json;
using WorkflowEngine.Application.Commands;
using WorkflowEngine.Application.Contracts;

namespace WorkflowEngine.Application.Demo;

public sealed class WorkflowEngineDemoStepRunner(
    IStartWorkflowCommandHandler startWorkflowCommandHandler,
    IExecuteJsonCommandHandler executeJsonCommandHandler,
    IShowContextCommandHandler showContextCommandHandler,
    IShowCommandHandler showCommandHandler,
    IShowNotificationsCommandHandler showNotificationsCommandHandler,
    IRunSlaCommandHandler runSlaCommandHandler) : IDemoStepRunner
{
    public async Task<DemoStepResult> RunStepAsync(DemoStep step, CancellationToken cancellationToken)
    {
        if (string.Equals(step.CommandName, "demo", StringComparison.OrdinalIgnoreCase))
        {
            return new DemoStepResult(false, "InvalidTransition", "demo cannot invoke itself.", null);
        }

        var result = await DispatchAsync(step, cancellationToken);
        if (step.Expectations is null)
        {
            return new DemoStepResult(
                result.ExitCode == 0,
                result.ExitCode == 0 ? "Success" : "CommandFailed",
                result.Message,
                null);
        }

        if (result.ExitCode != 0)
        {
            return new DemoStepResult(false, "CommandFailed", result.Message, null);
        }

        WorkflowActionResultEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WorkflowActionResultEnvelope>(result.Message, WorkflowJson.Options);
        }
        catch (JsonException exception)
        {
            return new DemoStepResult(false, "ValidationFailed", $"Could not parse step result JSON: {exception.Message}", null);
        }

        if (envelope is null)
        {
            return new DemoStepResult(false, "ValidationFailed", "Step result JSON was empty.", null);
        }

        var failures = CompareExpectations(step.Expectations, envelope.Result);
        var success = envelope.Result.Success && failures.Count == 0;
        var message = failures.Count == 0
            ? envelope.Result.Message ?? "Expectations passed."
            : string.Join(Environment.NewLine, failures);

        return new DemoStepResult(success, envelope.Result.ResultCode, message, step.Expectations);
    }

    private async Task<CommandResult> DispatchAsync(DemoStep step, CancellationToken cancellationToken)
    {
        if (string.Equals(step.CommandName, "start-workflow", StringComparison.OrdinalIgnoreCase))
        {
            return startWorkflowCommandHandler.Execute(step.Arguments);
        }

        if (string.Equals(step.CommandName, "execute-json", StringComparison.OrdinalIgnoreCase))
        {
            var args = step.Expectations is null
                ? step.Arguments
                : [.. step.Arguments, "--format", "json"];
            return executeJsonCommandHandler.Execute(args.Select(ResolveSampleArgument).ToArray());
        }

        if (string.Equals(step.CommandName, "show-context", StringComparison.OrdinalIgnoreCase))
        {
            return showContextCommandHandler.Execute(step.Arguments);
        }

        if (string.Equals(step.CommandName, "show", StringComparison.OrdinalIgnoreCase))
        {
            return showCommandHandler.Execute(step.Arguments);
        }

        if (string.Equals(step.CommandName, "show-notifications", StringComparison.OrdinalIgnoreCase))
        {
            return showNotificationsCommandHandler.Execute(step.Arguments);
        }

        if (string.Equals(step.CommandName, "run-sla", StringComparison.OrdinalIgnoreCase))
        {
            return await runSlaCommandHandler.RunAsync(step.Arguments, cancellationToken);
        }

        return CommandResult.Failure($"Unsupported demo step command '{step.CommandName}'.");
    }

    private static List<string> CompareExpectations(DemoStepExpectations expectations, WorkflowActionResult result)
    {
        var failures = new List<string>();
        AssertEqual(expectations.ExpectedResult, result.ResultCode, "resultCode", failures);
        AssertEqual(expectations.ExpectedOldState, result.OldState, "oldState", failures);
        AssertEqual(expectations.ExpectedNewState, result.NewState, "newState", failures);
        AssertEqual(expectations.ExpectedClosedTaskKey, result.ClosedTask?.TaskKey, "closedTask.taskKey", failures);
        AssertEqual(expectations.ExpectedCreatedTaskKey, result.CreatedTask?.TaskKey, "createdTask.taskKey", failures);
        AssertEqual(expectations.ExpectedAssignedRole, result.CreatedTask?.AssignedRole, "createdTask.assignedRole", failures);
        AssertEqual(expectations.ExpectedNotificationType, result.Notification?.NotificationType, "notification.notificationType", failures);
        AssertEqual(expectations.ExpectedAuditAction, result.Audit?.ActionKey, "audit.actionKey", failures);
        return failures;
    }

    private static void AssertEqual(string? expected, string? actual, string field, List<string> failures)
    {
        if (expected is null)
        {
            return;
        }

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            failures.Add($"Expectation failed for {field}: expected '{expected}', actual '{actual ?? "<null>"}'.");
        }
    }

    private static string ResolveSampleArgument(string arg)
    {
        if (!arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || File.Exists(arg))
        {
            return arg;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, arg);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return arg;
    }
}
