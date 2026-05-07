namespace WorkflowEngine.Application.Persistence;

public sealed record SeedVerificationResult(IReadOnlyList<string> Errors)
{
    public bool Passed => Errors.Count == 0;
}

