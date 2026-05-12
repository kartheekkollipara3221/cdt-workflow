namespace WorkflowEngine.Api.Services;

public sealed record WorkflowDatabaseReadinessResult(
    bool MigrationsAndSeedApplied,
    bool SeedVerificationRan,
    bool SeedVerificationPassed,
    IReadOnlyList<string> SeedVerificationErrors,
    string Message);
