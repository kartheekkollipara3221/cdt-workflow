namespace WorkflowEngine.Application.Demo;

public interface IDemoStepRunner
{
    Task<DemoStepResult> RunStepAsync(DemoStep step, CancellationToken cancellationToken);
}
